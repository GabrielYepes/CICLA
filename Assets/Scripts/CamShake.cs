using SBPScripts;
using Unity.Cinemachine;
using UnityEngine;

public class CamShake : MonoBehaviour
{
    [SerializeField] CinemachineImpulseSource screenShake;
    [SerializeField] BicycleController bicycleController;

    [Header("Airtime-Based Shake Settings")]
    [SerializeField] float minAirtimeForShake = 0.5f;
    [SerializeField] float shakeIntensityPerSecond = 2f;
    [SerializeField] float maxShakeIntensity = 5f;

    [Header("Speed-Based Perlin Noise Settings")]
    [SerializeField] CinemachineCamera cinemachineCamera;
    [SerializeField] float speedThreshold = 15f; // Speed to activate shake
    [Tooltip("Optional multiplier for your configured amplitude (1 = use your settings as-is)")]
    [SerializeField] float amplitudeMultiplier = 1f;
    [SerializeField] float transitionSpeed = 3f; // How fast shake fades in/out

    private bool wasInAir = false;
    private float airtimeCounter = 0f;

    // Perlin Noise component reference
    private CinemachineBasicMultiChannelPerlin perlinNoise;
    private float targetAmplitude = 0f;
    private float baseAmplitude = 0f; // Store your configured amplitude

    void Start()
    {
        // Get the Perlin Noise component from the Cinemachine camera
        if (cinemachineCamera != null)
        {
            perlinNoise = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();

            if (perlinNoise == null)
            {
                //Debug.LogWarning("CamShake: No Basic Multichannel Perlin component found on Cinemachine camera!");
            }
            else
            {
                // Store your configured amplitude
                baseAmplitude = perlinNoise.AmplitudeGain;
                // Start disabled
                perlinNoise.AmplitudeGain = 0f;
            }
        }
    }

    void Update()
    {
        // Count airtime for landing shake
        if (bicycleController.isAirborne)
        {
            airtimeCounter += Time.deltaTime;
        }

        // Detect landing for impulse shake
        if (wasInAir && !bicycleController.isAirborne)
        {
            //Debug.Log($"LANDED! Airtime: {airtimeCounter:F2} seconds");

            if (airtimeCounter >= minAirtimeForShake)
            {
                float impactForce = airtimeCounter * shakeIntensityPerSecond;
                impactForce = Mathf.Clamp(impactForce, 0f, maxShakeIntensity);
                Vector3 shakeDirection = Vector3.down * impactForce;

                //Debug.Log($"SHAKE TRIGGERED! Airtime: {airtimeCounter:F2}s, Force: {impactForce}");
                ScreenShake(shakeDirection);
            }

            airtimeCounter = 0f;
        }

        wasInAir = bicycleController.isAirborne;

        // Update speed-based Perlin noise
        UpdateSpeedBasedShake();
    }

    void UpdateSpeedBasedShake()
    {
        if (perlinNoise == null || bicycleController == null) return;

        // Get current speed
        float currentSpeed = bicycleController.rb.linearVelocity.magnitude;

        // Determine target amplitude based on speed threshold
        if (currentSpeed >= speedThreshold)
        {
            targetAmplitude = baseAmplitude * amplitudeMultiplier;
        }
        else
        {
            targetAmplitude = 0f;
        }

        // Smoothly lerp to target
        float currentAmplitude = Mathf.Lerp(
            perlinNoise.AmplitudeGain,
            targetAmplitude,
            Time.deltaTime * transitionSpeed
        );

        // Apply to Perlin Noise component
        perlinNoise.AmplitudeGain = currentAmplitude;
    }

    public void ScreenShake(Vector3 dir)
    {
        screenShake.GenerateImpulseWithVelocity(dir);
    }

    // Debug display
    //void OnGUI()
    //{
    //    if (Debug.isDebugBuild && bicycleController != null)
    //    {
    //        float speed = bicycleController.rb.linearVelocity.magnitude;
    //        GUI.Label(new Rect(10, 10, 400, 20),
    //            $"Speed: {speed:F1} | Perlin Active: {perlinNoise.AmplitudeGain > 0.01f}");
    //    }
    //}
}