using UnityEngine;

public class LocalPortal : MonoBehaviour
{
    [Header("Teleport Settings")]
    public GraphNode destinationNode; 
    
    [Header("Camera Settings")]
    public bool followPlayerAtDestination = false;
    public Transform newCameraMount;
}