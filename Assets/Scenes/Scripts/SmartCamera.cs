using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SmartCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform player; 
    
    [Header("Follow Mode Settings")]
    public float smoothSpeed = 5f;
    public Vector3 followOffset = new Vector3(0, 0, -10); 

    [Header("Zone Mode Settings")]
    [Tooltip("Enable untuk menggunakan CameraZone system (auto-transition saat player out of bounds)")]
    public bool useZoneSystem = false;
    public CameraZone currentZone;
    
    [Header("Transition Effect")]
    [Tooltip("Smooth transition atau instant snap dengan fade")]
    public bool useSmoothTransition = false;
    public float zoneTransitionSpeed = 3f;
    public float fadeDuration = 0.5f;
    public CanvasGroup fadeCanvasGroup;
    
    [Header("Disable During Transition")]
    [Tooltip("Disable YOLO detector dan camera rendering saat transisi")]
    public bool disableDuringTransition = true;
    private Camera mainCam;
    private YoloDetector yoloDetector;
    private YoloAlphabet yoloAlphabet;
    
    public bool isFollowing = true; 
    private bool isTransitioning = false;
    private Vector3 targetZonePosition;
    private bool isInitialized = false;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }
        
        // Get main camera component
        mainCam = GetComponent<Camera>();
        
        // Find YOLO detectors
        yoloDetector = FindFirstObjectByType<YoloDetector>();
        yoloAlphabet = FindFirstObjectByType<YoloAlphabet>();
        
        // Auto-initialize jika menggunakan zone system tapi belum initialized
        if (useZoneSystem && currentZone != null)
        {
            // Tunggu 1 frame agar player position sudah benar
            StartCoroutine(DelayedInitialization());
        }
        else if (!useZoneSystem)
        {
            // Jika tidak pakai zone system, langsung set initialized = true
            isInitialized = true;
        }
    }
    
    IEnumerator DelayedInitialization()
    {
        yield return new WaitForEndOfFrame();
        
        if (!isInitialized)
        {
            InitializeToPlayerZone();
        }
    }
    
    public void InitializeToPlayerZone()
    {
        if (player == null)
        {
            Debug.LogError("[SmartCamera] Cannot initialize - player is NULL!");
            return;
        }
        
        // Don't reset isInitialized if already true - hanya set false saat awal saja
        bool wasInitialized = isInitialized;
        
        if (!wasInitialized)
        {
            isInitialized = false;
        }
        
        CameraZone[] allZones = FindObjectsByType<CameraZone>(FindObjectsSortMode.None);
        
        CameraZone playerZone = null;
        foreach (CameraZone zone in allZones)
        {
            if (zone.IsPlayerInside(player.position))
            {
                playerZone = zone;
                break;
            }
        }
        
        if (playerZone != null && playerZone.cameraMount != null)
        {
            currentZone = playerZone;
            
            // Hanya snap position jika belum pernah initialized
            if (!wasInitialized)
            {
                transform.position = new Vector3(
                    playerZone.cameraMount.position.x, 
                    playerZone.cameraMount.position.y, 
                    followOffset.z
                );
            }
            
            isFollowing = false;
        }
        else
        {
            if (currentZone != null && currentZone.cameraMount != null)
            {
                // Hanya snap position jika belum pernah initialized
                if (!wasInitialized)
                {
                    transform.position = new Vector3(
                        currentZone.cameraMount.position.x, 
                        currentZone.cameraMount.position.y, 
                        followOffset.z
                    );
                }
                
                isFollowing = false;
            }
            else
            {
                isFollowing = true;
                Debug.LogWarning("[SmartCamera] No zone found for player, using follow mode");
            }
        }
        
        isInitialized = true;
    }

    void LateUpdate()
    {
        if (!isInitialized) return;
        
        if (useZoneSystem && currentZone != null)
        {
            HandleZoneSystem();
        }
        else if (isFollowing && player != null)
        {
            Vector3 targetPos = player.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
        }
    }
    
    void HandleZoneSystem()
    {
        if (player == null || isTransitioning) return;
        
        if (!currentZone.IsPlayerInside(player.position))
        {
            CameraZone nextZone = currentZone.GetNextZone(player.position);
            
            if (nextZone != null && nextZone.cameraMount != null)
            {
                targetZonePosition = new Vector3(
                    nextZone.cameraMount.position.x,
                    nextZone.cameraMount.position.y,
                    followOffset.z
                );
                
                // INSTANT TRANSITION dengan fade
                if (!useSmoothTransition)
                {
                    StartCoroutine(InstantTransitionWithFade(nextZone));
                }
                // SMOOTH TRANSITION - juga pakai fade
                else
                {
                    StartCoroutine(SmoothTransitionWithFade(nextZone));
                }
            }
        }
        
        // Stay at current zone position (tidak transitioning)
        if (!isTransitioning && currentZone != null && currentZone.cameraMount != null)
        {
            Vector3 zonePos = new Vector3(
                currentZone.cameraMount.position.x,
                currentZone.cameraMount.position.y,
                followOffset.z
            );
            transform.position = zonePos;
        }
    }
    
    void DisableCameraAndYOLO()
    {
        if (!disableDuringTransition) return;
        
        if (mainCam != null)
        {
            mainCam.enabled = false;
        }
        
        if (yoloDetector != null)
        {
            yoloDetector.enabled = false;
        }
        
        if (yoloAlphabet != null)
        {
            yoloAlphabet.enabled = false;
        }
    }
    
    void EnableCameraAndYOLO()
    {
        if (!disableDuringTransition) return;
        
        if (mainCam != null)
        {
            mainCam.enabled = true;
        }
        
        if (yoloDetector != null)
        {
            yoloDetector.enabled = true;
        }
        
        if (yoloAlphabet != null)
        {
            yoloAlphabet.enabled = true;
        }
    }
    
    IEnumerator InstantTransitionWithFade(CameraZone nextZone)
    {
        isTransitioning = true;
        
        // Disable camera dan YOLO sebelum transisi
        DisableCameraAndYOLO();
        
        if (fadeCanvasGroup != null)
        {
            // Pastikan parent hierarchy juga aktif
            Transform parent = fadeCanvasGroup.transform;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 0;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
        else
        {
            Debug.LogError("[SmartCamera] CRITICAL: FadeCanvasGroup is NULL! Assign it in Inspector!");
        }
        
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeToBlack());
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }
        
        currentZone = nextZone;
        transform.position = targetZonePosition;
        
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeFromBlack());
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }
        
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(false);
        }
        
        // Re-enable camera dan YOLO setelah transisi selesai
        EnableCameraAndYOLO();
        
        isTransitioning = false;
    }
    
    IEnumerator SmoothTransitionWithFade(CameraZone nextZone)
    {
        isTransitioning = true;
        
        // Disable camera dan YOLO sebelum transisi
        DisableCameraAndYOLO();
        
        if (fadeCanvasGroup != null)
        {
            // Pastikan parent hierarchy juga aktif
            Transform parent = fadeCanvasGroup.transform;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                }
                parent = parent.parent;
            }
            
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.alpha = 0;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
        
        // Fade to black
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeToBlack());
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }
        
        currentZone = nextZone;
        
        // Smooth lerp ke target position selama fade black
        float lerpProgress = 0f;
        Vector3 startPos = transform.position;
        
        while (lerpProgress < 1f)
        {
            lerpProgress += Time.deltaTime * zoneTransitionSpeed;
            transform.position = Vector3.Lerp(startPos, targetZonePosition, lerpProgress);
            yield return null;
        }
        
        transform.position = targetZonePosition;
        
        // Fade from black
        if (fadeCanvasGroup != null)
        {
            yield return StartCoroutine(FadeFromBlack());
            fadeCanvasGroup.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }
        
        // Re-enable camera dan YOLO setelah transisi selesai
        EnableCameraAndYOLO();
        
        isTransitioning = false;
    }
    
    IEnumerator FadeToBlack()
    {
        float elapsed = 0f;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        
        fadeCanvasGroup.alpha = 1f;
    }
    
    IEnumerator FadeFromBlack()
    {
        float elapsed = 0f;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        
        fadeCanvasGroup.alpha = 0f;
    }

    public void SnapToTarget()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player != null)
        {
            transform.position = player.position + followOffset;
            isFollowing = true; 
        }
    }

    public void SetFixedPosition(Vector3 newPosition)
    {
        isFollowing = false;
        transform.position = new Vector3(newPosition.x, newPosition.y, followOffset.z);
    }

    public void SetFollowMode()
    {
        isFollowing = true;
        SnapToTarget();
    }
    
    public void SetZone(CameraZone zone)
    {
        if (zone == null || !useZoneSystem) return;
        
        currentZone = zone;
        
        if (zone.cameraMount != null)
        {
            targetZonePosition = new Vector3(
                zone.cameraMount.position.x,
                zone.cameraMount.position.y,
                followOffset.z
            );
            transform.position = targetZonePosition;
        }
        
        Debug.Log($"[SmartCamera] Set to zone: {zone.zoneName}");
    }
}
