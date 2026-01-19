using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TutorialCutscene : MonoBehaviour
{
    [Header("Managers")]
    public DialogManager dialogManager;
    public NodePlayerController playerController;
    public YoloDetector yoloDetector; 

    [Header("Hand Sign Assets")]
    public Sprite signAtas;
    public Sprite signBawah;
    public Sprite signKiri;
    public Sprite signKanan;
    
    [Header("Animation Sprites (for KANAN/KIRI)")]
    public Sprite signKananFrame1;
    public Sprite signKananFrame2;
    public Sprite signKiriFrame1;
    public Sprite signKiriFrame2;
    public float animationSpeed = 0.5f; // Time between frames

    [Header("Character Expressions (Optional)")]
    public Sprite alyaNormal;
    public Sprite alyaSurprised;
    public Sprite alyaHappy;

    [Header("Tutorial UI")]
    public GameObject tutorialGuidePanel; // Panel Induk
    public Image gestureDisplay;          // Tempat gambar muncul
    
    private Coroutine animationCoroutine; // For looping animation

    public void StartTutorial()
    {
        Debug.Log(">>> FUNGSI StartTutorial DIPANGGIL! <<<");

        // ------------------------------------------------------------------
        // 1. CEK SAVE DATA TERLEBIH DAHULU (PRIORITAS UTAMA)
        // ------------------------------------------------------------------
        if (PlayerPrefs.GetInt("MovementTutorialDone", 0) == 1)
        {
            Debug.Log("Tutorial Gerakan sudah tamat. Menghancurkan Object Tutorial.");

            // Langkah 1: Pastikan UI Panel bersih dulu
            HideGestureImage();
            if (tutorialGuidePanel != null) tutorialGuidePanel.SetActive(false);

            // Langkah 2: Pastikan Player BEBAS (Penting!)
            if (playerController != null) playerController.isInputLocked = false;

            // Langkah 3: HANCURKAN OBJECT INI SELAMANYA
            Destroy(this.gameObject); 
            
            return; // Berhenti di sini
        }
        // ------------------------------------------------------------------

        // Jika belum tamat, lanjutkan setup seperti biasa
        this.gameObject.SetActive(true);
        this.enabled = true;

        Debug.Log("Tutorial Started!");

        // Kunci Player
        if (playerController != null) playerController.isInputLocked = true; 
        
        // Setup Awal
        HideGestureImage();

        // Mulai Cerita
        StartCoroutine(PlayTutorialSequence());
    }

    IEnumerator PlayTutorialSequence()
    {
        Debug.Log(">>> COROUTINE MULAI JALAN! <<<");
        if (dialogManager != null)
        {
            dialogManager.HideDialog();
            dialogManager.ClearBackground(); 
        }
        
        yield return new WaitForSeconds(0.3f);

        // --- LANGSUNG KE TUTORIAL ---
        yield return RunDialog("Tutorial", "Pelajari gerakan dasar untuk bergerak di dunia Magica.", null);
        yield return WaitForInput();

        // --- LANGSUNG KE INTERAKTIF ---
        if (dialogManager != null) dialogManager.HideDialog();
        
        yield return StartCoroutine(InteractiveTutorial());
    }

    IEnumerator InteractiveTutorial()
    {
        yield return RunDialog("Tutorial", "Tirukan gerakan tangan yang muncul di layar.", null);
        yield return new WaitForSeconds(2f); 
        if (dialogManager != null) dialogManager.HideDialog();

        // --- TES 1: ATAS ---
        yield return PerformTest("ATAS", signAtas);
        
        // --- TES 2: BAWAH ---
        yield return PerformTest("BAWAH", signBawah);
        
        // --- TES 3: KIRI ---
        yield return PerformTest("KIRI", signKiri);

        // --- TES 4: KANAN ---
        yield return PerformTest("KANAN", signKanan);

        // --- SELESAI ---
        HideGestureImage(); 

        yield return RunDialog("Buku Misterius", "Sempurna. Kau siap menjelajah.", null);
        yield return WaitForInput();
        
        if (dialogManager != null) dialogManager.HideDialog();
        
        // --- SAVE DATA & CLEANUP ---
        
        // 1. Buka Kunci Input Player (sementara)
        if (playerController != null) playerController.isInputLocked = false;
        
        Debug.Log("Tutorial Gerakan Selesai! Menyimpan data...");
        PlayerPrefs.SetInt("MovementTutorialDone", 1);
        PlayerPrefs.Save();
        
        // 2. Cek dan mulai UI Tutorial sebelum Destroy
        UITutorialManager uiTutorial = FindFirstObjectByType<UITutorialManager>();
        if (uiTutorial != null && PlayerPrefs.GetInt("UITutorialDone", 0) == 0)
        {
            Debug.Log("[TutorialCutscene] Starting UI Tutorial...");
            yield return new WaitForSeconds(0.5f);
            uiTutorial.StartUITutorial();
        }
        
        Destroy(this.gameObject);
    }

    IEnumerator PerformTest(string targetWord, Sprite signImage)
    {
        // Start animation if KANAN or KIRI
        if (targetWord == "KANAN" && signKananFrame1 != null && signKananFrame2 != null)
        {
            animationCoroutine = StartCoroutine(AnimateGesture(signKananFrame1, signKananFrame2));
        }
        else if (targetWord == "KIRI" && signKiriFrame1 != null && signKiriFrame2 != null)
        {
            animationCoroutine = StartCoroutine(AnimateGesture(signKiriFrame1, signKiriFrame2));
        }
        else
        {
            ShowGestureImage(signImage);
        }

        if (dialogManager != null)
        {
            dialogManager.ClearBackground();
            StartCoroutine(dialogManager.TypeDialog("Tutorial", $"Lakukan Gerakan: {targetWord}", null));
        }
        
        bool success = false;
        while (!success)
        {
            if (yoloDetector != null && !string.IsNullOrEmpty(yoloDetector.currentDetectedWord))
            {
                string detected = yoloDetector.currentDetectedWord.Trim().ToUpper();
                
                if (detected == targetWord)
                {
                    success = true;
                    
                    // Stop animation
                    if (animationCoroutine != null)
                    {
                        StopCoroutine(animationCoroutine);
                        animationCoroutine = null;
                    }
                    
                    HideGestureImage(); 
                    if (dialogManager != null) dialogManager.HideDialog();
                    if (playerController != null) playerController.TryMove(targetWord);
                    yield return new WaitForSeconds(1.0f); 
                }
            }
            yield return null;
        }
    }

    // --- ANIMATION HELPER ---
    IEnumerator AnimateGesture(Sprite frame1, Sprite frame2)
    {
        if (tutorialGuidePanel != null) tutorialGuidePanel.SetActive(true);
        
        while (true)
        {
            if (gestureDisplay != null)
            {
                gestureDisplay.sprite = frame1;
                gestureDisplay.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(animationSpeed);
            
            if (gestureDisplay != null)
            {
                gestureDisplay.sprite = frame2;
            }
            yield return new WaitForSeconds(animationSpeed);
        }
    }

    // --- FUNGSI HELPER UI ---
    void ShowGestureImage(Sprite sprite)
    {
        if (tutorialGuidePanel != null) tutorialGuidePanel.SetActive(true);
        if (gestureDisplay != null)
        {
            gestureDisplay.sprite = sprite;
            gestureDisplay.gameObject.SetActive(true);
        }
    }

    void HideGestureImage()
    {
        if (tutorialGuidePanel != null) tutorialGuidePanel.SetActive(false);
    }

    // --- FUNGSI DIALOG MANAGER (DENGAN SAFE WAKE UP) ---
    IEnumerator RunDialog(string name, string text, Sprite sprite)
    {
        if (dialogManager != null)
        {
            // Pastikan Dialog Manager bangun dulu
            dialogManager.gameObject.SetActive(true);
            dialogManager.enabled = true;

            dialogManager.ClearBackground();
            yield return dialogManager.TypeDialog(name, text, sprite);
        }
        else
        {
            Debug.LogError("Dialog Manager hilang/belum di-assign!");
        }
    }

    IEnumerator WaitForInput()
    {
        yield return null; 
        if (dialogManager != null)
        {
            while (dialogManager.IsTyping() || (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space)))
            {
                yield return null;
            }
        }
    }
}