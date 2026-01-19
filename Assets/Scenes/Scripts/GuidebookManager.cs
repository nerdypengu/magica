using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class GuidebookManager : MonoBehaviour
{
    [System.Serializable]
    public class SignData
    {
        public string name;
        [Tooltip("For single image signs, add 1 sprite. For animated signs (like J), add multiple sprites")]
        public Sprite[] sprites;
        [TextArea(2, 4)]
        public string description;
        
        [Tooltip("Animation speed in seconds (0 = no animation)")]
        public float animationSpeed = 0.5f;
        
        public bool IsAnimated => sprites != null && sprites.Length > 1;
    }
    
    [Header("Section Panels")]
    public GameObject profileSection;
    public GameObject alphabetSection;
    public GameObject controllerSection;
    public GameObject conversationSection;
    
    [Header("Section Buttons")]
    public Button profileButton;
    public Button alphabetButton;
    public Button controllerButton;
    public Button conversationButton;
    
    [Header("Profile UI")]
    public TextMeshProUGUI profileLevelText;
    public TextMeshProUGUI profileExpText;
    public Slider profileExpBar;
    
    [Header("Alphabet Content (All 26 Letters)")]
    [Tooltip("Configure each letter. For J, add 3 sprites for animation. For others, just 1 sprite.")]
    public SignData[] alphabetSigns = new SignData[26];
    
    [Header("Alphabet Display (8 slots)")]
    public Image[] alphabetDisplayImages = new Image[8];
    public TextMeshProUGUI[] alphabetDisplayTexts = new TextMeshProUGUI[8];
    
    [Header("Alphabet Navigation")]
    public Button alphabetNextButton;
    public Button alphabetBackButton;
    public TextMeshProUGUI alphabetPageText;
    
    private int currentAlphabetPage = 0;
    private const int ALPHABET_PER_PAGE = 8;
    
    [Header("Controller Content (4 Controls)")]
    [Tooltip("For Kanan/Kiri, add 2 sprites for animation. For others, just 1 sprite.")]
    public SignData[] controllerSigns = new SignData[4];
    
    [Header("Controller Display")]
    public Image[] controllerDisplayImages = new Image[4];
    public TextMeshProUGUI[] controllerDisplayTexts = new TextMeshProUGUI[4];
    
    [Header("Conversation Content (5 Signs)")]
    public SignData[] conversationSigns = new SignData[5];
    
    [Header("Conversation Display")]
    public Image[] conversationDisplayImages = new Image[5];
    public TextMeshProUGUI[] conversationDisplayTexts = new TextMeshProUGUI[5];
    
    private Dictionary<Image, Coroutine> activeAnimations = new Dictionary<Image, Coroutine>();
    
    void Start()
    {
        // Setup alphabet navigation buttons
        if (alphabetNextButton != null)
            alphabetNextButton.onClick.AddListener(NextAlphabetPage);
        
        if (alphabetBackButton != null)
            alphabetBackButton.onClick.AddListener(PreviousAlphabetPage);
        
        // Default: Show Profile section
        ShowSection("Profile");
    }
    
    void OnEnable()
    {
        // Refresh profile when guidebook is opened
        if (profileSection != null && profileSection.activeSelf)
        {
            UpdateProfileSection();
        }
    }
    
    void OnDisable()
    {
        // Stop all animations when guidebook closes
        StopAllAnimations();
    }
    
    // ========================================================================
    // SECTION MANAGEMENT
    // ========================================================================
    
    public void ShowSection(string sectionName)
    {
        // Hide all sections first
        if (profileSection != null) profileSection.SetActive(false);
        if (alphabetSection != null) alphabetSection.SetActive(false);
        if (controllerSection != null) controllerSection.SetActive(false);
        if (conversationSection != null) conversationSection.SetActive(false);
        
        // Show requested section and select its button
        switch (sectionName.ToLower())
        {
            case "profile":
                if (profileSection != null)
                {
                    profileSection.SetActive(true);
                    UpdateProfileSection();
                }
                if (profileButton != null) profileButton.Select();
                break;
                
            case "alphabet":
                if (alphabetSection != null)
                {
                    alphabetSection.SetActive(true);
                    currentAlphabetPage = 0; // Reset to first page
                    UpdateAlphabetDisplay();
                }
                if (alphabetButton != null) alphabetButton.Select();
                break;
                
            case "controller":
                if (controllerSection != null)
                {
                    controllerSection.SetActive(true);
                    UpdateControllerDisplay();
                }
                if (controllerButton != null) controllerButton.Select();
                break;
                
            case "conversation":
                if (conversationSection != null)
                {
                    conversationSection.SetActive(true);
                    UpdateConversationDisplay();
                }
                if (conversationButton != null) conversationButton.Select();
                break;
        }
        
        Debug.Log($"[Guidebook] Showing section: {sectionName}");
    }
    
    // Public methods to call from UI buttons
    public void ShowProfile() => ShowSection("Profile");
    public void ShowAlphabet() => ShowSection("Alphabet");
    public void ShowController() => ShowSection("Controller");
    public void ShowConversation() => ShowSection("Conversation");
    
    // ========================================================================
    // PROFILE SECTION
    // ========================================================================
    
    void UpdateProfileSection()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning("[Guidebook] PlayerStats not found!");
            return;
        }
        
        int level = PlayerStats.Instance.GetCurrentLevel();
        int currentExp = PlayerStats.Instance.GetCurrentExp();
        int expNeeded = PlayerStats.Instance.GetExpToNextLevel();
        float expProgress = PlayerStats.Instance.GetExpProgress();
        
        // Update UI
        if (profileLevelText != null)
            profileLevelText.text = $"{level}";
        
        if (profileExpText != null)
            profileExpText.text = $"{currentExp} / {expNeeded} EXP";
        
        if (profileExpBar != null)
            profileExpBar.value = expProgress;
    }
    
    // ========================================================================
    // ALPHABET SECTION (Paginated)
    // ========================================================================
    
    void UpdateAlphabetDisplay()
    {
        // Stop previous page animations
        StopAllAnimations();
        
        int startIndex = currentAlphabetPage * ALPHABET_PER_PAGE;
        int totalPages = Mathf.CeilToInt((float)alphabetSigns.Length / ALPHABET_PER_PAGE);
        
        for (int i = 0; i < ALPHABET_PER_PAGE; i++)
        {
            int dataIndex = startIndex + i;
            
            // Check if this slot should show data
            if (dataIndex < alphabetSigns.Length && alphabetSigns[dataIndex] != null)
            {
                SignData sign = alphabetSigns[dataIndex];
                
                // Show image
                if (alphabetDisplayImages[i] != null)
                {
                    alphabetDisplayImages[i].gameObject.SetActive(true);
                    
                    // Start animation if multiple sprites, otherwise show single sprite
                    if (sign.IsAnimated)
                    {
                        StartSignAnimation(alphabetDisplayImages[i], sign);
                    }
                    else if (sign.sprites != null && sign.sprites.Length > 0)
                    {
                        alphabetDisplayImages[i].sprite = sign.sprites[0];
                    }
                }
                
                // Show text
                if (alphabetDisplayTexts[i] != null)
                {
                    alphabetDisplayTexts[i].gameObject.SetActive(true);
                    
                    // Auto-generate label if description is empty
                    string description = sign.description;
                    if (string.IsNullOrEmpty(description))
                    {
                        if (!string.IsNullOrEmpty(sign.name))
                            description = sign.name;
                        else
                        {
                            char letter = (char)('A' + dataIndex);
                            description = $"{letter}";
                        }
                    }
                    
                    alphabetDisplayTexts[i].text = description;
                }
            }
            else
            {
                // Hide empty slots
                if (alphabetDisplayImages[i] != null)
                    alphabetDisplayImages[i].gameObject.SetActive(false);
                
                if (alphabetDisplayTexts[i] != null)
                    alphabetDisplayTexts[i].gameObject.SetActive(false);
            }
        }
        
        // Update navigation buttons
        if (alphabetBackButton != null)
            alphabetBackButton.interactable = currentAlphabetPage > 0;
        
        if (alphabetNextButton != null)
            alphabetNextButton.interactable = currentAlphabetPage < totalPages - 1;
        
        // Update page indicator
        if (alphabetPageText != null)
            alphabetPageText.text = $"Page {currentAlphabetPage + 1} / {totalPages}";
    }
    
    void NextAlphabetPage()
    {
        int totalPages = Mathf.CeilToInt((float)alphabetSigns.Length / ALPHABET_PER_PAGE);
        if (currentAlphabetPage < totalPages - 1)
        {
            currentAlphabetPage++;
            UpdateAlphabetDisplay();
        }
    }
    
    void PreviousAlphabetPage()
    {
        if (currentAlphabetPage > 0)
        {
            currentAlphabetPage--;
            UpdateAlphabetDisplay();
        }
    }
    
    // ========================================================================
    // CONTROLLER SECTION (Static)
    // ========================================================================
    
    void UpdateControllerDisplay()
    {
        StopAllAnimations();
        
        for (int i = 0; i < controllerDisplayImages.Length && i < controllerSigns.Length; i++)
        {
            if (controllerSigns[i] == null) continue;
            
            SignData sign = controllerSigns[i];
            
            if (controllerDisplayImages[i] != null)
            {
                controllerDisplayImages[i].gameObject.SetActive(true);
                
                if (sign.IsAnimated)
                {
                    StartSignAnimation(controllerDisplayImages[i], sign);
                }
                else if (sign.sprites != null && sign.sprites.Length > 0)
                {
                    controllerDisplayImages[i].sprite = sign.sprites[0];
                }
            }
            
            if (controllerDisplayTexts[i] != null)
            {
                // Use description, or fallback to name if description is empty
                string displayText = !string.IsNullOrEmpty(sign.description) ? sign.description : sign.name;
                controllerDisplayTexts[i].text = displayText;
                controllerDisplayTexts[i].gameObject.SetActive(true);
            }
        }
    }
    
    // ========================================================================
    // CONVERSATION SECTION (Static)
    // ========================================================================
    
    void UpdateConversationDisplay()
    {
        StopAllAnimations();
        
        for (int i = 0; i < conversationDisplayImages.Length && i < conversationSigns.Length; i++)
        {
            if (conversationSigns[i] == null) continue;
            
            SignData sign = conversationSigns[i];
            
            if (conversationDisplayImages[i] != null)
            {
                conversationDisplayImages[i].gameObject.SetActive(true);
                
                if (sign.IsAnimated)
                {
                    StartSignAnimation(conversationDisplayImages[i], sign);
                }
                else if (sign.sprites != null && sign.sprites.Length > 0)
                {
                    conversationDisplayImages[i].sprite = sign.sprites[0];
                }
            }
            
            if (conversationDisplayTexts[i] != null)
            {
                // Use description, or fallback to name if description is empty
                string displayText = !string.IsNullOrEmpty(sign.description) ? sign.description : sign.name;
                conversationDisplayTexts[i].text = displayText;
                conversationDisplayTexts[i].gameObject.SetActive(true);
            }
        }
    }
    
    // ========================================================================
    // ANIMATION SYSTEM
    // ========================================================================
    
    void StartSignAnimation(Image targetImage, SignData sign)
    {
        if (targetImage == null || sign == null || !sign.IsAnimated) return;
        
        // Stop existing animation on this image if any
        StopSignAnimation(targetImage);
        
        // Start new animation
        Coroutine anim = StartCoroutine(AnimateSign(targetImage, sign));
        activeAnimations[targetImage] = anim;
    }
    
    void StopSignAnimation(Image targetImage)
    {
        if (targetImage != null && activeAnimations.ContainsKey(targetImage))
        {
            if (activeAnimations[targetImage] != null)
                StopCoroutine(activeAnimations[targetImage]);
            
            activeAnimations.Remove(targetImage);
        }
    }
    
    void StopAllAnimations()
    {
        foreach (var anim in activeAnimations.Values)
        {
            if (anim != null)
                StopCoroutine(anim);
        }
        activeAnimations.Clear();
    }
    
    IEnumerator AnimateSign(Image targetImage, SignData sign)
    {
        int currentFrame = 0;
        
        while (true)
        {
            if (targetImage == null || sign.sprites == null || sign.sprites.Length == 0)
                yield break;
            
            // Update sprite
            targetImage.sprite = sign.sprites[currentFrame];
            
            // Wait for next frame
            yield return new WaitForSeconds(sign.animationSpeed);
            
            // Loop to next frame
            currentFrame = (currentFrame + 1) % sign.sprites.Length;
        }
    }
}
