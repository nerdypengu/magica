using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using Unity.InferenceEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs; 

public class YoloAlphabet : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset modelAsset;
    public TextAsset labelsFile;
    
    [Header("Input Settings")]
    public UniversalCamera cameraScript;
    public int modelInputSize = 320; 

    [Header("Detection Settings")]
    [Range(0f, 1f)] public float confidenceThreshold = 0.5f;
    [Range(0f, 1f)] public float iouThreshold = 0.45f;

    [Header("Dynamic Letter Settings")]
    // Normalized thresholds (converted from Python's 30px and 15px)
    // 30px / 640 ≈ 0.047
    // 15px / 640 ≈ 0.023
    public float jDropThreshold = 0.047f;  
    public float jCurveThreshold = 0.023f;
    public int trajectoryLength = 30; // Matches Python deque(maxlen=30)
    public int smoothingLength = 8;   // Matches Python deque(maxlen=8)

    [Header("UI Settings")]
    public GameObject boxPrefab; 
    public Transform boxContainer; 
    public TextMeshProUGUI finalResultText;
    
    [Header("Optimization")]
    public int boxPoolSize = 10; // Pre-warm pool size

    [Header("Output for Game")]
    public string currentDetectedWord;

    // --- INFERENCE ENGINE VARIABLES ---
    private Worker worker;
    private Model model;
    private Tensor<float> inputTensor; 
    
    private string[] labels;
    private List<GameObject> boxPool = new List<GameObject>();
    private const int NUM_CLASSES = 26; // Assuming Alphabet A-Z based on Python logic

    // --- BUFFERS ---
    // Stores (x, y) coordinates for trajectory
    private Queue<Vector2> trajectory = new Queue<Vector2>();
    private Queue<string> transitionBuffer = new Queue<string>();

    private bool isProcessing = false;

    // Burst-compiled job for detection parsing
    [BurstCompile]
    struct DetectionParseJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> data;
        public int numAnchors;
        public int numClasses;
        public float confidenceThreshold;
        public int modelInputSize;
        
        [WriteOnly] public NativeArray<Detection> detections;
        [WriteOnly] public NativeArray<bool> isValid;
        
        public void Execute(int i)
        {
            float maxConf = 0f;
            int maxClass = -1;
            
            for (int c = 0; c < numClasses; c++)
            {
                float conf = data[(4 + c) * numAnchors + i];
                if (conf > maxConf)
                {
                    maxConf = conf;
                    maxClass = c;
                }
            }
            
            if (maxConf > confidenceThreshold)
            {
                float x = data[0 * numAnchors + i];
                float y = data[1 * numAnchors + i];
                float w = data[2 * numAnchors + i];
                float h = data[3 * numAnchors + i];
                
                detections[i] = new Detection
                {
                    box = new Rect(
                        (x - w / 2) / modelInputSize,
                        (y - h / 2) / modelInputSize,
                        w / modelInputSize,
                        h / modelInputSize
                    ),
                    conf = maxConf,
                    classId = maxClass
                };
                
                isValid[i] = true;
            }
            else
            {
                isValid[i] = false;
            }
        }
    }

    void Start()
    {
        model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, BackendType.GPUCompute);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, modelInputSize, modelInputSize));

        if (labelsFile)
            labels = labelsFile.text.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        // Pre-warm box pool
        for (int i = 0; i < boxPoolSize; i++)
        {
            GameObject box = Instantiate(boxPrefab, boxContainer);
            box.SetActive(false);
            boxPool.Add(box);
        }
    }

    void Update()
    {
        Texture camTexture = GetCameraTexture();
        if (camTexture == null) return;

        if (!isProcessing)
        {
            RunInferenceAsync(camTexture);
        }
    }

    async void RunInferenceAsync(Texture sourceTexture)
    {
        isProcessing = true;
        try
        {
            if (this == null) return; 

            RenderTexture rt = RenderTexture.GetTemporary(modelInputSize, modelInputSize, 0);
            
            float aspect = (float)sourceTexture.width / sourceTexture.height;
            
            Vector2 scale = new Vector2(1f, 1f);
            Vector2 offset = Vector2.zero;

            if (aspect > 1)
            {
                scale.x = 1f / aspect; 
                offset.x = (1f - scale.x) / 2f; 
            }
            else
            {
                scale.y = aspect;
                offset.y = (1f - scale.y) / 2f;
            }

            Graphics.Blit(sourceTexture, rt, scale, offset); 

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
            if (this != null) Debug.LogError($"Inference Error: {e.Message}");
        }
        finally
        {
            if (this != null) isProcessing = false;
        }
    }
    void ProcessOutput(float[] data)
    {
        foreach (var box in boxPool) box.SetActive(false);
        
        // 1. Parse YOLO Output using Burst-compiled job
        int numChannels = 4 + NUM_CLASSES;
        int numAnchors = data.Length / numChannels;
        
        // Allocate native arrays
        NativeArray<float> nativeData = new NativeArray<float>(data, Allocator.TempJob);
        NativeArray<Detection> nativeDetections = new NativeArray<Detection>(numAnchors, Allocator.TempJob);
        NativeArray<bool> isValid = new NativeArray<bool>(numAnchors, Allocator.TempJob);
        
        // Schedule job
        var job = new DetectionParseJob
        {
            data = nativeData,
            numAnchors = numAnchors,
            numClasses = NUM_CLASSES,
            confidenceThreshold = confidenceThreshold,
            modelInputSize = modelInputSize,
            detections = nativeDetections,
            isValid = isValid
        };
        
        var handle = job.Schedule(numAnchors, 64);
        handle.Complete();
        
        // Extract valid detections
        List<Detection> detections = new List<Detection>();
        for (int i = 0; i < numAnchors; i++)
        {
            if (isValid[i])
            {
                detections.Add(nativeDetections[i]);
            }
        }
        
        // Dispose native arrays
        nativeData.Dispose();
        nativeDetections.Dispose();
        isValid.Dispose();

        List<Detection> finalDetections = NMS(detections);
        
        string detectedLetter = null;

        // ---------------------------------------------------------
        // LOGIC FROM PYTHON SCRIPT (Step 2 equivalent)
        // ---------------------------------------------------------
        
        // Priority 1: Check primary detection
        if (finalDetections.Count > 0)
        {
            // Pick strongest detection (highest confidence) or largest box
            finalDetections.Sort((a, b) => b.conf.CompareTo(a.conf));
            Detection primary = finalDetections[0];

            string label = (labels != null && primary.classId < labels.Length) ? labels[primary.classId] : "?";

            // Case 1: Static Letters (Not J, Not G)
            if (label != "J" && label != "G")
            {
                detectedLetter = label;
                // Reset trajectory if not tracking J
                // trajectory.Clear(); // Optional: Clear if strict, or keep for transition smoothing
            }
            // Case 2: Dynamic J
            else if (label == "J")
            {
                detectedLetter = CheckTrajectoryForJ(primary.box);
            }
        }

        // Case 3: G (Two fists stacked) - Fallback logic if nothing detected yet
        // The python code checks for G if detected_letter is None AND detections >= 2
        if (detectedLetter == null && finalDetections.Count >= 2)
        {
            detectedLetter = CheckStackedHandsForG(finalDetections);
        }

        // ---------------------------------------------------------
        // Step 3. Transition Smoothing
        // ---------------------------------------------------------
        string finalWord = SmoothResult(detectedLetter);

        currentDetectedWord = finalWord;

        if (finalResultText != null)
        {
            finalResultText.text = !string.IsNullOrEmpty(finalWord) ? $"Detected: {finalWord}" : "Detected: ...";
        }

        DrawBoxes(finalDetections);
    }

    // Logic: Checks for downward curve and rightward flick
    string CheckTrajectoryForJ(Rect box)
    {
        // Add current center to trajectory
        trajectory.Enqueue(box.center);
        if (trajectory.Count > trajectoryLength) trajectory.Dequeue();

        if (trajectory.Count >= 15) // Wait for enough data points
        {
            Vector2 start = trajectory.Peek(); // First item
            Vector2 end = trajectory.Last();   // Last item
            
            // Get midpoint (approximate)
            Vector2 mid = trajectory.ElementAt(trajectory.Count / 2);

            // Python Logic Translation:
            // (end[1] - start[1] > 30) -> Downward movement (Unity Y is inverted in UI, so be careful. Here Y is normalized 0-1)
            // Note: In Unity Texture/Tensor space, Y=0 is bottom usually, but YOLO output is usually Top-Left origin.
            // If Y increases as we go down: end.y > start.y
            
            float dy = end.y - start.y;
            float dx = end.x - mid.x;

            // Check thresholds
            if (dy > jDropThreshold && dx > jCurveThreshold)
            {
                return "J";
            }
        }
        return null; // Not enough movement yet to confirm J
    }

    // Logic: Checks if two boxes are stacked vertically
    string CheckStackedHandsForG(List<Detection> dets)
    {
        // Sort by confidence to get top 2
        dets.Sort((a, b) => b.conf.CompareTo(a.conf));
        
        Detection d1 = dets[0];
        Detection d2 = dets[1];

        Vector2 c1 = d1.box.center;
        Vector2 c2 = d2.box.center;

        float dx = Mathf.Abs(c1.x - c2.x);
        float dy = Mathf.Abs(c1.y - c2.y);

        // Python Logic: if dy > 1.5 * dx
        if (dy > 1.5f * dx)
        {
            return "G";
        }
        
        return null;
    }

    string SmoothResult(string currentLetter)
    {
        // Only append if not null (Python code logic)
        if (currentLetter != null)
        {
            transitionBuffer.Enqueue(currentLetter);
            if (transitionBuffer.Count > smoothingLength) 
                transitionBuffer.Dequeue();
        }

        if (transitionBuffer.Count == 0) return null;

        // Find most common
        var groups = transitionBuffer.GroupBy(x => x);
        var mostCommon = groups.OrderByDescending(g => g.Count()).First();

        // Python Logic: if count > len // 2
        if (mostCommon.Count() > transitionBuffer.Count / 2)
        {
            return mostCommon.Key;
        }
        
        return null;
    }

    Texture GetCameraTexture()
    {
        var rawImg = cameraScript.GetComponent<RawImage>();
        if (rawImg != null && rawImg.texture != null)
            return rawImg.texture;
        return null;
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
        worker?.Dispose();
        inputTensor?.Dispose();
    }

    struct Detection { public Rect box; public float conf; public int classId; }
}