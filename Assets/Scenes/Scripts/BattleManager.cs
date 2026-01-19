using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; 

public class BattleManager : MonoBehaviour
{
    [Header("References")]
    public UnitStats playerStats;
    public UnitStats enemyStats;
    public Slider enemyAttackBar;
    public TextMeshProUGUI spellText; 
    public TextMeshProUGUI waveText;
    public Slider waveProgressSlider;

    [Header("YOLO Integration")]
    public YoloAlphabet yoloDetector; 

    [Header("Spell UI Settings")]
    public Transform spellContainer; 
    public GameObject letterPrefab;  
    public Sprite[] alphabetSprites; // Sprite untuk Huruf di barisan Spell (A-Z)
    
    private List<GameObject> activeLetterUI = new List<GameObject>(); 

    // -----------------------------------------------------------
    // [GUIDE SYSTEM] VARIABEL BARU
    // -----------------------------------------------------------
    [Header("Guide System (Hand Signs)")]
    public GameObject guidePanel;       // Panel pembungkus guide (agar bisa di-hide)
    public Image guideImage;            // Image UI untuk menampilkan gerakan tangan
    public Toggle guideToggle;          // Referensi ke UI Toggle
    public Sprite[] handSignSprites;    // Sprite gerakan tangan A-Z (Drag di Inspector)
    
    private bool isGuideActive = true;  // Status nyala/mati
    // -----------------------------------------------------------

    [Header("Enemy Objects")]
    public GameObject[] enemyObjects; 
    
    [Header("Result Screens")]
    public GameObject winningScreen;
    public GameObject bintang1;
    public GameObject bintang2;
    public GameObject bintang3;
    public GameObject losingScreen;
    
    [Header("Result Screen Texts")]
    public TextMeshProUGUI winningTitleText;
    public TextMeshProUGUI winningCountdownText;
    public TextMeshProUGUI losingTitleText;
    public TextMeshProUGUI losingCountdownText;
    
    [Header("Roguelike Settings")]
    public GameObject levelSelectionPanel; 
    public int currentDifficulty = 1; 
    public int currentWave = 1;       
    public int maxWaves = 7;
    
    [Header("Level Requirements")]
    [Tooltip("Minimum player level for each difficulty (Difficulty 1 = index 0)")]
    public int[] difficultyLevelRequirements = new int[] { 1, 2, 3 }; // Difficulty 1,2,3 requires Level 1,2,3
    
    [Header("Chapter Selection UI")]
    public Button[] chapterButtons; // Drag 3 buttons for Chapter I, II, III

    [Header("Navigation")]
    public string returnSceneName = "MapScene";
    
    [Tooltip("DEPRECATED: Now uses BattleDataHolder to get return position")]
    public int returnNodeID = 0; 

    [Header("Balancing")]
    public float baseEnemyAttackSpeed = 60f; 
    public float baseEnemyDamage = 15f; 
    private int currentEnemyCount = 1;
    private float currentWaveAttackSpeed = 60f; 

    private Dictionary<int, List<string>> levelDictionaries = new Dictionary<int, List<string>>()
    {
        { 1, new List<string> { "A", "B", "C", "D", "E","F", "G", "H" } }, 
        { 2, new List<string> { "I", "J", "K", "L", "M", "N", "O", "P" } }, 
        { 3, new List<string> {"Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" } } 
    };

    [Header("Runtime State")]
    private List<string> currentSpellQueue = new List<string>();
    private float currentAttackTimer = 0f;
    private bool isEnemyStunned = false;
    private bool isBattleActive = false;
    private bool isPerfectRun = true;
    private int mistakeCount = 0;

    private float inputCooldown = 0.5f; 
    private float lastInputTime = 0f;
    private int lastExpReward = 0;

