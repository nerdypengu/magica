using UnityEngine;

[System.Serializable]
public struct StoryLine 
{
    public string speakerName; // Kosongkan jika narasi
    [TextArea(3, 5)] public string textContent;
}

public class NodeStoryTrigger : MonoBehaviour
{
    [Header("Identitas Event (Wajib Unik)")]
    [Tooltip("Contoh: IntroScene, KetemuBoss, TutorialSelesai")]
    public string eventID; 
    
    [Header("Syarat (Chain Event)")]
    [Tooltip("Isi dengan EventID lain jika event ini butuh syarat. Kosongkan jika tidak butuh.")]
    public string requiredEventID = ""; 

    [Header("Isi Cerita")]
    public StoryLine[] storySequence;

    [Header("References")]
    public DialogManager dialogManager; 

    // --- LOGIKA PENGECEKAN BARU (VIA GAME MANAGER) ---
    public bool ShouldPlay()
    {
        // 1. Validasi ID
        if (string.IsNullOrEmpty(eventID)) return false;

        // Safety check jika GameManager belum siap (misal saat testing scene terpisah)
        if (GameManager.Instance == null) 
        {
            Debug.LogWarning("GameManager tidak ditemukan! Event dianggap boleh main (Mode Debug).");
            return true;
        }

        // 2. Cek apakah Event INI sudah pernah tamat?
        if (GameManager.Instance.IsEventComplete(eventID)) 
        {
            return false; // Sudah ada di list GameManager, jangan main lagi.
        }

        // 3. Cek Syarat Event Lain (Chain Event)
        // Misal: Trigger ini hanya aktif jika pemain sudah menyelesaikan 'IntroScene'
        if (!string.IsNullOrEmpty(requiredEventID))
        {
            if (!GameManager.Instance.IsEventComplete(requiredEventID))
            {
                return false; // Syarat belum terpenuhi (Event sebelumnya belum kelar)
            }
        }

        return true;
    }

    // --- FUNGSI CARI DIALOG MANAGER (TETAP SAMA) ---
    public DialogManager GetActiveDialogManager()
    {
        // 1. Jika referensi kosong, cari otomatis (TERMASUK YANG MATI/INACTIVE)
        if (dialogManager == null)
        {
            dialogManager = FindFirstObjectByType<DialogManager>(FindObjectsInactive.Include);
        }

        // 2. Jika ketemu, BANGUNKAN SECARA PAKSA!
        if (dialogManager != null)
        {
            dialogManager.gameObject.SetActive(true); // Nyalakan GameObject-nya
            dialogManager.enabled = true;             // Nyalakan Script-nya
        }
        else
        {
            Debug.LogError($"[NodeStoryTrigger] Gagal menemukan DialogManager di scene ini!");
        }

        return dialogManager;
    }

    // --- UPDATE: MENYIMPAN KE GAMEMANAGER ---
    public void MarkAsPlayed()
    {
        if (GameManager.Instance != null)
        {
            // Lapor ke Boss (GameManager) kalau event ini sudah selesai
            GameManager.Instance.MarkEventComplete(eventID);
            Debug.Log($"[Story] Event '{eventID}' disimpan ke GameManager.");
        }
        else
        {
            // Fallback (Jaga-jaga)
            PlayerPrefs.SetInt(eventID, 1);
            PlayerPrefs.Save();
        }
    }
}