using UnityEngine;

public class GraphNode : MonoBehaviour
{
    [Header("Identity")]
    public int nodeID = 0; 

    [Header("Neighbors (Drag Node Here)")]
    public GraphNode upNode;
    public GraphNode downNode;
    public GraphNode leftNode;
    public GraphNode rightNode;

    [Header("Camera Setting (Optional)")]
    [Tooltip("Isi ini jika Node ini berada di ruangan dengan kamera statis/fixed. Kosongkan jika ingin kamera follow player.")]
    public Transform fixedCameraMount;

    // Visualization
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.2f);

        Gizmos.color = Color.yellow;
        if (upNode) Gizmos.DrawLine(transform.position, upNode.transform.position);
        if (downNode) Gizmos.DrawLine(transform.position, downNode.transform.position);
        if (leftNode) Gizmos.DrawLine(transform.position, leftNode.transform.position);
        if (rightNode) Gizmos.DrawLine(transform.position, rightNode.transform.position);

        // Visualisasi Kamera (Opsional: Garis Merah ke posisi kamera)
        if (fixedCameraMount != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, fixedCameraMount.position);
            Gizmos.DrawSphere(fixedCameraMount.position, 0.3f);
        }
    }
}