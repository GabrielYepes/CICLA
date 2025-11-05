using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RailScript : MonoBehaviour
{
    public bool normalDir;
    public SplineContainer railSpline;
    public float totalSplineLength;

    private void Start()
    {
        railSpline = GetComponent<SplineContainer>();
        totalSplineLength = railSpline.CalculateLength();
    }

    // Converts local float3 positions to Vector3 world positions
    public Vector3 LocalToWorldConversion(float3 localPoint)
    {
        Vector3 worldPos = transform.TransformPoint(localPoint);
        return worldPos;
    }

    // Converts world Vector3 positions to local float3 positions
    public float3 WorldToLocalConversion(Vector3 worldPoint)
    {
        float3 localPos = transform.InverseTransformPoint(worldPoint);
        return localPos;
    }

    // Calculates the normalised time value for the rail's spline by evlating the player's position.
    public float CalculateTargetRailPoint(Vector3 playerPos, out Vector3 worldPosOnSpline)
    {
        float3 nearestPoint;
        float time;
        SplineUtility.GetNearestPoint(railSpline.Spline, WorldToLocalConversion(playerPos), out nearestPoint, out time);
        worldPosOnSpline = LocalToWorldConversion(nearestPoint);
        return time;
    }

    // Calculates the direction the player is going on the rail based on their direction during the initial collision.
    public void CalculateDirection(float3 railForward, Vector3 playerForward)
    {
        //This calculates the severity of the angle between the player's forward and the forward of the point on the spline.
        //90 degrees is the cutoff point as it's the perpendicular to the rail. Anything more than that and the player is clearly
        //facing the other direction to the rail point.
        float angle = Vector3.Angle(railForward, playerForward.normalized);
        if (angle > 90f)
            normalDir = false;
        else
            normalDir = true;
    }
}
