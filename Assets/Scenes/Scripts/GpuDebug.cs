using UnityEngine;
using TMPro; // Don't forget this!

public class GpuDebugger : MonoBehaviour
{
    [Header("UI Reference")]
    public TextMeshProUGUI debugText; // Drag your Text object here

    void Start()
    {
        UpdateDebugInfo();
    }

    void UpdateDebugInfo()
    {
        if (debugText == null) return;

        string info = "--- HARDWARE INFO ---\n";

        // 1. CHECK GPU NAME
        info += $"GPU: <color=yellow>{SystemInfo.graphicsDeviceName}</color>\n";

        // 2. CHECK API (Vulkan is best for Android AI)
        string apiType = SystemInfo.graphicsDeviceType.ToString();
        string color = apiType.Contains("Vulkan") || apiType.Contains("Metal") ? "green" : "red";
        info += $"API: <color={color}>{apiType}</color>\n";

        // 3. CHECK MEMORY (VRAM & RAM)
        info += $"VRAM: {SystemInfo.graphicsMemorySize} MB\n";
        info += $"RAM: {SystemInfo.systemMemorySize} MB\n";

        // 4. CHECK IF GPU INSTANCING IS SUPPORTED (Good indicator of modern GPU)
        bool instancing = SystemInfo.supportsInstancing;
        info += $"Instancing: {(instancing ? "<color=green>Yes</color>" : "<color=red>No</color>")}\n";

        // 5. CHECK COMPUTE SHADERS (CRITICAL FOR YOLO ON MOBILE)
        bool compute = SystemInfo.supportsComputeShaders;
        info += $"Compute Shaders: {(compute ? "<color=green>Supported</color>" : "<color=red>UNSUPPORTED!</color>")}\n";

        debugText.text = info;
        
        // Print to console too
        Debug.Log(info.Replace("<color=yellow>", "").Replace("</color>", "").Replace("<color=green>", "").Replace("<color=red>", ""));
    }
}