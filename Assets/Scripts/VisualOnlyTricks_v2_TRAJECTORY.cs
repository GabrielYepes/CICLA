using UnityEngine;
using SBPScripts;

/// <summary>
/// Visual-Only Trick System v2 - Trajectory Prediction
/// 
/// New Features:
/// - Trajectory-based trick detection (predicts air time on takeoff)
/// - Toggleable angle snapping system
/// - Enhanced debug info showing trick availability
/// - 360° snapping for BOTH frontflips/backflips AND spins
/// - Velocity-based trick speeds (faster flips/spins when riding fast)
/// - Direction-aware landing snapping (completes rotation in current direction)
/// - Post-landing cooldown (prevents accidental tricks during bounce)
/// 
/// No more raycast issues with obstacles or complex terrain!
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class VisualOnlyTricks_v2_Trajectory : MonoBehaviour
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
    [Tooltip("Base rotation speed for flips (degrees per second)")]
    [Range(180f, 720f)]
    public float baseTrickSpeed = 360f;

    [Tooltip("Enable velocity-based speed for flips")]
    public bool useVelocityForFlips = true;

    [Tooltip("Velocity influence on flip speed (0 = no influence, 1 = double speed at max velocity)")]
    [Range(0f, 2f)]
    public float flipVelocityMultiplier = 0.5f;

    [Header("Trick Detection - Trajectory Prediction")]
    [Tooltip("Minimum predicted air time required to allow tricks (seconds)")]
    [Range(0.1f, 2f)]
    public float minAirTimeForTricks = 0.5f;

    [Tooltip("Use predicted trajectory instead of height check")]
    public bool useTrajectoryPrediction = true;

    [Tooltip("Fallback: minimum height for tricks (used if trajectory prediction disabled)")]
    [Range(1f, 10f)]
    public float minTrickHeight = 2.5f;

    [Header("Input (Set by Input Bridge)")]
    public bool frontflipPressed;
    public bool backflipPressed;

    [Header("Landing Settings - Angle Snapping")]
    [Tooltip("Enable 360° angle snapping on landing")]
    public bool enableAngleSnapping = true;

    [Tooltip("Angle threshold for 360° snapping (±degrees)")]
    [Range(5f, 45f)]
    public float snap360Threshold = 20f;

    [Tooltip("Cooldown after landing before tricks are allowed again (prevents accidental bounce tricks)")]
    [Range(0f, 1f)]
    public float postLandingCooldown = 0.3f;

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

    [Header("Spins")]
    [Tooltip("Allow spins with L-stick in the air")]
    public bool allowSpins = true;

    [Tooltip("Base rotation speed for spins (degrees per second)")]
    [Range(90f, 360f)]
    public float baseSpinSpeed = 180f;

    [Tooltip("Enable velocity-based speed for spins")]
    public bool useVelocityForSpins = true;

    [Tooltip("Velocity influence on spin speed (0 = no influence, 1 = double speed at max velocity)")]
    [Range(0f, 2f)]
    public float spinVelocityMultiplier = 0.3f;

    [Header("Velocity Settings")]
    [Tooltip("Reference velocity for max speed bonus (typically max bike speed)")]
    [Range(10f, 50f)]
    public float referenceVelocity = 25f;

    [Tooltip("Show velocity debug info")]
    public bool showVelocityDebug = false;

    [Header("Debug")]
    public bool showDebug = false;

    // Internal state
    private BicycleController bikeController;
    private Rigidbody rb;
    private float currentXRotation = 0f;  // Frontflip/backflip rotation
    private float currentYRotation = 0f;  // Spin rotation
    private float targetXRotation = 0f;
    private bool isPerformingTrick = false;
    private Vector3 initialVisualRotation;

    // Landing state
    private bool wasAirborne = false;
    private float landingTargetAngleX = 0f;  // Target for frontflips/backflips
    private float landingTargetAngleY = 0f;  // Target for spins
    private bool isSnappingToTarget = false;
    private float currentLandingSpeed = 10f; // Dynamic landing speed

    // Trajectory prediction state
    private bool tricksAllowedThisJump = false;
    private float predictedAirTime = 0f;
    private float takeoffTime = 0f;

    // Post-landing cooldown
    private float landingTime = 0f;
    private bool isInLandingCooldown = false;

    void Start()
    {
        bikeController = GetComponent<BicycleController>();
        rb = GetComponent<Rigidbody>();

        if (visualBikeParent == null)
        {
            Debug.LogError("VisualOnlyTricks_v2_Trajectory: Please assign Visual Bike Parent in inspector!");
            enabled = false;
            return;
        }

        if (rb == null)
        {
            Debug.LogError("VisualOnlyTricks_v2_Trajectory: Rigidbody not found! Trajectory prediction requires Rigidbody.");
            useTrajectoryPrediction = false;
        }

        // Store initial rotation
        initialVisualRotation = visualBikeParent.localEulerAngles;

        // DISABLE the original freestyle system to avoid conflicts
        if (bikeController.airTimeSettings.freestyle)
        {
            Debug.Log("<color=yellow>VisualOnlyTricks_v2_Trajectory: Disabling original freestyle system</color>");
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
        if (showDebug) Debug.Log("<color=green>═══ TAKEOFF ═══</color>");

        // Reset rotation for new jump
        currentYRotation = 0f;
        isSnappingToTarget = false;
        takeoffTime = Time.time;

        // Clear landing cooldown
        isInLandingCooldown = false;

        // Predict if this jump allows tricks
        if (useTrajectoryPrediction && rb != null)
        {
            predictedAirTime = CalculatePredictedAirTime();
            tricksAllowedThisJump = predictedAirTime >= minAirTimeForTricks;

            if (showDebug)
            {
                string statusColor = tricksAllowedThisJump ? "cyan" : "red";
                string status = tricksAllowedThisJump ? "ALLOWED ✓" : "BLOCKED ✗";
                Debug.Log($"<color={statusColor}>Predicted air time: {predictedAirTime:F2}s</color>");
                Debug.Log($"<color={statusColor}>Tricks: {status}</color>");
            }
        }
        else
        {
            // Fallback to always allowing tricks if prediction disabled
            tricksAllowedThisJump = true;
            if (showDebug) Debug.Log("<color=yellow>Trajectory prediction disabled - tricks allowed by default</color>");
        }
    }

    float CalculatePredictedAirTime()
    {
        // Get current upward velocity
        float upwardVelocity = rb.linearVelocity.y;

        // If moving downward already, we're probably falling (shouldn't happen at takeoff but safety check)
        if (upwardVelocity <= 0)
        {
            return 0f;
        }

        // Get current height above ground (for more accurate calculation)
        float currentHeight = GetHeightAboveGround();

        // Physics calculations:
        // Time to reach apex: t = v / g
        float gravity = -Physics.gravity.y; // Make positive for calculations
        float timeToApex = upwardVelocity / gravity;

        // Maximum height above current position: h = v² / (2g)
        float additionalHeight = (upwardVelocity * upwardVelocity) / (2f * gravity);

        // Total height at apex
        float totalApexHeight = currentHeight + additionalHeight;

        // Time to fall from apex back to ground: t = sqrt(2h / g)
        float timeToGround = Mathf.Sqrt((2f * totalApexHeight) / gravity);

        // Total predicted air time
        float totalAirTime = timeToApex + timeToGround;

        return totalAirTime;
    }

    void OnLanding()
    {
        if (showDebug) Debug.Log("<color=yellow>═══ LANDING ═══</color>");

        // Reset trick permission
        tricksAllowedThisJump = false;

        // Start post-landing cooldown
        landingTime = Time.time;
        isInLandingCooldown = true;

        if (showDebug)
        {
            Debug.Log($"<color=orange>Post-landing cooldown started ({postLandingCooldown:F2}s)</color>");
        }

        // Handle angle snapping if enabled
        if (enableAngleSnapping)
        {
            // Calculate target angles for BOTH X and Y rotations (nearest 360° increment)
            landingTargetAngleX = FindNearestCleanAngle(currentXRotation);
            landingTargetAngleY = FindNearestCleanAngle(currentYRotation);

            // Calculate appropriate landing speed based on Y angle (spins determine speed)
            currentLandingSpeed = CalculateLandingSpeed(currentYRotation);

            isSnappingToTarget = true;

            if (showDebug)
            {
                string speedType = GetSpeedType(currentYRotation);
                Debug.Log($"<color=yellow>X-Rotation: {currentXRotation:F1}° → {landingTargetAngleX:F1}°</color>");
                Debug.Log($"<color=yellow>Y-Rotation: {currentYRotation:F1}° → {landingTargetAngleY:F1}°, Speed: {speedType} ({currentLandingSpeed:F1})</color>");
            }
        }
        else
        {
            // No snapping - just lerp to 0
            landingTargetAngleX = 0f;
            landingTargetAngleY = 0f;
            currentLandingSpeed = mediumSnapSpeed;
            isSnappingToTarget = true;

            if (showDebug)
            {
                Debug.Log($"<color=yellow>Angle snapping disabled - lerping to 0° at medium speed</color>");
            }
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
        // Check if we're still in post-landing cooldown
        if (isInLandingCooldown)
        {
            float timeSinceLanding = Time.time - landingTime;
            if (timeSinceLanding < postLandingCooldown)
            {
                // Still in cooldown - block tricks
                if (showDebug && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"<color=orange>Cooldown active: {timeSinceLanding:F2}s / {postLandingCooldown:F2}s</color>");
                }
                return;
            }
            else
            {
                // Cooldown expired
                isInLandingCooldown = false;
                if (showDebug)
                {
                    Debug.Log("<color=green>Post-landing cooldown expired - tricks available</color>");
                }
            }
        }

        // Check if tricks are allowed this jump
        if (useTrajectoryPrediction)
        {
            // Use predicted trajectory
            if (!tricksAllowedThisJump)
            {
                // Fallback: If we've been in air longer than predicted, allow tricks anyway
                float actualAirTime = Time.time - takeoffTime;
                if (actualAirTime > predictedAirTime && actualAirTime > minAirTimeForTricks)
                {
                    tricksAllowedThisJump = true;
                    if (showDebug) Debug.Log("<color=green>Air time exceeded prediction - tricks now allowed!</color>");
                }
                else
                {
                    return; // Block tricks
                }
            }
        }
        else
        {
            // Fallback to height-based detection
            float heightAboveGround = GetHeightAboveGround();
            if (heightAboveGround < minTrickHeight)
            {
                return; // Too low for tricks
            }
        }

        // FRONTFLIP - Press X button
        if (frontflipPressed && !isPerformingTrick)
        {
            StartTrick(360f);
            if (showDebug) Debug.Log("<color=cyan>FRONTFLIP!</color>");
        }
        // BACKFLIP - Press B button  
        else if (backflipPressed && !isPerformingTrick)
        {
            StartTrick(-360f);
            if (showDebug) Debug.Log("<color=cyan>BACKFLIP!</color>");
        }

        // Handle trick rotation (flips)
        if (isPerformingTrick)
        {
            PerformTrick();
        }

        // SPINS - L-stick left/right (can be done simultaneously with flips)
        if (allowSpins)
        {
            float spinInput = bikeController.customSteerAxis;
            if (Mathf.Abs(spinInput) > 0.1f)
            {
                // Calculate velocity-based spin speed
                float currentSpinSpeed = baseSpinSpeed;
                if (useVelocityForSpins && rb != null)
                {
                    float velocityMultiplier = GetVelocitySpeedMultiplier();
                    currentSpinSpeed = baseSpinSpeed * (1f + (spinVelocityMultiplier * velocityMultiplier));

                    if (showVelocityDebug && Time.frameCount % 30 == 0)
                    {
                        Debug.Log($"<color=magenta>Spin Speed: {currentSpinSpeed:F1}°/s (base: {baseSpinSpeed}, mult: {velocityMultiplier:F2})</color>");
                    }
                }

                currentYRotation += spinInput * currentSpinSpeed * Time.deltaTime;

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
        // Calculate velocity-based speed
        float currentTrickSpeed = baseTrickSpeed;
        if (useVelocityForFlips && rb != null)
        {
            float velocityMultiplier = GetVelocitySpeedMultiplier();
            currentTrickSpeed = baseTrickSpeed * (1f + (flipVelocityMultiplier * velocityMultiplier));

            if (showVelocityDebug && Time.frameCount % 30 == 0)
            {
                Debug.Log($"<color=magenta>Flip Speed: {currentTrickSpeed:F1}°/s (base: {baseTrickSpeed}, mult: {velocityMultiplier:F2})</color>");
            }
        }

        // Smoothly rotate towards target
        currentXRotation = Mathf.MoveTowards(
            currentXRotation,
            targetXRotation,
            currentTrickSpeed * Time.deltaTime
        );

        // Check if trick is complete
        if (Mathf.Abs(currentXRotation - targetXRotation) < 5f)
        {
            isPerformingTrick = false;
            if (showDebug) Debug.Log("<color=green>Trick complete!</color>");
        }
    }

    float GetVelocitySpeedMultiplier()
    {
        if (rb == null) return 0f;

        // Get horizontal velocity (ignore vertical component for more consistent feel)
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        // Normalize to 0-1 range based on reference velocity
        float normalizedSpeed = Mathf.Clamp01(speed / referenceVelocity);

        return normalizedSpeed;
    }

    void HandleLanding()
    {
        // Apply 360° snapping to BOTH X and Y rotations
        if (isSnappingToTarget)
        {
            // Handle X rotation (frontflips/backflips) with 360° snapping
            // Always use fast speed for frontflip/backflip recovery
            currentXRotation = Mathf.Lerp(
                currentXRotation,
                landingTargetAngleX,
                Time.deltaTime * fastSnapSpeed
            );

            // Handle Y rotation (spins) with variable speed based on angle
            currentYRotation = Mathf.Lerp(
                currentYRotation,
                landingTargetAngleY,
                Time.deltaTime * currentLandingSpeed
            );

            // Check if both rotations have settled
            bool xSettled = Mathf.Abs(currentXRotation - landingTargetAngleX) < 1f;
            bool ySettled = Mathf.Abs(currentYRotation - landingTargetAngleY) < 1f;

            if (xSettled && ySettled)
            {
                // Reset both to 0° (since 360° = 0° visually)
                currentXRotation = 0f;
                currentYRotation = 0f;
                landingTargetAngleX = 0f;
                landingTargetAngleY = 0f;
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
            currentXRotation = Mathf.Lerp(currentXRotation, 0f, Time.deltaTime * fastSnapSpeed);
            currentYRotation = Mathf.Lerp(currentYRotation, 0f, Time.deltaTime * mediumSnapSpeed);
        }

        isPerformingTrick = false;
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
            // Not close to a 360° increment - snap in the direction that completes the rotation
            // For angles > 180°, continue forward to next 360° multiple
            // For angles < 180°, go back to previous 360° multiple

            if (normalized > 180f)
            {
                // Past halfway (e.g., 270° backflip) - complete the rotation forward
                return Mathf.Ceil(angle / 360f) * 360f;
            }
            else
            {
                // Before halfway (e.g., 90° frontflip) - snap back to previous
                return Mathf.Floor(angle / 360f) * 360f;
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
    public bool TricksAllowed => tricksAllowedThisJump;
    public float PredictedAirTime => predictedAirTime;

    // Debug gizmos
    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // Draw current state
        if (bikeController != null && bikeController.isAirborne)
        {
            // Color based on trick availability
            if (tricksAllowedThisJump)
                Gizmos.color = isPerformingTrick ? Color.cyan : Color.green;
            else
                Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}