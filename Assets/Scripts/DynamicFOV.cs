using UnityEngine;

namespace SBPScripts
{
    [RequireComponent(typeof(Camera))]
    public class DynamicFOV : MonoBehaviour
    {
        [Header("References")]
        public BicycleController bicycleController;

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

        private Camera cam;
        private float targetFOV;

        void Start()
        {
            cam = GetComponent<Camera>();

            // Auto-find bicycle controller if not assigned
            if (bicycleController == null)
            {
                bicycleController = FindObjectOfType<BicycleController>();
                if (bicycleController == null)
                {
                    Debug.LogError("DynamicFOV: No BicycleController found!");
                    enabled = false;
                    return;
                }
            }

            // Set initial FOV
            cam.fieldOfView = baseFOV;
            targetFOV = baseFOV;
        }

        void LateUpdate()
        {
            if (bicycleController == null) return;

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
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
        }

        // Debug visualization
        void OnGUI()
        {
            if (Debug.isDebugBuild && bicycleController != null)
            {
                float speed = bicycleController.rb.linearVelocity.magnitude;
                GUI.Label(new Rect(10, 10, 300, 20),
                    $"Speed: {speed:F1} | FOV: {cam.fieldOfView:F1}° (Target: {targetFOV:F1}°)");
            }
        }
    }
}