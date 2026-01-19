using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed = 5;
    public Rigidbody2D rb;
    public Animator anim;

    [Header("AI Connection")]
    public YoloDetector yoloDetector;

    void FixedUpdate()
    {
        // 1. Default to Keyboard Input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 2. Override with YOLO Input (if a gesture is detected)
        if (yoloDetector != null && !string.IsNullOrEmpty(yoloDetector.currentDetectedWord))
        {
            string command = yoloDetector.currentDetectedWord.Trim().ToLower();

            if (command == "kanan")
            {
                horizontal = 1f;
            }
            else if (command == "kiri")
            {
                horizontal = -1f;
            }
            else if (command == "atas")
            {
                vertical = 1f;
            }
            else if (command == "bawah")
            {
                vertical = -1f;
            }
        }

        if (yoloDetector != null)
        Debug.Log($"YOLO sends: '{yoloDetector.currentDetectedWord}' | Player Horizontal: {horizontal}");

        anim.SetFloat("horizontal", horizontal);
        anim.SetFloat("vertical", vertical);

        rb.linearVelocity = new Vector2(horizontal, vertical).normalized * speed;
    }
}