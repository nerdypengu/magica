using UnityEngine;

// Singleton to hold Battle data when transitioning to Battle scene
public class BattleDataHolder : MonoBehaviour
{
    public static BattleDataHolder Instance;

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

    public void SetBattleData(string fromScene, int fromNode)
    {
        returnScene = fromScene;
        returnNodeID = fromNode;

        Debug.Log($"[BattleDataHolder] Set data - will return to {fromScene} Node {fromNode}");
    }

    public void ClearData()
    {
        returnScene = null;
        returnNodeID = 0;
    }
}