    void Start()
    {
        if(spellText != null) spellText.gameObject.SetActive(false);
        
        // Hide all result screens
        if (winningScreen != null) winningScreen.SetActive(false);
        if (losingScreen != null) losingScreen.SetActive(false);
        
        // [GUIDE SYSTEM] Setup Toggle di awal
        if (guideToggle != null)
        {
            isGuideActive = guideToggle.isOn;
            guideToggle.onValueChanged.AddListener(OnToggleGuide); // Dengarkan perubahan
        }
        UpdateGuideVisual(); // Reset visual
        
        // Subscribe to level up events to update chapter availability
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnLevelUp += OnPlayerLevelUp;
        }
        
        // Update chapter buttons based on player level
        UpdateChapterButtons();

        ShowLevelSelection();
    }
    
    void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnLevelUp -= OnPlayerLevelUp;
        }
    }
    
    void OnPlayerLevelUp(int newLevel)
    {
        Debug.Log($"[BattleManager] Player leveled up to {newLevel}! Updating chapter availability...");
        UpdateChapterButtons();
    }

    // [GUIDE SYSTEM] Fungsi yang dipanggil Toggle UI
    public void OnToggleGuide(bool isOn)
    {
        isGuideActive = isOn;
        UpdateGuideVisual(); // Update tampilan langsung saat diklik
    }

    // [GUIDE SYSTEM] Logic utama update gambar
    void UpdateGuideVisual()
    {
        // 1. Cek apakah fitur nyala DAN battle sedang aktif DAN ada antrian spell?
        if (isGuideActive && isBattleActive && currentSpellQueue.Count > 0)
        {
            if (guidePanel != null) guidePanel.SetActive(true);

            // Ambil huruf pertama yang harus ditebak
            string targetChar = currentSpellQueue[0];
            char c = targetChar[0];
            int index = c - 'A'; // Konversi ASCII ke Index (0-25)

            // Pasang gambar gerakan tangan yang sesuai
            if (guideImage != null && handSignSprites != null && index >= 0 && index < handSignSprites.Length)
            {
                guideImage.sprite = handSignSprites[index];
                guideImage.preserveAspect = true; // Biar gambar tidak gepeng
            }
        }
        else
        {
            // Matikan guide jika tidak memenuhi syarat
            if (guidePanel != null) guidePanel.SetActive(false);
        }
    }

    public void ShowLevelSelection()
    {
        isBattleActive = false;
        UpdateGuideVisual(); // Sembunyikan guide saat di menu

        foreach (GameObject enemy in enemyObjects) enemy.SetActive(false);
        if (waveText != null) waveText.gameObject.SetActive(false);
        if (waveProgressSlider != null) waveProgressSlider.gameObject.SetActive(false); 
        
        // Update chapter buttons when showing selection
        UpdateChapterButtons();
        
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(true);
    }
    
    // Update chapter button states based on player level
    void UpdateChapterButtons()
    {
        if (chapterButtons == null || chapterButtons.Length == 0) return;
        
        int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.GetCurrentLevel() : 1;
        
        for (int i = 0; i < chapterButtons.Length; i++)
        {
            if (chapterButtons[i] == null) continue;
            
            int difficulty = i + 1; // Chapter I = difficulty 1, Chapter II = difficulty 2, etc.
            bool canAccess = CanAccessDifficulty(difficulty);
            
            // Unity will automatically show disabled sprite/color
            chapterButtons[i].interactable = canAccess;
            
            // Debug log
            string status = canAccess ? "UNLOCKED" : "LOCKED";
            int requiredLevel = GetRequiredLevel(difficulty);
            Debug.Log($"[BattleManager] Chapter {difficulty} ({status}) - Requires Level {requiredLevel} | Player Level: {playerLevel}");
        }
    }

    public void SelectDifficulty(int difficulty)
    {
        // Check level requirement
        if (!CanAccessDifficulty(difficulty))
        {
            int requiredLevel = GetRequiredLevel(difficulty);
            int playerLevel = PlayerStats.Instance != null ? PlayerStats.Instance.GetCurrentLevel() : 1;
            
            Debug.LogWarning($"Cannot access Difficulty {difficulty} - Requires Level {requiredLevel} (Current: Level {playerLevel})");
            
            // Show error message to player
            if (spellText != null)
            {
                StartCoroutine(ShowTempMessage($"LOCKED!\nRequires Level {requiredLevel}\n(Current: Level {playerLevel})"));
            }
            return;
        }
        
        currentDifficulty = difficulty;
        currentWave = 1;
        isPerfectRun = true;
        mistakeCount = 0;
        
        if (levelSelectionPanel != null) levelSelectionPanel.SetActive(false);
        if (waveText != null) waveText.gameObject.SetActive(true); 
        
        if (waveProgressSlider != null)
        {
            waveProgressSlider.gameObject.SetActive(true);
            waveProgressSlider.maxValue = maxWaves;
            waveProgressSlider.value = currentWave;
        }
        
        Debug.Log($"Starting Run: Difficulty {currentDifficulty}, Wave 1");
        
        SetupWave();
        StartNewRound();
    }
    
    // Check if player can access this difficulty
    public bool CanAccessDifficulty(int difficulty)
    {
        if (PlayerStats.Instance == null) return true; // If no PlayerStats, allow all
        
        int requiredLevel = GetRequiredLevel(difficulty);
        int playerLevel = PlayerStats.Instance.GetCurrentLevel();
        
        return playerLevel >= requiredLevel;
    }
    
    // Get required level for a difficulty
    public int GetRequiredLevel(int difficulty)
    {
        int index = difficulty - 1; // Difficulty 1 = index 0
        if (index >= 0 && index < difficultyLevelRequirements.Length)
        {
            return difficultyLevelRequirements[index];
        }
        return 1; // Default to level 1
    }
    
    // Get status message for difficulty button
    public string GetDifficultyStatusMessage(int difficulty)
    {
        if (CanAccessDifficulty(difficulty))
        {
            return "UNLOCKED";
        }
        else
        {
            int requiredLevel = GetRequiredLevel(difficulty);
            return $"Requires Level {requiredLevel}";
        }
    }
    
    IEnumerator ShowTempMessage(string message)
    {
        if (spellText == null) yield break;
        
        spellText.text = message;
        spellText.gameObject.SetActive(true);
        
        yield return new WaitForSeconds(2f);
        
        spellText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!isBattleActive) return;

        if (!isEnemyStunned)
        {
            currentAttackTimer += Time.deltaTime;
            enemyAttackBar.value = currentAttackTimer / currentWaveAttackSpeed;

            if (currentAttackTimer >= currentWaveAttackSpeed)
            {
                EnemyAttack();
            }
        }
        
        if (currentSpellQueue.Count > 0)
        {
            if (Time.time - lastInputTime < inputCooldown) return;

            string detectedChar = "";

            if (yoloDetector != null && !string.IsNullOrEmpty(yoloDetector.currentDetectedWord))
            {
                detectedChar = yoloDetector.currentDetectedWord.ToUpper();
            }
            
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

    void SetupWave()
    {
        if (waveText != null)
        {
            waveText.text = $"WAVE {currentWave}/{maxWaves}";
        }

        if (waveProgressSlider != null)
        {
            waveProgressSlider.value = currentWave;
        }
        
        // Semakin tinggi wave, semakin cepat attack speed (dikurangi 5 detik per wave)
        currentWaveAttackSpeed = baseEnemyAttackSpeed - ((currentWave - 1) * 5f);
        if (currentWaveAttackSpeed < 20f) currentWaveAttackSpeed = 20f; // Minimal 20 detik
        Debug.Log($"Wave {currentWave} Attack Speed: {currentWaveAttackSpeed}s");

        enemyStats.HealFull(); 

        foreach (GameObject enemy in enemyObjects)
        {
            if(enemy != null) enemy.SetActive(false);
        }

        if (currentWave <= 3) currentEnemyCount = 1;
        else if (currentWave <= 7) currentEnemyCount = 2;
        else currentEnemyCount = 3;

        if (currentEnemyCount > enemyObjects.Length) currentEnemyCount = enemyObjects.Length;

        if (currentEnemyCount == 1)
        {
            if (enemyObjects.Length > 1) enemyObjects[1].SetActive(true); 
        }
        else if (currentEnemyCount == 2)
        {
            if (enemyObjects.Length > 0) enemyObjects[0].SetActive(true); 
            if (enemyObjects.Length > 2) enemyObjects[2].SetActive(true); 
        }
        else
        {
            foreach (GameObject enemy in enemyObjects) enemy.SetActive(true); 
        }

        Debug.Log($"Wave {currentWave}/{maxWaves} Start! Enemies: {currentEnemyCount}");
    }

    void StartNewRound()
    {
        isBattleActive = true;
        GenerateSpell();
        currentAttackTimer = 0f; 
        enemyAttackBar.value = 0;
    }

    void GenerateSpell()
    {
        currentSpellQueue.Clear();
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();

        List<string> availableChars = levelDictionaries.ContainsKey(currentDifficulty) ? levelDictionaries[currentDifficulty] : levelDictionaries[1];
        int spellLength = 3 + (currentWave / 4); 

        for (int i = 0; i < spellLength; i++)
        {
            string randomChar = availableChars[Random.Range(0, availableChars.Count)];
            currentSpellQueue.Add(randomChar);
            SpawnLetterUI(randomChar);
        }

        // [GUIDE SYSTEM] Update guide untuk huruf pertama yang baru di-generate
        UpdateGuideVisual();
    }

    void SpawnLetterUI(string charString)
    {
        GameObject newLetterObj = Instantiate(letterPrefab, spellContainer);
        Image imgComp = newLetterObj.GetComponent<Image>();
        char c = charString[0]; 
        int index = c - 'A'; 
        if (index >= 0 && index < alphabetSprites.Length) imgComp.sprite = alphabetSprites[index];
        activeLetterUI.Add(newLetterObj);
    }

    public void CheckInput(string detectedLetter)
    {
        if (currentSpellQueue.Count == 0) return;
        
        // Cek huruf paling depan
        if (detectedLetter == currentSpellQueue[0])
        {
            lastInputTime = Time.time; 
            currentSpellQueue.RemoveAt(0);
            
            if (activeLetterUI.Count > 0)
            {
                Destroy(activeLetterUI[0]);
                activeLetterUI.RemoveAt(0);
            }

            // [GUIDE SYSTEM] Huruf depan sudah hilang, update guide ke huruf berikutnya (kalau ada)
            UpdateGuideVisual();

            if (currentSpellQueue.Count == 0) PlayerAttack();
        }
    }

    void PlayerAttack()
    {
        enemyStats.TakeDamage(20); 
        foreach (GameObject enemy in enemyObjects)
        {
            if (enemy != null && enemy.activeSelf) 
            {
                EnemyController ctrl = enemy.GetComponent<EnemyController>();
                if (ctrl != null) ctrl.PlayHit();
            }
        }
        if (enemyStats.IsDead())
        {
            WinWave(); 
            return;
        }
        StartCoroutine(StunEnemyRoutine()); 
    }

    void EnemyAttack()
    {
        float scaledDamage = baseEnemyDamage + (currentWave * 2);
        float totalDamage = scaledDamage * currentEnemyCount;
        playerStats.TakeDamage(totalDamage);
        
        isPerfectRun = false;
        mistakeCount++;
        
        foreach (GameObject enemy in enemyObjects)
        {
            if (enemy != null && enemy.activeSelf)
            {
                EnemyController ctrl = enemy.GetComponent<EnemyController>();
                if (ctrl != null) ctrl.PlayAttack();
            }
        }
        currentAttackTimer = 0f; 
        if (playerStats.IsDead()) LoseRun();
    }

    IEnumerator StunEnemyRoutine()
    {
        isEnemyStunned = true;
        
        // [GUIDE SYSTEM] Sembunyikan guide saat musuh stun (tidak ada spell)
        if(guidePanel != null) guidePanel.SetActive(false);

        yield return new WaitForSeconds(1.5f); 
        isEnemyStunned = false;
        GenerateSpell(); 
    }

    void WinWave()
    {
        isBattleActive = false; 
        UpdateGuideVisual(); // Hide guide

        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();

        if (currentWave < maxWaves)
        {
            Debug.Log($"Wave {currentWave} Complete! Next...");
            StartCoroutine(NextWaveRoutine());
        }
        else
        {
            if (waveProgressSlider != null) waveProgressSlider.value = maxWaves;
            WinRun();
        }
    }

    IEnumerator NextWaveRoutine()
    {
        if (spellText != null)
        {
            spellText.text = $"WAVE {currentWave} CLEARED!";
            spellText.gameObject.SetActive(true);
        }

        foreach (GameObject enemy in enemyObjects)
        {
            if (enemy != null && enemy.activeSelf)
            {
                EnemyController ctrl = enemy.GetComponent<EnemyController>();
                if (ctrl != null) ctrl.PlayDead();
            }
        }

        yield return new WaitForSeconds(3f); 

        if (spellText != null) spellText.gameObject.SetActive(false);

        currentWave++;
        SetupWave();
        StartNewRound();
    }

    // --- MENANG & PULANG ---
    void WinRun()
    {
        isBattleActive = false;
        UpdateGuideVisual(); // Hide guide
        
        foreach (GameObject enemy in enemyObjects)
        {
            if (enemy != null && enemy.activeSelf)
            {
                EnemyController ctrl = enemy.GetComponent<EnemyController>();
                if (ctrl != null) ctrl.PlayDead();
            }
        }
        
        // CALCULATE STARS
        int stars = CalculateStars();
        Debug.Log($"<color=yellow>★ Battle Complete! Stars: {stars}/3</color>");
        
        // AWARD EXP BASED ON DIFFICULTY AND STARS
        if (PlayerStats.Instance != null)
        {
            // EXP maksimal per difficulty (untuk 3 bintang)
            int maxExpForDifficulty = 0;
            
            if (currentDifficulty == 1)
                maxExpForDifficulty = 100;  // Level 1→2 requirement
            else if (currentDifficulty == 2)
                maxExpForDifficulty = 150;  // Level 2→3 requirement
            else if (currentDifficulty == 3)
                maxExpForDifficulty = 225;  // Level 3→4 requirement
            else
                maxExpForDifficulty = PlayerStats.Instance.expPerBattle; // Fallback
            
            int expReward = maxExpForDifficulty;
            
            if (stars == 3)
            {
                // Bintang 3: Full EXP sesuai difficulty
                expReward = maxExpForDifficulty;
                Debug.Log($"<color=gold>★★★ PERFECT! +{expReward} EXP (Difficulty {currentDifficulty})</color>");
            }
            else if (stars == 2)
            {
                // Bintang 2: 70% dari max exp difficulty
                expReward = Mathf.RoundToInt(maxExpForDifficulty * 0.7f);
                Debug.Log($"<color=green>★★ Great! +{expReward} EXP (70%)</color>");
            }
            else
            {
                // Bintang 1: 40% dari max exp difficulty
                expReward = Mathf.RoundToInt(maxExpForDifficulty * 0.4f);
                Debug.Log($"<color=yellow>★ Victory! +{expReward} EXP (40%)</color>");
            }
            
            lastExpReward = expReward;
            PlayerStats.Instance.AddExp(expReward);
        }
        
        // SHOW WINNING SCREEN
        ShowWinningScreen(stars);
    }

    // --- KALAH & PULANG ---
    void LoseRun()
    {
        isBattleActive = false;
        UpdateGuideVisual();
    
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();
        
        // SHOW LOSING SCREEN
        if (losingScreen != null)
        {
            losingScreen.SetActive(true);
        }
        
        if (losingTitleText != null)
        {
            losingTitleText.text = "Kamu Kalah...";
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.overrideTargetNodeID = 70;
            Debug.Log("[BattleManager] Player defeated - Teleporting to Spawn Room");
        }

        StartCoroutine(ReturnToMapRoutine("KALAH"));
    }
    
    int CalculateStars()
    {
        if (playerStats == null) return 1;
        
        float hpPercent = playerStats.currentHealth / playerStats.maxHealth;
        
        // Bintang 3: HP Penuh (100%) ATAU Perfect (no mistakes)
        if (hpPercent >= 0.99f || isPerfectRun)
        {
            return 3;
        }
        // Bintang 2: HP > 50%
        else if (hpPercent > 0.5f)
        {
            return 2;
        }
        // Bintang 1: HP sekarat (<=50%)
        else
        {
            return 1;
        }
    }
    
    void ShowWinningScreen(int stars)
    {
        // Show winning panel
        if (winningScreen != null)
        {
            winningScreen.SetActive(true);
        }
        
        // Control star visibility based on result
        if (bintang1 != null) bintang1.SetActive(stars >= 1);
        if (bintang2 != null) bintang2.SetActive(stars >= 2);
        if (bintang3 != null) bintang3.SetActive(stars >= 3);
        
        // Update title text with EXP info
        if (winningTitleText != null)
        {
            string expInfo = lastExpReward > 0 ? $" (+{lastExpReward} EXP)" : "";
            winningTitleText.text = $"Kamu Menang!{expInfo}";
        }
        
        // Auto return to map after delay
        StartCoroutine(ReturnToMapRoutine($"LEVEL {currentDifficulty} SELESAI - {stars} BINTANG!"));
    }

    // --- Coroutine untuk Pulang ke Map ---
    IEnumerator ReturnToMapRoutine(string message)
    {
        int countdown = 4;
        while(countdown > 0)
        {
            string countdownMessage = $"{message}\nReturning in {countdown}...";
            
            if (spellText != null) 
            {
                spellText.text = countdownMessage;
                spellText.gameObject.SetActive(true);
            }
            
            // Update winning/losing countdown texts if active
            if (winningCountdownText != null && winningScreen != null && winningScreen.activeSelf)
            {
                winningCountdownText.text = $"Kembali ke map dalam {countdown}...";
            }
            
            if (losingCountdownText != null && losingScreen != null && losingScreen.activeSelf)
            {
                losingCountdownText.text = $"Kembali ke spawn dalam {countdown}...";
            }
            
            yield return new WaitForSeconds(1f);
            countdown--;
        }

        if (spellText != null) spellText.text = "Loading...";
        if (winningCountdownText != null) winningCountdownText.text = "Loading...";
        if (losingCountdownText != null) losingCountdownText.text = "Loading...";

        // CLEANUP RESOURCES
        YoloAlphabet currentYolo = FindFirstObjectByType<YoloAlphabet>();
        if (currentYolo != null)
        {
            Debug.Log("Cleaning up Battle YOLO...");
            currentYolo.gameObject.SetActive(false);
        }

        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("Cleaning up Battle Camera...");
            camScript.gameObject.SetActive(false);
        }

        // SET RETURN POSITION from BattleDataHolder
        if (GameManager.Instance != null && BattleDataHolder.Instance != null)
        {
            GameManager.Instance.overrideTargetNodeID = BattleDataHolder.Instance.returnNodeID;
            Debug.Log($"[BattleManager] Returning to {BattleDataHolder.Instance.returnScene} Node {BattleDataHolder.Instance.returnNodeID}");
        }
        else if (GameManager.Instance != null)
        {
            // Fallback to hard-coded returnNodeID if BattleDataHolder not found
            GameManager.Instance.overrideTargetNodeID = returnNodeID;
            Debug.LogWarning("[BattleManager] BattleDataHolder not found, using fallback returnNodeID");
        }

        // LOAD SCENE
        string sceneToLoad = BattleDataHolder.Instance != null ? BattleDataHolder.Instance.returnScene : returnSceneName;
        SceneManager.LoadScene(sceneToLoad);
    }
}