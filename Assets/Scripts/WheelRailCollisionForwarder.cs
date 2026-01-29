using UnityEngine;

namespace SBPScripts
{
    /// <summary>
    /// Rail Collision Forwarder - Attach to RPhysicsWheel
    /// Detects rail collisions and forwards them to BikeGrindController on parent
    /// </summary>
    public class WheelRailCollisionForwarder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BikeGrindController grindController;
        
        [Header("Debug")]
        [SerializeField] private bool showDebug = false;

        private void Start()
        {
            // Try to find grind controller on parent/root
            if (grindController == null)
            {
                grindController = GetComponentInParent<BikeGrindController>();
                
                if (grindController == null)
                {
                    Debug.LogError($"WheelRailCollisionForwarder on {gameObject.name}: Could not find BikeGrindController on parent!");
                }
                else if (showDebug)
                {
                    Debug.Log($"<color=green>WheelRailCollisionForwarder on {gameObject.name}: Found BikeGrindController</color>");
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Check if we hit a rail
            if (collision.gameObject.CompareTag("Rail"))
            {
                if (showDebug)
                {
                    Debug.Log($"<color=cyan>WHEEL HIT RAIL! {gameObject.name} collided with {collision.gameObject.name}</color>");
                }
                
                // Forward to grind controller
                if (grindController != null)
                {
                    grindController.OnWheelHitRail(collision.gameObject, collision);
                }
                else
                {
                    Debug.LogWarning($"WheelRailCollisionForwarder: grindController is null!");
                }
            }
        }

        private void OnValidate()
        {
            // Auto-find in editor
            if (grindController == null && Application.isPlaying == false)
            {
                grindController = GetComponentInParent<BikeGrindController>();
            }
        }
    }
}
