using UnityEngine;
using UnityEngine.UI; 

public class UnitStats : MonoBehaviour
{
    [Header("Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    
    [Header("UI Reference")]
    public Slider healthSlider; // Since the script is ON the slider, this will fill automatically

    void Start()
    {
        // Auto-find the slider if you forgot to drag it in
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }

        currentHealth = maxHealth;
        UpdateUI();
    }

    // --- THIS WAS MISSING ---
    // This function resets health for the next level
    public void HealFull()
    {
        currentHealth = maxHealth;
        UpdateUI(); 
        
        // Ensure the Health Bar GameObject is visible
        gameObject.SetActive(true); 
    }
    // -------------------------

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        UpdateUI();
        
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            // BattleManager will check IsDead() and handle the rest
        }
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }

    void UpdateUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }
}