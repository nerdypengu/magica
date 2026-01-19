using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video; 

public class IntroCutscene : MonoBehaviour
{
    [Header("Manager Reference")]
    public DialogManager dialogManager;

    [Header("Video Settings")]
    public VideoPlayer introVideoPlayer; 
    public GameObject videoScreenObject; 

    [Header("Assets - Sprites")]
    public Sprite bgRoom;
    
    [Header("Gameplay Objects")]
    public GameObject cutsceneParent; 
    public GameObject gameplayParent; 
    
    [Header("Transition Settings")]
    public CanvasGroup fadePanel; 
    public float fadeDuration = 1.0f;
    public TutorialCutscene tutorialScript; 

    [Header("Debug")]
    public bool forcePlayIntro = false;

    // --- VARIABEL BARU UNTUK UI INPUT ---
    private bool _manualSkipTrigger = false; 

    private void Start()
    {
        // 1. CHECK IF INTRO ALREADY PLAYED
        bool hasPlayed = PlayerPrefs.GetInt("IntroPlayed", 0) == 1;

        if (hasPlayed && !forcePlayIntro)
        {
            SkipToGameplayImmediate(); 
            return; 
        }

        PlayerPrefs.SetInt("IntroPlayed", 1);
        PlayerPrefs.Save();

        // Standard Setup
        if (gameplayParent != null) gameplayParent.SetActive(false);
        if (cutsceneParent != null) cutsceneParent.SetActive(true);
        if (tutorialScript != null) tutorialScript.enabled = false;

        dialogManager.HideDialog();
        StartCoroutine(StartIntroSequence());
    }

    private void Update()
    {
        // 1. Skip Video (Space OR UI Button)
        if (introVideoPlayer.isPlaying)
        {
            if (Input.GetKeyDown(KeyCode.Space) || _manualSkipTrigger)
            {
                introVideoPlayer.Stop();
                _manualSkipTrigger = false; // Reset trigger
            }
        }

        // 2. Skip Text Typing (Space OR Click OR UI Button)
        if (!introVideoPlayer.isPlaying)
        {
            bool inputDetected = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || _manualSkipTrigger;

            if (inputDetected)
            {
                if (dialogManager.IsTyping())
                {
                    dialogManager.FinishTyping();
                    _manualSkipTrigger = false; // Reset trigger jika dipakai untuk finish typing
                }
            }
        }
    }

    // --- FUNGSI UNTUK TOMBOL UI (Hubungkan di Inspector) ---

    // OPSI 1: Tombol "Next" (Fungsinya sama seperti klik mouse/spasi)
    // Gunakan ini untuk tombol transparan seukuran layar (mobile tap)
    public void OnNextButton()
    {
        _manualSkipTrigger = true;
    }

    // OPSI 2: Tombol "Skip Intro" (Langsung loncat ke Gameplay)
    // Gunakan ini untuk tombol kecil "Skip >>" di pojok layar
    public void OnSkipAllButton()
    {
        // Hentikan semua urutan cerita yang sedang berjalan
        StopAllCoroutines(); 
        
        // Bersihkan UI Video & Dialog
        if (introVideoPlayer.isPlaying) introVideoPlayer.Stop();
        if (videoScreenObject != null) videoScreenObject.SetActive(false);
        if (dialogManager != null) dialogManager.HideDialog();

        // Langsung jalankan transisi ke gameplay
        Debug.Log("Skipping Cutscene via Button...");
        StartCoroutine(SwitchToGameplayWithFade());
    }

    // -------------------------------------------------------

