using UnityEngine;
using SBPScripts;

/// <summary>
/// Improved Trick System for Simple Bicycle Physics
/// Addresses two key issues:
/// 1. Separates air tricks from acceleration/braking inputs (uses face buttons instead)
/// 2. Provides assisted/smooth tricks that are more predictable for novice players
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class ImprovedTrickSystem : MonoBehaviour
{
    [Header("References")]
    private BicycleController bicycleController;
    private Rigidbody rb;

    [Header("Trick Input (Face Buttons)")]
    [Tooltip("Button for frontflips (North/Y button)")]
    public bool frontflipInput;

    [Tooltip("Button for backflips (South/A button)")]
    public bool backflipInput;

    [Header("Trick Assistance Settings")]
    [Tooltip("Enable assisted tricks (more predictable, less physics-based)")]
    public bool assistedTricks = true;

    [Tooltip("How fast tricks rotate (degrees per second)")]
    [Range(90f, 540f)]
    public float trickRotationSpeed = 360f;

    [Tooltip("How much to stabilize the bike during tricks")]
    [Range(0f, 1f)]
    public float stabilizationAmount = 0.7f;

    [Tooltip("Minimum height to perform tricks")]
    [Range(1f, 10f)]
    public float minTrickHeight = 2f;

    [Header("Trick State")]
    private bool isPerformingTrick = false;
    private float targetTrickRotation = 0f;
    private float currentTrickRotation = 0f;
    private Vector3 trickStartRotation;
    private Vector3 stabilizedVelocity;

    [Header("Visual Feedback")]
    [Tooltip("GameObject to rotate for tricks (usually the bike model, not the physics root)")]
    public Transform visualBikeTransform;

    [Header("Debug")]
    public bool showDebugInfo = false;

    void Start()
    {
        bicycleController = GetComponent<BicycleController>();
        rb = bicycleController.rb;

        // If no visual transform assigned, use the bicycle's transform
        if (visualBikeTransform == null)
        {
            visualBikeTransform = transform;
        }
    }

    void FixedUpdate()
    {
        // Only intercept trick physics when in stunt mode
        if (bicycleController.stuntMode && bicycleController.isAirborne)
        {
            HandleImprovedTricks();
        }
        else
        {
            // Reset trick state when not in the air
            isPerformingTrick = false;
            currentTrickRotation = 0f;
            targetTrickRotation = 0f;
        }
    }

    void HandleImprovedTricks()
    {
        float currentHeight = GetHeightAboveGround();

        // Check if we're high enough for tricks
        if (currentHeight < minTrickHeight)
        {
            if (showDebugInfo)
                Debug.Log($"Too low for tricks: {currentHeight:F1}m");
            return;
        }

        // ISSUE #1 FIX: Use face buttons instead of acceleration axis
        // Detect NEW trick inputs (button press, not hold)
        if (backflipInput && !isPerformingTrick)
        {
            StartTrick(-360f); // Backflip
            if (showDebugInfo)
                Debug.Log("<color=cyan>BACKFLIP initiated!</color>");
        }
        else if (frontflipInput && !isPerformingTrick)
        {
            StartTrick(360f); // Frontflip
            if (showDebugInfo)
                Debug.Log("<color=cyan>FRONTFLIP initiated!</color>");
        }

        // ISSUE #2 FIX: Apply assisted trick physics
        if (assistedTricks && isPerformingTrick)
        {
            ApplyAssistedTrick();
        }
        else
        {
            // Allow original physics-based tricks from L-stick
            ApplyOriginalTrickPhysics();
        }
    }

    void StartTrick(float rotationDegrees)
    {
        isPerformingTrick = true;
        targetTrickRotation = rotationDegrees;
        currentTrickRotation = 0f;
        trickStartRotation = transform.rotation.eulerAngles;
        // Only stabilize horizontal velocity (X and Z), let gravity affect Y
        stabilizedVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    void ApplyAssistedTrick()
    {
        // Smoothly rotate towards target
        currentTrickRotation = Mathf.MoveTowards(
            currentTrickRotation,
            targetTrickRotation,
            trickRotationSpeed * Time.fixedDeltaTime
        );

        // Calculate rotation
        float rotationProgress = currentTrickRotation / targetTrickRotation;
        Quaternion trickRotation = Quaternion.Euler(currentTrickRotation, 0, 0);
        Quaternion baseRotation = Quaternion.Euler(trickStartRotation);

        // Apply to VISUAL transform (keeps physics stable)
        if (visualBikeTransform != transform)
        {
            // Rotate only the visual model, physics stays stable
            visualBikeTransform.localRotation = trickRotation;
        }
        else
        {
            // Rotate the main transform (less stable but works)
            transform.rotation = baseRotation * trickRotation;
        }

        // Stabilize HORIZONTAL velocity only (X and Z), let gravity affect Y naturally
        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 stabilizedHorizontalVel = Vector3.Lerp(currentHorizontalVel, stabilizedVelocity, stabilizationAmount);

        // Preserve vertical velocity (gravity)
        rb.linearVelocity = new Vector3(stabilizedHorizontalVel.x, rb.linearVelocity.y, stabilizedHorizontalVel.z);

        // Reduce angular velocity for smoother tricks
        rb.angularVelocity *= (1f - stabilizationAmount);

        // Complete trick when rotation is done
        if (Mathf.Abs(currentTrickRotation - targetTrickRotation) < 5f)
        {
            isPerformingTrick = false;
            if (showDebugInfo)
                Debug.Log("<color=green>TRICK COMPLETE!</color>");
        }
    }

    void ApplyOriginalTrickPhysics()
    {
        // Allow L-stick to control spins (left/right)
        if (Mathf.Abs(bicycleController.customSteerAxis) > 0.1f)
        {
            rb.AddTorque(
                Vector3.up * bicycleController.customSteerAxis * 4 * bicycleController.airTimeSettings.airTimeRotationSensitivity,
                ForceMode.Impulse
            );
        }

        // NOTE: We've removed rawCustomAccelerationAxis from affecting tricks
        // This fixes Issue #1 - acceleration no longer causes unwanted tricks
    }

    float GetHeightAboveGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, Mathf.Infinity))
        {
            return hit.distance;
        }
        return 999f;
    }

    // Public method to be called by the input system
    public void OnFrontflipButton(bool pressed)
    {
        frontflipInput = pressed;
    }

    public void OnBackflipButton(bool pressed)
    {
        backflipInput = pressed;
    }
}