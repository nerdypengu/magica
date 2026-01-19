using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ConversationGameloop : MonoBehaviour
{
    [Header("Managers")]
    public DialogManager dialogManager;
    public NodePlayerController playerController;

    [Header("Yolo Integration")]
    public GameObject navigationYoloObject; 
    public GameObject conversationYoloObject;
    
    // Referensi script detector
    public YoloDetector conversationDetector; 
    public YoloDetector navigationDetector; 

    [Header("Camera Control")]
    public UniversalCamera universalCamera; 

    [Header("UI References")]
    public GameObject interactionPanel;   
    public TextMeshProUGUI instructionText;
    public Image interactionIcon; 

    [Header("Assets - Special Signs")]
    public Sprite signHalo;
    public Sprite signAmbil;
    public Sprite signTerimaKasih;
    
    [Header("Detection Settings")]
    public float detectionHoldTime = 1.0f; 
    
    // --- STATE INTERNAL ---
    private NPCInteractable currentNPC;
    private int currentStepIndex = 0;
    private float holdTimer = 0f;
    private string lastFrameWord = "";
    private string currentStableKeyword = "";

    void Start()
    {
        if (universalCamera == null) 
            universalCamera = FindFirstObjectByType<UniversalCamera>();
        
        if (navigationDetector == null && navigationYoloObject != null)
            navigationDetector = navigationYoloObject.GetComponent<YoloDetector>();

        ForceResetState();
    }

    // Fungsi pembersih INSTANT
    void ForceResetState()
    {
        if (conversationYoloObject != null) conversationYoloObject.SetActive(false);
        if (interactionPanel != null) interactionPanel.SetActive(false);
        if (navigationYoloObject != null) navigationYoloObject.SetActive(true);

        ResetDetection();
        currentNPC = null;
        currentStepIndex = 0;
    }

    void Update()
    {
        // Safety Check: Jika sistem mati, jangan proses apapun
        if (conversationDetector == null || conversationYoloObject == null || !conversationYoloObject.activeSelf) 
        {
            return;
        }

        string rawWord = "";
        if (!string.IsNullOrEmpty(conversationDetector.currentDetectedWord))
        {
            rawWord = conversationDetector.currentDetectedWord.Trim(); 
        }

        // --- Debugging ---
        // if (!string.IsNullOrEmpty(rawWord)) Debug.Log($"[YOLO CONVO] Raw: '{rawWord}'");

        string processedWord = rawWord.ToUpper();

        // Logic Penstabil (Hold Timer)
        if (!string.IsNullOrEmpty(processedWord) && processedWord == lastFrameWord)
        {
            holdTimer += Time.deltaTime;
            if (holdTimer >= detectionHoldTime)
            {
                currentStableKeyword = processedWord;
                // Debug.Log($"<color=green>STABIL: {currentStableKeyword}</color>");
            }
        }
        else
        {
            holdTimer = 0f;
            currentStableKeyword = ""; 
        }

        lastFrameWord = processedWord;
    }

    public void StartInteraction(NPCInteractable npc)
    {
        if (npc == null || npc.isInteractionCompleted) return;

        currentNPC = npc;
        StartCoroutine(SwitchToConversationMode());
    }

    // --- TRANSISI MASUK (Nav -> Convo) ---
    IEnumerator SwitchToConversationMode()
    {
        if (playerController != null) playerController.isInputLocked = true;

        // 1. Matikan Navigasi & Bersihkan Layar
        if (navigationYoloObject != null) 
        {
            if (navigationDetector != null) navigationDetector.ForceClearBoxes();
            navigationYoloObject.SetActive(false);
        }

        // 2. Restart Kamera (Matikan -> Tunggu -> Nyalakan)
        if (universalCamera != null) universalCamera.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.5f);
        
        if (universalCamera != null) universalCamera.gameObject.SetActive(true);
        yield return new WaitForSeconds(0.2f); // Warmup

        // 3. Nyalakan Conversation
        if (conversationYoloObject != null) 
        {
            conversationYoloObject.SetActive(true);
            if(conversationDetector != null) 
            {
                conversationDetector.ForceClearBoxes();
                conversationDetector.currentDetectedWord = "";
            }
        }

        ResetDetection();
        StartCoroutine(SequenceWaitForHalo());
    }

    IEnumerator SequenceWaitForHalo()
    {
        ShowInstruction("Sapa NPC dengan isyarat:", signHalo);
    
        yield return RunDialog("", "NPC ini menatapmu menunggu sapaan...", currentNPC.npcSprite);
        
        yield return WaitForKeyword("HALO");

        HideInstruction();
        yield return RunDialog(currentNPC.npcName, "Halo juga! Ada yang bisa kubantu?", currentNPC.npcSprite);
        
        currentStepIndex = 0;
        StartCoroutine(ProcessConversationStep());
    }

    IEnumerator ProcessConversationStep()
    {
        if (currentStepIndex >= currentNPC.conversationSteps.Length)
        {
            StartCoroutine(SequenceWaitForThanks());
            yield break;
        }

        ConversationStep step = currentNPC.conversationSteps[currentStepIndex];

        switch (step.type)
        {
            case InteractionType.NormalDialog:
                yield return RunDialog(step.speakerName, step.text, currentNPC.npcSprite);
                yield return WaitForClick(); 
                NextStep();
                break;

            case InteractionType.Quiz:
                yield return HandleQuiz(step);
                break;

            case InteractionType.GiveItem:
                yield return HandleGiveItem(step);
                break;
        }
    }

    IEnumerator HandleQuiz(ConversationStep step)
    {
        yield return RunDialog(step.speakerName, step.text, currentNPC.npcSprite);
        ShowInstruction("Jawab pertanyaan (BETUL / SALAH)", null);

        ResetDetection();

        bool answered = false;
        while (!answered)
        {
            if (!string.IsNullOrEmpty(currentStableKeyword))
            {
                // Support "BETUL" dan "BENAR"
                bool isCorrectAnswer = (currentStableKeyword == step.correctAnswer) || 
                                       (step.correctAnswer == "BENAR" && currentStableKeyword == "BETUL") ||
                                       (step.correctAnswer == "BETUL" && currentStableKeyword == "BENAR");

                if (isCorrectAnswer)
                {
                    HideInstruction();
                    yield return RunDialog(currentNPC.npcName, step.correctFeedback, currentNPC.npcSprite);
                    yield return WaitForClick();
                    answered = true;
                    NextStep();
                }
                else if (currentStableKeyword == "BENAR" || currentStableKeyword == "SALAH" || currentStableKeyword == "BETUL")
                {
                    HideInstruction();
                    yield return RunDialog(currentNPC.npcName, step.wrongFeedback, currentNPC.npcSprite);
                    yield return WaitForClick();
                    answered = true; // Lanjut walaupun salah
                    NextStep();      
                }
            }
            yield return null;
        }
    }

    IEnumerator HandleGiveItem(ConversationStep step)
    {
        yield return RunDialog(step.speakerName, step.text, currentNPC.npcSprite);
        ShowInstruction($"Isyarat 'AMBIL' untuk menerima {step.itemName}", signAmbil);
        
        yield return WaitForKeyword("AMBIL");

        HideInstruction();
        yield return RunDialog("", $"Kamu menerima {step.itemName}!", step.itemIcon);
        yield return WaitForClick();
        
        NextStep();
    }

    IEnumerator SequenceWaitForThanks()
    {
        ShowInstruction("Ucapkan TERIMA KASIH", signTerimaKasih);
        
        yield return WaitForKeyword("TERIMA KASIH");

        HideInstruction();
        yield return RunDialog(currentNPC.npcName, "Sama-sama! Sampai jumpa.", currentNPC.npcSprite);
        yield return WaitForClick();

        EndInteraction();
    }

    void EndInteraction()
    {
       if (dialogManager != null) 
        {
            dialogManager.ClearBackground();
            dialogManager.HideDialog();
        }

        if (currentNPC != null) currentNPC.isInteractionCompleted = true;
        
        // Panggil proses bersih-bersih dan restart kamera
        StartCoroutine(SwitchToNavigationMode());
    }

    // --- TRANSISI KELUAR (Convo -> Nav) ---
    IEnumerator SwitchToNavigationMode()
    {
        // 1. LANGSUNG MATIKAN SEMUA KOMPONEN CONVERSATION
        if (conversationYoloObject != null) 
        {
            if (conversationDetector != null) conversationDetector.ForceClearBoxes();
            conversationYoloObject.SetActive(false); // <--- Langsung dimatikan
        }
        
        if (interactionPanel != null) interactionPanel.SetActive(false); // <--- UI Mati
        
        // Reset variabel internal biar bersih total
        ResetDetection();

        // 2. RESTART KAMERA (Matikan -> Tunggu -> Nyalakan)
        // Ini penting supaya resource kamera dilepas sebelum dipakai Navigasi
        if (universalCamera != null)
        {
            universalCamera.gameObject.SetActive(false);
        }

        yield return new WaitForSeconds(0.5f); // Jeda aman

        if (universalCamera != null)
        {
            universalCamera.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(0.2f); // Jeda warmup kamera

        // 3. NYALAKAN KEMBALI NAVIGASI
        if (navigationYoloObject != null) 
        {
            navigationYoloObject.SetActive(true);
            if (navigationDetector != null) 
            {
                navigationDetector.ForceClearBoxes();
                navigationDetector.currentDetectedWord = "";
            }
        }

        // 4. KEMBALIKAN KONTROL PLAYER
        if (playerController != null) playerController.isInputLocked = false;
        
        // Debug.Log("Selesai. Kembali ke Mode Navigasi.");
    }

    // --- Helper Functions ---

    IEnumerator WaitForKeyword(string targetWord)
    {
        ResetDetection();
        while (currentStableKeyword != targetWord)
        {
            yield return null;
        }
    }

    void ResetDetection()
    {
        holdTimer = 0f;
        currentStableKeyword = "";
        lastFrameWord = "";
        if(conversationDetector != null) conversationDetector.currentDetectedWord = "";
    }

    void NextStep()
    {
        currentStepIndex++;
        StartCoroutine(ProcessConversationStep());
    }

    void ShowInstruction(string text, Sprite icon)
    {
        if (interactionPanel != null) interactionPanel.SetActive(true);
        if (instructionText != null) instructionText.text = text;
        if (interactionIcon != null)
        {
            interactionIcon.sprite = icon;
            interactionIcon.gameObject.SetActive(icon != null);
        }
    }

    void HideInstruction()
    {
        if (interactionPanel != null) interactionPanel.SetActive(false);
    }

    IEnumerator RunDialog(string name, string text, Sprite sprite)
    {
        if (dialogManager != null)
        {
            dialogManager.gameObject.SetActive(true);
            dialogManager.enabled = true;
            dialogManager.ClearBackground();
            yield return dialogManager.TypeDialog(name, text, sprite);
        }
        else
        {
            Debug.LogError("DialogManager belum di-assign!");
        }
    }

    IEnumerator WaitForClick()
    {
        yield return null;
        if (dialogManager != null)
        {
            while (dialogManager.IsTyping() || (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space)))
            {
                yield return null;
            }
        }
        else
        {
            while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
            {
                yield return null;
            }
        }
    }
}