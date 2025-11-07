using SBPScripts;
using Unity.Cinemachine;
using UnityEngine;

public class CamShake : MonoBehaviour
{
    [SerializeField] CinemachineImpulseSource screenShake;
    [SerializeField] BicycleController bicycleController;

    [Header("Airtime-Based Shake Settings")]
    [SerializeField] float minAirtimeForShake = 0.5f; // Minimum seconds in air to trigger shake
    [SerializeField] float shakeIntensityPerSecond = 2f; // Shake strength per second of airtime
    [SerializeField] float maxShakeIntensity = 5f; // Cap the shake strength

    private bool wasInAir = false;
    private float airtimeCounter = 0f;

    void Update()
    {
        // Count airtime
        if (bicycleController.isAirborne)
        {
            airtimeCounter += Time.deltaTime;
        }

        // Detect landing
        if (wasInAir && !bicycleController.isAirborne)
        {
            Debug.Log($"LANDED! Airtime: {airtimeCounter:F2} seconds");

            // Check if airtime was long enough
            if (airtimeCounter >= minAirtimeForShake)
            {
                // Calculate shake intensity based on airtime
                float impactForce = airtimeCounter * shakeIntensityPerSecond;
                impactForce = Mathf.Clamp(impactForce, 0f, maxShakeIntensity);

                // Use a simple downward direction for the shake
                Vector3 shakeDirection = Vector3.down * impactForce;

                Debug.Log($"SHAKE TRIGGERED! Airtime: {airtimeCounter:F2}s, Force: {impactForce}");
                ScreenShake(shakeDirection);
            }
            else
            {
                Debug.Log($"Airtime too short. Minimum: {minAirtimeForShake}s");
            }

            // Reset counter
            airtimeCounter = 0f;
        }

        wasInAir = bicycleController.isAirborne;
    }

    public void ScreenShake(Vector3 dir)
    {
        screenShake.GenerateImpulseWithVelocity(dir);
    }
}