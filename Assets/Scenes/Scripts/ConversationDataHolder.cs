using UnityEngine;

// Singleton to hold NPC conversation data when transitioning to Conversation scene
public class ConversationDataHolder : MonoBehaviour
{
    public static ConversationDataHolder Instance;

    // NPC Data
    public string npcID;
    public string npcName;
    public Sprite npcSprite;
    public Sprite backgroundSprite;
    public ConversationStep[] conversationSteps;
    
    // Scene management
    public string returnScene;
    public int returnNodeID;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetNPCData(NPCInteractable npc, string fromScene, int fromNode)
    {
        npcID = npc.npcID;
        npcName = npc.npcName;
        npcSprite = npc.npcSprite;
        backgroundSprite = npc.backgroundSprite;
        conversationSteps = npc.conversationSteps;
        returnScene = fromScene;
        returnNodeID = fromNode;

        Debug.Log($"ConversationDataHolder: Set data for '{npcName}', will return to {fromScene} Node {fromNode}");
    }

    public void ClearData()
    {
        npcID = null;
        npcName = null;
        backgroundSprite = null;
        npcSprite = null;
        conversationSteps = null;
        returnScene = null;
        returnNodeID = 0;
    }
}
