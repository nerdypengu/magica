using UnityEngine;

public class ScenePortal : MonoBehaviour
{
    [Header("Portal Settings")]
    public string sceneToLoad;
    public int spawnAtNodeID;
    public bool isBattleEncounter = false;
    
    [Header("YOLO Settings")]
    [Tooltip("Centang jika scene tujuan menggunakan YOLO Detector")]
    public bool nextSceneNeedsYolo = true;

    void Start()
    {
        // Logika menghancurkan portal tutorial (Sama seperti sebelumnya)
        if (sceneToLoad == "TutorialBattle" && PlayerPrefs.GetInt("BattleTutorialDone", 0) == 1)
        {
            // Hapus portal ini -> Player tidak bisa masuk lagi
            Destroy(this); 
        }
    }
}