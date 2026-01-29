using UnityEngine;
using SBPScripts;

/// <summary>
/// Visual-Only Trick System v3 - Multi-Angle Snap Points
/// 
/// Changes from v2:
/// - Multiple snap points: 0°, 90°, 180°, 270°, 360°
/// - Individual tolerance sliders for each snap point
/// - Landing at 180° keeps you backwards (no auto-return to 0°)
/// - Quarter spins (90°) and 3/4 spins (270°) now have snap targets
/// - Variable landing speeds still apply
/// 
/// This creates natural "locking" at cardinal angles.
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class VisualOnlyTricks_v3 : MonoBehaviour
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

    [Header("Snap Point Tolerances")]
    [Tooltip("Tolerance for 0° snap (full forward)")]
    [Range(5f, 45f)]
    public float snap0Threshold = 30f;

    [Tooltip("Tolerance for 90° snap (quarter turn right)")]
    [Range(5f, 45f)]
    public float snap90Threshold = 30f;

    [Tooltip("Tolerance for 180° snap (backwards)")]
    [Range(5f, 45f)]
    public float snap180Threshold = 30f;

    [Tooltip("Tolerance for 270° snap (quarter turn left)")]
    [Range(5f, 45f)]
    public float snap270Threshold = 30f;

    [Tooltip("Tolerance for 360° snap (full rotation complete)")]
    [Range(5f, 45f)]
    public float snap360Threshold = 20f;

    [Header("Landing Settings - Variable Speed")]
    [Tooltip("Fast snap for angles close to 0° or 360°")]
    [Range(5f, 30f)]
    public float fastSnapSpeed = 15f;

    [Tooltip("Medium speed for 90° and 270° angles")]
    [Range(3f, 15f)]
    public float mediumSnapSpeed = 8f;

    [Tooltip("Slow smooth turn for 180° region")]
    [Range(1f, 8f)]
    public float slowSnapSpeed = 3f;

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
    private float settledAngle = 0f; // The angle we've settled at (0, 90, 180, 270)

    void Start()
    {
        bikeController = GetComponent<BicycleController>();

        if (visualBikeParent == null)
        {
            Debug.LogError("VisualOnlyTricks_v3: Please assign Visual Bike Parent in inspector!");
            enabled = false;
            return;
        }

        // Store initial rotation
        initialVisualRotation = visualBikeParent.localEulerAngles;

        // DISABLE the original freestyle system to avoid conflicts
        if (bikeController.airTimeSettings.freestyle)
        {
            Debug.Log("<color=yellow>VisualOnlyTricks_v3: Disabling original freestyle system</color>");
            bikeController.airTimeSettings.freestyle = false;
        }

        // Start at 0°
        settledAngle = 0f;
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
        if (showDebug) Debug.Log($"<color=green>TAKEOFF - Starting from {settledAngle}°</color>");

        // Start spinning from wherever we settled
        currentYRotation = settledAngle;
        isSnappingToTarget = false;
    }

    void OnLanding()
    {
        // Find the nearest snap point to land at
        landingTargetAngle = FindNearestSnapPoint(currentYRotation);

        // Calculate appropriate landing speed based on target angle
        currentLandingSpeed = CalculateLandingSpeed(landingTargetAngle);

        isSnappingToTarget = true;

        if (showDebug)
        {
            string speedType = GetSpeedType(landingTargetAngle);
            Debug.Log($"<color=yellow>LANDING - Current: {currentYRotation:F1}°, Target: {landingTargetAngle:F1}°, Speed: {speedType} ({currentLandingSpeed:F1})</color>");
        }
    }

    float FindNearestSnapPoint(float angle)
    {
        // Normalize angle to 0-360 range for checking
        float normalized = angle % 360f;
        if (normalized < 0) normalized += 360f;

        // Calculate how many full rotations we've done
        int fullRotations = Mathf.FloorToInt(angle / 360f);
        float baseRotation = fullRotations * 360f;

        // Check each cardinal angle in order of priority

        // Check 0° / 360° (these are equivalent visually)
        if (IsWithinThreshold(normalized, 0f, snap0Threshold))
        {
            return baseRotation; // Snap to 0° (with full rotations)
        }
        if (IsWithinThreshold(normalized, 360f, snap360Threshold))
        {
            return baseRotation + 360f; // Snap to 360° (next rotation level)
        }

        // Check 90°
        if (IsWithinThreshold(normalized, 90f, snap90Threshold))
        {
            return baseRotation + 90f;
        }

        // Check 180°
        if (IsWithinThreshold(normalized, 180f, snap180Threshold))
        {
            return baseRotation + 180f;
        }

        // Check 270°
        if (IsWithinThreshold(normalized, 270f, snap270Threshold))
        {
            return baseRotation + 270f;
        }

        // Not near any snap point - return the closest one
        // This ensures we always have a target
        float[] snapPoints = { 0f, 90f, 180f, 270f, 360f };
        float closestSnap = 0f;
        float closestDistance = 999f;

        foreach (float snap in snapPoints)
        {
            float distance = Mathf.Abs(Mathf.DeltaAngle(normalized, snap));
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestSnap = snap;
            }
        }

        return baseRotation + closestSnap;
    }

    bool IsWithinThreshold(float angle, float target, float threshold)
    {
        float distance = Mathf.Abs(Mathf.DeltaAngle(angle, target));
        return distance <= threshold;
    }

    float CalculateLandingSpeed(float targetAngle)
    {
        // Normalize target to determine speed category
        float normalized = targetAngle % 360f;
        if (normalized < 0) normalized += 360f;

        // Speed based on target snap point
        if (Mathf.Abs(normalized) < 0.1f || Mathf.Abs(normalized - 360f) < 0.1f)
        {
            // Landing at 0° or 360° - FAST
            return fastSnapSpeed;
        }
        else if (Mathf.Abs(normalized - 180f) < 0.1f)
        {
            // Landing at 180° - SLOW (smooth turn to backwards)
            return slowSnapSpeed;
        }
        else
        {
            // Landing at 90° or 270° - MEDIUM
            return mediumSnapSpeed;
        }
    }

    string GetSpeedType(float angle)
    {
        // Helper for debug logging
        float normalized = angle % 360f;
        if (normalized < 0) normalized += 360f;

        if (Mathf.Abs(normalized) < 0.1f || Mathf.Abs(normalized - 360f) < 0.1f)
            return "FAST";
        else if (Mathf.Abs(normalized - 180f) < 0.1f)
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

        // Handle Y rotation (spins) with snap points
        if (isSnappingToTarget)
        {
            // Lerp toward target snap angle using calculated speed
            currentYRotation = Mathf.Lerp(
                currentYRotation,
                landingTargetAngle,
                Time.deltaTime * currentLandingSpeed
            );

            // Once close enough to target, lock it in and settle
            if (Mathf.Abs(currentYRotation - landingTargetAngle) < 1f)
            {
                currentYRotation = landingTargetAngle;

                // Normalize the settled angle to 0-360 range
                settledAngle = landingTargetAngle % 360f;
                if (settledAngle < 0) settledAngle += 360f;

                // Update current rotation to match settled angle
                currentYRotation = settledAngle;

                isSnappingToTarget = false;

                if (showDebug)
                {
                    Debug.Log($"<color=green>Landing complete - Settled at {settledAngle}°</color>");
                }
            }
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
    public float SettledAngle => settledAngle;
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

        // Draw settled angle indicator on ground
        if (bikeController != null && !bikeController.isAirborne && !isSnappingToTarget)
        {
            // Show which cardinal angle we're at
            if (Mathf.Abs(settledAngle) < 5f || Mathf.Abs(settledAngle - 360f) < 5f)
                Gizmos.color = Color.white;    // 0° / 360°
            else if (Mathf.Abs(settledAngle - 90f) < 5f)
                Gizmos.color = Color.magenta;  // 90°
            else if (Mathf.Abs(settledAngle - 180f) < 5f)
                Gizmos.color = Color.blue;     // 180°
            else if (Mathf.Abs(settledAngle - 270f) < 5f)
                Gizmos.color = Color.cyan;     // 270°
            else
                Gizmos.color = Color.gray;     // Other

            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.15f);
        }
    }
}