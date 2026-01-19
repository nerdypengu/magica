using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(InitializeScene());
    }

    // PUBLIC: Bisa dipanggil dari script lain (seperti cutscene) untuk mengaktifkan camera
    public void ActivateCamera()
    {
        StartCoroutine(ActivateCameraCoroutine());
    }

    IEnumerator ActivateCameraCoroutine()
    {
        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("MenuManager: Mengaktifkan WebCam...");
            camScript.AllowCameraStart();
            
            // Cek apakah kamera baru di-stop
            float timeSinceStop = Time.time - UniversalCamera.GetLastStopTime();
            if (timeSinceStop < 2f)
            {
                Debug.Log($"Camera baru di-stop {timeSinceStop:F1}s lalu, tunggu 0.5s...");
                yield return new WaitForSeconds(0.5f);
            }
            
            camScript.StartCamera();
        }
    }

    IEnumerator InitializeScene()
    {
        yield return new WaitForEndOfFrame();
        
        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            if (!camScript.gameObject.activeSelf)
            {
                Debug.Log("Reactivating Camera GameObject...");
                camScript.gameObject.SetActive(true);
            }
            
            Debug.Log("Mengaktifkan WebCam untuk Scene1...");
            camScript.AllowCameraStart();
            
            float timeSinceStop = Time.time - UniversalCamera.GetLastStopTime();
            if (timeSinceStop < 2f)
            {
                Debug.Log($"Camera baru di-stop {timeSinceStop:F1}s lalu, tunggu 0.5s...");
                yield return new WaitForSeconds(0.5f);
            }
            
            camScript.StartCamera();
            
            SmartCamera smartCam = camScript.GetComponent<SmartCamera>();
            if (smartCam != null && smartCam.useZoneSystem)
            {
                smartCam.InitializeToPlayerZone();
                Debug.Log("[Scene1] Camera initialized to player zone");
            }
        }

        YoloDetector yolo = FindFirstObjectByType<YoloDetector>();
        if (yolo != null)
        {
            // Reactivate GameObject if needed
            if (!yolo.gameObject.activeSelf)
            {
                Debug.Log("Reactivating YOLO Detector GameObject...");
                yolo.gameObject.SetActive(true);
            }
            
            if (!yolo.enabled)
            {
                Debug.Log("Mengaktifkan kembali AI Detector...");
                yolo.enabled = true;
            }
        }
        
        YoloAlphabet yoloAlpha = FindFirstObjectByType<YoloAlphabet>();
        if (yoloAlpha != null)
        {
            // Reactivate GameObject if needed
            if (!yoloAlpha.gameObject.activeSelf)
            {
                Debug.Log("Reactivating YOLO Alphabet GameObject...");
                yoloAlpha.gameObject.SetActive(true);
            }
            
            if (!yoloAlpha.enabled)
            {
                Debug.Log("Mengaktifkan kembali AI Alphabet...");
                yoloAlpha.enabled = true;
            }
        }
    }

    void Update()
    {
        // Cek input untuk save game (F5)
        if (Input.GetKeyDown(KeyCode.F5))
        {
            SaveGame();
        }
    }

    public void SaveGame()
    {
        // Cari NodePlayerController untuk mendapatkan current node
        NodePlayerController player = FindFirstObjectByType<NodePlayerController>();
        
        if (player == null || player.currentNode == null)
        {
            Debug.LogWarning("Tidak bisa save: Player tidak berada di node yang valid!");
            return;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        int currentNodeID = player.currentNode.nodeID;
        
        // Simpan ke MainMenu save system
        MainMenuManager.SaveGame(currentScene, currentNodeID);
        
        // Opsional: Tampilkan feedback visual/audio
        Debug.Log($"✓ GAME SAVED! Scene: {currentScene}, Node: {currentNodeID}");
        
        // TODO: Tambahkan UI feedback (misalnya "Game Saved!" text yang fade out)
    }

    public void Back()
    {
        Debug.Log("Kembali ke Main Menu...");
        
        // Cleanup: Stop WebCam (JANGAN deactivate GameObject!)
        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("Menghentikan WebCam...");
            camScript.StopCamera(); // Panggil method StopCamera() saja
        }

        // Cleanup: Matikan YOLO Detector (disable component, bukan gameObject)
        YoloDetector yolo = FindFirstObjectByType<YoloDetector>();
        if (yolo != null)
        {
            Debug.Log("Mematikan proses AI Detector...");
            yolo.enabled = false; // Disable component saja
        }
        
        // Cleanup: Matikan YOLO Alphabet (disable component, bukan gameObject)
        YoloAlphabet yoloAlpha = FindFirstObjectByType<YoloAlphabet>();
        if (yoloAlpha != null)
        {
            Debug.Log("Mematikan proses AI Alphabet...");
            yoloAlpha.enabled = false; // Disable component saja
        }

        string sceneName = "Main Menu";
        Debug.Log("Memuat scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }
}