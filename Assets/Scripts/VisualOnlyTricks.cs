using UnityEngine;
using SBPScripts;

/// <summary>
/// Visual-Only Trick System with 360° Landing Snap
/// Rotates the bike VISUAL meshes to perform tricks while physics remains stable.
/// Features:
/// - Smooth 360° increment snapping on landing (no counter-spinning)
/// - Automatic reset to 0° after settling
/// - Foundation for future trick tracking system
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class VisualOnlyTricks : MonoBehaviour
{
    [Header("Required Setup")]
    [Tooltip("Drag your visual bike parent here (everything that should rotate during tricks)")]
    public Transform visualBikeParent;

    [Tooltip("Optional: Character transform to also rotate during tricks")]
    public Transform characterTransform;

    [Header("Extra Visual Objects (Optional)")]
    [Tooltip("Add any meshes that are outside VisualBike but should still rotate (like rear wheel visual)")]
    public Transform[] extraVisualObjects;

    [Header("Trick Settings")]
    [Tooltip("How fast tricks rotate (degrees per second)")]
    [Range(180f, 720f)]
    public float trickSpeed = 360f;

    [Tooltip("Minimum height above ground to perform tricks")]
    [Range(1f, 10f)]
    public float minTrickHeight = 2.5f;

    [Header("Input (Set by Input Bridge)")]
    public bool frontflipPressed;
    public bool backflipPressed;

    [Header("Landing Settings")]
    [Tooltip("Snap to upright when landing (higher = faster)")]
    [Range(1f, 20f)]
    public float landingSnapSpeed = 10f;

    [Tooltip("Angle threshold for 360° snapping (±degrees)")]
    [Range(5f, 45f)]
    public float snap360Threshold = 20f;

    [Header("Spins")]
    [Tooltip("Allow spins with L-stick in the air")]
    public bool allowSpins = true;

    [Range(90f, 360f)]
    public float spinSpeed = 180f;

    [Header("Debug")]
    public bool showDebug = false;

    // Internal state
    private BicycleController bikeController;
    private float currentXRotation = 0f;  // Frontflip/backflip rotation
    private float currentYRotation = 0f;  // Spin rotation
    private float targetXRotation = 0f;
    private bool isPerformingTrick = false;
    private Vector3 initialVisualRotation;

    // Landing state
    private bool wasAirborne = false;
    private float landingTargetAngle = 0f;
    private bool isSnappingToTarget = false;

    void Start()
    {
        bikeController = GetComponent<BicycleController>();

        if (visualBikeParent == null)
        {
            Debug.LogError("VisualOnlyTricks: Please assign Visual Bike Parent in inspector!");
            enabled = false;
            return;
        }

        // Store initial rotation
        initialVisualRotation = visualBikeParent.localEulerAngles;

        // DISABLE the original freestyle system to avoid conflicts
        if (bikeController.airTimeSettings.freestyle)
        {
            Debug.Log("<color=yellow>VisualOnlyTricks: Disabling original freestyle system</color>");
            bikeController.airTimeSettings.freestyle = false;
        }
    }

    void Update()
    {
        // Detect state transitions
        bool isCurrentlyAirborne = bikeController.isAirborne;

        if (isCurrentlyAirborne)
        {
            if (!wasAirborne)
            {
                // Just took off
                OnTakeoff();
            }
            HandleAirTricks();
        }
        else
        {
            if (wasAirborne)
            {
                // Just landed
                OnLanding();
            }
            HandleLanding();
        }

        wasAirborne = isCurrentlyAirborne;

        // Apply visual rotation
        ApplyVisualRotation();
    }

    void OnTakeoff()
    {
        if (showDebug) Debug.Log("<color=green>TAKEOFF - Rotation reset to 0°</color>");
        // Ensure we start fresh (in case we didn't fully settle)
        currentYRotation = 0f;
        isSnappingToTarget = false;
    }

    void OnLanding()
    {
        // Calculate the target angle to snap to (nearest 360° increment)
        landingTargetAngle = FindNearestCleanAngle(currentYRotation);
        isSnappingToTarget = true;

        if (showDebug)
        {
            Debug.Log($"<color=yellow>LANDING - Current: {currentYRotation:F1}°, Target: {landingTargetAngle:F1}°</color>");
        }
    }

    void HandleAirTricks()
    {
        float heightAboveGround = GetHeightAboveGround();

        // Too low for tricks
        if (heightAboveGround < minTrickHeight)
        {
            return;
        }

        // FRONTFLIP - Press X button
        if (frontflipPressed && !isPerformingTrick)
        {
            StartTrick(360f);
            if (showDebug) Debug.Log("<color=cyan>FRONTFLIP!</color>");
        }
        // BACKFLIP - Press Y button  
        else if (backflipPressed && !isPerformingTrick)
        {
            StartTrick(-360f);
            if (showDebug) Debug.Log("<color=cyan>BACKFLIP!</color>");
        }

        // Handle trick rotation
        if (isPerformingTrick)
        {
            PerformTrick();
        }

        // SPINS - L-stick left/right
        if (allowSpins && !isPerformingTrick)
        {
            float spinInput = bikeController.customSteerAxis;
            if (Mathf.Abs(spinInput) > 0.1f)
            {
                currentYRotation += spinInput * spinSpeed * Time.deltaTime;

                if (showDebug && Time.frameCount % 30 == 0) // Log every 30 frames
                {
                    Debug.Log($"<color=cyan>Spinning: {currentYRotation:F1}°</color>");
                }
            }
        }
    }

    void StartTrick(float rotationAmount)
    {
        isPerformingTrick = true;
        targetXRotation = currentXRotation + rotationAmount;
    }

    void PerformTrick()
    {
        // Smoothly rotate towards target
        currentXRotation = Mathf.MoveTowards(
            currentXRotation,
            targetXRotation,
            trickSpeed * Time.deltaTime
        );

        // Check if trick is complete
        if (Mathf.Abs(currentXRotation - targetXRotation) < 5f)
        {
            isPerformingTrick = false;
            if (showDebug) Debug.Log("<color=green>Trick complete!</color>");
        }
    }

    void HandleLanding()
    {
        // Smoothly return to upright for X rotation (frontflips/backflips)
        currentXRotation = Mathf.Lerp(currentXRotation, 0f, Time.deltaTime * landingSnapSpeed);
        isPerformingTrick = false;

        // Handle Y rotation (spins) with 360° snapping
        if (isSnappingToTarget)
        {
            // Lerp toward target snap angle
            currentYRotation = Mathf.Lerp(currentYRotation, landingTargetAngle, Time.deltaTime * landingSnapSpeed);

            // Once close enough to target, reset to 0°
            if (Mathf.Abs(currentYRotation - landingTargetAngle) < 1f)
            {
                // Since 360° = 0° visually, we can just set to 0
                currentYRotation = 0f;
                landingTargetAngle = 0f;
                isSnappingToTarget = false;

                if (showDebug)
                {
                    Debug.Log("<color=green>Landing complete - Reset to 0°</color>");
                }
            }
        }
        else
        {
            // Normal lerp to 0 if not snapping (for X rotation settling)
            currentYRotation = Mathf.Lerp(currentYRotation, 0f, Time.deltaTime * landingSnapSpeed);
        }
    }

    float FindNearestCleanAngle(float angle)
    {
        // Normalize angle to 0-360 range
        float normalized = angle % 360f;
        if (normalized < 0) normalized += 360f;

        // Check if we're close to 0° or 360° (within threshold)
        if (normalized < snap360Threshold)
        {
            // Close to 0° - snap to nearest lower 360 multiple
            // e.g., 10° → snap to 0°, 370° → snap to 360°
            return Mathf.Floor(angle / 360f) * 360f;
        }
        else if (normalized > (360f - snap360Threshold))
        {
            // Close to 360° - snap to nearest upper 360 multiple
            // e.g., 350° → snap to 360°, 710° → snap to 720°
            return Mathf.Ceil(angle / 360f) * 360f;
        }
        else
        {
            // Not close to a 360° increment - just return as-is
            // This handles cases like 180° spins (which we don't snap)
            return angle;
        }
    }

    void ApplyVisualRotation()
    {
        // Apply the trick rotation to the visual bike
        // X = Pitch (frontflip/backflip)
        // Y = Yaw (spins - 360s)
        // Z = Roll (keep at 0 for now, could add for barrel rolls later)
        Vector3 newRotation = new Vector3(
            currentXRotation,    // Pitch (frontflip/backflip)
            currentYRotation,    // Yaw (spins - 360s)
            0f                   // Roll (not used)
        );

        visualBikeParent.localRotation = Quaternion.Euler(newRotation);

        // Also rotate character if assigned
        if (characterTransform != null)
        {
            characterTransform.localRotation = visualBikeParent.localRotation;
        }

        // Rotate any extra visual objects (like rear wheel if it's outside VisualBike)
        if (extraVisualObjects != null && extraVisualObjects.Length > 0)
        {
            foreach (Transform extraObj in extraVisualObjects)
            {
                if (extraObj != null)
                {
                    extraObj.localRotation = Quaternion.Euler(newRotation);
                }
            }
        }
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

    // Public methods for input bridge
    public void OnFrontflipButton(bool pressed)
    {
        frontflipPressed = pressed;
    }

    public void OnBackflipButton(bool pressed)
    {
        backflipPressed = pressed;
    }

    // Public read-only properties for future trick tracking/UI
    public float CurrentRotation => currentYRotation;
    public bool IsAirborne => bikeController.isAirborne;
    public bool IsPerformingTrick => isPerformingTrick;

    // Debug gizmos
    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // Draw height threshold
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position - Vector3.up * minTrickHeight, 0.5f);

        // Draw current state
        if (bikeController != null && bikeController.isAirborne)
        {
            Gizmos.color = isPerformingTrick ? Color.cyan : Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        // Draw landing target indicator when on ground and snapping
        if (bikeController != null && !bikeController.isAirborne && isSnappingToTarget)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.3f);
        }
    }
}