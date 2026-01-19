using UnityEngine;
using UnityEngine.SceneManagement; 

public class SimpleSceneLoader : MonoBehaviour
{
    [Tooltip("Nama Scene yang ingin dituju (misal: MapScene)")]
    public string sceneToLoad = "Scene1";

    // Fungsi ini dipanggil saat tombol diklik
    public void LoadScene()
    {
        Debug.Log("Persiapan pindah scene...");

        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("Mematikan WebCam agar tidak crash saat loading...");
            camScript.gameObject.SetActive(false);
        }

        YoloAlphabet yolo = FindFirstObjectByType<YoloAlphabet>();
        if (yolo != null)
        {
            yolo.gameObject.SetActive(false);
        }

        // 2. RESET WAKTU (Normal Speed)
        Time.timeScale = 1f; 

        // 3. PINDAH SCENE
        Debug.Log($"Memuat scene: {sceneToLoad}");
        SceneManager.LoadScene(sceneToLoad);
    }
}