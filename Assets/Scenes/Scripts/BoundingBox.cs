using UnityEngine;
using UnityEngine.UI;
using TMPro; // Pastikan pakai TextMeshPro

public class BoundingBox : MonoBehaviour
{
    public Image borderImage;
    public TextMeshProUGUI labelText;

    // Fungsi untuk mengatur tampilan kotak
    public void SetBox(Rect rect, string label, float confidence, Color color)
    {
        // Atur posisi dan ukuran kotak
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchoredPosition = rect.position;
        rt.sizeDelta = rect.size;

        // Atur teks dan warna
        labelText.text = $"{label} ({confidence:P0})"; // Format persen (e.g. 90%)
        borderImage.color = color;
        labelText.color = color;
        
        // Pastikan aktif
        gameObject.SetActive(true);
    }
}