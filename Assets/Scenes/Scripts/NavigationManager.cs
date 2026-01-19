using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NavigationManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject navigationPanel;
    public RectTransform arrowRect;
    public Image arrowImage;
    
    [Header("Navigation Settings")]
    public Transform playerTransform;
    public Camera mainCamera;
    public float arrivalDistance = 1.5f; // Jarak untuk dianggap "sampai"
    public float edgeOffset = 50f; // Offset dari edge screen
    
    [Header("Current Navigation")]
    public List<Transform> waypointPath = new List<Transform>(); // Path ke tujuan
    private int currentWaypointIndex = 0;
    private bool isNavigating = false;
    
    private const string SAVE_KEY_WAYPOINT_INDEX = "NavigationWaypointIndex";
    private const string SAVE_KEY_IS_NAVIGATING = "NavigationIsActive";
    
    private bool isSystemReady = false;
    
    private Canvas canvas;
    private RectTransform canvasRect;
    
    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        // Try to find canvas from parent first
        canvas = GetComponentInParent<Canvas>();
        
        // If not found in parent, try to find in scene
        if (canvas == null)
        {
            canvas = FindFirstObjectByType<Canvas>();
        }
        
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogError("[Navigation] No Canvas found in scene!");
        }
        
        // Sembunyikan panel dulu
        if (navigationPanel != null)
        {
            navigationPanel.SetActive(false);
        }
        
        // Cek SEKALI saja: apakah UI tutorial sudah pernah selesai?
        bool uiTutorialDone = PlayerPrefs.GetInt("UITutorialDone", 0) == 1;
        
        if (uiTutorialDone)
        {
            EnableNavigationSystem();
        }
    }
    
    // Method ini dipanggil dari UITutorialManager saat tutorial selesai
    public void EnableNavigationSystem()
    {
        if (isSystemReady) return; // Sudah enabled
        
        isSystemReady = true;
        
        // Load saved navigation progress (jika ada)
        bool hadSavedProgress = LoadNavigationProgress();
        
        // Auto-start HANYA jika belum ada saved progress dan waypoint sudah di-set di Inspector
        if (!hadSavedProgress && waypointPath != null && waypointPath.Count > 0)
        {
            isNavigating = true;
            currentWaypointIndex = 0;
            
            if (navigationPanel != null)
            {
                navigationPanel.SetActive(true);
            }
            
            Debug.Log($"[Navigation] Auto-started with {waypointPath.Count} waypoints from Inspector");
        }
        else if (hadSavedProgress && isNavigating)
        {
            // Ada saved progress, aktifkan panel
            if (navigationPanel != null)
            {
                navigationPanel.SetActive(true);
            }
            
            Debug.Log($"[Navigation] Resumed from saved progress - Waypoint {currentWaypointIndex}/{waypointPath.Count}");
        }
    }
    
    // Debug method - cek status system
    public bool IsSystemReady()
    {
        return isSystemReady;
    }
    
    void Update()
    {
        // System belum ready (tutorial belum selesai)
        if (!isSystemReady) return;
        
        if (!isNavigating || waypointPath.Count == 0) return;
        
        // Validation - pastikan reference ada
        if (playerTransform == null)
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (playerTransform == null)
            {
                Debug.LogError("[Navigation] Player Transform is NULL! Assign it in Inspector or tag player as 'Player'");
                return;
            }
        }
        
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Navigation] Main Camera is NULL!");
                return;
            }
        }
        
        if (arrowRect == null || arrowImage == null)
        {
            Debug.LogError("[Navigation] Arrow UI references are NULL! Assign in Inspector.");
            return;
        }
        
        if (currentWaypointIndex >= waypointPath.Count)
        {
            // Sudah sampai tujuan
            StopNavigation();
            return;
        }
        
        Transform targetWaypoint = waypointPath[currentWaypointIndex];
        if (targetWaypoint == null)
        {
            Debug.LogWarning("[Navigation] Target waypoint is null!");
            StopNavigation();
            return;
        }
        
        // Update arrow position and rotation
        UpdateArrowPositionAndRotation(targetWaypoint.position);
        
        // Check if arrived at current waypoint
        float distance = Vector2.Distance(playerTransform.position, targetWaypoint.position);
        if (distance <= arrivalDistance)
        {
            // Pindah ke waypoint berikutnya
            currentWaypointIndex++;
            SaveNavigationProgress();
            
            if (currentWaypointIndex >= waypointPath.Count)
            {
                // Sudah sampai tujuan akhir
                StopNavigation();
            }
        }
    }
    
    void UpdateArrowPositionAndRotation(Vector3 targetWorldPosition)
    {
        if (arrowRect == null || mainCamera == null || canvasRect == null)
        {
            Debug.LogError($"[Navigation] Missing refs - arrowRect: {arrowRect != null}, mainCamera: {mainCamera != null}, canvasRect: {canvasRect != null}");
            return;
        }
        
        // Convert target world position to screen position
        Vector3 targetScreenPos = mainCamera.WorldToScreenPoint(targetWorldPosition);
        
        // Convert screen position to canvas position
        Vector2 targetCanvasPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, 
            targetScreenPos, 
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCamera, 
            out targetCanvasPos
        );
        
        // Get canvas bounds
        Vector2 canvasSize = canvasRect.sizeDelta;
        float halfWidth = canvasSize.x / 2f;
        float halfHeight = canvasSize.y / 2f;
        
        // Hitung direction dari center screen ke target
        Vector2 direction = targetCanvasPos.normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Check if target is on screen
        bool isOnScreen = targetScreenPos.z > 0 && 
                         targetScreenPos.x > 0 && targetScreenPos.x < Screen.width &&
                         targetScreenPos.y > 0 && targetScreenPos.y < Screen.height;
        
        Vector2 arrowPosition;
        
        if (isOnScreen)
        {
            // Target visible: arrow langsung ke posisi target
            arrowPosition = targetCanvasPos;
        }
        else
        {
            // Target di luar screen: clamp arrow ke edge
            arrowPosition = ClampToScreenEdge(targetCanvasPos, halfWidth, halfHeight);
        }
        
        // Set arrow position and rotation
        arrowRect.anchoredPosition = arrowPosition;
        arrowRect.localRotation = Quaternion.Euler(0, 0, angle);
    }
    
    Vector2 ClampToScreenEdge(Vector2 targetPos, float halfWidth, float halfHeight)
    {
        // Clamp position to screen edge with offset
        float clampedX = Mathf.Clamp(targetPos.x, -halfWidth + edgeOffset, halfWidth - edgeOffset);
        float clampedY = Mathf.Clamp(targetPos.y, -halfHeight + edgeOffset, halfHeight - edgeOffset);
        
        // Jika target di luar, pastikan arrow nempel di edge yang paling dekat
        if (Mathf.Abs(targetPos.x) > halfWidth || Mathf.Abs(targetPos.y) > halfHeight)
        {
            // Hitung slope untuk nempel di edge yang tepat
            float slope = targetPos.y / targetPos.x;
            
            if (Mathf.Abs(targetPos.x) / halfWidth > Mathf.Abs(targetPos.y) / halfHeight)
            {
                // Nempel di left/right edge
                clampedX = targetPos.x > 0 ? halfWidth - edgeOffset : -halfWidth + edgeOffset;
                clampedY = Mathf.Clamp(slope * clampedX, -halfHeight + edgeOffset, halfHeight - edgeOffset);
            }
            else
            {
                // Nempel di top/bottom edge
                clampedY = targetPos.y > 0 ? halfHeight - edgeOffset : -halfHeight + edgeOffset;
                clampedX = Mathf.Clamp(clampedY / slope, -halfWidth + edgeOffset, halfWidth - edgeOffset);
            }
        }
        
        return new Vector2(clampedX, clampedY);
    }
    
    // Mulai navigasi dengan list waypoints manual
    public void StartNavigation(List<Transform> waypoints)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            Debug.LogWarning("[Navigation] No waypoints provided!");
            return;
        }
        
        // Check if system is ready
        if (!isSystemReady)
        {
            Debug.LogWarning("[Navigation] System not ready yet. UI Tutorial must be completed first.");
            return;
        }
        
        waypointPath = new List<Transform>(waypoints);
        currentWaypointIndex = 0;
        isNavigating = true;
        
        SaveNavigationProgress();
        
        if (navigationPanel != null)
        {
            navigationPanel.SetActive(true);
        }
        
        Debug.Log($"[Navigation] Started navigation with {waypointPath.Count} waypoints");
    }
    
    // Mulai navigasi ke single target
    public void StartNavigation(Transform targetNode)
    {
        if (targetNode == null)
        {
            Debug.LogWarning("[Navigation] Target node is null!");
            return;
        }
        
        List<Transform> singlePath = new List<Transform> { targetNode };
        StartNavigation(singlePath);
    }
    
    // Stop navigasi
    public void StopNavigation()
    {
        isNavigating = false;
        waypointPath.Clear();
        currentWaypointIndex = 0;
        
        ClearNavigationProgress();
        
        if (navigationPanel != null)
        {
            navigationPanel.SetActive(false);
        }
        
        Debug.Log("[Navigation] Navigation stopped");
    }
    
    // Check apakah sedang navigasi
    public bool IsNavigating()
    {
        return isNavigating;
    }
    
    // Get target saat ini
    public Transform GetCurrentTarget()
    {
        if (!isNavigating || currentWaypointIndex >= waypointPath.Count)
        {
            return null;
        }
        
        return waypointPath[currentWaypointIndex];
    }
    
    // Get jarak ke target saat ini
    public float GetDistanceToCurrentTarget()
    {
        Transform target = GetCurrentTarget();
        if (target == null || playerTransform == null)
        {
            return -1f;
        }
        
        return Vector2.Distance(playerTransform.position, target.position);
    }
    
    // Save navigation progress
    void SaveNavigationProgress()
    {
        PlayerPrefs.SetInt(SAVE_KEY_WAYPOINT_INDEX, currentWaypointIndex);
        PlayerPrefs.SetInt(SAVE_KEY_IS_NAVIGATING, isNavigating ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"[Navigation] Progress saved - Index: {currentWaypointIndex}/{waypointPath.Count}");
    }
    
    // Load navigation progress
    bool LoadNavigationProgress()
    {
        bool wasNavigating = PlayerPrefs.GetInt(SAVE_KEY_IS_NAVIGATING, 0) == 1;
        if (wasNavigating)
        {
            currentWaypointIndex = PlayerPrefs.GetInt(SAVE_KEY_WAYPOINT_INDEX, 0);
            isNavigating = true; // IMPORTANT: Restore navigating state
            
            Debug.Log($"[Navigation] Loaded progress - Index: {currentWaypointIndex}, Navigating: {isNavigating}");
            return true;
        }
        
        return false;
    }
    
    // Clear saved navigation progress
    void ClearNavigationProgress()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY_WAYPOINT_INDEX);
        PlayerPrefs.DeleteKey(SAVE_KEY_IS_NAVIGATING);
        PlayerPrefs.Save();
    }
    
    // Public method to resume navigation with saved progress
    public void ResumeNavigation(List<Transform> waypoints)
    {
        if (waypoints == null || waypoints.Count == 0) return;
        
        waypointPath = new List<Transform>(waypoints);
        
        // Use saved waypoint index if valid
        int savedIndex = PlayerPrefs.GetInt(SAVE_KEY_WAYPOINT_INDEX, 0);
        if (savedIndex >= 0 && savedIndex < waypointPath.Count)
        {
            currentWaypointIndex = savedIndex;
        }
        else
        {
            currentWaypointIndex = 0;
        }
        
        isNavigating = true;
        
        if (navigationPanel != null)
        {
            navigationPanel.SetActive(true);
        }
    }
}
