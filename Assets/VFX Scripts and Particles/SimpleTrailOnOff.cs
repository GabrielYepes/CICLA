using UnityEngine;
using SBPScripts; // Make sure this matches your namespace

/// <summary>
/// Controls the visibility of the bike's trails based on current speed (in km/h or m/s)
/// </summary>
[RequireComponent(typeof(BicycleController))]
public class BikeTrailController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BicycleController bicycleController;
    [SerializeField] private BikeGrindController grindController;
    [SerializeField] private TrailRenderer[] trails;

    [Header("Settings")]
    [SerializeField] private bool useHorizontalSpeedOnly = true;
    [SerializeField] private bool showInKMH = true;
    [SerializeField] private float speedThreshold = 15f;  // km/h if showInKMH = true
    [SerializeField] private float fadeSpeed = 5f;        // optional fade
    [SerializeField] private float maxTrailWidth = 0.3f;

    private Rigidbody rb;
    private bool trailsActive;

    void Start()
    {
        // Auto-find references if needed
        if (bicycleController == null)
            bicycleController = GetComponent<BicycleController>();

        if (bicycleController != null)
            rb = bicycleController.GetComponent<Rigidbody>();

        if (grindController == null)
            grindController = FindObjectOfType<BikeGrindController>();

        if (trails == null || trails.Length == 0)
            trails = GetComponentsInChildren<TrailRenderer>();

        foreach (var t in trails)
        {
            t.emitting = false;
            t.startWidth = 0f;
            t.endWidth = 0f;
        }
    }

    void Update()
    {
        float speed = GetCurrentSpeed();

        bool shouldEmit = speed >= speedThreshold;

        if (shouldEmit != trailsActive)
        {
            trailsActive = shouldEmit;
            StopAllCoroutines();
            StartCoroutine(FadeTrails(trailsActive));
        }
    }

    private float GetCurrentSpeed()
    {
        if (grindController != null && grindController.IsGrinding)
        {
            float grindSpeed = grindController.GrindSpeed;
            return showInKMH ? grindSpeed * 3.6f : grindSpeed;
        }

        if (rb == null) return 0f;

        Vector3 velocity = rb.linearVelocity;
        if (useHorizontalSpeedOnly) velocity.y = 0f;

        float speed = velocity.magnitude;
        if (showInKMH) speed *= 3.6f;

        return speed;
    }

    private System.Collections.IEnumerator FadeTrails(bool enable)
    {
        float duration = 1f / fadeSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = enable ? t : (1 - t);

            foreach (var trail in trails)
            {
                trail.emitting = enable;
                trail.startWidth = Mathf.Lerp(0f, maxTrailWidth, alpha);
                trail.endWidth = trail.startWidth * 0.5f;
            }

            yield return null;
        }

        foreach (var trail in trails)
        {
            trail.emitting = enable;
            if (!enable)
            {
                trail.startWidth = 0f;
                trail.endWidth = 0f;
            }
        }
    }
}
