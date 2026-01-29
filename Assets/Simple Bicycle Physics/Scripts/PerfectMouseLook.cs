using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace SBPScripts
{
    public class PerfectMouseLook : MonoBehaviour
    {
        Vector2 _mouseAbsolute;
        Vector2 _smoothMouse;

        public Vector2 clampInDegrees = new Vector2(360, 180);
        public Vector2 sensitivity = new Vector2(2, 2);
        [Tooltip("Sensitivity multiplier for gamepad right stick")]
        public float gamepadSensitivityMultiplier = 50f;
        [Tooltip("Input below this threshold is ignored (prevents drift/accidental taps)")]
        [Range(0.01f, 0.5f)]
        public float inputDeadzone = 0.15f;
        public Vector2 smoothing = new Vector2(3, 3);
        public Vector2 targetDirection;
        public Vector2 targetCharacterDirection;
        [HideInInspector]
        public bool movement;
        public bool autoRotate;
        [Tooltip("Time in seconds before auto-rotate starts after stick release")]
        [Range(0f, 5f)]
        public float autoRotateDelay = 1.5f;

        // Input System support
        private InputSystem_Actions inputActions;
        private Vector2 lookInput;
        private Mouse mouse;

        // Auto-rotate delay tracking
        private float timeSinceLastInput = 0f;

        void Awake()
        {
            inputActions = new InputSystem_Actions();

            // Subscribe to Look action (gamepad right stick)
            inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

            // Get mouse reference
            mouse = Mouse.current;
        }

        void OnEnable()
        {
            if (inputActions != null)
                inputActions.Enable();
        }

        void OnDisable()
        {
            if (inputActions != null)
                inputActions.Disable();
        }

        void Start()
        {
            // Set target direction to the camera's initial orientation.
            targetDirection = transform.localRotation.eulerAngles;
        }

        void LateUpdate()
        {
            // Allow the script to clamp based on a desired target value.
            var targetOrientation = Quaternion.Euler(targetDirection);
            var targetCharacterOrientation = Quaternion.Euler(targetCharacterDirection);

            // Get mouse delta using new Input System
            Vector2 mouseDelta = Vector2.zero;
            if (mouse != null)
            {
                mouseDelta = mouse.delta.ReadValue();
            }

            // Apply deadzone to gamepad input to prevent accidental taps/drift
            Vector2 gamepadInput = lookInput;
            if (gamepadInput.magnitude < inputDeadzone)
            {
                gamepadInput = Vector2.zero;
            }

            // Add gamepad right stick input (multiplied by deltaTime for frame-rate independence)
            var gamepadDelta = gamepadInput * gamepadSensitivityMultiplier * Time.deltaTime;

            // Combine mouse and gamepad input
            mouseDelta += gamepadDelta;

            // Scale input against the sensitivity setting and multiply that against the smoothing value.
            mouseDelta = Vector2.Scale(mouseDelta, new Vector2(sensitivity.x * smoothing.x, sensitivity.y * smoothing.y));

            // Interpolate mouse movement over time to apply smoothing delta.
            _smoothMouse.x = Mathf.Lerp(_smoothMouse.x, mouseDelta.x, 1f / smoothing.x);
            _smoothMouse.y = Mathf.Lerp(_smoothMouse.y, mouseDelta.y, 1f / smoothing.y);

            // Find the absolute mouse movement value from point zero.
            _mouseAbsolute += _smoothMouse;

            // Track time since last input for auto-rotate delay
            if (_smoothMouse == new Vector2(0, 0))
            {
                timeSinceLastInput += Time.deltaTime;
            }
            else
            {
                timeSinceLastInput = 0f;
            }

            // Only auto-rotate after the delay has passed
            if (_smoothMouse == new Vector2(0, 0) && autoRotate && timeSinceLastInput >= autoRotateDelay)
            {
                targetDirection = transform.localRotation.eulerAngles;
                _mouseAbsolute = new Vector2(0, 0);
                movement = false;

            }
            else
            {
                movement = true;
                // Clamp and apply the local x value first, so as not to be affected by world transforms.
                if (clampInDegrees.x < 360)
                    _mouseAbsolute.x = Mathf.Clamp(_mouseAbsolute.x, -clampInDegrees.x * 0.5f, clampInDegrees.x * 0.5f);

                // Then clamp and apply the global y value.
                if (clampInDegrees.y < 360)
                    _mouseAbsolute.y = Mathf.Clamp(_mouseAbsolute.y, -clampInDegrees.y * 0.5f, clampInDegrees.y * 0.5f);

                transform.localRotation = Quaternion.AngleAxis(-_mouseAbsolute.y, targetOrientation * Vector3.right) * targetOrientation;

                var yRotation = Quaternion.AngleAxis(_mouseAbsolute.x, transform.InverseTransformDirection(Vector3.up));
                transform.localRotation *= yRotation;
            }
        }

        void OnDestroy()
        {
            if (inputActions != null)
            {
                inputActions.Player.Look.performed -= ctx => lookInput = ctx.ReadValue<Vector2>();
                inputActions.Player.Look.canceled -= ctx => lookInput = Vector2.zero;
                inputActions.Dispose();
            }
        }
    }
}