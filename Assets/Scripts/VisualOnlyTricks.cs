using UnityEngine;
using SBPScripts;

/// <summary>
/// Visual-Only Trick System
/// Rotates the bike VISUAL meshes to perform tricks while physics remains stable.
/// This is the arcade approach - looks good, feels predictable, easy to implement!
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

    [Header("Advanced")]
    [Tooltip("Snap to upright when landing (higher = faster)")]
    [Range(1f, 20f)]
    public float landingSnapSpeed = 10f;

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
        if (bikeController.isAirborne)
        {
            HandleAirTricks();
        }
        else
        {
            HandleLanding();
        }

        // Apply visual rotation
        ApplyVisualRotation();
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
        // Smoothly return to upright position when on ground
        currentXRotation = Mathf.Lerp(currentXRotation, 0f, Time.deltaTime * landingSnapSpeed);
        currentYRotation = Mathf.Lerp(currentYRotation, 0f, Time.deltaTime * landingSnapSpeed);
        isPerformingTrick = false;
    }

    void ApplyVisualRotation()
    {
        // Apply the trick rotation to the visual bike
        // X = Pitch (frontflip/backflip)
        // Y = Yaw (spins - helicopter rotation)
        // Z = Roll (keep at 0 for now, could add for barrel rolls later)
        Vector3 newRotation = new Vector3(
            currentXRotation,                           // Pitch (frontflip/backflip)
            currentYRotation,                           // Yaw (spins - 360s)
            0f                                          // Roll (not used)
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
    }
}