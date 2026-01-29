using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CullingManager : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("Drag your main camera here (auto-finds if empty)")]
    public Camera mainCamera;

    [Header("Detection Settings")]
    public float m_occlusionCapsuleHeight = 0f;
    public float m_occlusionCapsuleRadius = 1f;

    [Header("Important Objects")]
    [Tooltip("Objects that should always be visible (e.g., player)")]
    public List<GameObject> m_importantObjects = new List<GameObject>();

    [Tooltip("Include mouse cursor position as important")]
    public bool m_includeMouse;

    [Header("Layer Settings")]
    [Tooltip("Only objects on this layer will be culled")]
    public LayerMask m_layerMask;

    [Header("Debug Visualization")]
    public bool showDebugGizmos = false;

    // List of all the objects that we've set to occluding state
    private List<Cullable> m_occludingObjects = new List<Cullable>();

    void Start()
    {
        // Try to find camera if not assigned
        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                mainCamera = Object.FindFirstObjectByType<Camera>();
            }

            if (mainCamera == null)
            {
                Debug.LogError("CullingManager: No camera found! Please assign mainCamera in Inspector.");
            }
        }
    }

    void Update()
    {
        // Can only do occlusion checks if we have a camera
        if (mainCamera == null) return;

        // This is the list of positions we're trying not to occlude
        List<Vector3> importantPositions = FindImportantPositions();

        // This is the list of objects which are in the way
        List<Cullable> newOccludingObjects = FindOccludingObjects(importantPositions);

        SetOccludingObjects(newOccludingObjects);
    }

    private List<Vector3> FindImportantPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        // All units are important
        foreach (GameObject unit in m_importantObjects)
        {
            if (unit != null)
            {
                positions.Add(unit.transform.position);
            }
        }

        // Include mouse position if enabled
        if (m_includeMouse && mainCamera != null)
        {
            if (Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100, m_layerMask))
            {
                Vector3 mousePos = hit.point;
                if (!positions.Contains(mousePos))
                {
                    positions.Add(mousePos);
                }
            }
        }

        return positions;
    }

    // Update the stored list of occluding objects
    private void SetOccludingObjects(List<Cullable> newList)
    {
        foreach (Cullable cullable in newList)
        {
            int foundIndex = m_occludingObjects.IndexOf(cullable);

            if (foundIndex < 0)
            {
                // This object isn't in the old list, so we need to mark it as occluding
                cullable.Occluding = true;
            }
            else
            {
                // This object was already in the list, so remove it from the old list
                m_occludingObjects.RemoveAt(foundIndex);
            }
        }

        // Any object left in the old list was not in the new list, so it's no longer occluding
        foreach (Cullable cullable in m_occludingObjects)
        {
            cullable.Occluding = false;
        }

        m_occludingObjects = newList;
    }

    private List<Cullable> FindOccludingObjects(List<Vector3> importantPositions)
    {
        List<Cullable> occludingObjects = new List<Cullable>();

        // We want to do a capsule check from each position to the camera
        foreach (Vector3 pos in importantPositions)
        {
            Vector3 capsuleStart = pos;
            capsuleStart.y += m_occlusionCapsuleHeight;

            Collider[] colliders = Physics.OverlapCapsule(
                capsuleStart,
                mainCamera.transform.position,
                m_occlusionCapsuleRadius,
                m_layerMask,
                QueryTriggerInteraction.Ignore
            );

            // Add cullable objects we found to the list
            foreach (Collider collider in colliders)
            {
                Cullable cullable = collider.GetComponent<Cullable>();

                if (cullable == null)
                {
                    Debug.LogWarning($"CullingManager: Object '{collider.gameObject.name}' on occlusion layer is missing Cullable component!");
                    continue;
                }

                if (!occludingObjects.Contains(cullable))
                {
                    occludingObjects.Add(cullable);
                }
            }
        }

        return occludingObjects;
    }

    // Optional: Visualize detection capsules in Scene view
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        if (m_importantObjects.Count == 0) return;

        Camera cam = mainCamera;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        foreach (GameObject obj in m_importantObjects)
        {
            if (obj == null) continue;

            Vector3 capsuleStart = obj.transform.position;
            capsuleStart.y += m_occlusionCapsuleHeight;
            Vector3 capsuleEnd = cam.transform.position;

            // Draw detection capsule
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(capsuleStart, capsuleEnd);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(capsuleStart, m_occlusionCapsuleRadius);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(capsuleEnd, m_occlusionCapsuleRadius);
        }
    }
}