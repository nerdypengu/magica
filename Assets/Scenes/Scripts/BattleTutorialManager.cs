using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class BattleTutorialManager : MonoBehaviour
{
    [Header("References")]
    public UnitStats playerStats;
    public UnitStats enemyStats;
    public Slider enemyAttackBar;
    
    // Gunakan spellText ini untuk Instruksi Tutorial juga
    public TextMeshProUGUI spellText; 
    public TextMeshProUGUI waveText;
    public Slider waveProgressSlider;

    [Header("YOLO Integration")]
    public YoloAlphabet yoloDetector; 

    [Header("Spell UI Settings")]
    public Transform spellContainer; 
    public GameObject letterPrefab;  
    public Sprite[] alphabetSprites; 
    
    private List<GameObject> activeLetterUI = new List<GameObject>(); 
    
    // -----------------------------------------------------------
    // [GUIDE SYSTEM] VARIABEL BARU
    // -----------------------------------------------------------
    [Header("Guide System (Hand Signs)")]
    public GameObject guidePanel;       // Panel pembungkus guide (agar bisa di-hide)
    public Image guideImage;            // Image UI untuk menampilkan gerakan tangan
    public Sprite[] handSignSprites;    // Sprite gerakan tangan A-Z (Drag di Inspector)
    
    private bool isGuideActive = true;  // Always active in tutorial
    // -----------------------------------------------------------

    [Header("Enemy Objects")]
    public GameObject[] enemyObjects; 

    [Header("Tutorial Highlights")]
    public RectTransform highlightFrame; // Kotak merah/kuning transparan (UI Image)
    public RectTransform playerHealthRect; // Drag Slider HP Player kesini
    public RectTransform enemyHealthRect;  // Drag Slider HP Enemy kesini
    public RectTransform attackBarRect;    // Drag Slider Attack Bar kesini
    public RectTransform spellContainerRect; // Drag Spell Container kesini

    [Header("Navigation")]
    public string returnSceneName = "Scene1"; 
    
    // --- TAMBAHAN BARU: ID Node Tujuan Saat Pulang ---
    [Tooltip("Isi dengan ID Node tempat player harus muncul setelah battle (Misal: 16)")]
    public int returnNodeID = 18; 
    // -------------------------------------------------

    [Header("Balancing")]
    public float baseEnemyAttackSpeed = 10f; // Dibuat lambat untuk tutorial
    public float baseEnemyDamage = 5f; 
    
    // --- RUNTIME STATE ---
    private List<string> currentSpellQueue = new List<string>();
    private float currentAttackTimer = 0f;
    private bool isEnemyStunned = false;
    private bool isBattleActive = false; 

    private float inputCooldown = 0.5f; 
    private float lastInputTime = 0f;

    void Start()
    {
        // Matikan highlight di awal
        if(highlightFrame != null) highlightFrame.gameObject.SetActive(false);
        
        // [GUIDE SYSTEM] Always active in tutorial
        isGuideActive = true;
        UpdateGuideVisual(); // Initialize guide

        // Hide UI Wave sementara tutorial jalan
        if (waveText != null) waveText.gameObject.SetActive(false);
        if (waveProgressSlider != null) waveProgressSlider.gameObject.SetActive(false);

        // Reset Musuh
        foreach (GameObject enemy in enemyObjects) enemy.SetActive(false);
        if(enemyObjects.Length > 0) enemyObjects[0].SetActive(true);

        // Mulai Tutorial
        StartCoroutine(RunTutorialSequence());
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

    // --- TUTORIAL SEQUENCE ---
    IEnumerator RunTutorialSequence()
    {
        isBattleActive = false;
        
        // 1. INTRO
        ShowAnnouncement("SELAMAT DATANG DI TUTORIAL BATTLE!");
        yield return WaitForInputClick();

        // 2. EXPLAIN PLAYER HEALTH
        ShowAnnouncement("Ini adalah HP kamu.\nJangan sampai habis!");
        SetHighlight(playerHealthRect);
        yield return WaitForInputClick();

        // 3. EXPLAIN ENEMY HEALTH
        ShowAnnouncement("Ini adalah HP musuh.\nHabiskan untuk menang!");
        SetHighlight(enemyHealthRect);
        yield return WaitForInputClick();

        // 4. EXPLAIN ATTACK BAR
        ShowAnnouncement("Perhatikan BAR SERANGAN ini.\nKalau penuh, musuh akan menyerang!");
        SetHighlight(attackBarRect);
        yield return WaitForInputClick();

        // 5. EXPLAIN SPELLS
        ShowAnnouncement("Huruf spell muncul di sini.\nTiru gerakan tangan untuk menyerang!");
        SetHighlight(spellContainerRect);
        yield return WaitForInputClick();

        // 6. START PRACTICE
        if(highlightFrame != null) highlightFrame.gameObject.SetActive(false); // Hide Box
        ShowAnnouncement("Ayo coba! Tunjukkan huruf 'A'!");
        
        yield return new WaitForSeconds(2f);
        
        // Start The Battle Logic
        if (waveText != null) waveText.gameObject.SetActive(true);
        if (waveProgressSlider != null) waveProgressSlider.gameObject.SetActive(true);
        
        StartNewRound(); // Ini akan memicu GenerateSpell (Isinya A)
    }

    void SetHighlight(RectTransform target)
    {
        if (highlightFrame == null || target == null) return;

        highlightFrame.gameObject.SetActive(true);
        highlightFrame.position = target.position;
        highlightFrame.sizeDelta = target.sizeDelta + new Vector2(20, 20); 
    }

    void ShowAnnouncement(string text)
    {
        if (spellText != null)
        {
            spellText.text = text;
            spellText.gameObject.SetActive(true);
        }
    }

    IEnumerator WaitForInputClick()
    {
        yield return new WaitForSeconds(0.5f);
        while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }
    }

    // --- BATTLE LOGIC ---

    void Update()
    {
        if (!isBattleActive) return;

        // --- ENEMY ATTACK TIMER ---
        if (!isEnemyStunned)
        {
            currentAttackTimer += Time.deltaTime;
            enemyAttackBar.value = currentAttackTimer / baseEnemyAttackSpeed;

            if (currentAttackTimer >= baseEnemyAttackSpeed)
            {
                EnemyAttack();
            }
        }
        
        // --- INPUT HANDLING ---
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

    void StartNewRound()
    {
        isBattleActive = true;
        GenerateSpell();
        UpdateGuideVisual();
        currentAttackTimer = 0f; 
        enemyAttackBar.value = 0;
        ShowAnnouncement("TUNJUKKAN: [ A ]"); 
    }

    void GenerateSpell()
    {
        currentSpellQueue.Clear();
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();

        // --- TUTORIAL KHUSUS: HANYA HURUF 'A' ---
        int spellLength = 1; 
        for (int i = 0; i < spellLength; i++)
        {
            string fixedChar = "A"; 
            currentSpellQueue.Add(fixedChar);
            SpawnLetterUI(fixedChar);
        }
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
        if (detectedLetter == currentSpellQueue[0])
        {
            lastInputTime = Time.time; 
            currentSpellQueue.RemoveAt(0);
            if (activeLetterUI.Count > 0)
            {
                Destroy(activeLetterUI[0]);
                activeLetterUI.RemoveAt(0);
            }
            UpdateGuideVisual();
            if (currentSpellQueue.Count == 0) PlayerAttack();
        }
    }

    void PlayerAttack()
    {
        enemyStats.TakeDamage(10); 
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
            WinTutorial(); 
            return;
        }
        StartCoroutine(StunEnemyRoutine()); 
    }

    void EnemyAttack()
    {
        playerStats.TakeDamage(baseEnemyDamage);
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
        ShowAnnouncement("BAGUS SEKALI!");
        yield return new WaitForSeconds(1.0f); 
        isEnemyStunned = false;
        StartNewRound(); 
    }

    void WinTutorial()
    {
        isBattleActive = false;
        UpdateGuideVisual();
        
        // --- LOG DEBUGGING & SAVE ---
        Debug.Log("MENANG! Menyimpan status 'BattleTutorialDone' = 1");
        // 1. Simpan tanda bahwa tutorial sudah selesai
        PlayerPrefs.SetInt("BattleTutorialDone", 1);
        PlayerPrefs.Save(); 
        // -----------------------------

        foreach (GameObject enemy in enemyObjects)
        {
            if (enemy != null && enemy.activeSelf)
            {
                EnemyController ctrl = enemy.GetComponent<EnemyController>();
                if (ctrl != null) ctrl.PlayDead();
            }
        }

        StartCoroutine(ReturnToMapRoutine("TUTORIAL SELESAI!"));
    }

    void LoseRun()
    {
        isBattleActive = false;
        UpdateGuideVisual();
        foreach (GameObject uiObj in activeLetterUI) Destroy(uiObj);
        activeLetterUI.Clear();
        StartCoroutine(ReturnToMapRoutine("COBA LAGI"));
    }

    IEnumerator ReturnToMapRoutine(string message)
    {
        if (spellText != null) spellText.gameObject.SetActive(true);

        int countdown = 4;
        while (countdown > 0)
        {
            if (spellText != null) spellText.text = $"{message}\nReturning in {countdown}...";
            yield return new WaitForSeconds(1f);
            countdown--;
        }

        if (spellText != null) spellText.text = "Loading...";
        Debug.Log("Pindah Scene sekarang...");

        // === CLEANUP SYSTEM (Matikan YOLO & Kamera) ===
        YoloAlphabet currentYolo = FindFirstObjectByType<YoloAlphabet>(); 
        if (currentYolo != null)
        {
            Debug.Log("Shutting down Battle YOLO Detector...");
            currentYolo.gameObject.SetActive(false);
        }

        UniversalCamera camScript = FindFirstObjectByType<UniversalCamera>();
        if (camScript != null)
        {
            Debug.Log("Stopping Battle WebCam...");
            camScript.gameObject.SetActive(false); 
        }
        // ==============================================

        // === FIX POSISI PULANG (Override GameManager) ===
        if (GameManager.Instance != null)
        {
            // Catat di GameManager mau muncul di Node mana
            GameManager.Instance.overrideTargetNodeID = returnNodeID;
            Debug.Log($"Request Spawn Pulang di Node ID: {returnNodeID}");
        }
        // ================================================

        SceneManager.LoadScene(returnSceneName);
    }
}