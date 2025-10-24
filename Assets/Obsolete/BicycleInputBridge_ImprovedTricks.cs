using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    [RequireComponent(typeof(BicycleController))]
    public class BicycleInputBridge_ImprovedTricks : MonoBehaviour
    {
        private BicycleController bicycleController;
        private ImprovedTrickSystem trickSystem;
        private InputSystem_Actions inputActions;

        // Input values
        private Vector2 moveInput;
        private bool accelerateInput;  // ZR trigger
        private bool brakeInput;       // ZL trigger
        private bool sprintInput;
        private bool jumpInput;
        private bool wheelieInput;

        // NEW: Trick button inputs
        private bool frontflipInput;   // North button (Y on Switch)
        private bool backflipInput;    // South button (B on Switch)

        // For detecting button state changes
        private bool wasJumpPressed;
        private bool wasFrontflipPressed;
        private bool wasBackflipPressed;

        [Header("Input Sensitivity")]
        public float steerSensitivityMultiplier = 5f;
        public float stickDeadzone = 0.15f;

        [Header("Trigger Settings")]
        [Tooltip("How fast triggers ramp up when pressed")]
        public float triggerPressSpeed = 3f;

        [Tooltip("How fast triggers release (should be faster than press)")]
        public float triggerReleaseSpeed = 9f;

        [Tooltip("Threshold to snap to zero (prevents lingering values)")]
        public float triggerSnapThreshold = 0.05f;

        [Header("Steering")]
        public bool smoothSteering = false;
        [Range(1f, 20f)]
        public float steeringSmoothSpeed = 10f;

        private float smoothedSteer = 0f;

        [Header("Air Trick Prevention")]
        [Tooltip("Prevent ZR/ZL from causing tricks in the air")]
        public bool preventAccelTricks = true;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Smooth trigger values
        private float smoothedAcceleration = 0f;
        private float smoothedAccelerationForTricks = 0f; // Separate value for tricks

        void Awake()
        {
            bicycleController = GetComponent<BicycleController>();
            trickSystem = GetComponent<ImprovedTrickSystem>();

            if (trickSystem == null)
            {
                Debug.LogWarning("ImprovedTrickSystem not found! Add it to use improved tricks.");
            }

            inputActions = new InputSystem_Actions();

            // Subscribe to input events
            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;

            inputActions.Player.Accelerate.performed += ctx => OnAccelerate(ctx);
            inputActions.Player.Accelerate.canceled += ctx => OnAccelerate(ctx);

            inputActions.Player.Brake.performed += ctx => OnBrake(ctx);
            inputActions.Player.Brake.canceled += ctx => OnBrake(ctx);

            inputActions.Player.Sprint.performed += ctx => OnSprint(ctx);
            inputActions.Player.Sprint.canceled += ctx => OnSprint(ctx);

            inputActions.Player.Jump.performed += ctx => OnJump(ctx);
            inputActions.Player.Jump.canceled += ctx => OnJump(ctx);

            inputActions.Player.Wheelie.performed += ctx => OnWheelie(ctx);
            inputActions.Player.Wheelie.canceled += ctx => OnWheelie(ctx);

            // NEW: Subscribe to trick buttons
            // TEMPORARY: Using Previous (buttonNorth/X) and Next (buttonEast/A) 
            // You'll want to add proper Frontflip/Backflip actions in Input Actions asset
            inputActions.Player.FrontFlip.performed += ctx => OnFrontflip(ctx); // X button (North)
            inputActions.Player.FrontFlip.canceled += ctx => OnFrontflip(ctx);

            inputActions.Player.BackFlip.performed += ctx => OnBackflip(ctx); // Y button (West) 
            inputActions.Player.BackFlip.canceled += ctx => OnBackflip(ctx);

            Debug.Log("<color=green>BicycleInputBridge_ImprovedTricks: Initialized with trick button support</color>");
        }

        void OnEnable()
        {
            inputActions.Enable();
        }

        void OnDisable()
        {
            inputActions.Disable();
        }

        void Update()
        {
            ApplyInputToBicycle();
        }

        void ApplyInputToBicycle()
        {
            // Process steering from left stick X only
            float steerInput = moveInput.x;
            if (Mathf.Abs(steerInput) < stickDeadzone)
            {
                steerInput = 0f;
            }

            // Boost steering
            float boostedSteerInput = steerInput * steerSensitivityMultiplier;
            boostedSteerInput = Mathf.Clamp(boostedSteerInput, -1f, 1f);

            // Process triggers for acceleration/braking
            float targetAcceleration = 0f;
            if (accelerateInput) targetAcceleration = 1f;
            if (brakeInput) targetAcceleration = -1f;
            if (accelerateInput && brakeInput) targetAcceleration = -1f;

            // Smooth acceleration with different speeds for press vs release
            float accelerationSpeed;
            if (Mathf.Abs(targetAcceleration) > Mathf.Abs(smoothedAcceleration))
            {
                accelerationSpeed = triggerPressSpeed;
            }
            else
            {
                accelerationSpeed = triggerReleaseSpeed;
            }

            smoothedAcceleration = Mathf.Lerp(smoothedAcceleration, targetAcceleration, Time.deltaTime * accelerationSpeed);

            if (Mathf.Abs(smoothedAcceleration) < triggerSnapThreshold && targetAcceleration == 0f)
            {
                smoothedAcceleration = 0f;
            }

            // FIX ISSUE #1: Separate acceleration for tricks
            if (preventAccelTricks && bicycleController.stuntMode && bicycleController.isAirborne)
            {
                // When in the air doing tricks, don't let ZR/ZL affect trick rotation
                smoothedAccelerationForTricks = 0f;

                if (showDebugLogs)
                {
                    Debug.Log("<color=orange>Air tricks: Acceleration input disabled for tricks</color>");
                }
            }
            else
            {
                // Normal behavior - acceleration affects movement
                smoothedAccelerationForTricks = smoothedAcceleration;
            }

            // DIRECT or SMOOTHED STEERING
            if (smoothSteering)
            {
                smoothedSteer = Mathf.Lerp(smoothedSteer, boostedSteerInput, Time.deltaTime * steeringSmoothSpeed);
                bicycleController.customSteerAxis = smoothedSteer;
                bicycleController.customLeanAxis = smoothedSteer;
            }
            else
            {
                bicycleController.customSteerAxis = boostedSteerInput;
                bicycleController.customLeanAxis = boostedSteerInput;
            }

            // Apply acceleration (always use smoothed for movement)
            bicycleController.customAccelerationAxis = smoothedAcceleration;

            // BUT use the trick-safe version for the raw axis (used in trick physics)
            bicycleController.rawCustomAccelerationAxis = smoothedAccelerationForTricks;

            // Debug logging
            if (showDebugLogs && (Mathf.Abs(steerInput) > 0.1f || Mathf.Abs(targetAcceleration) > 0.1f || Mathf.Abs(smoothedAcceleration) > 0.05f))
            {
                Debug.Log($"<color=cyan>Steer: {bicycleController.customSteerAxis:F2} | Accel: {smoothedAcceleration:F2} | Raw (Tricks): {smoothedAccelerationForTricks:F2}</color>");
            }

            // Bunny hop state machine
            if (jumpInput && !wasJumpPressed)
                bicycleController.bunnyHopInputState = 1;
            else if (!jumpInput && wasJumpPressed)
                bicycleController.bunnyHopInputState = -1;
            else if (jumpInput)
                bicycleController.bunnyHopInputState = 1;
            else
                bicycleController.bunnyHopInputState = 0;

            wasJumpPressed = jumpInput;

            // SET ALL BOOLEAN STATES
            bicycleController.wheelieInput = wheelieInput;
            bicycleController.sprint = sprintInput;

            // FIX ISSUE #2: Handle trick buttons
            if (trickSystem != null)
            {
                // Only trigger on button press, not hold
                if (frontflipInput && !wasFrontflipPressed)
                {
                    trickSystem.OnFrontflipButton(true);
                }
                else if (!frontflipInput && wasFrontflipPressed)
                {
                    trickSystem.OnFrontflipButton(false);
                }

                if (backflipInput && !wasBackflipPressed)
                {
                    trickSystem.OnBackflipButton(true);
                }
                else if (!backflipInput && wasBackflipPressed)
                {
                    trickSystem.OnBackflipButton(false);
                }

                wasFrontflipPressed = frontflipInput;
                wasBackflipPressed = backflipInput;
            }

            if (showDebugLogs && (jumpInput || wheelieInput || sprintInput || frontflipInput || backflipInput))
            {
                Debug.Log($"<color=yellow>BUTTONS - Sprint: {sprintInput} | Jump: {jumpInput} | Wheelie: {wheelieInput} | Frontflip: {frontflipInput} | Backflip: {backflipInput}</color>");
            }
        }

        float CustomInput(float inputValue, ref float axis, float sensitivity, float gravity, bool isRaw)
        {
            float r = inputValue;
            float s = sensitivity;
            float g = gravity;
            float t = Time.unscaledDeltaTime;

            if (isRaw)
                axis = r;
            else
            {
                if (r != 0)
                    axis = Mathf.Clamp(axis + r * s * t, -1f, 1f);
                else
                    axis = Mathf.Clamp01(Mathf.Abs(axis) - g * t) * Mathf.Sign(axis);
            }

            return axis;
        }

        // Input callbacks
        void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        void OnAccelerate(InputAction.CallbackContext context)
        {
            if (context.performed)
                accelerateInput = true;
            else if (context.canceled)
                accelerateInput = false;
        }

        void OnBrake(InputAction.CallbackContext context)
        {
            if (context.performed)
                brakeInput = true;
            else if (context.canceled)
                brakeInput = false;
        }

        void OnSprint(InputAction.CallbackContext context)
        {
            if (context.performed)
                sprintInput = true;
            else if (context.canceled)
                sprintInput = false;
        }

        void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed)
                jumpInput = true;
            else if (context.canceled)
                jumpInput = false;
        }

        void OnWheelie(InputAction.CallbackContext context)
        {
            if (context.performed)
                wheelieInput = true;
            else if (context.canceled)
                wheelieInput = false;
        }

        // NEW: Trick button callbacks
        void OnFrontflip(InputAction.CallbackContext context)
        {
            if (context.performed)
                frontflipInput = true;
            else if (context.canceled)
                frontflipInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=magenta>North Button (Frontflip): {frontflipInput}</color>");
        }

        void OnBackflip(InputAction.CallbackContext context)
        {
            if (context.performed)
                backflipInput = true;
            else if (context.canceled)
                backflipInput = false;

            if (showDebugLogs)
                Debug.Log($"<color=magenta>South Button (Backflip): {backflipInput}</color>");
        }

        void OnDestroy()
        {
            if (inputActions != null)
            {
                inputActions.Player.Move.performed -= OnMove;
                inputActions.Player.Move.canceled -= OnMove;

                inputActions.Player.Accelerate.performed -= ctx => OnAccelerate(ctx);
                inputActions.Player.Accelerate.canceled -= ctx => OnAccelerate(ctx);

                inputActions.Player.Brake.performed -= ctx => OnBrake(ctx);
                inputActions.Player.Brake.canceled -= ctx => OnBrake(ctx);

                inputActions.Player.Sprint.performed -= ctx => OnSprint(ctx);
                inputActions.Player.Sprint.canceled -= ctx => OnSprint(ctx);

                inputActions.Player.Jump.performed -= ctx => OnJump(ctx);
                inputActions.Player.Jump.canceled -= ctx => OnJump(ctx);

                inputActions.Player.Wheelie.performed -= ctx => OnWheelie(ctx);
                inputActions.Player.Wheelie.canceled -= ctx => OnWheelie(ctx);

                inputActions.Player.FrontFlip.performed -= ctx => OnFrontflip(ctx);
                inputActions.Player.FrontFlip.canceled -= ctx => OnFrontflip(ctx);

                inputActions.Player.BackFlip.performed -= ctx => OnBackflip(ctx);
                inputActions.Player.BackFlip.canceled -= ctx => OnBackflip(ctx);
            }
        }
    }
}