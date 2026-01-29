using UnityEngine;
using TMPro;
using SBPScripts;

/// <summary>
/// Simple speed meter that displays the bike's current speed
/// </summary>
public class SpeedMeterUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private BicycleController bicycleController;
    [SerializeField] private BikeGrindController grindController;

    [Header("Settings")]
    [SerializeField] private bool useHorizontalSpeedOnly = true;
    [SerializeField] private bool showInKMH = false;
    [SerializeField] private string speedUnit = "m/s";

    [Header("Smoothing (Optional)")]
    [SerializeField] private bool smoothSpeed = true;
    [SerializeField] private float smoothingSpeed = 5f;  // ← FIXED: Changed name

    private Rigidbody rb;
    private float currentDisplaySpeed = 0f;

    void Start()
    {
        // Auto-find references if not assigned
        if (bicycleController == null)
        {
            bicycleController = FindObjectOfType<BicycleController>();
            if (bicycleController == null)
            {
                Debug.LogError("SpeedMeterUI: BicycleController not found!");
                enabled = false;
                return;
            }
        }

        rb = bicycleController.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("SpeedMeterUI: Rigidbody not found on BicycleController!");
            enabled = false;
            return;
        }

        if (speedText == null)
        {
            Debug.LogError("SpeedMeterUI: Speed Text not assigned!");
            enabled = false;
            return;
        }
    }

    void Update()
    {
        // Get current speed
        float targetSpeed = GetCurrentSpeed();

        // Apply smoothing if enabled
        if (smoothSpeed)
        {
            currentDisplaySpeed = Mathf.Lerp(currentDisplaySpeed, targetSpeed, Time.deltaTime * smoothingSpeed);  // ← FIXED: Using smoothingSpeed
        }
        else
        {
            currentDisplaySpeed = targetSpeed;
        }

        // Update text
        speedText.text = Mathf.RoundToInt(currentDisplaySpeed).ToString() + " " + speedUnit;
    }

    float GetCurrentSpeed()
    {
        // Check if grinding first
        if (grindController != null && grindController.IsGrinding)
        {
            float grindSpeed = grindController.GrindSpeed;

            // Convert to km/h if needed
            if (showInKMH)
            {
                grindSpeed *= 3.6f;
            }

            return grindSpeed;
        }

        // Normal riding speed
        Vector3 velocity = rb.linearVelocity;

        if (useHorizontalSpeedOnly)
        {
            velocity.y = 0f;
        }

        float speed = velocity.magnitude;

        if (showInKMH)
        {
            speed *= 3.6f;
        }

        return speed;
    }
}