    IEnumerator StartIntroSequence()
    {
        // --- TEMPLATE VIDEO START ---
        videoScreenObject.SetActive(true);
        dialogManager.HideDialog(); 
        
        // Safety check untuk Video Player
        if (introVideoPlayer != null)
        {
            introVideoPlayer.Prepare();
            while (!introVideoPlayer.isPrepared) yield return null;
            
            RawImage screen = videoScreenObject.GetComponent<RawImage>();
            if (screen != null) { screen.texture = introVideoPlayer.texture; screen.color = Color.white; }

            introVideoPlayer.Play();
            yield return new WaitForSeconds(0.2f); 
            while (introVideoPlayer.isPlaying) yield return null;
        }
        
        videoScreenObject.SetActive(false);

        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        dialogManager.SetBackground(bgRoom);
        yield return new WaitForSeconds(0.5f); 

        yield return RunDialog("Buku Misterius", "......", null);
        yield return WaitForInput();

        yield return RunDialog("Alya", "???", null);
        yield return WaitForInput();

        yield return RunDialog("Buku Misterius", "Oh—kau tidak mendengarku, ya?", null);
        yield return WaitForInput();

        yield return RunDialog("Buku Misterius", "<muncul dalam bentuk tulisan>", null);
        yield return WaitForInput();

        yield return RunDialog("Alya", "(Tertegun. Matanya membulat, lalu tangannya ragu-ragu menirukan bentuk itu)", null);
        yield return WaitForInput();

        yield return RunDialog("Buku Misterius", "Bagus. Kau bisa melihat dan memahami bahasa yang hilang dari banyak orang.", null);
        yield return WaitForInput();

        yield return RunDialog("Alya", "(Menyentuh halaman buku dengan lembut. Ada rasa damai di wajahnya)", null);
        yield return WaitForInput();

        yield return RunDialog("Buku Misterius", "Mari kubimbing kamu dalam BISINDO.", null);
        yield return WaitForInput();

        dialogManager.HideDialog();
        
        StartCoroutine(SwitchToGameplayWithFade());
    }

    IEnumerator SwitchToGameplayWithFade()
    {
        // 1. Fade Out
        if (fadePanel != null)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadePanel.alpha = Mathf.Lerp(0f, 1f, timer / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = 1f;
        }

        yield return new WaitForSeconds(0.5f);
        Debug.Log("Switching to Gameplay...");

        SetupGameplayState();

        yield return new WaitForSeconds(0.5f);

        // 2. Fade In
        if (fadePanel != null)
        {
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                fadePanel.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                yield return null;
            }
            fadePanel.alpha = 0f;
        }
    }

    // Helper: Dipakai saat Start jika intro sudah pernah dimainkan
    void SkipToGameplayImmediate()
    {
        Debug.Log("Intro already played. Skipping directly to Gameplay.");
        SetupGameplayState();
        if (fadePanel != null) fadePanel.alpha = 0f; 
    }

    void SetupGameplayState()
    {
        if (cutsceneParent != null) cutsceneParent.SetActive(false);
        if (videoScreenObject != null) videoScreenObject.SetActive(false);
        if (gameplayParent != null) gameplayParent.SetActive(true);
        
        // PENTING: Aktifkan kamera setelah cutscene selesai
        MenuManager menuMgr = FindFirstObjectByType<MenuManager>();
        if (menuMgr != null)
        {
            Debug.Log("IntroCutscene: Mengaktifkan kamera setelah cutscene");
            menuMgr.ActivateCamera();
        }
        
        if (tutorialScript != null)
        {
            tutorialScript.gameObject.SetActive(true); 
            tutorialScript.enabled = true; 
            tutorialScript.StartTutorial(); 
        }
    }

    IEnumerator RunDialog(string name, string text, Sprite sprite)
    {
        yield return dialogManager.TypeDialog(name, text, sprite);
    }

    IEnumerator WaitForInput()
    {
        yield return null; 
        
        // UPDATE: Menambahkan _manualSkipTrigger agar tombol UI bisa melanjutkan dialog
        while (dialogManager.IsTyping() || 
              (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(KeyCode.Space) && !_manualSkipTrigger))
        {
            yield return null;
        }

        _manualSkipTrigger = false; // Reset setelah input diterima
    }
}