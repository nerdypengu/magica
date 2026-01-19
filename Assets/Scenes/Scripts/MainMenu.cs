using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    private const string SAVE_EXISTS_KEY = "HasSaveData";
    private const string LAST_SCENE_KEY = "LastScene";
    private const string LAST_NODE_KEY = "LastNodeID";
    
    [Header("UI Message")]
    public TextMeshProUGUI messageText; // Drag UI Text here to show messages

    public void StartGame()
    {
        string sceneName = "Scene1";
        
        // IMPORTANT: Delete PlayerPrefs FIRST before GameManager clears memory
        PlayerPrefs.DeleteAll(); // Nuclear option - clear EVERYTHING
        PlayerPrefs.Save();
        
        Debug.Log("[MainMenu] PlayerPrefs completely cleared!");
        
        // Reset GameManager data (clear in-memory data)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetAllGameData();
        }

        Debug.Log("[MainMenu] Complete data reset - Starting NEW GAME");
        SceneManager.LoadScene(sceneName);
    }

    public void ContinueGame()
    {
        if (!HasSaveData())
        {
            Debug.LogWarning("Tidak ada data save yang tersedia!");
            ShowMessage("Tidak ada data save!\nMulai game baru terlebih dahulu.");
            return;
        }

        // Load last scene from old save system
        string lastScene = PlayerPrefs.GetString(LAST_SCENE_KEY, "Scene1");
        
        // Get last node position from GameManager's new system
        int lastNode = 0;
        if (GameManager.Instance != null)
        {
            lastNode = GameManager.Instance.GetSavedPosition(lastScene);
            
            // Set override to spawn at last visited node
            if (lastNode != 0)
            {
                GameManager.Instance.overrideTargetNodeID = lastNode;
                Debug.Log($"Melanjutkan game dari {lastScene}, Node {lastNode}");
            }
            else
            {
                Debug.Log($"Melanjutkan game dari {lastScene} (default spawn)");
            }
        }

        SceneManager.LoadScene(lastScene);
    }

    public bool HasSaveData()
    {
        return PlayerPrefs.HasKey(SAVE_EXISTS_KEY) && PlayerPrefs.GetInt(SAVE_EXISTS_KEY) == 1;
    }

    public static void SaveGame(string currentScene, int currentNode)
    {
        PlayerPrefs.SetInt(SAVE_EXISTS_KEY, 1);
        PlayerPrefs.SetString(LAST_SCENE_KEY, currentScene);
        PlayerPrefs.SetInt(LAST_NODE_KEY, currentNode);
        PlayerPrefs.Save();
        
        // IMPORTANT: Also save to GameManager's persistent system
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SavePosition(currentScene, currentNode);
        }
        
        Debug.Log($"Game berhasil disimpan di {currentScene}, Node {currentNode}");
    }
    
    void ShowMessage(string message)
    {
        if (messageText != null)
        {
            StopAllCoroutines(); // Stop any previous message
            StartCoroutine(ShowMessageRoutine(message));
        }
    }
    
    IEnumerator ShowMessageRoutine(string message)
    {
        messageText.text = message;
        messageText.gameObject.SetActive(true);
        
        yield return new WaitForSeconds(1f);
        
        messageText.gameObject.SetActive(false);
    }

    public void OpenSettings()
    {
        Debug.Log("Tombol Pengaturan diklik!");
    }

    public void QuitGame()
    {
        Debug.Log("Sedang menutup aplikasi...");

        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("Menutup akses WebCam...");
            camScript.StopCamera(); // Properly stop and destroy
        }

        YoloDetector yolo = FindFirstObjectByType<YoloDetector>();
        if (yolo != null)
        {
            Debug.Log("Mematikan proses AI...");
            yolo.enabled = false;
        }
        
        YoloAlphabet yoloAlpha = FindFirstObjectByType<YoloAlphabet>();
        if (yoloAlpha != null)
        {
            yoloAlpha.enabled = false;
        }

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}