using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SBPScripts
{
    /// <summary>
    /// Dynamically controls motion blur intensity based on bicycle speed.
    /// Works with both URP and Built-in pipeline post-processing.
    /// </summary>
    public class DynamicMotionBlur : MonoBehaviour
    {
        [Header("References")]
        public BicycleController bicycleController;
        public Volume postProcessVolume; // For URP

        [Header("Speed Settings")]
        [Tooltip("Speed below this has no blur")]
        public float minSpeedThreshold = 10f;

        [Tooltip("Speed at which maximum blur is reached")]
        public float maxSpeedThreshold = 40f;

        [Header("Blur Settings")]
        [Tooltip("Maximum blur intensity (0-1)")]
        [Range(0f, 1f)]
        public float maxBlurIntensity = 0.5f;

        [Tooltip("How quickly blur fades in/out")]
        [Range(1f, 10f)]
        public float transitionSpeed = 3f;

        [Header("Advanced")]
        [Tooltip("Use custom curve for blur scaling")]
        public bool useCustomCurve = false;
        public AnimationCurve blurCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private float currentBlurIntensity = 0f;

        // URP Motion Blur (you'll need to add using statement if using URP)
        // private UnityEngine.Rendering.Universal.MotionBlur motionBlur;

        void Start()
        {
            // Auto-find bicycle controller
            if (bicycleController == null)
            {
                bicycleController = FindObjectOfType<BicycleController>();
                if (bicycleController == null)
                {
                    Debug.LogError("DynamicMotionBlur: No BicycleController found!");
                    enabled = false;
                    return;
                }
            }

            // Auto-find post-process volume if not assigned
            if (postProcessVolume == null)
            {
                postProcessVolume = FindObjectOfType<Volume>();
            }

            /* 
            // Uncomment if using URP:
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                if (!postProcessVolume.profile.TryGet(out motionBlur))
                {
                    Debug.LogWarning("DynamicMotionBlur: No Motion Blur effect found in Volume Profile!");
                }
            }
            */
        }

        void Update()
        {
            if (bicycleController == null) return;

            // Get current speed
            float currentSpeed = bicycleController.rb.linearVelocity.magnitude;

            // Calculate speed ratio (0 to 1) within threshold range
            float speedRatio = Mathf.Clamp01(
                (currentSpeed - minSpeedThreshold) / (maxSpeedThreshold - minSpeedThreshold)
            );

            // Apply custom curve if enabled
            if (useCustomCurve)
            {
                speedRatio = blurCurve.Evaluate(speedRatio);
            }

            // Calculate target blur intensity
            float targetBlurIntensity = speedRatio * maxBlurIntensity;

            // Smoothly transition to target
            currentBlurIntensity = Mathf.Lerp(
                currentBlurIntensity,
                targetBlurIntensity,
                Time.deltaTime * transitionSpeed
            );

            // Apply to post-processing
            ApplyBlur(currentBlurIntensity);
        }

        void ApplyBlur(float intensity)
        {
            /* 
            // FOR URP - Uncomment this section and comment out the warning below:
            
            if (motionBlur != null)
            {
                motionBlur.intensity.value = intensity;
            }
            */

            /* 
            // FOR BUILT-IN PIPELINE - You'll need to add the Post-processing stack v2 package
            // and modify this based on your setup. Example:
            
            if (postProcessVolume != null)
            {
                UnityEngine.Rendering.PostProcessing.MotionBlur blur;
                if (postProcessVolume.profile.TryGetSettings(out blur))
                {
                    blur.shutterAngle.value = intensity * 360f; // Scale to shutter angle
                }
            }
            */

            // Placeholder - implement based on your render pipeline
            // Debug.Log($"Motion Blur Intensity: {intensity:F2}");
        }

        // Debug visualization
        void OnGUI()
        {
            if (Debug.isDebugBuild && bicycleController != null)
            {
                float speed = bicycleController.rb.linearVelocity.magnitude;
                GUI.Label(new Rect(10, 30, 300, 20),
                    $"Blur: {currentBlurIntensity:F2} (Speed: {speed:F1})");
            }
        }
    }
}

/*
=== IMPLEMENTATION NOTES ===

This script is a template that needs to be customized for your render pipeline:

FOR URP (Universal Render Pipeline):
1. Add this using statement at the top:
   using UnityEngine.Rendering.Universal;
   
2. Uncomment the URP sections marked above
3. Make sure your Volume has a Motion Blur override

FOR BUILT-IN PIPELINE:
1. Install Post-processing Stack v2 from Package Manager
2. Add this using statement:
   using UnityEngine.Rendering.PostProcessing;
   
3. Uncomment the Built-in sections marked above
4. Adjust the blur parameter names based on your version

The core logic (speed detection and smoothing) works regardless of pipeline!
*/