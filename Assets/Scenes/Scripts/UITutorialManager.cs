using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITutorialManager : MonoBehaviour
{
    [Header("Tutorial UI References")]
    public TextMeshProUGUI instructionText;
    public GameObject instructionPanel;
    public GameObject highlightFrame;
    
    private CanvasGroup instructionCanvasGroup;
    private CanvasGroup highlightCanvasGroup;
    private Color originalTextColor;
    
    [Header("UI Elements to Highlight")]
    public RectTransform cameraViewRect;
    public RectTransform directionArrowsRect;
    public Transform playerTransform;
    public RectTransform pauseButtonRect;
    
    [Header("Pause Menu References")]
    public GameObject pauseMenuPanel;
    public Button saveButton;
    public Button continueButton;
    public Button guideButton;
    
    [Header("Guide References")]
    public GameObject guidePanel;
    public RectTransform profileSection;
    public RectTransform alphabetSection;
    public RectTransform controllerSection;
    public RectTransform conversationSection;
    
    [Header("Player Controller")]
    public NodePlayerController playerController;
    
    // Store states for re-enabling after tutorial
    private bool wasCameraRunning = false;
    private bool wasDetectorRunning = false;
    
    public void StartUITutorial()
    {
        if (PlayerPrefs.GetInt("UITutorialDone", 0) == 1)
        {
            Debug.Log("[UITutorial] Already completed. Skipping...");
            Destroy(this.gameObject);
            return;
        }
        
        if (playerController != null)
        {
            playerController.isInputLocked = true;
        }
        
        // Pause camera and YOLO detector during tutorial (like dialog system)
        UniversalCamera camera = FindFirstObjectByType<UniversalCamera>();
        if (camera != null)
        {
            wasCameraRunning = camera.enabled;
            if (wasCameraRunning)
            {
                camera.StopCamera();
                Debug.Log("[UITutorial] Camera paused for tutorial");
            }
        }
        
        YoloDetector detector = FindFirstObjectByType<YoloDetector>();
        if (detector != null)
        {
            wasDetectorRunning = detector.enabled;
            if (wasDetectorRunning)
            {
                detector.enabled = false;
                Debug.Log("[UITutorial] YOLO detector paused for tutorial");
            }
        }
        
        // Setup CanvasGroups untuk kontrol raycast blocking
        SetupCanvasGroups();
        
        // Simpan warna text original
        if (instructionText != null)
        {
            originalTextColor = instructionText.color;
        }
        
        if (highlightFrame != null) highlightFrame.SetActive(false);
        if (instructionPanel != null) instructionPanel.SetActive(true);
        
        // Ensure tutorial UI renders on top
        EnsureTutorialUIOnTop();
        
        StartCoroutine(RunUITutorialSequence());
    }
    
    // Setup CanvasGroups untuk kontrol raycast
    void SetupCanvasGroups()
    {
        // CanvasGroup untuk instructionPanel
        if (instructionPanel != null)
        {
            instructionCanvasGroup = instructionPanel.GetComponent<CanvasGroup>();
            if (instructionCanvasGroup == null)
            {
                instructionCanvasGroup = instructionPanel.AddComponent<CanvasGroup>();
            }
            instructionCanvasGroup.blocksRaycasts = false; // Default: tidak block klik
        }
        
        // CanvasGroup untuk highlightFrame
        if (highlightFrame != null)
        {
            highlightCanvasGroup = highlightFrame.GetComponent<CanvasGroup>();
            if (highlightCanvasGroup == null)
            {
                highlightCanvasGroup = highlightFrame.AddComponent<CanvasGroup>();
            }
            highlightCanvasGroup.blocksRaycasts = false; // Tidak block klik
        }
    }
    
    // Pastikan tutorial UI selalu render paling atas
    void EnsureTutorialUIOnTop()
    {
        if (instructionPanel != null)
        {
            instructionPanel.transform.SetAsLastSibling();
        }
        if (highlightFrame != null)
        {
            highlightFrame.transform.SetAsLastSibling();
        }
    }
    
    IEnumerator RunUITutorialSequence()
    {
        yield return new WaitForSeconds(0.5f);
        
        yield return ShowInstruction("Selamat datang di Tutorial UI!\nKlik layar untuk lanjut.");
        yield return WaitForClick();
        
        yield return Step1_CameraIntroduction();
        yield return Step2_DirectionArrows();
        yield return Step3_PauseMenu();
        yield return Step4_GuideButton();
        
        yield return ShowInstruction("Tutorial UI selesai!\nKlik untuk melanjutkan permainan.");
        yield return WaitForClick();
        
        CompleteTutorial();
    }
    
    IEnumerator Step1_CameraIntroduction()
    {
        yield return ShowInstruction("Ini adalah area kamera.\nKamera akan mengikuti pergerakan karaktermu.");
        
        if (cameraViewRect != null)
        {
            HighlightElement(cameraViewRect);
        }
        
        yield return new WaitForSeconds(0.5f);
        yield return WaitForClick();
        HideHighlight();
    }
    
    IEnumerator Step2_DirectionArrows()
    {
        yield return ShowInstruction("Perhatikan panah di bawah karakter.\nPanah menunjukkan arah yang bisa kamu tuju.");
        
        if (directionArrowsRect != null)
        {
            HighlightElement(directionArrowsRect);
        }
        else if (playerTransform != null)
        {
            HighlightAroundPlayer();
        }
        
        yield return new WaitForSeconds(0.5f);
        yield return WaitForClick();
        HideHighlight();
    }
    
    IEnumerator Step3_PauseMenu()
    {
        yield return ShowInstruction("Sekarang coba klik tombol Pause\ndi pojok kanan atas!");
        
        if (pauseButtonRect != null)
        {
            HighlightElement(pauseButtonRect);
        }
        
        // Pastikan tutorial panel tidak blocking klik ke pause button
        if (instructionCanvasGroup != null)
        {
            instructionCanvasGroup.blocksRaycasts = false;
        }
        
        // Tunggu player membuka pause menu
        yield return WaitForPauseMenuOpen();
        HideHighlight();
        
        yield return new WaitForSeconds(0.5f);
        
        yield return ShowInstruction("Menu Pause berisi beberapa tombol:");
        yield return new WaitForSeconds(0.5f);
        yield return WaitForClick();
        
        // Highlight Save Button
        if (saveButton != null)
        {
            HighlightElement(saveButton.GetComponent<RectTransform>());
        }
        yield return ShowInstruction("SAVE: Untuk menyimpan progres permainanmu");
        yield return new WaitForSeconds(0.5f);
        yield return WaitForClick();
        HideHighlight();
        
        // Highlight Continue Button
        if (continueButton != null)
        {
            HighlightElement(continueButton.GetComponent<RectTransform>());
        }
        yield return ShowInstruction("CONTINUE: Untuk melanjutkan permainan");
        yield return new WaitForSeconds(0.5f);
        yield return WaitForClick();
        HideHighlight();
    }
    
    IEnumerator Step4_GuideButton()
    {
        // Highlight Guide Button
        if (guideButton != null)
        {
            HighlightElement(guideButton.GetComponent<RectTransform>());
        }
        yield return ShowInstruction("Sekarang coba klik tombol GUIDE!");
        yield return new WaitForSeconds(1f);
        
        // Pastikan tutorial panel tidak blocking klik
        if (instructionCanvasGroup != null)
        {
            instructionCanvasGroup.blocksRaycasts = false;
        }
        
        // Tunggu player membuka guide
        yield return WaitForGuideOpen();
        HideHighlight();
        
        // Ubah text jadi hitam agar lebih terbaca di guide menu
        if (instructionText != null)
        {
            instructionText.color = Color.black;
        }
        
        yield return new WaitForSeconds(0.5f);
        
        yield return ShowInstruction("Panduan berisi 4 bagian:");
        yield return new WaitForSeconds(1f);
        yield return WaitForClick();
        
        // Highlight Profile Section
        if (profileSection != null)
        {
            HighlightElement(profileSection);
        }
        yield return ShowInstruction("PROFILE: Informasi tentang karaktermu");
        yield return new WaitForSeconds(1f);
        yield return WaitForClick();
        HideHighlight();
        
        // Highlight Alphabet Section
        if (alphabetSection != null)
        {
            HighlightElement(alphabetSection);
        }
        yield return ShowInstruction("ALPHABET: Panduan huruf dan gestur BISINDO");
        yield return new WaitForSeconds(1f);
        yield return WaitForClick();
        HideHighlight();
        
        // Highlight Controller Section
        if (controllerSection != null)
        {
            HighlightElement(controllerSection);
        }
        yield return ShowInstruction("CONTROLLER: Cara mengontrol karakter");
        yield return new WaitForSeconds(1f);
        yield return WaitForClick();
        HideHighlight();
        
        // Highlight Conversation Section
        if (conversationSection != null)
        {
            HighlightElement(conversationSection);
        }
        yield return ShowInstruction("CONVERSATION: Panduan percakapan dengan NPC");
        yield return new WaitForSeconds(1f);
        yield return WaitForClick();
        HideHighlight();
        
        yield return ShowInstruction("Kamu bisa menutup panduan sekarang.\nKlik di luar panel atau tombol X.");
        yield return WaitForGuideClose();
        
        // Kembalikan warna text ke original
        if (instructionText != null)
        {
            instructionText.color = originalTextColor;
        }
        
        // Buka pause menu lagi agar player bisa klik Continue
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
        
        yield return new WaitForSeconds(0.5f);
        
        // Highlight Continue Button lagi
        if (continueButton != null)
        {
            HighlightElement(continueButton.GetComponent<RectTransform>());
        }
        yield return ShowInstruction("Sekarang klik CONTINUE untuk melanjutkan permainan!");
        yield return new WaitForSeconds(1.5f);
        
        HideHighlight();
        
        // Tunggu player menutup pause menu dengan klik Continue
        yield return WaitForPauseMenuClose();
    }
    
    IEnumerator ShowInstruction(string text)
    {
        if (instructionText != null)
        {
            instructionText.text = text;
        }
        
        // Ensure tutorial UI stays on top
        EnsureTutorialUIOnTop();
        
        yield return null;
    }
    
    void HighlightElement(RectTransform target)
    {
        if (highlightFrame == null || target == null) return;
        
        RectTransform highlightRect = highlightFrame.GetComponent<RectTransform>();
        if (highlightRect != null)
        {
            highlightFrame.SetActive(true);
            highlightRect.position = target.position;
            highlightRect.sizeDelta = target.sizeDelta + new Vector2(20, 20);
        }        
        // Ensure highlight stays on top of menus
        EnsureTutorialUIOnTop();        
        // Ensure highlight stays on top of menus
        EnsureTutorialUIOnTop();
    }
    
    void HideHighlight()
    {
        if (highlightFrame != null)
        {
            highlightFrame.SetActive(false);
        }
    }
    
    void HighlightAroundPlayer()
    {
        if (highlightFrame == null || playerTransform == null) return;
        
        Camera mainCam = Camera.main;
        if (mainCam == null) return;
        
        Vector3 playerScreenPos = mainCam.WorldToScreenPoint(playerTransform.position);
        
        RectTransform highlightRect = highlightFrame.GetComponent<RectTransform>();
        if (highlightRect != null)
        {
            highlightFrame.SetActive(true);
            highlightRect.position = playerScreenPos;
            highlightRect.sizeDelta = new Vector2(200, 200);
        }
    }
    
    IEnumerator WaitForClick()
    {
        yield return new WaitForSeconds(0.2f);
        
        while (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space))
        {
            yield return null;
        }
    }
    
    IEnumerator WaitForPauseMenuOpen()
    {
        if (pauseMenuPanel == null) yield break;
        
        while (!pauseMenuPanel.activeSelf)
        {
            yield return null;
        }
        
        // Ensure tutorial UI renders above pause menu
        EnsureTutorialUIOnTop();
    }
    
    IEnumerator WaitForGuideOpen()
    {
        if (guidePanel == null) yield break;
        
        while (!guidePanel.activeSelf)
        {
            yield return null;
        }
        
        // Ensure tutorial UI renders above guide menu
        EnsureTutorialUIOnTop();
    }
    
    IEnumerator WaitForGuideClose()
    {
        if (guidePanel == null) yield break;
        
        while (guidePanel.activeSelf)
        {
            yield return null;
        }        
        // Ensure tutorial UI renders above guide menu
        EnsureTutorialUIOnTop();    
    }
    
    IEnumerator WaitForPauseMenuClose()
    {
        if (pauseMenuPanel == null) yield break;
        
        while (pauseMenuPanel.activeSelf)
        {
            yield return null;
        }
        
        Debug.Log("[UITutorial] Pause menu closed, continuing tutorial...");
    }
    
    void CompleteTutorial()
    {
        PlayerPrefs.SetInt("UITutorialDone", 1);
        PlayerPrefs.Save();
        Debug.Log("[UITutorial] Completed and saved!");
        
        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (highlightFrame != null) highlightFrame.SetActive(false);
        
        if (playerController != null)
        {
            playerController.isInputLocked = false;
        }
        
        // Resume camera and YOLO detector after tutorial (like dialog system)
        if (wasCameraRunning)
        {
            UniversalCamera camera = FindFirstObjectByType<UniversalCamera>();
            if (camera != null)
            {
                camera.AllowCameraStart();
                camera.StartCamera();
                Debug.Log("[UITutorial] Camera resumed after tutorial");
            }
            wasCameraRunning = false;
        }
        
        if (wasDetectorRunning)
        {
            YoloDetector detector = FindFirstObjectByType<YoloDetector>();
            if (detector != null)
            {
                detector.enabled = true;
                Debug.Log("[UITutorial] YOLO detector resumed after tutorial");
            }
            wasDetectorRunning = false;
        }
        
        // Aktifkan navigation system
        NavigationManager navManager = FindFirstObjectByType<NavigationManager>();
        if (navManager != null)
        {
            navManager.EnableNavigationSystem();
        }
        
        Destroy(this.gameObject);
    }
}
