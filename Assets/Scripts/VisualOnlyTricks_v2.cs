using UnityEngine;
using SBPScripts;

/// <summary>
/// Visual-Only Trick System v2 - Variable Landing Speed
/// 
/// Changes from v1:
/// - Different landing speeds based on rotation angle
/// - 360° spins: Fast snap (instant feel)
/// - 180° spins: Slow smooth turn (no abrupt snap)
/// - Other angles: Medium speed transition
/// 
/// This creates a more natural feel for different trick types.
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class VisualOnlyTricks_v2 : MonoBehaviour
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

    [Header("Landing Settings - Variable Speed")]
    [Tooltip("Fast snap for angles close to 0° or 360° (0-30°, 330-390°)")]
    [Range(5f, 30f)]
    public float fastSnapSpeed = 15f;

    [Tooltip("Medium speed for moderate angles (30-150°, 210-330°)")]
    [Range(3f, 15f)]
    public float mediumSnapSpeed = 8f;

    [Tooltip("Slow smooth turn for 180° region (150-210°)")]
    [Range(1f, 8f)]
    public float slowSnapSpeed = 3f;

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
    private float currentLandingSpeed = 10f; // Dynamic landing speed

    void Start()
    {
        bikeController = GetComponent<BicycleController>();

        if (visualBikeParent == null)
        {
            Debug.LogError("VisualOnlyTricks_v2: Please assign Visual Bike Parent in inspector!");
            enabled = false;
            return;
        }

        // Store initial rotation
        initialVisualRotation = visualBikeParent.localEulerAngles;

        // DISABLE the original freestyle system to avoid conflicts
        if (bikeController.airTimeSettings.freestyle)
        {
            Debug.Log("<color=yellow>VisualOnlyTricks_v2: Disabling original freestyle system</color>");
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

        // Calculate appropriate landing speed based on angle
        currentLandingSpeed = CalculateLandingSpeed(currentYRotation);

        isSnappingToTarget = true;

        if (showDebug)
        {
            string speedType = GetSpeedType(currentYRotation);
            Debug.Log($"<color=yellow>LANDING - Current: {currentYRotation:F1}°, Target: {landingTargetAngle:F1}°, Speed: {speedType} ({currentLandingSpeed:F1})</color>");
        }
    }

    float CalculateLandingSpeed(float angle)
    {
        // Normalize angle to 0-360 range for categorization
        float normalized = angle % 360f;
        if (normalized < 0) normalized += 360f;

        // Categorize based on angle ranges
        if (normalized < 30f || normalized > 330f)
        {
            // Close to 0° or 360° - FAST snap
            return fastSnapSpeed;
        }
        else if (normalized >= 150f && normalized <= 210f)
        {
            // Around 180° - SLOW smooth turn
            return slowSnapSpeed;
        }
        else
        {
            // Everything else - MEDIUM speed
            return mediumSnapSpeed;
        }
    }

    string GetSpeedType(float angle)
    {
        // Helper for debug logging
        float normalized = angle % 360f;
        if (normalized < 0) normalized += 360f;

        if (normalized < 30f || normalized > 330f)
            return "FAST";
        else if (normalized >= 150f && normalized <= 210f)
            return "SLOW";
        else
            return "MEDIUM";
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
        // Always use fast speed for frontflip/backflip recovery
        currentXRotation = Mathf.Lerp(currentXRotation, 0f, Time.deltaTime * fastSnapSpeed);
        isPerformingTrick = false;

        // Handle Y rotation (spins) with variable speed based on angle
        if (isSnappingToTarget)
        {
            // Lerp toward target snap angle using calculated speed
            currentYRotation = Mathf.Lerp(
                currentYRotation,
                landingTargetAngle,
                Time.deltaTime * currentLandingSpeed
            );

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
            // Normal lerp to 0 if not snapping (shouldn't happen, but safety)
            currentYRotation = Mathf.Lerp(currentYRotation, 0f, Time.deltaTime * mediumSnapSpeed);
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
            // Not close to a 360° increment - return 0 (will lerp smoothly)
            // This handles 180° and other angles
            return 0f;
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
        Vector3 rayStart = transform.position;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, Mathf.Infinity))
        {
            // Debug visualization
            if (showDebug)
            {
                Debug.DrawLine(rayStart, hit.point, Color.green, 0.1f);
            }

            return hit.distance;
        }

        // No ground detected
        if (showDebug)
        {
            Debug.DrawRay(rayStart, Vector3.down * 50f, Color.red, 0.1f);
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

        // Draw height threshold sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position - Vector3.up * minTrickHeight, 0.5f);

        // Draw current state
        if (bikeController != null && bikeController.isAirborne)
        {
            Gizmos.color = isPerformingTrick ? Color.cyan : Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f);

            // Draw raycast line in Scene view
            float height = GetHeightAboveGround();
            Vector3 groundPoint = transform.position + Vector3.down * height;

            // Color based on whether tricks are allowed
            if (height < minTrickHeight)
                Gizmos.color = Color.red; // Too low for tricks
            else
                Gizmos.color = Color.green; // Tricks allowed

            Gizmos.DrawLine(transform.position, groundPoint);
            Gizmos.DrawSphere(groundPoint, 0.3f);
        }

        // Draw landing speed indicator when on ground and snapping
        if (bikeController != null && !bikeController.isAirborne && isSnappingToTarget)
        {
            // Color based on current landing speed
            if (currentLandingSpeed > 12f)
                Gizmos.color = Color.red;      // Fast
            else if (currentLandingSpeed < 5f)
                Gizmos.color = Color.blue;     // Slow
            else
                Gizmos.color = Color.yellow;   // Medium

            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.3f);
        }
    }
}