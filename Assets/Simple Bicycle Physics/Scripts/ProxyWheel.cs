using UnityEngine;

/// <summary>
/// Simple proxy wheel that copies rotation from an original wheel.
/// Place this on visual wheel duplicates inside VisualBike.
/// The proxy inherits BOTH the original wheel rotation AND VisualBike's trick rotations!
/// </summary>
public class ProxyWheel : MonoBehaviour
{
    [Header("Source Wheel")]
    [Tooltip("The original wheel to copy rotation from (e.g., the one BicycleController controls)")]
    public Transform sourceWheel;

    [Header("Rotation Mode")]
    public RotationMode rotationMode = RotationMode.Local;

    [Tooltip("Reference transform for Relative mode (usually the bike root object)")]
    public Transform referenceTransform;

    [Header("Position")]
    [Tooltip("Copy position from source")]
    public bool copyPosition = false;

    [Tooltip("Apply a position offset relative to source")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("Debug")]
    public bool showDebug = false;

    public enum RotationMode
    {
        Local,      // Copy local rotation (rear wheel)
        World,      // Copy world rotation (simple front wheel)
        Relative    // Copy relative to reference (front wheel with tricks!)
    }

    void Start()
    {
        // Auto-find reference if not set and using Relative mode
        if (rotationMode == RotationMode.Relative && referenceTransform == null)
        {
            referenceTransform = transform.root;
            if (showDebug)
                Debug.Log($"ProxyWheel: Auto-set reference to root: {referenceTransform.name}");
        }
    }

    void LateUpdate()
    {
        if (sourceWheel == null)
        {
            if (showDebug)
                Debug.LogWarning($"ProxyWheel on {gameObject.name}: No source wheel assigned!");
            return;
        }

        // Copy position if enabled
        if (copyPosition)
        {
            transform.position = sourceWheel.position + positionOffset;
        }

        // Copy rotation based on mode
        switch (rotationMode)
        {
            case RotationMode.Local:
                CopyLocalRotation();
                break;

            case RotationMode.World:
                CopyWorldRotation();
                break;

            case RotationMode.Relative:
                CopyRelativeRotation();
                break;
        }
    }

    void CopyLocalRotation()
    {
        // Copy LOCAL rotation - just the wheel's own rotation
        // Perfect for rear wheels with no steering parent
        transform.localRotation = sourceWheel.localRotation;

        if (showDebug)
            Debug.Log($"{gameObject.name} [LOCAL] - Copied: {sourceWheel.localRotation.eulerAngles}");
    }

    void CopyWorldRotation()
    {
        // Copy WORLD rotation - includes parent steering rotation
        // Good for front wheels, but conflicts with parent tricks
        transform.rotation = sourceWheel.rotation;

        if (showDebug)
            Debug.Log($"{gameObject.name} [WORLD] - Copied: {sourceWheel.rotation.eulerAngles}");
    }

    void CopyRelativeRotation()
    {
        // Copy rotation RELATIVE to reference transform
        // This converts from source's coordinate space to proxy's parent space
        // Perfect for wheels that need steering AND to follow trick rotations!

        if (referenceTransform == null)
        {
            Debug.LogError($"ProxyWheel: Reference Transform not set for Relative mode!");
            return;
        }

        // Calculate source wheel's rotation relative to the reference (bike root)
        Quaternion relativeRotation = Quaternion.Inverse(referenceTransform.rotation) * sourceWheel.rotation;

        // Apply as local rotation (will inherit parent's trick rotations naturally)
        transform.localRotation = relativeRotation;

        if (showDebug)
            Debug.Log($"{gameObject.name} [RELATIVE] - Source world: {sourceWheel.rotation.eulerAngles}, Relative: {relativeRotation.eulerAngles}, Final world: {transform.rotation.eulerAngles}");
    }

    void OnDrawGizmos()
    {
        if (!showDebug || sourceWheel == null) return;

        // Draw a line connecting proxy to source
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, sourceWheel.position);

        // Draw sphere at proxy position (color based on mode)
        Gizmos.color = rotationMode == RotationMode.Relative ? Color.cyan : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);

        // Draw reference transform if using Relative mode
        if (rotationMode == RotationMode.Relative && referenceTransform != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, referenceTransform.position);
        }
    }
}