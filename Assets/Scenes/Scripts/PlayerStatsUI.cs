using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerStatsUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI expText;
    public Slider expProgressBar;
    
    [Header("Display Format")]
    public bool showDetailedExp = true; // Show "50/100 EXP" vs just progress bar
    
    void Start()
    {
        // Subscribe to PlayerStats events
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnExpChanged += UpdateExpDisplay;
            PlayerStats.Instance.OnLevelUp += UpdateLevelDisplay;
            
            // Initial display
            UpdateDisplay();
        }
        else
        {
            Debug.LogWarning("[PlayerStatsUI] PlayerStats not found!");
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnExpChanged -= UpdateExpDisplay;
            PlayerStats.Instance.OnLevelUp -= UpdateLevelDisplay;
        }
    }
    
    void UpdateDisplay()
    {
        if (PlayerStats.Instance == null) return;
        
        UpdateLevelDisplay(PlayerStats.Instance.GetCurrentLevel());
        UpdateExpDisplay(PlayerStats.Instance.GetCurrentExp(), PlayerStats.Instance.GetExpToNextLevel());
    }
    
    void UpdateLevelDisplay(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Level {level}";
        }
    }
    
    void UpdateExpDisplay(int currentExp, int expToNextLevel)
    {
        // Update text
        if (expText != null)
        {
            if (showDetailedExp)
            {
                expText.text = $"{currentExp}/{expToNextLevel} EXP";
            }
            else
            {
                expText.text = "EXP";
            }
        }
        
        // Update progress bar
        if (expProgressBar != null)
        {
            float progress = (float)currentExp / expToNextLevel;
            expProgressBar.value = progress;
        }
    }
    
    // Optional: Manual refresh (call from inspector button)
    public void RefreshDisplay()
    {
        UpdateDisplay();
    }
}
