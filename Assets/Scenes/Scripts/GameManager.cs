using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Wajib ada untuk memproses data Event

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // --- 1. DATA POSISI NODE ---
    private Dictionary<string, int> savedNodePositions = new Dictionary<string, int>();
    public int overrideTargetNodeID = 0;
    private const string NODE_POSITIONS_KEY = "SavedNodePositions";

    // --- 2. DATA STORY/EVENT (BARU) ---
    private const string EVENT_PREFS_KEY = "CompletedEvents";
    private HashSet<string> completedEvents = new HashSet<string>();

    // --- 3. FPS VARIABLES ---
    private float deltaTime = 0.0f;
    
    // --- DEBUG UI SETTINGS ---
    public bool showDebugUI = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load data saat game baru nyala
            LoadEvents();
            LoadNodePositions();
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    void Start() 
    {
        Debug.Log("=== STATUS GAME MANAGER ===");
        Debug.Log($"Events Loaded: {completedEvents.Count}");
        Debug.Log($"Node Positions Loaded: {savedNodePositions.Count}");
        foreach (var pos in savedNodePositions)
        {
            Debug.Log($"  - {pos.Key}: Node {pos.Value}");
        }
        
        // Initialize Player Stats & Battle Stages
        InitializePlayerSystems();
    }
    
    void InitializePlayerSystems()
    {
        // Ensure PlayerStats exists
        if (PlayerStats.Instance == null)
        {
            GameObject statsObj = new GameObject("PlayerStats");
            statsObj.AddComponent<PlayerStats>();
            Debug.Log("[GameManager] PlayerStats initialized");
        }
        
        // Subscribe to level up events
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnLevelUp += OnPlayerLevelUp;
        }
    }
    
    void OnPlayerLevelUp(int newLevel)
    {
        Debug.Log($"<color=yellow>[GameManager] Player reached Level {newLevel}!</color>");
    }

    void Update()
    {
        // Hitung FPS (Smoothed)
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    // ========================================================================
    // BAGIAN 1: POSISI NODE
    // ========================================================================

    // A. Load dari PlayerPrefs (String -> Dictionary)
    void LoadNodePositions()
    {
        string data = PlayerPrefs.GetString(NODE_POSITIONS_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            string[] entries = data.Split(';');
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                string[] parts = entry.Split(':');
                if (parts.Length == 2)
                {
                    string sceneName = parts[0];
                    if (int.TryParse(parts[1], out int nodeID))
                    {
                        savedNodePositions[sceneName] = nodeID;
                    }
                }
            }
            Debug.Log($"GameManager: Loaded {savedNodePositions.Count} node positions from PlayerPrefs");
        }
    }

    // B. Simpan ke PlayerPrefs (Dictionary -> String)
    void SaveNodePositions()
    {
        List<string> entries = new List<string>();
        foreach (var kvp in savedNodePositions)
        {
            entries.Add($"{kvp.Key}:{kvp.Value}");
        }
        string data = string.Join(";", entries);
        PlayerPrefs.SetString(NODE_POSITIONS_KEY, data);
        PlayerPrefs.Save();
    }

    public void SavePosition(string sceneName, int nodeID)
    {
        if (savedNodePositions.ContainsKey(sceneName))
            savedNodePositions[sceneName] = nodeID;
        else
            savedNodePositions.Add(sceneName, nodeID);
            
        SaveNodePositions(); // Persist to PlayerPrefs
        Debug.Log($"GameManager: Saved {sceneName} at Node {nodeID}");
    }

    public int GetSavedPosition(string sceneName)
    {
        if (savedNodePositions.ContainsKey(sceneName))
            return savedNodePositions[sceneName];
        return 0; 
    }

    // C. Reset node positions (untuk New Game)
    public void ResetAllNodePositions()
    {
        savedNodePositions.Clear();
        PlayerPrefs.DeleteKey(NODE_POSITIONS_KEY);
        PlayerPrefs.Save();
        Debug.Log("GameManager: Node Positions Reset!");
    }

    // ========================================================================
    // BAGIAN 2: STORY / EVENT SYSTEM (BARU)
    // ========================================================================

    // A. Load dari PlayerPrefs (String -> HashSet)
    void LoadEvents()
    {
        string data = PlayerPrefs.GetString(EVENT_PREFS_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            string[] events = data.Split(',');
            foreach (var e in events)
            {
                if(!string.IsNullOrEmpty(e)) completedEvents.Add(e);
            }
        }
    }

    // B. Simpan ke PlayerPrefs (HashSet -> String)
    private void SaveEvents()
    {
        string data = string.Join(",", completedEvents);
        PlayerPrefs.SetString(EVENT_PREFS_KEY, data);
        PlayerPrefs.Save();
    }

    // C. Cek apakah event sudah selesai?
    public bool IsEventComplete(string eventID)
    {
        return completedEvents.Contains(eventID);
    }

    // D. Tandai event selesai
    public void MarkEventComplete(string eventID)
    {
        if (!completedEvents.Contains(eventID))
        {
            completedEvents.Add(eventID);
            SaveEvents();
        }
    }

    // E. Reset Semua (Untuk tombol New Game)
    public void ResetAllEvents()
    {
        completedEvents.Clear();
        PlayerPrefs.DeleteKey(EVENT_PREFS_KEY);
        PlayerPrefs.Save();
        Debug.Log("GameManager: Story Progress Reset!");
    }

    // F. Reset SEMUA Data (Events + Node Positions + Player Stats)
    public void ResetAllGameData()
    {
        // Clear in-memory data first
        completedEvents.Clear();
        savedNodePositions.Clear();
        overrideTargetNodeID = 0;
        
        // Clear PlayerPrefs
        PlayerPrefs.DeleteKey(EVENT_PREFS_KEY);
        PlayerPrefs.DeleteKey(NODE_POSITIONS_KEY);
        
        // Reset navigation progress (FIXED: use correct keys)
        PlayerPrefs.DeleteKey("NavigationWaypointIndex");
        PlayerPrefs.DeleteKey("NavigationIsActive");
        
        // Reset player stats (level & exp)
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ClearSavedData();
            PlayerStats.Instance.ResetStats();
        }
        
        PlayerPrefs.Save();
        
        Debug.Log("GameManager: All Game Data Reset! (Events, Positions, Stats, Navigation)");
    }

    [ContextMenu("DEBUG: Print Console")]
    public void DebugSavedData()
    {
        Debug.Log("=== DATA POSISI ===");
        foreach (var item in savedNodePositions) Debug.Log($"{item.Key} : Node {item.Value}");
        
        Debug.Log("=== DATA EVENTS ===");
        foreach (var ev in completedEvents) Debug.Log($"[DONE] {ev}");
    }

    void OnGUI()
    {
        if (!showDebugUI) return;

        // --- FPS CALCULATION ---
        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        string fpsText = string.Format("{0:0.} FPS", fps);
        string msText = string.Format("({0:0.0} ms)", msec);

        // PERBESAR KOTAK (Agar muat list event)
        GUI.Box(new Rect(10, 10, 350, 500), "Game Manager Debug");

        // --- FPS DISPLAY (Besar & Berwarna) ---
        GUIStyle fpsStyle = new GUIStyle(GUI.skin.label);
        fpsStyle.fontSize = 45; 
        fpsStyle.fontStyle = FontStyle.Bold;
        fpsStyle.normal.textColor = fps < 20 ? Color.red : (fps < 40 ? Color.yellow : Color.green);
        
        GUI.Label(new Rect(20, 30, 320, 50), fpsText, fpsStyle);
        GUI.Label(new Rect(25, 75, 320, 20), msText);

        // --- INFO LAIN ---
        GUI.Label(new Rect(20, 110, 280, 20), $"Target Override ID: {overrideTargetNodeID}");

        // List Posisi Node
        GUI.Label(new Rect(20, 140, 280, 20), "--- Saved Positions ---");
        int yPos = 165;
        foreach (var item in savedNodePositions)
        {
            GUI.Label(new Rect(20, yPos, 280, 20), $"{item.Key} : Node {item.Value}");
            yPos += 20;
        }

        // List Completed Events (BARU)
        yPos += 10;
        GUI.Label(new Rect(20, yPos, 280, 20), "--- Completed Events ---");
        yPos += 20;
        
        foreach (var ev in completedEvents)
        {
            GUI.Label(new Rect(20, yPos, 280, 20), $"[✓] {ev}");
            yPos += 20;
        }
    }
}