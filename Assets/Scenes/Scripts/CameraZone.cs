using UnityEngine;

[System.Serializable]
public class CameraZone : MonoBehaviour
{
    [Header("Zone Identity")]
    public string zoneName = "Zone 1";
    
    [Header("Camera Position")]
    [Tooltip("Position kamera untuk zone ini")]
    public Transform cameraMount;
    
    [Header("Zone Bounds")]
    [Tooltip("Lebar zone (berapa unit dari center)")]
    public float boundsWidth = 10f;
    [Tooltip("Tinggi zone (berapa unit dari center)")]
    public float boundsHeight = 6f;
    
    [Header("Neighbor Zones")]
    [Tooltip("Zone di atas (untuk transisi otomatis)")]
    public CameraZone upZone;
    [Tooltip("Zone di bawah")]
    public CameraZone downZone;
    [Tooltip("Zone di kiri")]
    public CameraZone leftZone;
    [Tooltip("Zone di kanan")]
    public CameraZone rightZone;
    
    // Check if player is inside this zone's bounds
    public bool IsPlayerInside(Vector3 playerPosition)
    {
        Vector3 center = cameraMount != null ? cameraMount.position : transform.position;
        
        float halfWidth = boundsWidth / 2f;
        float halfHeight = boundsHeight / 2f;
        
        bool insideX = playerPosition.x >= center.x - halfWidth && playerPosition.x <= center.x + halfWidth;
        bool insideY = playerPosition.y >= center.y - halfHeight && playerPosition.y <= center.y + halfHeight;
        
        return insideX && insideY;
    }
    
    // Get which neighbor zone player moved to (if out of bounds)
    public CameraZone GetNextZone(Vector3 playerPosition)
    {
        Vector3 center = cameraMount != null ? cameraMount.position : transform.position;
        
        float halfWidth = boundsWidth / 2f;
        float halfHeight = boundsHeight / 2f;
        
        // Check which direction player went
        if (playerPosition.x > center.x + halfWidth && rightZone != null)
            return rightZone;
        
        if (playerPosition.x < center.x - halfWidth && leftZone != null)
            return leftZone;
        
        if (playerPosition.y > center.y + halfHeight && upZone != null)
            return upZone;
        
        if (playerPosition.y < center.y - halfHeight && downZone != null)
            return downZone;
        
        return null; // Still in current zone
    }
    
    // Visualization in Scene view
    private void OnDrawGizmos()
    {
        Vector3 center = cameraMount != null ? cameraMount.position : transform.position;
        
        // Draw zone bounds (green)
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, new Vector3(boundsWidth, boundsHeight, 0));
        
        // Draw camera position (red sphere)
        if (cameraMount != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(cameraMount.position, 0.5f);
        }
        
        // Draw connections to neighbors (blue lines)
        Gizmos.color = Color.blue;
        if (upZone != null && upZone.cameraMount != null)
            Gizmos.DrawLine(center, upZone.cameraMount.position);
        if (downZone != null && downZone.cameraMount != null)
            Gizmos.DrawLine(center, downZone.cameraMount.position);
        if (leftZone != null && leftZone.cameraMount != null)
            Gizmos.DrawLine(center, leftZone.cameraMount.position);
        if (rightZone != null && rightZone.cameraMount != null)
            Gizmos.DrawLine(center, rightZone.cameraMount.position);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Show zone name when selected
        Vector3 center = cameraMount != null ? cameraMount.position : transform.position;
        
#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + Vector3.up * 2f, zoneName);
#endif
    }
}
