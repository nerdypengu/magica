using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Level & Experience")]
    public int currentLevel = 1;
    public int currentExp = 0;
    public int expToNextLevel = 100; // EXP needed for level 2

    [Header("EXP Curve Settings")]
    [Tooltip("Base EXP untuk level 2")]
    public int baseExpRequirement = 100;
    [Tooltip("Multiplier untuk setiap level (1.5 = +50% per level)")]
    public float expMultiplier = 1.5f;

    [Header("Battle Rewards")]
    [Tooltip("EXP reward per battle win (adjust supaya 2-3 battle = level up)")]
    public int expPerBattle = 40;

    public event Action<int, int> OnExpChanged;
    public event Action<int> OnLevelUp;

    private const string LEVEL_KEY = "Player_Level";
    private const string EXP_KEY = "Player_Exp";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPlayerStats();
    }

    void Start()
    {
        CalculateExpRequirement();
        Debug.Log($"[PlayerStats] Loaded - Level: {currentLevel} | EXP: {currentExp}/{expToNextLevel}");
    }

    public void AddExp(int amount)
    {
        currentExp += amount;
        Debug.Log($"[PlayerStats] +{amount} EXP | Total: {currentExp}/{expToNextLevel}");

        OnExpChanged?.Invoke(currentExp, expToNextLevel);

        while (currentExp >= expToNextLevel)
        {
            LevelUp();
        }

        SavePlayerStats();
    }

    void LevelUp()
    {
        currentExp -= expToNextLevel;
        currentLevel++;

        Debug.Log($"<color=yellow>★ LEVEL UP! ★ Now Level {currentLevel}</color>");

        CalculateExpRequirement();

        OnLevelUp?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentExp, expToNextLevel);

        SavePlayerStats();
    }

    void CalculateExpRequirement()
    {
        expToNextLevel = Mathf.RoundToInt(baseExpRequirement * Mathf.Pow(expMultiplier, currentLevel - 1));
        Debug.Log($"[PlayerStats] Level {currentLevel} requires {expToNextLevel} EXP to level up");
    }

    public bool CanAccessBattle(int requiredLevel)
    {
        return currentLevel >= requiredLevel;
    }

    public string GetBattleRequirementMessage(int requiredLevel)
    {
        if (currentLevel >= requiredLevel)
            return "Ready!";
        
        int levelsNeeded = requiredLevel - currentLevel;
        return $"Requires Level {requiredLevel} (Need {levelsNeeded} more level{(levelsNeeded > 1 ? "s" : "")})";
    }

    // ========================================================================
    // SAVE / LOAD
    // ========================================================================

    void SavePlayerStats()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, currentLevel);
        PlayerPrefs.SetInt(EXP_KEY, currentExp);
        PlayerPrefs.Save();
        Debug.Log($"[PlayerStats] Saved - Level: {currentLevel} | EXP: {currentExp}");
    }

    void LoadPlayerStats()
    {
        if (PlayerPrefs.HasKey(LEVEL_KEY))
        {
            currentLevel = PlayerPrefs.GetInt(LEVEL_KEY, 1);
            currentExp = PlayerPrefs.GetInt(EXP_KEY, 0);
            Debug.Log($"[PlayerStats] Loaded from save - Level: {currentLevel} | EXP: {currentExp}");
        }
        else
        {
            // New game defaults
            currentLevel = 1;
            currentExp = 0;
            Debug.Log("[PlayerStats] New game - Starting at Level 1");
        }
    }

    public void ResetStats()
    {
        currentLevel = 1;
        currentExp = 0;
        CalculateExpRequirement();
        SavePlayerStats();
        
        OnLevelUp?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentExp, expToNextLevel);
        
        Debug.Log("[PlayerStats] Stats reset to Level 1");
    }

    // ========================================================================
    // TESTING / DEBUG METHODS
    // ========================================================================

    [ContextMenu("Clear Saved Data (PlayerPrefs)")]
    public void ClearSavedData()
    {
        PlayerPrefs.DeleteKey(LEVEL_KEY);
        PlayerPrefs.DeleteKey(EXP_KEY);
        PlayerPrefs.Save();
        Debug.Log("<color=orange>[PlayerStats] Cleared all saved data! Restart the game to apply.</color>");
    }

    [ContextMenu("Add 100 EXP (Test)")]
    public void TestAdd100Exp()
    {
        AddExp(100);
    }

    [ContextMenu("Add 500 EXP (Test)")]
    public void TestAdd500Exp()
    {
        AddExp(500);
    }

    [ContextMenu("Level Up (Test)")]
    public void TestLevelUp()
    {
        currentExp = expToNextLevel; // Set to exact requirement
        AddExp(0); // Trigger level up check
    }

    [ContextMenu("Set Level 5 (Test)")]
    public void TestSetLevel5()
    {
        SetLevel(5);
    }

    [ContextMenu("Set Level 10 (Test)")]
    public void TestSetLevel10()
    {
        SetLevel(10);
    }

    [ContextMenu("Reset to Level 1 (Test)")]
    public void TestReset()
    {
        ResetStats();
    }

    public void SetLevel(int targetLevel)
    {
        if (targetLevel < 1) targetLevel = 1;
        
        currentLevel = targetLevel;
        currentExp = 0;
        CalculateExpRequirement();
        SavePlayerStats();
        
        OnLevelUp?.Invoke(currentLevel);
        OnExpChanged?.Invoke(currentExp, expToNextLevel);
        
        Debug.Log($"[PlayerStats] Set to Level {currentLevel} (Testing)");
    }

    public void AddExpForTesting(int amount)
    {
        AddExp(amount);
    }


    // ========================================================================
    // PUBLIC GETTERS
    // ========================================================================

    public int GetCurrentLevel() => currentLevel;
    public int GetCurrentExp() => currentExp;
    public int GetExpToNextLevel() => expToNextLevel;
    public float GetExpProgress() => (float)currentExp / expToNextLevel; // For progress bars

    // Calculate how many more battles needed to level up
    public int BattlesNeededToLevelUp()
    {
        int expNeeded = expToNextLevel - currentExp;
        return Mathf.CeilToInt((float)expNeeded / expPerBattle);
    }
}
