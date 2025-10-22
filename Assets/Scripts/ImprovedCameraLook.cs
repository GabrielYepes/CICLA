using UnityEngine;
using UnityEngine.InputSystem;

namespace SBPScripts
{
    public class ImprovedCameraLook : MonoBehaviour
    {
        Vector2 _mouseAbsolute;
        Vector2 _smoothMouse;

        public Vector2 clampInDegrees = new Vector2(360, 180);
        public Vector2 sensitivity = new Vector2(2, 2);
        public Vector2 gamepadSensitivity = new Vector2(100, 100); // Higher for gamepad
        public Vector2 smoothing = new Vector2(3, 3);
        public Vector2 targetDirection;
        public Vector2 targetCharacterDirection;

        [HideInInspector]
        public bool movement;
        public bool autoRotate;

        // Reference to input actions
        private InputSystem_Actions inputActions;
        private Vector2 lookInput;

        void Awake()
        {
            inputActions = new InputSystem_Actions();

            // Subscribe to Look action
            inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
            inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        }

        void OnEnable()
        {
            inputActions.Enable();
        }

        void OnDisable()
        {
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

            // Get raw mouse input for a cleaner reading on more sensitive mice.
            var mouseDelta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

            // Get gamepad right stick input from the new Input System
            var gamepadDelta = lookInput * Time.deltaTime;

            // Combine inputs - mouse uses frame-independent values, gamepad needs deltaTime
            var combinedDelta = mouseDelta + gamepadDelta * gamepadSensitivity.x / sensitivity.x;

            // Scale input against the sensitivity setting and multiply that against the smoothing value.
            combinedDelta = Vector2.Scale(combinedDelta, new Vector2(sensitivity.x * smoothing.x, sensitivity.y * smoothing.y));

            // Interpolate mouse movement over time to apply smoothing delta.
            _smoothMouse.x = Mathf.Lerp(_smoothMouse.x, combinedDelta.x, 1f / smoothing.x);
            _smoothMouse.y = Mathf.Lerp(_smoothMouse.y, combinedDelta.y, 1f / smoothing.y);

            // Find the absolute mouse movement value from point zero.
            _mouseAbsolute += _smoothMouse;

            if (_smoothMouse == Vector2.zero && autoRotate)
            {
                targetDirection = transform.localRotation.eulerAngles;
                _mouseAbsolute = Vector2.zero;
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
            }
        }
    }
}