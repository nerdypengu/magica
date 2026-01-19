using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class BossBattle : MonoBehaviour
{
    [Header("References")]
    public UnitStats playerStats;
    public UnitStats enemyStats;
    public Slider enemyAttackBar;
    public TextMeshProUGUI spellText; 

    [Header("YOLO Integration")]
    public YoloAlphabet yoloDetector; 

    [Header("Spell UI Settings")]
    public Transform spellContainer; 
    public GameObject letterPrefab;  
    public Sprite[] alphabetSprites; 
    
    private List<GameObject> activeLetterUI = new List<GameObject>(); 

    [Header("Boss Object")]
    public GameObject bossObject; // Assign your single Boss prefab here
    
    [Header("Boss Settings")]
    public float bossAttackSpeed = 5f; // Faster attacks for boss?
    public float bossDamage = 25f; 
    public float bossHealthMultiplier = 5f; // 5x Normal Health
    
    [Header("Level Requirement")]
    public int requiredLevel = 4; // Boss requires level 4
    public string returnScene = "Scene1";
    public int returnNodeID = 81; // Teleport to node 81 if level insufficient

    // All Alphabets A-Z
    private List<string> allAlphabets = new List<string>() 
    { 
        "A","B","C","D","E","F","G","H","I","J","K","L","M",
        "N","O","P","Q","R","S","T","U","V","W","X","Y","Z" 
    };

    [Header("Runtime State")]
    private List<string> currentSpellQueue = new List<string>();
    private float currentAttackTimer = 0f;
    private bool isEnemyStunned = false;
    private bool isBattleActive = true;

    private float inputCooldown = 0.5f; 
    private float lastInputTime = 0f;

    void Start()
    {
        // Check level requirement FIRST
        if (!CheckLevelRequirement())
        {
            return; // Don't start battle, return to Scene1
        }
        
        if(spellText != null) spellText.gameObject.SetActive(false);

        SetupBoss(); 
        StartNewRound();  
    }
    
    bool CheckLevelRequirement()
    {
        if (PlayerStats.Instance == null) return true; // No PlayerStats, allow battle
        
        int playerLevel = PlayerStats.Instance.GetCurrentLevel();
        
        if (playerLevel < requiredLevel)
        {
            Debug.LogWarning($"Level insufficient! Required: {requiredLevel}, Current: {playerLevel}");
            StartCoroutine(ReturnToSceneRoutine(playerLevel));
            return false;
        }
        
        Debug.Log($"Level check passed! Player Level: {playerLevel} >= Required: {requiredLevel}");
        return true;
    }
    
    IEnumerator ReturnToSceneRoutine(int playerLevel)
    {
        if (spellText != null)
        {
            spellText.text = $"LEVEL TIDAK CUKUP!\n\nDibutuhkan: Level {requiredLevel}\nLevel Anda: Level {playerLevel}\n\nKembali ke map dalam 3 detik...";
            spellText.gameObject.SetActive(true);
        }
        
        yield return new WaitForSeconds(2f);
        
        // Set return position
        if (GameManager.Instance != null)
        {
            GameManager.Instance.overrideTargetNodeID = returnNodeID;
            Debug.Log($"[BossBattle] Insufficient level - Returning to {returnScene} Node {returnNodeID}");
        }
        
        SceneManager.LoadScene(returnScene);
    }

    void SetupBoss()
    {
        // 1. Activate Boss Object
        if (bossObject != null) bossObject.SetActive(true);

        // 2. Set Boss Stats (Make it Tanky!)
        enemyStats.maxHealth *= bossHealthMultiplier; 
        enemyStats.HealFull(); // Fill the new giant HP bar

        Debug.Log($"BOSS FIGHT STARTED! HP: {enemyStats.currentHealth}");
    }

    void Update()
    {
        if (!isBattleActive) return;

        // --- BOSS ATTACK TIMER ---
        if (!isEnemyStunned)
        {
            currentAttackTimer += Time.deltaTime;
            enemyAttackBar.value = currentAttackTimer / bossAttackSpeed;

            if (currentAttackTimer >= bossAttackSpeed)
            {
                BossAttack();
            }
        }
        
        // --- INPUT HANDLING ---
        if (currentSpellQueue.Count > 0)
        {
            if (Time.time - lastInputTime < inputCooldown) return;

            string detectedChar = "";

            // YOLO Input
            if (yoloDetector != null && !string.IsNullOrEmpty(yoloDetector.currentDetectedWord))
            {
                detectedChar = yoloDetector.currentDetectedWord.ToUpper();
            }
            // Keyboard Fallback
            if (string.IsNullOrEmpty(detectedChar) && Input.anyKeyDown)
            {
                detectedChar = Input.inputString.ToUpper();
            }

            if (!string.IsNullOrEmpty(detectedChar))
            {
                CheckInput(detectedChar);
            }
        }
    }

    void StartNewRound()
    {
        GenerateSpell();
        currentAttackTimer = 0f; 
        enemyAttackBar.value = 0;
    }

    void GenerateSpell()
    {
        currentSpellQueue.Clear();
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();

        // Generate 3 to 4 letters
        int spellLength = Random.Range(3, 5); 

        for (int i = 0; i < spellLength; i++)
        {
            string randomChar = allAlphabets[Random.Range(0, allAlphabets.Count)];
            currentSpellQueue.Add(randomChar);
            SpawnLetterUI(randomChar);
        }
    }

    void SpawnLetterUI(string charString)
    {
        GameObject newLetterObj = Instantiate(letterPrefab, spellContainer);
        Image imgComp = newLetterObj.GetComponent<Image>();
        
        char c = charString[0]; 
        int index = c - 'A'; 

        if (index >= 0 && index < alphabetSprites.Length)
        {
            imgComp.sprite = alphabetSprites[index];
        }

        activeLetterUI.Add(newLetterObj);
    }

    public void CheckInput(string detectedLetter)
    {
        if (currentSpellQueue.Count == 0) return;

        if (detectedLetter == currentSpellQueue[0])
        {
            lastInputTime = Time.time; 
            currentSpellQueue.RemoveAt(0);

            if (activeLetterUI.Count > 0)
            {
                Destroy(activeLetterUI[0]);
                activeLetterUI.RemoveAt(0);
            }

            if (currentSpellQueue.Count == 0)
            {
                PlayerAttack();
            }
        }
    }

    void PlayerAttack()
    {
        // Player deals fixed damage (e.g., 20)
        enemyStats.TakeDamage(20); 

        // Play Hit Animation
        if (bossObject != null)
        {
            EnemyController ctrl = bossObject.GetComponent<EnemyController>();
            if (ctrl != null) ctrl.PlayHit();
        }

        if (enemyStats.IsDead())
        {
            WinGame(); 
            return;
        }

        StartCoroutine(StunBossRoutine()); 
    }

    void BossAttack()
    {
        playerStats.TakeDamage(bossDamage);
        
        if (bossObject != null)
        {
            EnemyController ctrl = bossObject.GetComponent<EnemyController>();
            if (ctrl != null) ctrl.PlayAttack();
        }
        
        currentAttackTimer = 0f; 

        if (playerStats.IsDead()) LoseGame();
    }

    IEnumerator StunBossRoutine()
    {
        isEnemyStunned = true;
        yield return new WaitForSeconds(1.5f); 
        isEnemyStunned = false;
        GenerateSpell(); 
    }

    void WinGame()
    {
        isBattleActive = false;
        
        if (bossObject != null)
        {
            EnemyController ctrl = bossObject.GetComponent<EnemyController>();
            if (ctrl != null) ctrl.PlayDead();
        }

        StartCoroutine(ShowWinScreenDelay());
    }

    IEnumerator ShowWinScreenDelay()
    {
        yield return new WaitForSeconds(1.5f);

        if (spellText != null)
        {
            spellText.text = "BOSS DEFEATED! YOU WIN!";
            spellText.gameObject.SetActive(true);
        }
    }

    void LoseGame()
    {
        isBattleActive = false;
    
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();

        if (spellText != null)
        {
            spellText.text = "GAME OVER";
            spellText.gameObject.SetActive(true);
        }
    }
}