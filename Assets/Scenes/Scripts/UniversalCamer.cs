using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage), typeof(AspectRatioFitter))]
public class UniversalCamera : MonoBehaviour
{
    // --- Pengaturan Publik ---
    public bool useFrontCamera = true;
    public int requestedWidth = 640;
    public int requestedHeight = 360;

    // --- Referensi Internal ---
    private WebCamTexture webCamTexture;
    private RawImage rawImage;
    private AspectRatioFitter fitter;
    private bool isReady = false;
    private bool shouldRun = true; // Flag to control auto-start
    private static float lastStopTime = 0f; // Track when camera was last stopped

    // --- SIKLUS HIDUP (LIFECYCLE) ---
    // Camera is now FULLY MANUAL - no auto-start on Enable
    // Scene managers must explicitly call StartCamera()

    void OnEnable()
    {
        // Do nothing - manual control only
        Debug.Log("UniversalCamera: OnEnable (manual mode)");
    }

    void OnDisable()
    {
        StopCamera();
    }

    // Handle app going to background (important for mobile)
    void OnApplicationPause(bool pauseStatus)
    {
        #if UNITY_ANDROID || UNITY_IOS
        if (pauseStatus)
        {
            // App masuk background - pause camera
            if (webCamTexture != null && webCamTexture.isPlaying)
            {
                webCamTexture.Pause();
                Debug.Log("UniversalCamera: Paused (app background)");
            }
        }
        else
        {
            // App kembali ke foreground - resume camera
            if (webCamTexture != null && !webCamTexture.isPlaying && isReady)
            {
                webCamTexture.Play();
                Debug.Log("UniversalCamera: Resumed (app foreground)");
            }
        }
        #endif
    }

    // PUBLIC: Start the camera (call this from Scene managers)
    public void StartCamera()
    {
        if (!shouldRun)
        {
            Debug.LogWarning("Camera is locked. Call AllowCameraStart() first.");
            return;
        }
        
        StartCoroutine(InitializeCamera());
    }

    public void StopCamera()
    {
        shouldRun = false; // Prevent auto-restart
        StopAllCoroutines(); // Stop any running initialization
        
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
            // PENTING: DESTROY texture untuk benar-benar melepas kamera dari sistem
            Destroy(webCamTexture);
            webCamTexture = null; 
        }
        isReady = false;
        lastStopTime = Time.time; // Record when we stopped
        Debug.Log("UniversalCamera: Stopped, destroyed, and locked.");
    }

    // Internal cleanup without locking shouldRun
    private void CleanupCamera()
    {
        StopAllCoroutines();
        
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
            Destroy(webCamTexture);
            webCamTexture = null;
        }
        isReady = false;
    }

    public void AllowCameraStart()
    {
        shouldRun = true;
        Debug.Log("UniversalCamera: Allowed to start.");
    }

    public void PauseCamera()
    {
        if (!isReady || webCamTexture == null)
        {
            Debug.LogWarning("UniversalCamera: Cannot pause - not ready or null.");
            return;
        }

        if (webCamTexture.isPlaying)
        {
            webCamTexture.Pause();
            Debug.Log("UniversalCamera: Paused (texture kept alive).");
        }
    }

    public void ResumeCamera()
    {
        if (!isReady || webCamTexture == null)
        {
            Debug.LogWarning("UniversalCamera: Cannot resume - not ready or null.");
            return;
        }

        if (!webCamTexture.isPlaying)
        {
            webCamTexture.Play();
            Debug.Log("UniversalCamera: Resumed.");
        }
    }

    public static float GetLastStopTime()
    {
        return lastStopTime;
    }

    // --- FUNGSI KONTROL TOMBOL ---

    public void TogglePauseCamera()
    {
        if (!isReady || webCamTexture == null)
        {
            Debug.LogWarning("Kamera belum siap/null.");
            return;
        }

        if (webCamTexture.isPlaying)
        {
            webCamTexture.Pause();
            Debug.Log("Kamera dijeda.");
        }
    }

    public void ToggleStartCamera()
    {
        if (!isReady || webCamTexture == null)
        {
            Debug.LogWarning("Kamera belum siap/null.");
            return;
        }

        if (!webCamTexture.isPlaying)
        {
            webCamTexture.Play();
            Debug.Log("Kamera dilanjutkan.");
        }
    }

    // ------------------------------------------

    public IEnumerator InitializeCamera()
    {
        // Pastikan bersih dulu sebelum mulai (gunakan cleanup internal)
        CleanupCamera(); 

        // 1. Request Izin (Khusus Mobile)
        #if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("Pengguna menolak izin kamera.");
                yield break;
            }
        }
        #endif

        // 2. Cari Perangkat Kamera
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("Tidak ada kamera yang ditemukan.");
            yield break;
        }

        // Cari kamera depan/belakang sesuai setting
        WebCamDevice device = devices.FirstOrDefault(d => d.isFrontFacing == useFrontCamera);
        
        // Fallback: Jika tidak ketemu (misal cari depan tapi cuma ada belakang), pakai yang pertama
        if (string.IsNullOrEmpty(device.name))
        {
            device = devices[0];
        }

        Debug.Log("Menggunakan kamera: " + device.name);

        // 3. Inisialisasi Texture
        webCamTexture = new WebCamTexture(device.name, requestedWidth, requestedHeight);
        
        rawImage = GetComponent<RawImage>();
        fitter = GetComponent<AspectRatioFitter>();

        rawImage.texture = webCamTexture;
        rawImage.material.mainTexture = webCamTexture; // Tambahan: Update material juga kadang diperlukan

        webCamTexture.Play();

        // 4. Tunggu sampai kamera benar-benar mengirim data (resolusi valid)
        // Mobile kadang butuh waktu lebih lama untuk warm-up
        #if UNITY_ANDROID || UNITY_IOS
        float timeout = 10f; // 10 detik untuk mobile
        #else
        float timeout = 5f;  // 5 detik untuk PC
        #endif
        
        while (webCamTexture.width < 100 && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (timeout <= 0)
        {
            Debug.LogError($"Timeout: Kamera gagal memulai. Platform: {Application.platform}");
            CleanupCamera();
            yield break;
        }

        Debug.Log($"Kamera siap. Resolusi: {webCamTexture.width}x{webCamTexture.height}");
        isReady = true;
    }

    void Update()
    {
        // Hentikan update jika kamera tidak siap ATAU tidak sedang 'play'
        if (!isReady || webCamTexture == null || !webCamTexture.isPlaying) 
        {
            return;
        }

        // Hanya update orientasi jika ada frame baru
        if (webCamTexture.didUpdateThisFrame)
        {
            float videoRatio = (float)webCamTexture.width / (float)webCamTexture.height;
            fitter.aspectRatio = videoRatio;

            // Handle Rotasi (Mobile seringkali terputar)
            rawImage.rectTransform.localEulerAngles = new Vector3(0, 0, -webCamTexture.videoRotationAngle);

            // Handle Mirroring (Kamera depan biasanya perlu dimirror)
            float scaleY = webCamTexture.videoVerticallyMirrored ? -1f : 1f;
            float scaleX = useFrontCamera ? -1f : 1f; 

            rawImage.rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
    }
}