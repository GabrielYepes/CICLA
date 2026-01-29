using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Rail Script - Handles spline-based rail grinding
/// Attach this to any GameObject with a SplineContainer that you want to grind on
/// Make sure to tag the GameObject with "Rail"
/// </summary>
[RequireComponent(typeof(SplineContainer))]
public class RailScript : MonoBehaviour
{
    [Header("Rail Configuration")]
    [Tooltip("Direction the player is moving along the rail (calculated automatically)")]
    public bool normalDir;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    [SerializeField] private bool drawGizmos = true;

    // Internal references
    [HideInInspector] public SplineContainer railSpline;
    [HideInInspector] public float totalSplineLength;

    private void Start()
    {
        InitializeRail();
    }

    private void OnValidate()
    {
        // Initialize in editor for immediate feedback
        if (Application.isPlaying)
            return;

        InitializeRail();
    }

    private void InitializeRail()
    {
        railSpline = GetComponent<SplineContainer>();

        if (railSpline == null)
        {
            Debug.LogError($"RailScript on {gameObject.name}: SplineContainer component not found!");
            return;
        }

        totalSplineLength = railSpline.CalculateLength();

        // Ensure correct tag
        if (!gameObject.CompareTag("Rail"))
        {
            Debug.LogWarning($"RailScript on {gameObject.name}: GameObject should be tagged 'Rail'");
        }

        if (showDebug)
        {
            Debug.Log($"<color=cyan>RailScript on {gameObject.name}: Initialized (Length: {totalSplineLength:F2}m)</color>");
        }
    }

    /// <summary>
    /// Converts local float3 positions to Vector3 world positions
    /// </summary>
    public Vector3 LocalToWorldConversion(float3 localPoint)
    {
        Vector3 worldPos = transform.TransformPoint(localPoint);
        return worldPos;
    }

    /// <summary>
    /// Converts world Vector3 positions to local float3 positions
    /// </summary>
    public float3 WorldToLocalConversion(Vector3 worldPoint)
    {
        float3 localPos = transform.InverseTransformPoint(worldPoint);
        return localPos;
    }

    /// <summary>
    /// Calculates the normalized time value (0-1) for the rail's spline by evaluating the player's position.
    /// Also outputs the actual world position on the spline.
    /// </summary>
    public float CalculateTargetRailPoint(Vector3 playerPos, out Vector3 worldPosOnSpline)
    {
        float3 nearestPoint;
        float time;

        // Find nearest point on spline to player position
        SplineUtility.GetNearestPoint(
            railSpline.Spline,
            WorldToLocalConversion(playerPos),
            out nearestPoint,
            out time
        );

        worldPosOnSpline = LocalToWorldConversion(nearestPoint);

        if (showDebug)
        {
            Debug.Log($"<color=yellow>Rail point calculated - Time: {time:F3}, World pos: {worldPosOnSpline}</color>");
        }

        return time;
    }

    /// <summary>
    /// Calculates the direction the player is going on the rail based on their direction during the initial collision.
    /// Uses dot product to determine if player is moving along or against the rail's forward direction.
    /// </summary>
    public void CalculateDirection(float3 railForward, Vector3 playerForward)
    {
        // Calculate angle between player's forward and the rail's forward at the point of contact
        // 90 degrees is the cutoff point as it's perpendicular to the rail
        // Anything more than 90° means the player is facing opposite to the rail direction
        float angle = Vector3.Angle(railForward, playerForward.normalized);

        if (angle > 90f)
            normalDir = false; // Player moving backward along spline
        else
            normalDir = true;  // Player moving forward along spline

        if (showDebug)
        {
            Debug.Log($"<color=cyan>Direction calculated - Angle: {angle:F1}°, Normal Dir: {normalDir}</color>");
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        // Draw the rail spline in the scene view
        if (railSpline == null)
            railSpline = GetComponent<SplineContainer>();

        if (railSpline == null || railSpline.Spline == null)
            return;

        // Draw spline points
        Gizmos.color = Color.cyan;

        int segments = 50;
        Vector3 previousPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float3 localPos, tangent, up;

            SplineUtility.Evaluate(railSpline.Spline, t, out localPos, out tangent, out up);
            Vector3 worldPos = LocalToWorldConversion(localPos);

            if (i > 0)
            {
                Gizmos.DrawLine(previousPoint, worldPos);
            }

            previousPoint = worldPos;

            // Draw dots at regular intervals
            if (i % 5 == 0)
            {
                Gizmos.DrawSphere(worldPos, 0.1f);
            }
        }

        // Draw start and end points in different colors
        float3 startPos, startTan, startUp;
        float3 endPos, endTan, endUp;

        SplineUtility.Evaluate(railSpline.Spline, 0f, out startPos, out startTan, out startUp);
        SplineUtility.Evaluate(railSpline.Spline, 1f, out endPos, out endTan, out endUp);

        // Start point (green)
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(LocalToWorldConversion(startPos), 0.2f);

        // End point (red)
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(LocalToWorldConversion(endPos), 0.2f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || railSpline == null)
            return;

        // Draw more detailed info when selected
        Gizmos.color = Color.yellow;

        // Draw normal direction arrows along the spline
        int arrowCount = 10;
        for (int i = 0; i < arrowCount; i++)
        {
            float t = i / (float)(arrowCount - 1);
            float3 pos, tangent, up;

            SplineUtility.Evaluate(railSpline.Spline, t, out pos, out tangent, out up);
            Vector3 worldPos = LocalToWorldConversion(pos);

            // Draw up direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(worldPos, (Vector3)up * 0.5f);

            // Draw forward direction
            Gizmos.color = Color.yellow;
            //Gizmos.DrawRay(worldPos, (Vector3)tangent.normalized * 0.5f);
        }
    }
}