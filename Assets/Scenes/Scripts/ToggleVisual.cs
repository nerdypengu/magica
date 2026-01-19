using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Toggle))]
public class ToggleSwitchVisual : MonoBehaviour
{
    [Header("References")]
    public Image backgroundImage;   // Capsule Background
    public RectTransform handle;    // Bola/Knob

    [Header("Settings")]
    public Color offColor = Color.gray;
    public Color onColor = new Color(0.2f, 0.8f, 0.2f); // Hijau cerah
    public float transitionSpeed = 10f;

    [Header("Handle Positions")]
    public float handleOffX = -30f; // Posisi X saat mati (kiri)
    public float handleOnX = 30f;   // Posisi X saat nyala (kanan)

    private Toggle toggle;
    private Coroutine animateRoutine;

    void Awake()
    {
        toggle = GetComponent<Toggle>();
        
        // Matikan transisi bawaan Unity biar tidak bentrok
        toggle.transition = Selectable.Transition.None; 
        
        // Listen saat nilai berubah
        toggle.onValueChanged.AddListener(OnToggleValueChanged);
        
        // Set tampilan awal tanpa animasi (instant)
        UpdateVisual(toggle.isOn, true);
    }

    void OnToggleValueChanged(bool isOn)
    {
        // Jalankan animasi
        UpdateVisual(isOn, false);
    }

    void UpdateVisual(bool isOn, bool instant)
    {
        if (animateRoutine != null) StopCoroutine(animateRoutine);

        Color targetColor = isOn ? onColor : offColor;
        Vector2 targetPos = new Vector2(isOn ? handleOnX : handleOffX, 0f);

        if (instant)
        {
            if (backgroundImage) backgroundImage.color = targetColor;
            if (handle) handle.anchoredPosition = targetPos;
        }
        else
        {
            animateRoutine = StartCoroutine(AnimateSwitch(targetColor, targetPos));
        }
    }

    IEnumerator AnimateSwitch(Color targetColor, Vector2 targetPos)
    {
        while (Vector2.Distance(handle.anchoredPosition, targetPos) > 0.1f)
        {
            // Lerp Warna
            if (backgroundImage)
            {
                backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, transitionSpeed * Time.deltaTime);
            }

            // Lerp Posisi Handle
            if (handle)
            {
                handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, targetPos, transitionSpeed * Time.deltaTime);
            }

            yield return null;
        }

        // Snap di akhir biar presisi
        if (backgroundImage) backgroundImage.color = targetColor;
        if (handle) handle.anchoredPosition = targetPos;
    }
}