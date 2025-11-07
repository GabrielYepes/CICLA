using UnityEngine;
using Unity.Cinemachine;

namespace SBPScripts
{
    public class DynamicFOV : MonoBehaviour
    {
        [Header("References")]
        public BicycleController bicycleController;

        [Tooltip("The Cinemachine camera to control FOV on")]
        public CinemachineCamera cinemachineCamera;

        [Header("FOV Settings")]
        [Tooltip("Base FOV when stationary")]
        public float baseFOV = 60f;

        [Tooltip("Maximum FOV at top speed")]
        public float maxFOV = 75f;

        [Tooltip("Speed at which max FOV is reached")]
        public float maxSpeedThreshold = 30f;

        [Tooltip("How quickly FOV changes")]
        [Range(1f, 10f)]
        public float fovTransitionSpeed = 3f;

        [Header("Advanced")]
        [Tooltip("Use animation curve for non-linear FOV scaling")]
        public bool useCustomCurve = false;
        public AnimationCurve fovCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private float targetFOV;
        private float currentFOV;

        void Start()
        {
            // Auto-find Cinemachine camera if not assigned
            if (cinemachineCamera == null)
            {
                cinemachineCamera = GetComponent<CinemachineCamera>();

                if (cinemachineCamera == null)
                {
                    cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
                }

                if (cinemachineCamera == null)
                {
                    Debug.LogError("DynamicFOV: No CinemachineCamera found!");
                    enabled = false;
                    return;
                }
            }

            // Auto-find bicycle controller if not assigned
            if (bicycleController == null)
            {
                bicycleController = FindFirstObjectByType<BicycleController>();
                if (bicycleController == null)
                {
                    Debug.LogError("DynamicFOV: No BicycleController found!");
                    enabled = false;
                    return;
                }
            }

            // Set initial FOV
            cinemachineCamera.Lens.FieldOfView = baseFOV;
            currentFOV = baseFOV;
            targetFOV = baseFOV;
        }

        void LateUpdate()
        {
            if (bicycleController == null || cinemachineCamera == null) return;

            // Get current speed
            float currentSpeed = bicycleController.rb.linearVelocity.magnitude;

            // Calculate speed ratio (0 to 1)
            float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeedThreshold);

            // Apply custom curve if enabled
            if (useCustomCurve)
            {
                speedRatio = fovCurve.Evaluate(speedRatio);
            }

            // Calculate target FOV
            targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);

            // Smoothly transition to target FOV
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * fovTransitionSpeed);

            // Apply to Cinemachine camera
            cinemachineCamera.Lens.FieldOfView = currentFOV;
        }

        // Debug visualization
        void OnGUI()
        {
            if (Debug.isDebugBuild && bicycleController != null)
            {
                float speed = bicycleController.rb.linearVelocity.magnitude;
                GUI.Label(new Rect(10, 10, 300, 20),
                    $"Speed: {speed:F1} | FOV: {currentFOV:F1}° (Target: {targetFOV:F1}°)");
            }
        }
    }
}