using UnityEngine;
using SBPScripts;

/// <summary>
/// Handles visual wheel spinning AND steering for the Bicycle Experimental prefab.
/// Takes full control from BicycleController to avoid conflicts.
/// Calculates wheel rotation based on bike movement for smooth, realistic spinning.
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class WheelSpinHandler : MonoBehaviour
{
    [Header("Front Wheel Setup")]
    [Tooltip("Parent object that rotates for steering (usually FWheelQuarternion)")]
    public Transform frontWheelParent;

    [Tooltip("Child wheel mesh that spins (usually FWheel inside FWheelQuarternion)")]
    public Transform frontWheelMesh;

    [Header("Rear Wheel Setup")]
    [Tooltip("The rear wheel mesh that spins")]
    public Transform rearWheelMesh;

    [Header("Physics Wheels")]
    [Tooltip("FPhysicsWheel for position tracking")]
    public Transform frontPhysicsWheel;

    [Tooltip("RPhysicsWheel for position tracking")]
    public Transform rearPhysicsWheel;

    [Header("Wheel Properties")]
    [Tooltip("Radius of the wheels in meters (used for rotation calculation)")]
    public float wheelRadius = 0.33f;

    [Header("Steering Settings")]
    [Tooltip("Copy from BicycleController's steerAngle curve")]
    public AnimationCurve steerAngle;

    [Tooltip("Copy from BicycleController's axisAngle value")]
    public float axisAngle = 5f;

    [Header("Debug")]
    public bool showDebug = false;

    // Internal state
    private BicycleController bikeController;
    private Rigidbody rb;
    private float frontWheelRotation = 0f;
    private float rearWheelRotation = 0f;
    private Vector3 lastFrontWheelPos;
    private Vector3 lastRearWheelPos;
    private bool initialized = false;

    void Start()
    {
        bikeController = GetComponent<BicycleController>();
        rb = bikeController.rb;

        // Initialize last positions
        if (frontPhysicsWheel != null)
            lastFrontWheelPos = frontPhysicsWheel.position;
        if (rearPhysicsWheel != null)
            lastRearWheelPos = rearPhysicsWheel.position;

        // Validate setup
        ValidateSetup();

        initialized = true;
    }

    void ValidateSetup()
    {
        if (frontWheelParent == null)
            Debug.LogError("WheelSpinHandler: Front Wheel Parent not assigned!");
        if (frontWheelMesh == null)
            Debug.LogError("WheelSpinHandler: Front Wheel Mesh not assigned!");
        if (rearWheelMesh == null)
            Debug.LogError("WheelSpinHandler: Rear Wheel Mesh not assigned!");
        if (frontPhysicsWheel == null)
            Debug.LogError("WheelSpinHandler: Front Physics Wheel not assigned!");
        if (rearPhysicsWheel == null)
            Debug.LogError("WheelSpinHandler: Rear Physics Wheel not assigned!");

        if (steerAngle == null || steerAngle.length == 0)
        {
            Debug.LogWarning("WheelSpinHandler: Steer Angle curve not set! Creating default.");
            steerAngle = AnimationCurve.Linear(0, 45, 10, 15);
        }
    }

    void LateUpdate()
    {
        if (!initialized) return;

        UpdateFrontWheel();
        UpdateRearWheel();
    }

    void UpdateFrontWheel()
    {
        if (frontPhysicsWheel == null || frontWheelParent == null || frontWheelMesh == null)
            return;

        // === STEERING (Parent rotation) ===
        // Calculate steering angle based on speed and input
        float currentSpeed = rb.linearVelocity.magnitude;
        float steeringAngle = bikeController.customSteerAxis * steerAngle.Evaluate(currentSpeed);

        // Add oscillation if bike is moving
        float oscillation = 0f;
        if (!bikeController.isAirborne)
        {
            // You can add oscillation effects here if needed
        }

        // Apply steering to parent (world space rotation for steering)
        float xTilt = Mathf.Sin(Mathf.Deg2Rad * transform.rotation.eulerAngles.y) * (bikeController.customSteerAxis * -axisAngle);
        float yRotation = steeringAngle + oscillation;
        float zTilt = Mathf.Cos(Mathf.Deg2Rad * transform.rotation.eulerAngles.y) * (bikeController.customSteerAxis * -axisAngle);

        frontWheelParent.rotation = Quaternion.Euler(xTilt, transform.rotation.eulerAngles.y + yRotation, zTilt);

        // === SPINNING (Mesh rotation) ===
        // Calculate distance traveled
        Vector3 currentPos = frontPhysicsWheel.position;
        float distanceTraveled = Vector3.Distance(currentPos, lastFrontWheelPos);
        lastFrontWheelPos = currentPos;

        // Convert distance to rotation (circumference = 2 * pi * radius)
        float rotationDegrees = (distanceTraveled / (2f * Mathf.PI * wheelRadius)) * 360f;

        // Determine direction (forward or backward)
        Vector3 movement = currentPos - lastFrontWheelPos;
        float forwardDot = Vector3.Dot(transform.forward, movement.normalized);
        if (forwardDot < 0) rotationDegrees = -rotationDegrees;

        // Accumulate rotation
        frontWheelRotation += rotationDegrees;
        frontWheelRotation %= 360f;

        // Apply to mesh (local rotation so it's relative to steering parent)
        frontWheelMesh.localRotation = Quaternion.Euler(frontWheelRotation, 0f, 0f);

        if (showDebug)
        {
            Debug.Log($"Front - Speed: {currentSpeed:F2}, Steering: {steeringAngle:F1}°, Spin: {frontWheelRotation:F1}°");
        }
    }

    void UpdateRearWheel()
    {
        if (rearPhysicsWheel == null || rearWheelMesh == null)
            return;

        // Calculate distance traveled
        Vector3 currentPos = rearPhysicsWheel.position;
        float distanceTraveled = Vector3.Distance(currentPos, lastRearWheelPos);
        lastRearWheelPos = currentPos;

        // Convert distance to rotation
        float rotationDegrees = (distanceTraveled / (2f * Mathf.PI * wheelRadius)) * 360f;

        // Determine direction
        Vector3 movement = currentPos - lastRearWheelPos;
        float forwardDot = Vector3.Dot(transform.forward, movement.normalized);
        if (forwardDot < 0) rotationDegrees = -rotationDegrees;

        // Accumulate rotation
        rearWheelRotation += rotationDegrees;
        rearWheelRotation %= 360f;

        // Apply to wheel (local rotation so it follows parent during tricks)
        rearWheelMesh.localRotation = Quaternion.Euler(rearWheelRotation, 0f, 0f);

        if (showDebug)
        {
            Debug.Log($"Rear - Distance: {distanceTraveled:F4}, Rotation: {rearWheelRotation:F1}°");
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // Draw wheel positions and movement vectors
        if (frontPhysicsWheel != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(frontPhysicsWheel.position, 0.15f);
            Gizmos.DrawLine(frontPhysicsWheel.position, lastFrontWheelPos);
        }

        if (rearPhysicsWheel != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rearPhysicsWheel.position, 0.15f);
            Gizmos.DrawLine(rearPhysicsWheel.position, lastRearWheelPos);
        }
    }
}