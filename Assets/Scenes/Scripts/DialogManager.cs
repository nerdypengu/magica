using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogManager : MonoBehaviour
{
    [Header("Dialog Mode Components")]
    public GameObject dialogPanel; 
    public TextMeshProUGUI dialogText;
    public TextMeshProUGUI nameText;
    public Image npcImage;
    
    [Header("Narrative Mode Components")]
    public GameObject narrativePanel; 
    public TextMeshProUGUI narrativeText; 

    [Header("Shared Components")]
    public Image backgroundImage;

    [Header("Settings")]
    public float typingSpeed = 0.003f; // 0.003s per character = super fast

    private bool isTyping = false;
    private string currentFullText = "";
    private TextMeshProUGUI currentActiveTextObject; 
    private Coroutine typingCoroutine; 

    public IEnumerator TypeDialog(string name, string text, Sprite npcSprite)
    {
        isTyping = true;
        
        // Check if we're in conversation scene (NPC sprite was set externally)
        bool isConversationMode = (npcImage != null && npcImage.sprite != null && npcSprite == null);
        
        // 1. Tentukan Mode: Narasi atau Dialog?
        if (string.IsNullOrEmpty(name))
        {
            // --- MODE NARASI ---
            if (dialogPanel) dialogPanel.SetActive(false); 
            if (dialogText) dialogText.gameObject.SetActive(false); 
            if (nameText) nameText.gameObject.SetActive(false);
            
            // KEEP NPC image visible in conversation mode (don't hide it!)
            if (npcImage && !isConversationMode)
            {
                npcImage.gameObject.SetActive(false);
            }

            if (narrativePanel) narrativePanel.SetActive(true); 
            
            // --- PENGAMAN CRASH (Cek apakah Narrative Text sudah diisi?) ---
            if (narrativeText != null) 
            {
                narrativeText.gameObject.SetActive(true); 
                currentActiveTextObject = narrativeText;
                narrativeText.text = ""; // Reset text
            }
            else
            {
                Debug.LogError("STOP! LUPA DRAG: Slot 'Narrative Text' di DialogManager masih kosong (None)!");
                isTyping = false; 
                yield break; // Batalkan proses biar tidak error
            }
        }
        else
        {
            // --- MODE DIALOG ---
            if (narrativePanel) narrativePanel.SetActive(false);
            if (narrativeText) narrativeText.gameObject.SetActive(false);
            
            if (dialogPanel) dialogPanel.SetActive(true);
            
            // --- PENGAMAN CRASH (Cek Dialog Text) ---
            if (dialogText != null) 
            {
                dialogText.gameObject.SetActive(true); 
                currentActiveTextObject = dialogText;
                dialogText.text = "";
            }
            else
            {
                Debug.LogError("STOP! LUPA DRAG: Slot 'Dialog Text' di DialogManager masih kosong!");
                isTyping = false;
                yield break;
            }

            if (nameText != null) 
            {
                nameText.gameObject.SetActive(true);    
                nameText.text = name;
            }
            
            // Setup Gambar NPC (Keep visible always in dialog mode)
            if (npcImage != null)
            {
                if (npcSprite != null)
                {
                    npcImage.sprite = npcSprite;
                }
                // Always show NPC image in dialog mode (sprite might be set from conversation start)
                npcImage.gameObject.SetActive(true);
                
                // Ensure full opacity
                npcImage.color = new Color(1, 1, 1, 1);
            }
        }

        currentFullText = text;

        // 2. Mulai Mengetik
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypingRoutine(text));

        while (isTyping)
        {
            yield return null;
        }
    }

    IEnumerator TypingRoutine(string text)
    {
        currentActiveTextObject.text = ""; 
        
        foreach (char letter in text.ToCharArray())
        {
            currentActiveTextObject.text += letter;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    public void FinishTyping()
    {
        if (isTyping)
        {
            // Hentikan animasi
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            
            // Tampilkan teks penuh
            if (currentActiveTextObject != null)
            {
                currentActiveTextObject.text = currentFullText;
            }
            
            // Ubah status jadi false -> Ini akan memutus loop 'while(isTyping)' di atas
            isTyping = false;
        }
    }

    public void SetBackground(Sprite bgSprite)
    {
        if (bgSprite != null && backgroundImage != null)
        {
            backgroundImage.sprite = bgSprite;
            backgroundImage.color = Color.white;
            backgroundImage.gameObject.SetActive(true); // Pastikan aktif saat di-set
        }
    }

    public bool IsTyping()
    {
        return isTyping;
    }

    public void ClearBackground()
    {
        if (backgroundImage != null)
        {
            // Ubah alpha jadi 0 atau matikan objectnya
            backgroundImage.gameObject.SetActive(false); 
        }
    }

    public void HideDialog()
    {
        if (dialogPanel) dialogPanel.SetActive(false);
        if (dialogText) dialogText.gameObject.SetActive(false);
        if (nameText) nameText.gameObject.SetActive(false);
        if (npcImage) npcImage.gameObject.SetActive(false);
        
        if (narrativePanel) narrativePanel.SetActive(false);
        if (narrativeText) narrativeText.gameObject.SetActive(false);
        if (backgroundImage != null) backgroundImage.gameObject.SetActive(false); 
    }
}