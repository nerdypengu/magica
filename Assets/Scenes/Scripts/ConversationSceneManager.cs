using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ConversationSceneManager : MonoBehaviour
{
    [Header("Managers")]
    public DialogManager dialogManager;

    [Header("Yolo Integration")]
    public GameObject conversationYoloObject;
    public YoloConversation conversationDetector; 

    [Header("Camera Control")]
    public UniversalCamera universalCamera; 

    [Header("UI References")]
    public GameObject interactionPanel;   
    public TextMeshProUGUI instructionText;
    public Image guideImage1;
    public TextMeshProUGUI guideText1;
    public Image guideImage2;
    public TextMeshProUGUI guideText2;
    public Toggle guideToggle;
    public GameObject loadingPanel;
    public Image backgroundImage;
    public Image itemDisplayImage;

    [Header("Assets - Special Signs")]
    public Sprite signHalo;
    public Sprite signAmbil;
    public Sprite signTerimaKasih;
    public Sprite signBetul;
    public Sprite signSalah; 
    
    private string npcID;
    private string npcName;
    private Sprite npcSprite;
    private ConversationStep[] conversationSteps;
    private int currentStepIndex = 0;
    private string currentStableKeyword = "";
    private bool isGuideActive = true;
    private bool isListeningForGesture = false;

    void Start()
    {
        LoadNPCData();

        if (universalCamera == null) 
            universalCamera = FindFirstObjectByType<UniversalCamera>();
        
        if (guideToggle != null)
        {
            guideToggle.enabled = true;
            guideToggle.interactable = true;
            
            isGuideActive = guideToggle.isOn;
            guideToggle.onValueChanged.AddListener(OnToggleGuide);
            
            Debug.Log($"[CONVO] Guide toggle setup - Initial state: {(guideToggle.isOn ? "ON" : "OFF")} | Interactable: {guideToggle.interactable}");
        }
        else
        {
            isGuideActive = true;
            Debug.Log("[CONVO] No guide toggle assigned - guide always ON");
        }

        StartCoroutine(InitializeConversationScene());
    }

    public void OnToggleGuide(bool isOn)
    {
        Debug.Log($"[CONVO] OnToggleGuide called - New value: {(isOn ? "ON" : "OFF")}");
        
        isGuideActive = isOn;
        
        if (!isGuideActive && interactionPanel != null)
        {
            interactionPanel.SetActive(false);
            Debug.Log("[CONVO] Guide OFF - hiding interaction panel");
        }
        else if (isGuideActive)
        {
            Debug.Log("[CONVO] Guide ON - interaction panel can show");
        }
        
        Debug.Log($"Conversation Guide: {(isGuideActive ? "ON" : "OFF")}");
    }
    
    public void SetGuideActive(bool active)
    {
        Debug.Log($"[CONVO] SetGuideActive called: {active}");
        isGuideActive = active;
        
        if (guideToggle != null)
        {
            guideToggle.isOn = active;
        }
        
        if (!active && interactionPanel != null)
        {
            interactionPanel.SetActive(false);
        }
    }

    void LoadNPCData()
    {
        if (ConversationDataHolder.Instance != null)
        {
            npcID = ConversationDataHolder.Instance.npcID;
            npcName = ConversationDataHolder.Instance.npcName;
            npcSprite = ConversationDataHolder.Instance.npcSprite;
            conversationSteps = ConversationDataHolder.Instance.conversationSteps;

            if (backgroundImage != null && ConversationDataHolder.Instance.backgroundSprite != null)
            {
                backgroundImage.sprite = ConversationDataHolder.Instance.backgroundSprite;
                Debug.Log($"[CONVO] Background set to: {ConversationDataHolder.Instance.backgroundSprite.name}");
            }

            Debug.Log($"ConversationScene: Loaded NPC '{npcName}' with {conversationSteps.Length} steps");
            Debug.Log($"NPC Sprite: {(npcSprite != null ? npcSprite.name : "NULL - NO SPRITE!")}");
        }
        else
        {
            Debug.LogError("ConversationDataHolder not found! Cannot load NPC data.");
        }
    }

    IEnumerator InitializeConversationScene()
    {
        // Show loading panel
        if (loadingPanel != null)
            loadingPanel.SetActive(true);
        
        // Show NPC image early (before HALO), but hide dialog text
        if (dialogManager != null)
        {
            // Hide dialog panel initially
            if (dialogManager.dialogPanel != null)
                dialogManager.dialogPanel.SetActive(false);
            
            // Also hide dialog text and name text individually
            if (dialogManager.dialogText != null)
                dialogManager.dialogText.gameObject.SetActive(false);
            if (dialogManager.nameText != null)
                dialogManager.nameText.gameObject.SetActive(false);
            
            // Show NPC sprite immediately and keep it visible
            if (dialogManager.npcImage != null && npcSprite != null)
            {
                dialogManager.npcImage.sprite = npcSprite;
                dialogManager.npcImage.gameObject.SetActive(true);
            }
        }
        
        // Wait for camera to initialize
        yield return new WaitForSeconds(0.5f);

        ResetDetection();
        
        // Hide loading panel - scene ready
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        StartCoroutine(SequenceWaitForHalo());
    }

    void Update()
    {
        // Only process detection when we're actively listening for gestures
        if (!isListeningForGesture || conversationDetector == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(conversationDetector.currentDetectedKeyword))
        {
            currentStableKeyword = conversationDetector.currentDetectedKeyword.ToUpper();
            Debug.Log($"<color=green>[CONVO] Keyword confirmed: '{currentStableKeyword}'</color>");
            
            conversationDetector.currentDetectedKeyword = "";
        }
    }

    // --- CONVERSATION SEQUENCE ---
    IEnumerator SequenceWaitForHalo()
    {
        if (universalCamera != null)
        {
            universalCamera.AllowCameraStart();
            universalCamera.StartCamera();
        }
        
        if (conversationYoloObject != null)
        {
            conversationYoloObject.SetActive(true);
            if (conversationDetector != null)
            {
                conversationDetector.ForceClearBoxes();
                conversationDetector.ResetDetection();
            }
        }
        
        yield return new WaitForSeconds(0.3f);
        
        isListeningForGesture = true;
        
        ShowInstruction("Sapa NPC dengan isyarat HALO:", signHalo);
        
        yield return WaitForKeyword("halo");

        // PAUSE camera and detector for dialog
        if (universalCamera != null)
        {
            universalCamera.PauseCamera();
        }
        if (conversationDetector != null)
        {
            conversationDetector.enabled = false;
        }
        
        isListeningForGesture = false;
        HideInstruction();
        ResetDetection();
        
        yield return new WaitForSeconds(0.3f);
        
        yield return RunDialog(npcName, "Halo juga! Apa yang bisa saya bantu?", npcSprite);
        yield return WaitForClick();
        
        yield return StartCoroutine(SequenceConversationSteps());
    }

    IEnumerator SequenceConversationSteps()
    {
        for (currentStepIndex = 0; currentStepIndex < conversationSteps.Length; currentStepIndex++)
        {
            ConversationStep step = conversationSteps[currentStepIndex];

            switch (step.type)
            {
                case InteractionType.NormalDialog:
                    isListeningForGesture = false;
                    yield return RunDialog(step.speakerName, step.text, npcSprite);
                    yield return WaitForClick();
                    break;

                case InteractionType.Quiz:
                    yield return HandleQuiz(step);
                    break;

                case InteractionType.GiveItem:
                    yield return HandleGiveItem(step);
                    break;
            }
        }

        // Camera and detector already running - just reset and enable listening
        // RESUME camera and detector for TERIMA KASIH detection
        if (universalCamera != null)
        {
            universalCamera.ResumeCamera();
        }
        if (conversationDetector != null)
        {
            conversationDetector.enabled = true;
        }
        
        ResetDetection();
        
        yield return new WaitForSeconds(0.3f);
        
        isListeningForGesture = true;
        ShowInstruction("Lakukan isyarat TERIMA KASIH untuk mengakhiri percakapan", signTerimaKasih);
        yield return WaitForKeyword("Terima Kasih");
        isListeningForGesture = false;
        
        // STOP camera before exit (not pause) - Scene1 needs clean restart
        if (universalCamera != null)
        {
            universalCamera.StopCamera();
        }
        if (conversationDetector != null)
        {
            conversationDetector.enabled = false;
        }
        
        MarkConversationComplete();
        ReturnToPreviousScene();
    }

    IEnumerator HandleQuiz(ConversationStep step)
    {
        isListeningForGesture = false;
        
        yield return RunDialog(step.speakerName, step.text, npcSprite);
        
        // RESUME camera and detector for quiz
        if (universalCamera != null)
        {
            universalCamera.ResumeCamera();
        }
        if (conversationDetector != null)
        {
            conversationDetector.enabled = true;
        }
        
        ResetDetection();
        
        yield return new WaitForSeconds(0.3f);
        
        isListeningForGesture = true;
        
        if (isGuideActive && interactionPanel != null)
        {
            interactionPanel.SetActive(true);
            
            if (instructionText != null)
                instructionText.text = "Jawab dengan isyarat BETUL atau SALAH";
            
            if (guideImage1 != null && signBetul != null)
            {
                guideImage1.sprite = signBetul;
                guideImage1.gameObject.SetActive(true);
            }
            if (guideText1 != null)
            {
                guideText1.text = "BETUL";
                guideText1.gameObject.SetActive(true);
            }
            
            // Image 2: SALAH
            if (guideImage2 != null && signSalah != null)
            {
                guideImage2.sprite = signSalah;
                guideImage2.gameObject.SetActive(true);
            }
            if (guideText2 != null)
            {
                guideText2.text = "SALAH";
                guideText2.gameObject.SetActive(true);
            }
        }

        ResetDetection();

        bool answered = false;
        while (!answered)
        {
            if (!string.IsNullOrEmpty(currentStableKeyword))
            {
                // Normalize: model uses "Betul" and "Salah" with capital first letter
                // Trim to remove any trailing/leading spaces from label file
                string normalizedKeyword = currentStableKeyword.ToUpper().Trim();
                string normalizedCorrect = step.correctAnswer.ToUpper().Trim();
                
                Debug.Log($"[QUIZ] Detected answer: '{normalizedKeyword}' | Correct: '{normalizedCorrect}'");
                
                // Check if it's Betul or Salah
                if (normalizedKeyword == "BETUL" || normalizedKeyword == "SALAH")
                {
                    // Clear immediately to prevent re-processing
                    currentStableKeyword = "";
                    ResetDetection(); // Also reset detector
                    
                    if (normalizedKeyword == normalizedCorrect)
                    {
                        HideInstruction();
                        // Stop listening during feedback
                        isListeningForGesture = false;
                        Debug.Log("[QUIZ] Correct answer! Showing feedback...");
                        yield return RunDialog(npcName, step.correctFeedback, npcSprite);
                        yield return WaitForClick();
                        answered = true;
                        Debug.Log("[QUIZ] Quiz completed - moving to next step");
                    }
                    else
                    {
                        HideInstruction();
                        // Stop listening during feedback
                        isListeningForGesture = false;
                        Debug.Log("[QUIZ] Wrong answer! Showing feedback...");
                        yield return RunDialog(npcName, step.wrongFeedback, npcSprite);
                        yield return WaitForClick();
                        answered = true; // Continue even if wrong
                        Debug.Log("[QUIZ] Quiz completed - moving to next step");
                    }
                }
                else
                {
                    Debug.LogWarning($"[QUIZ] Detected '{normalizedKeyword}' but not valid answer (need BETUL or SALAH)");
                    currentStableKeyword = ""; // Clear invalid keyword
                }
            }
            yield return null;
        }
        
        // Keep camera running - just stop listening
        isListeningForGesture = false;
        
        // PAUSE after quiz
        if (universalCamera != null) universalCamera.PauseCamera();
        if (conversationDetector != null) conversationDetector.enabled = false;
        
        Debug.Log("[QUIZ] Exiting HandleQuiz");
    }

    IEnumerator HandleGiveItem(ConversationStep step)
    {
        isListeningForGesture = false;
        
        yield return RunDialog(step.speakerName, step.text, npcSprite);
        
        if (itemDisplayImage != null && step.itemIcon != null)
        {
            // ACTIVATE first, then set sprite
            itemDisplayImage.gameObject.SetActive(true);
            itemDisplayImage.sprite = step.itemIcon;
            
            Debug.Log($"[CONVO] Showing item: {step.itemName} with icon: {step.itemIcon.name}");
        }
        else
        {
            if (itemDisplayImage == null)
                Debug.LogError("[CONVO] itemDisplayImage is NULL! Assign it in Inspector!");
            if (step.itemIcon == null)
                Debug.LogError($"[CONVO] itemIcon for '{step.itemName}' is NULL! Assign sprite in NPCInteractable Inspector!");
        }
        
        // RESUME camera and detector for AMBIL detection
        if (universalCamera != null)
        {
            universalCamera.ResumeCamera();
        }
        if (conversationDetector != null)
        {
            conversationDetector.enabled = true;
        }
        
        ResetDetection();
        
        yield return new WaitForSeconds(0.3f);
        
        isListeningForGesture = true;
        
        ShowInstruction($"Lakukan isyarat AMBIL untuk menerima {step.itemName}", signAmbil);

        yield return WaitForKeyword("ambil");
        
        // PAUSE after AMBIL detected
        if (universalCamera != null) universalCamera.PauseCamera();
        if (conversationDetector != null) conversationDetector.enabled = false;
        
        isListeningForGesture = false;
        
        HideInstruction();
        yield return RunDialog(npcName, $"Kamu mendapat: {step.itemName}!", npcSprite);
        yield return WaitForClick();

        if (itemDisplayImage != null)
        {
            itemDisplayImage.gameObject.SetActive(false);
        }

        Debug.Log($"Player received item: {step.itemName}");
    }

    IEnumerator WaitForClick()
    {
        yield return new WaitForSeconds(0.1f);

        while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }
    }

    void MarkConversationComplete()
    {
        if (!string.IsNullOrEmpty(npcID))
        {
            PlayerPrefs.SetInt(npcID + "_Done", 1);
            PlayerPrefs.Save();
            Debug.Log($"Conversation with {npcName} marked as complete!");
        }
    }

    void ReturnToPreviousScene()
    {
        Debug.Log("Returning to previous scene...");
        
        // CLEANUP RESOURCES (matching battle scene pattern)
        if (conversationYoloObject != null)
        {
            Debug.Log("Cleaning up Conversation YOLO...");
            conversationYoloObject.SetActive(false);
        }
        
        if (universalCamera != null)
        {
            Debug.Log("Cleaning up Conversation Camera...");
            universalCamera.gameObject.SetActive(false);
        }
        
        // SET RETURN POSITION
        if (GameManager.Instance != null && ConversationDataHolder.Instance != null)
        {
            GameManager.Instance.overrideTargetNodeID = ConversationDataHolder.Instance.returnNodeID;
            Debug.Log($"Setting Return Spawn ID: {ConversationDataHolder.Instance.returnNodeID}");
        }

        // LOAD SCENE
        string returnScene = ConversationDataHolder.Instance != null ? 
            ConversationDataHolder.Instance.returnScene : "Scene1";
        
        SceneManager.LoadScene(returnScene);
    }

    // --- HELPER FUNCTIONS ---
    void ResetDetection()
    {
        currentStableKeyword = "";
        if (conversationDetector != null)
        {
            conversationDetector.ResetDetection();
        }
    }

    void ShowInstruction(string text, Sprite icon)
    {
        // Hanya tampilkan jika guide aktif
        if (!isGuideActive)
        {
            return;
        }
        
        if (interactionPanel != null)
        {
            interactionPanel.SetActive(true);
        }
        
        if (instructionText != null)
        {
            instructionText.text = text;
        }
        
        // Single icon mode: Set image1 and text1
        if (guideImage1 != null && icon != null) 
        {
            guideImage1.sprite = icon;
            guideImage1.gameObject.SetActive(true);
            
            // Extract gesture name from text for guideText1
            if (guideText1 != null)
            {
                string gestureName = "";
                if (text.Contains("HALO")) gestureName = "HALO";
                else if (text.Contains("AMBIL")) gestureName = "AMBIL";
                else if (text.Contains("TERIMA KASIH")) gestureName = "TERIMA KASIH";
                
                guideText1.text = gestureName;
                guideText1.gameObject.SetActive(!string.IsNullOrEmpty(gestureName));
            }
        }
        else if (guideImage1 != null)
        {
            guideImage1.gameObject.SetActive(false);
            if (guideText1 != null) guideText1.gameObject.SetActive(false);
        }
        
        // Hide second image slot
        if (guideImage2 != null)
        {
            guideImage2.gameObject.SetActive(false);
        }
        if (guideText2 != null)
        {
            guideText2.gameObject.SetActive(false);
        }
    }

    void HideInstruction()
    {
        // Only hide instruction text - keep npcImage and itemImage visible!
        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(false);
        }
        
        // Also hide guide images/texts if they're shown
        if (guideImage1 != null)
        {
            guideImage1.gameObject.SetActive(false);
        }
        if (guideText1 != null)
        {
            guideText1.gameObject.SetActive(false);
        }
        if (guideImage2 != null)
        {
            guideImage2.gameObject.SetActive(false);
        }
        if (guideText2 != null)
        {
            guideText2.gameObject.SetActive(false);
        }
    }

    IEnumerator RunDialog(string speaker, string text, Sprite sprite)
    {
        HideInstruction();
        
        yield return dialogManager.TypeDialog(speaker, text, sprite);
        yield return new WaitForSeconds(0.1f);

        while (dialogManager.IsTyping() || 
              (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space)))
        {
            yield return null;
        }
    }

    IEnumerator WaitForKeyword(string keyword)
    {
        string target = keyword.ToUpper();
        currentStableKeyword = "";
        
        Debug.Log($"[CONVO] Waiting for keyword: {target}");
        float waitTime = 0f;

        while (currentStableKeyword != target)
        {
            // Debug log every 2 seconds
            waitTime += Time.deltaTime;
            if (waitTime >= 2f)
            {
                string detected = conversationDetector != null ? conversationDetector.currentDetectedWord : "null";
                Debug.Log($"[CONVO] Still waiting for '{target}'... Current stable: '{currentStableKeyword}' | Raw detected: '{detected}'");
                waitTime = 0f;
            }
            yield return null;
        }

        Debug.Log($"<color=cyan>[CONVO] Keyword '{target}' detected!</color>");
        yield return new WaitForSeconds(0.3f);
    }
}
