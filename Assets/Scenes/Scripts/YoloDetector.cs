using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using Unity.InferenceEngine; 
using Unity.Mathematics; 

public class YoloDetector : MonoBehaviour
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
    
    [Header("Cooldown / Buffer")]
    [Tooltip("How long to sleep after detecting a word (Seconds). Higher = Less Lag.")]
    public float detectionCooldown = 1.0f; 
    private bool triggerCooldown = false;

    [Header("Mode Settings")]
    public bool enableNavigationLogic = true; 

    [Header("Detection Settings")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.5f;
    [Range(0f, 1f)] public float iouThreshold = 0.45f;

    [Header("Zone / Barrier Settings")]
    [Range(0f, 0.5f)] public float leftBarrier = 0.35f; 
    [Range(0.5f, 1f)] public float rightBarrier = 0.65f; 

    [Header("Movement Settings")]
    public int smoothingLength = 8;   

    [Header("UI Settings")]
    public GameObject boxPrefab; 
    public Transform boxContainer; 
    public TextMeshProUGUI finalResultText;
    public RectTransform leftBarrierLine; 
    public RectTransform rightBarrierLine;

    [Header("Output for Game")]
    public string currentDetectedWord;

    // --- VARIABLES ---
    private Worker worker;
    private Model model;
    private Tensor<float> inputTensor; 
    
    private string[] labels;
    private List<GameObject> boxPool = new List<GameObject>();
    private const int NUM_CLASSES = 10; 
    private Queue<string> transitionBuffer = new Queue<string>();
    private bool isProcessing = false;
    private bool isRunning = false; 

    void Start()
    {
        model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute); 
        inputTensor = new Tensor<float>(new TensorShape(1, 3, modelInputSize, modelInputSize));
        Debug.Log($"[YOLO ENGINE] Worker Info: {worker.ToString()}");

        if (labelsFile)
            labels = labelsFile.text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
        UpdateDebugLines();

        StartCoroutine(DetectionLoop());
    }

    // --- OPTIMIZED LOOP WITH COOLDOWN ---
    IEnumerator DetectionLoop()
    {
        isRunning = true;
        WaitForSeconds fpsWait = new WaitForSeconds(1.0f / targetFPS);
        WaitForSeconds cooldownWait = new WaitForSeconds(detectionCooldown); // Cache the wait

        while (isRunning)
        {
            // Safety check: Skip if GameObject is inactive
            if (!gameObject.activeSelf)
            {
                yield return fpsWait;
                continue;
            }

            // 1. CHECK COOLDOWN
            if (triggerCooldown)
            {
                // Clear the board immediately as requested
                ForceClearBoxes();
                transitionBuffer.Clear(); 
                triggerCooldown = false; 

                // SLEEP! This gives your phone's CPU/GPU a massive break
                yield return cooldownWait; 
            }

            // 2. NORMAL DETECTION
            Texture camTexture = GetCameraTexture();
            if (camTexture != null && !isProcessing)
            {
                RunInferenceAsync(camTexture);
            }

            // 3. FPS LIMITER
            yield return fpsWait;
        }
    }

    void UpdateDebugLines()
    {
        if (leftBarrierLine != null) leftBarrierLine.gameObject.SetActive(enableNavigationLogic);
        if (rightBarrierLine != null) rightBarrierLine.gameObject.SetActive(enableNavigationLogic);

        if (!enableNavigationLogic || boxContainer == null) return;

        RectTransform canvasRect = boxContainer.GetComponent<RectTransform>();
        float w = canvasRect.rect.width;

        if(leftBarrierLine != null) 
            leftBarrierLine.anchoredPosition = new Vector2(w * leftBarrier, 0);
        
        if(rightBarrierLine != null) 
            rightBarrierLine.anchoredPosition = new Vector2(w * rightBarrier, 0);
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

            var rawOutput = worker.PeekOutput(0);
            if (rawOutput == null)
            {
                Debug.LogWarning("YOLO: PeekOutput returned null");
                return;
            }

            var outputTensor = rawOutput as Tensor<float>;
            if (outputTensor == null)
            {
                Debug.LogWarning("YOLO: Output is not Tensor<float>");
                return;
            }

            var outputAwaitable = outputTensor.ReadbackAndCloneAsync();
            using var output = await outputAwaitable;

            if (this == null || !gameObject.activeSelf || output == null) return;

            try
            {
                var outputArray = output.DownloadToArray();
                if (outputArray != null && outputArray.Length > 0)
                {
                    ProcessOutput(outputArray);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"YOLO: Failed to download array: {ex.Message}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Inference Error: {e.Message}");
        }
        finally
        {
            isProcessing = false;
        }
    }

    void ProcessOutput(float[] data)
    {
        // Safety check for null or empty data
        if (data == null || data.Length == 0)
        {
            Debug.LogWarning("YOLO: ProcessOutput received null or empty data");
            return;
        }

        foreach (var box in boxPool) box.SetActive(false);
        List<Detection> detections = new List<Detection>();

        // Dynamic Anchor Calculation
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
                    box = new Rect((x - w/2)/modelInputSize, (y - h/2)/modelInputSize, w/modelInputSize, h/modelInputSize), 
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
            string yoloLabel = (labels != null && mainHand.classId < labels.Length) ? labels[mainHand.classId] : "?";

            if (enableNavigationLogic)
            {
                float xCenter = mainHand.box.center.x;
                if (xCenter < leftBarrier) detectedWord = "KIRI"; 
                else if (xCenter > rightBarrier) detectedWord = "KANAN"; 
                else detectedWord = yoloLabel; 
            }
            else
            {
                detectedWord = yoloLabel;
            }
        }

        string finalWord = SmoothResult(detectedWord);
        currentDetectedWord = finalWord;

        // --- TRIGGER COOLDOWN LOGIC ---
        // If we found a stable word, set flag to sleep next frame
        if (!string.IsNullOrEmpty(finalWord))
        {
            // Only trigger cooldown if NOT navigating (or if you want jerky movement, remove this check)
            // Ideally for spelling we wait, for movement we don't.
            if (!enableNavigationLogic) 
            {
                triggerCooldown = true; 
            }
            else
            {
                triggerCooldown = true; 
            }
        }
        // ------------------------------

        if (finalResultText != null)
        {
            finalResultText.text = !string.IsNullOrEmpty(finalWord) ? $"{finalWord}" : "Detecting ...";
        }

        DrawBoxes(finalDetections);
    }

    // --- GARBAGE COLLECTION OPTIMIZED SMOOTHING ---
    // Removed LINQ GroupBy which causes mobile lag
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

        if (maxCount > transitionBuffer.Count / 2)
        {
            return bestCandidate;
        }
        return null;
    }

    Texture GetCameraTexture()
    {
        if (cameraScript == null) return null;
        var rawImg = cameraScript.GetComponent<RawImage>();
        return (rawImg != null) ? rawImg.texture : null;
    }

    void DrawBoxes(List<Detection> detections)
    {
        RectTransform canvasRect = boxContainer.GetComponent<RectTransform>();
        if (canvasRect.rect.width == 0) return;

        int poolIndex = 0;
        foreach (var det in detections)
        {
            GameObject boxObj;
            if (poolIndex < boxPool.Count) boxObj = boxPool[poolIndex];
            else { boxObj = Instantiate(boxPrefab, boxContainer); boxPool.Add(boxObj); }
            poolIndex++;
            boxObj.SetActive(true);

            float uiX = det.box.x * canvasRect.rect.width;
            float uiY = -det.box.y * canvasRect.rect.height; 
            float uiW = det.box.width * canvasRect.rect.width;
            float uiH = det.box.height * canvasRect.rect.height;

            string labelName = (labels != null && det.classId < labels.Length) ? labels[det.classId] : $"{det.classId}";
            boxObj.GetComponent<BoundingBox>().SetBox(new Rect(uiX, uiY, uiW, uiH), labelName, det.conf, Color.green);
        }
    }

    List<Detection> NMS(List<Detection> dets)
    {
        List<Detection> result = new List<Detection>();
        dets.Sort((a, b) => b.conf.CompareTo(a.conf)); 
        while (dets.Count > 0)
        {
            Detection best = dets[0];
            result.Add(best);
            dets.RemoveAt(0);
            for (int i = dets.Count - 1; i >= 0; i--)
            {
                if (CalculateIoU(best.box, dets[i].box) > iouThreshold) dets.RemoveAt(i);
            }
        }
        return result;
    }

    float CalculateIoU(Rect boxA, Rect boxB)
    {
        float xA = Mathf.Max(boxA.x, boxB.x);
        float yA = Mathf.Max(boxA.y, boxB.y);
        float xB = Mathf.Min(boxA.xMax, boxB.xMax);
        float yB = Mathf.Min(boxA.yMax, boxB.yMax);
        float interArea = Mathf.Max(0, xB - xA) * Mathf.Max(0, yB - yA);
        float boxAArea = boxA.width * boxA.height;
        float boxBArea = boxB.width * boxB.height;
        return interArea / (boxAArea + boxBArea - interArea);
    }

    void OnDestroy() 
    { 
        isRunning = false;
        worker?.Dispose();
        inputTensor?.Dispose();
    }

    public void ForceClearBoxes()
    {
        foreach (var box in boxPool)
        {
            if(box != null) box.SetActive(false);
        }
        if (finalResultText != null) finalResultText.text = "";
        currentDetectedWord = "";
    }

    struct Detection { public Rect box; public float conf; public int classId; }
}