using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    public class BicycleDebugHUD : MonoBehaviour
    {
        [Header("References")]
        public BicycleController bicycleController;

        [Header("UI Settings")]
        public bool showHUD = true;
        public int fontSize = 18;
        public Color textColor = Color.white;

        // GUI style
        private GUIStyle style;
        private string debugText;

        // New Input System for toggle
        private Key toggleKey = Key.F1;

        void Start()
        {
            // Auto-find bicycle controller if not assigned
            if (bicycleController == null)
            {
                bicycleController = FindFirstObjectByType<BicycleController>();
                if (bicycleController == null)
                {
                    Debug.LogError("BicycleDebugHUD: No BicycleController found in scene!");
                }
            }

            // Setup GUI style
            style = new GUIStyle();
            style.fontSize = fontSize;
            style.normal.textColor = textColor;
            style.padding = new RectOffset(10, 10, 10, 10);
            style.fontStyle = FontStyle.Bold;
        }

        void Update()
        {
            // Toggle HUD with F1 (New Input System)
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                showHUD = !showHUD;
            }

            if (!showHUD || bicycleController == null) return;

            // Build debug string from actual bicycle state
            UpdateDebugText();
        }

        void UpdateDebugText()
        {
            // Extract values from BicycleController
            float velocity = bicycleController.rb.linearVelocity.magnitude;
            float forwardVelocity = bicycleController.transform.InverseTransformDirection(bicycleController.rb.linearVelocity).z;
            float acceleration = bicycleController.customAccelerationAxis;
            float rawAcceleration = bicycleController.rawCustomAccelerationAxis;
            float steerAngle = bicycleController.customSteerAxis;
            float leanAngle = bicycleController.customLeanAxis;
            bool isAirborne = bicycleController.isAirborne;
            bool isWheelieActive = bicycleController.wheelieInput && bicycleController.wheeliePower > 50f;
            bool isReversing = bicycleController.isReversing;
            float bunnyHopCharge = bicycleController.bunnyHopAmount;
            int bunnyHopState = bicycleController.bunnyHopInputState;

            // Additional useful values
            float currentTopSpeed = bicycleController.currentTopSpeed;
            float speedPercent = (velocity / currentTopSpeed) * 100f;
            float oscillation = bicycleController.cycleOscillation;
            float turnLean = bicycleController.turnLeanAmount;
            float crankSpeed = bicycleController.crankSpeed;

            // Check if sprinting (top speed mode)
            bool isSprinting = Mathf.Approximately(currentTopSpeed, bicycleController.topSpeed);

            // Build formatted string
            debugText = "=== BICYCLE DEBUG ===\n\n";

            // Velocity
            debugText += "<b>VELOCITY</b>\n";
            debugText += $"  Total: {velocity:F2} m/s ({velocity * 3.6f:F1} km/h)\n";
            debugText += $"  Forward: {forwardVelocity:F2} m/s\n";
            debugText += $"  Speed: {speedPercent:F1}% of max\n";
            debugText += $"  Top Speed: {currentTopSpeed:F1} (Max: {bicycleController.topSpeed:F1})\n\n";

            // Acceleration & Input
            debugText += "<b>ACCELERATION</b>\n";
            debugText += $"  Smoothed: {acceleration:F3}\n";
            debugText += $"  Raw Input: {rawAcceleration:F3}\n";
            debugText += $"  Reversing: {isReversing}\n\n";

            // Steering
            debugText += "<b>STEERING</b>\n";
            debugText += $"  Steer Axis: {steerAngle:F3}\n";
            debugText += $"  Lean Input: {leanAngle:F3}\n";
            debugText += $"  Turn Lean: {turnLean:F2}°\n";
            debugText += $"  Oscillation: {oscillation:F2}\n\n";

            // States
            debugText += "<b>STATES</b>\n";
            debugText += $"  OnAir: {(isAirborne ? "<color=yellow>TRUE</color>" : "false")}\n";
            debugText += $"  Wheelie: {(isWheelieActive ? "<color=cyan>TRUE</color>" : "false")}";
            if (bicycleController.wheelieInput)
                debugText += $" (Power: {bicycleController.wheeliePower:F0})";
            debugText += "\n";
            debugText += $"  Sprinting: {(isSprinting ? "<color=lime>TRUE</color>" : "false")}\n";
            debugText += $"  Bunny Hop: State={bunnyHopState} Charge={bunnyHopCharge:F2}\n\n";

            // Additional Info
            debugText += "<b>PHYSICS</b>\n";
            debugText += $"  Crank Speed: {crankSpeed:F1}°\n";
            debugText += $"  F-Wheel Force: {bicycleController.fWheelRb.GetComponent<ConfigurableJoint>().currentForce.magnitude:F0}N\n";
            debugText += $"  R-Wheel Force: {bicycleController.rWheelRb.GetComponent<ConfigurableJoint>().currentForce.magnitude:F0}N\n";

            debugText += "\n<color=gray>[F1] Toggle HUD</color>";
        }

        void OnGUI()
        {
            if (!showHUD || bicycleController == null) return;

            // Draw semi-transparent background
            Rect backgroundRect = new Rect(5, 5, 420, 480);
            GUI.Box(backgroundRect, "");

            // Draw debug text
            GUI.Label(new Rect(10, 10, 400, 470), debugText, style);
        }
    }
}