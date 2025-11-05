using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace SBPScripts
{
    /// <summary>
    /// Bike Grinding System - Adapted for multi-rigidbody bicycle
    /// Handles grinding on spline-based rails with physics suspension during grind
    /// </summary>
    [RequireComponent(typeof(BicycleController))]
    public class BikeGrindController : MonoBehaviour
    {
        [Header("Grind State")]
        [SerializeField] private bool isGrinding;

        [Header("Grind Settings")]
        [SerializeField] private float grindSpeed = 8f;
        [SerializeField] private float heightOffset = 0.5f;
        [SerializeField] private float lerpSpeed = 10f;

        [Header("Exit Settings")]
        [SerializeField] private float exitForceMultiplier = 1.5f;
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private bool preserveHorizontalMomentum = true;
        [Tooltip("Apply exit velocity to wheels as well (prevents initial physics jitter)")]
        [SerializeField] private bool syncWheelVelocity = true;
        [SerializeField] private bool allowJumpOffRail = true;

        [Header("Physics Wheel References")]
        [SerializeField] private GameObject rPhysicsWheel; // Back wheel - will be auto-assigned

        [Header("Debug")]
        [SerializeField] private bool showDebug = false;

        // Internal references
        private RailScript currentRailScript;
        private BicycleController bicycleController;
        private Rigidbody mainRigidbody;
        private Rigidbody fWheelRb;
        private Rigidbody rWheelRb;

        // Grind state
        private float timeForFullSpline;
        private float elapsedTime;
        private bool wasGrinding;

        // Physics state storage (for restoration after grind)
        private Vector3 velocityBeforeGrind;
        private Vector3 angularVelocityBeforeGrind;

        // Real-time velocity tracking during grind
        private Vector3 lastGrindPosition;
        private Vector3 currentGrindVelocity;

        // Input tracking
        private bool jumpInputPressed;

        private void Start()
        {
            InitializeReferences();
        }

        private void InitializeReferences()
        {
            bicycleController = GetComponent<BicycleController>();
            mainRigidbody = GetComponent<Rigidbody>();

            // Get wheel references from BicycleController if not manually assigned
            if (rPhysicsWheel == null)
            {
                rPhysicsWheel = bicycleController.rPhysicsWheel;
            }

            if (rPhysicsWheel != null)
            {
                rWheelRb = rPhysicsWheel.GetComponent<Rigidbody>();

                // Check for collision forwarder
                var forwarder = rPhysicsWheel.GetComponent<WheelRailCollisionForwarder>();
                if (forwarder == null)
                {
                    Debug.LogWarning("BikeGrindController: RPhysicsWheel needs WheelRailCollisionForwarder component! Add it for rail detection to work.");
                }
            }
            else
            {
                Debug.LogError("BikeGrindController: RPhysicsWheel not assigned!");
            }

            // Get front wheel RB
            if (bicycleController.fPhysicsWheel != null)
            {
                fWheelRb = bicycleController.fPhysicsWheel.GetComponent<Rigidbody>();
            }

            if (showDebug)
            {
                Debug.Log("<color=cyan>BikeGrindController: Initialized</color>");
            }
        }

        public void HandleJumpInput(bool pressed)
        {
            jumpInputPressed = pressed;

            // Allow jumping off rail during grind
            if (isGrinding && pressed && allowJumpOffRail)
            {
                if (showDebug) Debug.Log("<color=yellow>Jumping off rail!</color>");
                ExitGrind(true);
            }
        }

        private void FixedUpdate()
        {
            if (isGrinding)
            {
                // Keep grounded state during entire grind (prevents tricks, maintains proper animations)
                if (bicycleController != null)
                {
                    bicycleController.isAirborne = false;
                }

                MoveAlongRail();
            }
        }

        private void Update()
        {
            // Detect grind state changes
            if (isGrinding != wasGrinding)
            {
                if (isGrinding)
                {
                    OnGrindStart();
                }
                else
                {
                    OnGrindEnd();
                }
                wasGrinding = isGrinding;
            }
        }

        /// <summary>
        /// Called by WheelRailCollisionForwarder when a wheel hits a rail
        /// </summary>
        public void OnWheelHitRail(GameObject railObject, Collision collision)
        {
            if (isGrinding)
            {
                if (showDebug) Debug.Log("<color=yellow>Already grinding, ignoring rail collision</color>");
                return;
            }

            if (showDebug) Debug.Log($"<color=green>Rail collision forwarded from wheel!</color>");
            StartGrind(railObject);
        }

        private void StartGrind(GameObject railObject)
        {
            currentRailScript = railObject.GetComponent<RailScript>();

            if (currentRailScript == null)
            {
                Debug.LogError("BikeGrindController: Rail object missing RailScript component!");
                return;
            }

            if (showDebug) Debug.Log("<color=cyan>═══ STARTING GRIND ═══</color>");

            // Store current velocity for exit
            velocityBeforeGrind = mainRigidbody.linearVelocity;
            angularVelocityBeforeGrind = mainRigidbody.angularVelocity;

            // Initialize velocity tracking
            lastGrindPosition = Vector3.zero; // Will be set on first frame
            currentGrindVelocity = velocityBeforeGrind; // Start with entry velocity

            // Freeze physics
            SuspendPhysics();

            // Calculate initial rail position
            CalculateAndSetRailPosition();

            // Set grind state
            isGrinding = true;
        }

        private void SuspendPhysics()
        {
            // Make all rigidbodies kinematic
            mainRigidbody.isKinematic = true;

            if (fWheelRb != null)
                fWheelRb.isKinematic = true;

            if (rWheelRb != null)
                rWheelRb.isKinematic = true;

            // Force grounded state to prevent tricks during grind
            if (bicycleController != null)
            {
                bicycleController.isAirborne = false;
            }

            // Disable bicycle controller temporarily
            if (bicycleController != null)
                bicycleController.enabled = false;

            if (showDebug) Debug.Log("<color=yellow>Physics suspended for grind (isAirborne = false)</color>");
        }

        private void ResumePhysics()
        {
            // Restore rigidbodies
            mainRigidbody.isKinematic = false;

            if (fWheelRb != null)
                fWheelRb.isKinematic = false;

            if (rWheelRb != null)
                rWheelRb.isKinematic = false;

            // Re-enable bicycle controller
            if (bicycleController != null)
                bicycleController.enabled = true;

            if (showDebug) Debug.Log("<color=yellow>Physics resumed</color>");
        }

        private void CalculateAndSetRailPosition()
        {
            // Calculate time needed to traverse full spline
            timeForFullSpline = currentRailScript.totalSplineLength / grindSpeed;

            // Find nearest point on spline to bike's current position
            Vector3 splinePoint;
            float normalisedTime = currentRailScript.CalculateTargetRailPoint(transform.position, out splinePoint);
            elapsedTime = timeForFullSpline * normalisedTime;

            // Get spline data at this position
            float3 pos, forward, up;
            SplineUtility.Evaluate(currentRailScript.railSpline.Spline, normalisedTime, out pos, out forward, out up);

            // Calculate grind direction based on bike's forward direction
            currentRailScript.CalculateDirection(forward, transform.forward);

            // Snap bike to rail position
            transform.position = splinePoint + (Vector3)up * heightOffset;

            if (showDebug)
            {
                Debug.Log($"<color=cyan>Rail position set - Progress: {normalisedTime:F2}, Direction: {(currentRailScript.normalDir ? "Forward" : "Backward")}</color>");
            }
        }

        private void MoveAlongRail()
        {
            if (currentRailScript == null || !isGrinding)
                return;

            // Calculate progress along rail (0 to 1)
            float progress = elapsedTime / timeForFullSpline;

            // Check if we've reached the end of the rail
            if (progress < 0 || progress > 1)
            {
                if (showDebug) Debug.Log("<color=yellow>Reached end of rail</color>");
                ExitGrind(false);
                return;
            }

            // Calculate next frame's progress for rotation calculation
            float nextTimeNormalised;
            if (currentRailScript.normalDir)
                nextTimeNormalised = (elapsedTime + Time.fixedDeltaTime) / timeForFullSpline;
            else
                nextTimeNormalised = (elapsedTime - Time.fixedDeltaTime) / timeForFullSpline;

            // Get current and next positions on spline
            float3 pos, tangent, up;
            float3 nextPos, nextTan, nextUp;
            SplineUtility.Evaluate(currentRailScript.railSpline.Spline, progress, out pos, out tangent, out up);
            SplineUtility.Evaluate(currentRailScript.railSpline.Spline, nextTimeNormalised, out nextPos, out nextTan, out nextUp);

            // Convert to world space
            Vector3 worldPos = currentRailScript.LocalToWorldConversion(pos);
            Vector3 nextWorldPos = currentRailScript.LocalToWorldConversion(nextPos);

            // CALCULATE ACTUAL VELOCITY - Track position change per frame
            if (lastGrindPosition != Vector3.zero) // Skip first frame
            {
                currentGrindVelocity = (worldPos - lastGrindPosition) / Time.fixedDeltaTime;

                if (showDebug && Time.frameCount % 30 == 0) // Log every 30 frames
                {
                    Debug.Log($"<color=cyan>Grind Velocity: {currentGrindVelocity.magnitude:F2} m/s</color>");
                }
            }
            lastGrindPosition = worldPos;

            // Set bike position on rail (with height offset)
            transform.position = worldPos + (Vector3)up * heightOffset;

            // Smoothly rotate bike to face direction of travel
            Vector3 lookDirection = nextWorldPos - worldPos;
            if (lookDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lerpSpeed * Time.fixedDeltaTime);
            }

            // Match rail's up direction
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.FromToRotation(transform.up, up) * transform.rotation,
                lerpSpeed * Time.fixedDeltaTime
            );

            // Update elapsed time based on direction
            if (currentRailScript.normalDir)
                elapsedTime += Time.fixedDeltaTime;
            else
                elapsedTime -= Time.fixedDeltaTime;
        }

        private void ExitGrind(bool jumped)
        {
            if (!isGrinding)
                return;

            if (showDebug) Debug.Log($"<color=yellow>═══ EXITING GRIND (Jumped: {jumped}) ═══</color>");

            // Calculate exit velocity BEFORE resuming physics (use actual grind velocity)
            Vector3 exitVelocity;

            if (jumped)
            {
                // JUMP OFF RAIL - Carry forward momentum + add upward jump force
                if (preserveHorizontalMomentum)
                {
                    // Use the ACTUAL velocity we had while grinding (full momentum transfer)
                    exitVelocity = currentGrindVelocity * exitForceMultiplier;
                    exitVelocity.y += jumpForce; // Add upward jump (independent of forward speed)
                }
                else
                {
                    // Use transform forward (direction only, not actual velocity)
                    exitVelocity = transform.forward * currentGrindVelocity.magnitude * exitForceMultiplier;
                    exitVelocity.y += jumpForce;
                }

                if (showDebug)
                {
                    Debug.Log($"<color=green>Jump exit velocity: {exitVelocity.magnitude:F2} m/s (grind speed was: {currentGrindVelocity.magnitude:F2})</color>");
                }
            }
            else
            {
                // NATURAL EXIT (reached end of rail) - Maintain grind velocity
                exitVelocity = currentGrindVelocity * exitForceMultiplier;

                if (showDebug)
                {
                    Debug.Log($"<color=green>Natural exit velocity: {exitVelocity.magnitude:F2} m/s</color>");
                }
            }

            // Resume physics
            ResumePhysics();

            // Apply the calculated exit velocity to main rigidbody
            mainRigidbody.linearVelocity = exitVelocity;

            // Optionally apply to wheels for consistency (prevents weird initial physics)
            if (syncWheelVelocity)
            {
                if (fWheelRb != null)
                    fWheelRb.linearVelocity = exitVelocity;
                if (rWheelRb != null)
                    rWheelRb.linearVelocity = exitVelocity;
            }

            // Clear grind state
            isGrinding = false;
            currentRailScript = null;
            lastGrindPosition = Vector3.zero;
            currentGrindVelocity = Vector3.zero;
        }

        private void OnGrindStart()
        {
            if (showDebug) Debug.Log("<color=green>GRIND STATE: ACTIVE</color>");

            // You can add visual/audio feedback here
            // Example: Play grind sound, spawn particles, etc.
        }

        private void OnGrindEnd()
        {
            if (showDebug) Debug.Log("<color=red>GRIND STATE: INACTIVE</color>");

            // Clean up any grind effects here
        }

        // Public getters for other systems
        public bool IsGrinding => isGrinding;
        public RailScript CurrentRail => currentRailScript;
        public float GrindProgress => isGrinding ? (elapsedTime / timeForFullSpline) : 0f;

        private void OnDrawGizmos()
        {
            if (!showDebug || !isGrinding || currentRailScript == null)
                return;

            // Draw current position on rail
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw rail direction
            Gizmos.color = currentRailScript.normalDir ? Color.green : Color.red;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}