using UnityEngine;

public class NPCInteractable : MonoBehaviour
{
    [Header("Save Data Settings")]
    [Tooltip("ID Unik untuk NPC ini")]
    public string npcID;

    [Header("NPC Profile")]
    public string npcName = "Warga Desa";
    public Sprite npcSprite;

    [Header("Scene Settings")]
    [Tooltip("Background untuk conversation scene dengan NPC ini")]
    public Sprite backgroundSprite;

    [Header("Conversation Flow")]
    public ConversationStep[] conversationSteps;
    
    public bool isInteractionCompleted 
    {
        get 
        {
            // Jika ID kosong, anggap tidak pernah selesai (untuk testing)
            if (string.IsNullOrEmpty(npcID)) return false;
            
            // Cek buku catatan (PlayerPrefs)
            return PlayerPrefs.GetInt(npcID + "_Done", 0) == 1;
        }
        set 
        {
            if (!string.IsNullOrEmpty(npcID) && value == true)
            {
                // Simpan ke buku catatan
                PlayerPrefs.SetInt(npcID + "_Done", 1);
                PlayerPrefs.Save();
            }
        }
    }
}