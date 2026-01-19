using UnityEngine;

[System.Serializable]
public enum InteractionType
{
    NormalDialog,
    Quiz,
    GiveItem,
}

[System.Serializable]
public struct ConversationStep
{
    public InteractionType type;
    
    [Header("Dialog Content")]
    public string speakerName;
    [TextArea(3, 5)] public string text;

    [Header("Quiz Settings (Only if Quiz)")]
    public string correctAnswer; // Misal "BENAR" atau "A"
    public string correctFeedback; // Dialog jika benar
    public string wrongFeedback;   // Dialog jika salah

    [Header("Item Settings (Only if GiveItem)")]
    public string itemName;
    public Sprite itemIcon;
}