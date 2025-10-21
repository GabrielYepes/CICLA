using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    [RequireComponent(typeof(BicycleController))]
    public class BicycleInputBridge : MonoBehaviour
    {
        private BicycleController bicycleController;
        private InputSystem_Actions inputActions;

        // Input values
        private Vector2 moveInput;
        private bool sprintInput;
        private bool jumpInput;
        private bool wheelieInput;

        // For detecting button state changes
        private bool wasJumpPressed;

        [Header("Input Sensitivity")]
        [Tooltip("Multiplier for steering input (higher = more responsive)")]
        public float steerSensitivityMultiplier = 3f;
        [Tooltip("Deadzone for stick input (0-1)")]
        public float stickDeadzone = 0.15f;

        void Awake()
        {
            bicycleController = GetComponent<BicycleController>();

            // Initialize input actions
            inputActions = new InputSystem_Actions();

            // Subscribe to input events
            inputActions.Player.Move.performed += OnMove;
            inputActions.Player.Move.canceled += OnMove;

            inputActions.Player.Sprint.performed += ctx => OnSprint(ctx);
            inputActions.Player.Sprint.canceled += ctx => OnSprint(ctx);

            inputActions.Player.Jump.performed += ctx => OnJump(ctx);
            inputActions.Player.Jump.canceled += ctx => OnJump(ctx);

            inputActions.Player.Wheelie.performed += ctx => OnWheelie(ctx);
            inputActions.Player.Wheelie.canceled += ctx => OnWheelie(ctx);
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
            // Apply deadzone
            Vector2 processedInput = moveInput;
            if (processedInput.magnitude < stickDeadzone)
            {
                processedInput = Vector2.zero;
            }

            // Boost steering input to overcome sensitivity issues
            float boostedSteerInput = processedInput.x * steerSensitivityMultiplier;
            boostedSteerInput = Mathf.Clamp(boostedSteerInput, -1f, 1f);

            // Apply movement
            CustomInput(boostedSteerInput, ref bicycleController.customSteerAxis, 5, 5, false);
            CustomInput(processedInput.y, ref bicycleController.customAccelerationAxis, 1, 1, false);
            CustomInput(boostedSteerInput, ref bicycleController.customLeanAxis, 1, 1, false);
            CustomInput(processedInput.y, ref bicycleController.rawCustomAccelerationAxis, 1, 1, true);

            // DEBUG: Log what we're setting
            if (Mathf.Abs(processedInput.x) > 0.1f || Mathf.Abs(processedInput.y) > 0.1f)
            {
                Debug.Log($"<color=cyan>BRIDGE - Steer: {bicycleController.customSteerAxis:F2} | Accel: {bicycleController.customAccelerationAxis:F2} | Raw Input X: {moveInput.x:F2} → Boosted: {boostedSteerInput:F2}</color>");
            }

            // Bunny hop state machine: 0 = nothing, 1 = held, -1 = just released
            if (jumpInput && !wasJumpPressed)
            {
                bicycleController.bunnyHopInputState = 1;
            }
            else if (!jumpInput && wasJumpPressed)
            {
                bicycleController.bunnyHopInputState = -1;
            }
            else if (jumpInput)
            {
                bicycleController.bunnyHopInputState = 1;
            }
            else
            {
                bicycleController.bunnyHopInputState = 0;
            }

            wasJumpPressed = jumpInput;

            // Simple boolean inputs - Note: BicycleController checks these in FixedUpdate
            // Sprint is not a boolean in BicycleController, it's handled in ApplyCustomInput
            // We need to access the private 'sprint' variable... let's use reflection or find another way

            // Actually, looking at BicycleController line 440: sprint = Input.GetKey(KeyCode.LeftShift);
            // This is still being set by old input! We need to override it

            bicycleController.wheelieInput = wheelieInput;

            // DEBUG: Log button states
            if (sprintInput || jumpInput || wheelieInput)
            {
                Debug.Log($"<color=yellow>BUTTONS - Sprint: {sprintInput} | Jump: {jumpInput} (HopState: {bicycleController.bunnyHopInputState}) | Wheelie: {wheelieInput}</color>");
            }
        }

        // Replicate the CustomInput method from BicycleController
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

        void OnSprint(InputAction.CallbackContext context)
        {
            sprintInput = context.ReadValueAsButton();
            Debug.Log($"<color=orange>Sprint callback: {sprintInput} | Phase: {context.phase}</color>");
        }

        void OnJump(InputAction.CallbackContext context)
        {
            jumpInput = context.ReadValueAsButton();
            Debug.Log($"<color=green>Jump callback: {jumpInput} | Phase: {context.phase}</color>");
        }

        void OnWheelie(InputAction.CallbackContext context)
        {
            wheelieInput = context.ReadValueAsButton();
            Debug.Log($"<color=magenta>Wheelie callback: {wheelieInput} | Phase: {context.phase}</color>");
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (inputActions != null)
            {
                inputActions.Player.Move.performed -= OnMove;
                inputActions.Player.Move.canceled -= OnMove;

                inputActions.Player.Sprint.performed -= ctx => OnSprint(ctx);
                inputActions.Player.Sprint.canceled -= ctx => OnSprint(ctx);

                inputActions.Player.Jump.performed -= ctx => OnJump(ctx);
                inputActions.Player.Jump.canceled -= ctx => OnJump(ctx);

                inputActions.Player.Wheelie.performed -= ctx => OnWheelie(ctx);
                inputActions.Player.Wheelie.canceled -= ctx => OnWheelie(ctx);
            }
        }
    }
}