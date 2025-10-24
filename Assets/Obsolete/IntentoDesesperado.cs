using UnityEngine;

public class WheelVisualFollower : MonoBehaviour
{
    public Transform physicsWheel;      // Reference to the physics wheel (sphere)
    public Transform visualRoot;        // Reference to BikeVisualRoot

    private Vector3 initialLocalOffset;
    private Quaternion initialLocalRotation;

    void Start()
    {
        // Cache the offset between the wheel visual and its physics counterpart
        initialLocalOffset = transform.position - physicsWheel.position;
        initialLocalRotation = Quaternion.Inverse(physicsWheel.rotation) * transform.rotation;
    }

    void LateUpdate()
    {
        if (physicsWheel == null || visualRoot == null) return;

        // Get the physics wheel's world transform
        Vector3 worldPos = physicsWheel.position;
        Quaternion worldRot = physicsWheel.rotation;

        // Apply the visual root's transformation on top
        Matrix4x4 visualRootMatrix = Matrix4x4.TRS(visualRoot.position, visualRoot.rotation, Vector3.one);
        Matrix4x4 wheelMatrix = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);

        // Combine them (visualRoot on top of physics wheel)
        Matrix4x4 combined = visualRootMatrix * wheelMatrix;

        transform.position = combined.GetColumn(3);
        transform.rotation = combined.rotation;
    }
}
