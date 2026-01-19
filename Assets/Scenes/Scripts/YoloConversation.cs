using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.InferenceEngine;
using Unity.Mathematics;

public class YoloConversation : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset modelAsset;
    public TextAsset labelsFile;
    
    [Header("Input Settings")]
    public UniversalCamera cameraScript;
    public int modelInputSize = 320;
    public bool flipInputX = true;

    [Header("Performance Settings")]
    [Range(5, 30)] public int targetFPS = 15;

    [Header("Detection Settings")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.5f;
    [Range(0f, 1f)] public float iouThreshold = 0.45f;

    [Header("Hold-to-Confirm Settings")]
    [Tooltip("How long player must hold gesture to confirm (seconds)")]
    public float detectionHoldTime = 0.3f;
    
    [Header("Smoothing Settings")]
    public int smoothingLength = 8;

    [Header("UI Settings")]
    public GameObject boxPrefab;
    public Transform boxContainer;
    public TextMeshProUGUI finalResultText;

    [Header("Output for Game")]
    public string currentDetectedWord; // Raw detection (updates every frame)
    public string currentDetectedKeyword; // Confirmed keyword (only after hold time)

    // --- CORE YOLO VARIABLES ---
    private Worker worker;
    private Model model;
    private Tensor<float> inputTensor;
    private string[] labels;
    private List<GameObject> boxPool = new List<GameObject>();
    private const int NUM_CLASSES = 10;
    private Queue<string> transitionBuffer = new Queue<string>();
    private bool isProcessing = false;
    private bool isRunning = false;

    // --- HOLD-TO-CONFIRM VARIABLES ---
    private float startHoldTime = 0f;
    private string lastFrameWord = "";
    private string lastConfirmedKeyword = "";

    void Start()
    {
        // Initialize YOLO model
        model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, modelInputSize, modelInputSize));
        Debug.Log($"[YOLO CONVERSATION] Worker initialized");

        if (labelsFile)
        {
            labels = labelsFile.text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            // Trim each label to remove whitespace and special characters
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = labels[i].Trim();
            }
        }

        StartCoroutine(DetectionLoop());
    }

    IEnumerator DetectionLoop()
    {
        isRunning = true;
        WaitForSeconds fpsWait = new WaitForSeconds(1.0f / targetFPS);

        while (isRunning)
        {
            // Skip if GameObject is inactive
            if (!gameObject.activeSelf)
            {
                yield return fpsWait;
                continue;
            }

            // Skip if component is disabled
            if (!enabled)
            {
                yield return fpsWait;
                continue;
            }

            // Run detection
            Texture camTexture = GetCameraTexture();
            if (camTexture != null && !isProcessing)
            {
                RunInferenceAsync(camTexture);
            }
            else if (camTexture == null && Time.frameCount % 120 == 0) // Every ~2 seconds
            {
                Debug.LogWarning($"[YOLO] No camera texture! GameObject.active={gameObject.activeSelf}, Component.enabled={enabled}");
            }

            yield return fpsWait;
        }
    }

    async void RunInferenceAsync(Texture sourceTexture)
    {
        isProcessing = true;
        try
        {
            if (this == null) return;

            RenderTexture rt = RenderTexture.GetTemporary(modelInputSize, modelInputSize, 0);
            Graphics.Blit(sourceTexture, rt);

            var transform = new TextureTransform().SetTensorLayout(TensorLayout.NCHW);
            TextureConverter.ToTensor(rt, inputTensor, transform);
            RenderTexture.ReleaseTemporary(rt);

            if (worker == null) return;
            worker.Schedule(inputTensor);

            var outputAwaitable = (worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync();
            using var output = await outputAwaitable;

            if (this == null || !gameObject.activeSelf) return;

            ProcessOutput(output.DownloadToArray());
        }
        catch (Exception e)
        {
            // Silently ignore errors during scene transitions
            if (this != null && gameObject != null && gameObject.activeSelf)
            {
                Debug.LogWarning($"[YOLO CONVERSATION] Inference warning: {e.Message}");
            }
        }
        finally
        {
            isProcessing = false;
        }
    }

    void ProcessOutput(float[] data)
    {
        if (data == null || data.Length == 0) return;

        foreach (var box in boxPool) box.SetActive(false);
        List<Detection> detections = new List<Detection>();

        int numChannels = 4 + (labels != null ? labels.Length : NUM_CLASSES);
        int numAnchors = data.Length / numChannels;

        for (int i = 0; i < numAnchors; i++)
        {
            float maxConf = 0f;
            int maxClass = -1;

            for (int c = 0; c < (numChannels - 4); c++)
            {
                float conf = data[(4 + c) * numAnchors + i];
                if (conf > maxConf) { maxConf = conf; maxClass = c; }
            }

            if (maxConf > confidenceThreshold)
            {
                float x = data[0 * numAnchors + i];
                float y = data[1 * numAnchors + i];
                float w = data[2 * numAnchors + i];
                float h = data[3 * numAnchors + i];

                if (flipInputX) x = modelInputSize - x;

                detections.Add(new Detection
                {
                    box = new Rect((x - w / 2) / modelInputSize, (y - h / 2) / modelInputSize, w / modelInputSize, h / modelInputSize),
                    conf = maxConf,
                    classId = maxClass
                });
            }
        }

        List<Detection> finalDetections = NMS(detections);

        string detectedWord = null;
        if (finalDetections.Count > 0)
        {
            finalDetections.Sort((a, b) => (b.box.width * b.box.height).CompareTo(a.box.width * a.box.height));
            Detection mainHand = finalDetections[0];
            detectedWord = (labels != null && mainHand.classId < labels.Length) ? labels[mainHand.classId] : "?";
        }

        string finalWord = SmoothResult(detectedWord);
        currentDetectedWord = finalWord;

        // Hold-to-confirm logic
        ProcessHoldToConfirm(finalWord);

        if (finalResultText != null)
        {
            finalResultText.text = !string.IsNullOrEmpty(currentDetectedWord) ? 
                currentDetectedWord : 
                "Detecting...";
        }

        DrawBoxes(finalDetections);
    }

    void ProcessHoldToConfirm(string rawWord)
    {
        if (string.IsNullOrEmpty(rawWord))
        {
            // Empty detection - reset after tolerance
            float elapsedSinceEmpty = Time.time - startHoldTime;
            if (elapsedSinceEmpty > 0.5f) // 0.5s tolerance for gaps
            {
                lastFrameWord = "";
                startHoldTime = 0f;
                if (!string.IsNullOrEmpty(currentDetectedKeyword))
                {
                    lastConfirmedKeyword = currentDetectedKeyword;
                    currentDetectedKeyword = "";
                }
            }
            return;
        }

        string processedWord = rawWord.ToUpper();

        // Same word as last frame - accumulate
        if (processedWord == lastFrameWord)
        {
            float holdDuration = Time.time - startHoldTime;
            
            if (holdDuration >= detectionHoldTime && currentDetectedKeyword != processedWord)
            {
                currentDetectedKeyword = processedWord;
                Debug.Log($"<color=cyan>[YOLO] CONFIRMED keyword: '{currentDetectedKeyword}'</color>");
            }
        }
        // Different word - reset
        else
        {
            startHoldTime = Time.time;
            lastFrameWord = processedWord;
        }
    }

    string SmoothResult(string currentWord)
    {
        transitionBuffer.Enqueue(currentWord);
        if (transitionBuffer.Count > smoothingLength) transitionBuffer.Dequeue();

        Dictionary<string, int> counts = new Dictionary<string, int>();
        string bestCandidate = null;
        int maxCount = 0;

        foreach (var word in transitionBuffer)
        {
            if (string.IsNullOrEmpty(word)) continue;

            if (!counts.ContainsKey(word)) counts[word] = 0;
            counts[word]++;

            if (counts[word] > maxCount)
            {
                maxCount = counts[word];
                bestCandidate = word;
            }
        }

        return bestCandidate;
    }

    List<Detection> NMS(List<Detection> detections)
    {
        detections.Sort((a, b) => b.conf.CompareTo(a.conf));
        List<Detection> result = new List<Detection>();

        foreach (var det in detections)
        {
            bool keep = true;
            foreach (var kept in result)
            {
                float iou = CalculateIoU(det.box, kept.box);
                if (iou > iouThreshold)
                {
                    keep = false;
                    break;
                }
            }
            if (keep) result.Add(det);
        }

        return result;
    }

    float CalculateIoU(Rect a, Rect b)
    {
        float xOverlap = Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin));
        float yOverlap = Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
        float intersection = xOverlap * yOverlap;
        float union = (a.width * a.height) + (b.width * b.height) - intersection;
        return union > 0 ? intersection / union : 0;
    }

    void DrawBoxes(List<Detection> detections)
    {
        if (boxContainer == null) return;

        for (int i = 0; i < detections.Count; i++)
        {
            GameObject box = (i < boxPool.Count) ? boxPool[i] : CreateBox();
            box.SetActive(true);

            RectTransform rt = box.GetComponent<RectTransform>();
            RectTransform canvasRect = boxContainer.GetComponent<RectTransform>();

            Detection det = detections[i];
            rt.anchorMin = new Vector2(det.box.x, 1 - det.box.y - det.box.height);
            rt.anchorMax = new Vector2(det.box.x + det.box.width, 1 - det.box.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            TextMeshProUGUI label = box.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string className = (labels != null && det.classId < labels.Length) ? labels[det.classId] : "?";
                label.text = $"{className} {det.conf:F2}";
            }
        }
    }

    GameObject CreateBox()
    {
        GameObject box = Instantiate(boxPrefab, boxContainer);
        boxPool.Add(box);
        return box;
    }

    Texture GetCameraTexture()
    {
        if (cameraScript == null)
        {
            if (Time.frameCount % 120 == 0) // Every ~2 seconds
                Debug.LogWarning("[YOLO] cameraScript is NULL!");
            return null;
        }
        
        var rawImg = cameraScript.GetComponent<RawImage>();
        if (rawImg == null)
        {
            if (Time.frameCount % 120 == 0)
                Debug.LogWarning("[YOLO] RawImage component not found on cameraScript!");
            return null;
        }
        
        if (rawImg.texture == null && Time.frameCount % 120 == 0)
        {
            Debug.LogWarning("[YOLO] RawImage.texture is NULL!");
        }
        
        return rawImg.texture;
    }

    public void ForceClearBoxes()
    {
        foreach (var box in boxPool) box.SetActive(false);
        currentDetectedWord = "";
    }

    public void ResetDetection()
    {
        startHoldTime = 0f;
        currentDetectedKeyword = "";
        lastFrameWord = "";
        transitionBuffer.Clear();
    }

    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();
        isRunning = false;
    }

    struct Detection
    {
        public Rect box;
        public float conf;
        public int classId;
    }
}