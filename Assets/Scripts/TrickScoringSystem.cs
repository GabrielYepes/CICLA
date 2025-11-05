using UnityEngine;
using SBPScripts;
using System.Collections.Generic;

/// <summary>
/// Trick Scoring System
/// Detects, names, and scores tricks performed by the player
/// Works with VisualOnlyTricks_v2_Trajectory and BikeGrindController
/// </summary>
[RequireComponent(typeof(VisualOnlyTricks_v2_Trajectory))]
[RequireComponent(typeof(BikeGrindController))]
public class TrickScoringSystem : MonoBehaviour
{
    [Header("References")]
    private VisualOnlyTricks_v2_Trajectory trickSystem;
    private BikeGrindController grindController;
    private BicycleController bikeController;
    private Rigidbody rb;

    [Header("Score Settings")]
    [SerializeField] private int baseFlipScore = 150;
    [SerializeField] private int baseSpinScore = 100;
    [SerializeField] private int grindScorePerSecond = 50;
    [SerializeField] private float longGrindBonusThreshold = 3f;
    [SerializeField] private int longGrindBonus = 100;

    [Header("Multipliers")]
    [SerializeField] private float speedMultiplierThreshold = 15f; // m/s
    [SerializeField] private float speedMultiplier = 1.2f;
    [SerializeField] private float comboMultiplier = 2.0f;
    [SerializeField] private float cleanLandingMultiplier = 1.5f;
    [SerializeField] private float landingAngleTolerance = 15f; // degrees

    [Header("Combo Settings")]
    [SerializeField] private float comboTimeWindow = 2f; // Time to chain tricks

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showDetailedStats = false;

    // Scoring state
    private int totalScore = 0;
    private int currentComboScore = 0;
    private List<string> currentComboTricks = new List<string>();
    private float lastTrickTime = 0f;

    // Trick detection state
    private bool wasAirborne = false;
    private float jumpStartRotationX = 0f; // Frontflip/backflip rotation at takeoff
    private float jumpStartRotationY = 0f; // Spin rotation at takeoff
    private float jumpStartTime = 0f;
    private float jumpStartSpeed = 0f;

    // Grind tracking
    private bool wasGrinding = false;
    private float grindStartTime = 0f;
    private int grindScore = 0;

    // Landing detection
    private Vector3 landingVelocity;
    private float landingAngle;

    void Start()
    {
        trickSystem = GetComponent<VisualOnlyTricks_v2_Trajectory>();
        grindController = GetComponent<BikeGrindController>();
        bikeController = GetComponent<BicycleController>();
        rb = GetComponent<Rigidbody>();

        if (trickSystem == null)
        {
            Debug.LogError("TrickScoringSystem: VisualOnlyTricks_v2_Trajectory not found!");
            enabled = false;
            return;
        }

        if (grindController == null)
        {
            Debug.LogWarning("TrickScoringSystem: BikeGrindController not found! Grinding won't be scored.");
        }

        if (bikeController == null)
        {
            Debug.LogError("TrickScoringSystem: BicycleController not found!");
            enabled = false;
            return;
        }

        if (rb == null)
        {
            Debug.LogError("TrickScoringSystem: Rigidbody not found!");
            enabled = false;
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log("<color=cyan>═══════════════════════════════════</color>");
            Debug.Log("<color=cyan>   TRICK SCORING SYSTEM ACTIVE</color>");
            Debug.Log("<color=cyan>═══════════════════════════════════</color>");
        }
    }

    void Update()
    {
        DetectTrickStates();
        UpdateGrindScore();
        CheckComboTimeout();
    }

    void DetectTrickStates()
    {
        bool isCurrentlyAirborne = bikeController.isAirborne;
        bool isCurrentlyGrinding = grindController.IsGrinding;

        // TAKEOFF DETECTION
        if (isCurrentlyAirborne && !wasAirborne)
        {
            OnTakeoff();
        }

        // LANDING DETECTION
        if (!isCurrentlyAirborne && wasAirborne)
        {
            OnLanding();
        }

        // GRIND START DETECTION
        if (isCurrentlyGrinding && !wasGrinding)
        {
            OnGrindStart();
        }

        // GRIND END DETECTION
        if (!isCurrentlyGrinding && wasGrinding)
        {
            OnGrindEnd();
        }

        wasAirborne = isCurrentlyAirborne;
        wasGrinding = isCurrentlyGrinding;
    }

    void OnTakeoff()
    {
        // Store starting rotations to calculate deltas on landing
        float currentXRotation = trickSystem.CurrentXRotation;
        float currentYRotation = trickSystem.CurrentYRotation;
        jumpStartTime = Time.time;
        jumpStartSpeed = rb.linearVelocity.magnitude;

        if (showDebugLogs && showDetailedStats)
        {
            Debug.Log($"<color=yellow>✈ TAKEOFF | Speed: {jumpStartSpeed:F1} m/s</color>");
        }
    }

    void OnLanding()
    {
        float airTime = Time.time - jumpStartTime;
        landingVelocity = rb.linearVelocity;
        landingAngle = Vector3.Angle(transform.up, Vector3.up);

        // Calculate rotation deltas
        float currentXRotation = trickSystem.CurrentXRotation;
        float currentYRotation = trickSystem.CurrentYRotation;

        float deltaRotationX = Mathf.Abs(currentXRotation - jumpStartRotationX);
        float deltaRotationY = Mathf.Abs(currentYRotation - jumpStartRotationY);

        // Normalize deltas to 0-360 range
        deltaRotationX = NormalizeAngle(deltaRotationX);
        deltaRotationY = NormalizeAngle(deltaRotationY);

        if (showDebugLogs && showDetailedStats)
        {
            Debug.Log($"<color=yellow>⬇ LANDING | Air Time: {airTime:F2}s | ΔX: {deltaRotationX:F0}° | ΔY: {deltaRotationY:F0}°</color>");
        }

        // Detect and score tricks
        DetectAndScoreTricks(deltaRotationX, deltaRotationY, airTime);
    }

    void DetectAndScoreTricks(float deltaRotationX, float deltaRotationY, float airTime)
    {
        List<string> tricksPerformed = new List<string>();
        int trickScore = 0;

        // FLIP DETECTION (X-axis rotation)
        if (deltaRotationX >= 330f) // ~360° (allowing some tolerance)
        {
            int flips = Mathf.RoundToInt(deltaRotationX / 360f);
            
            if (flips == 1)
            {
                tricksPerformed.Add(DetermineFlipDirection(deltaRotationX) + "flip");
                trickScore += baseFlipScore;
            }
            else if (flips >= 2)
            {
                string flipType = DetermineFlipDirection(deltaRotationX);
                tricksPerformed.Add($"Double {flipType}flip");
                trickScore += baseFlipScore * flips;
            }
        }
        else if (deltaRotationX >= 150f && deltaRotationX < 210f) // ~180°
        {
            tricksPerformed.Add(DetermineFlipDirection(deltaRotationX) + "flip Half");
            trickScore += baseFlipScore / 2;
        }

        // SPIN DETECTION (Y-axis rotation)
        if (deltaRotationY >= 330f) // ~360° or more
        {
            int spins = Mathf.RoundToInt(deltaRotationY / 360f);
            
            if (spins == 1)
            {
                tricksPerformed.Add("360°");
                trickScore += baseSpinScore;
            }
            else if (spins == 2)
            {
                tricksPerformed.Add("720°");
                trickScore += baseSpinScore * 2;
            }
            else if (spins >= 3)
            {
                tricksPerformed.Add($"{spins * 360}°");
                trickScore += baseSpinScore * spins;
            }
        }
        else if (deltaRotationY >= 150f && deltaRotationY < 210f) // ~180°
        {
            tricksPerformed.Add("180°");
            trickScore += baseSpinScore / 2;
        }
        else if (deltaRotationY >= 60f && deltaRotationY < 120f) // ~90°
        {
            tricksPerformed.Add("90°");
            trickScore += baseSpinScore / 4;
        }
        else if (deltaRotationY >= 240f && deltaRotationY < 300f) // ~270°
        {
            tricksPerformed.Add("270°");
            trickScore += (baseSpinScore * 3) / 4;
        }

        // Apply multipliers
        float finalMultiplier = 1f;
        List<string> bonuses = new List<string>();

        // Speed bonus
        if (jumpStartSpeed >= speedMultiplierThreshold)
        {
            finalMultiplier *= speedMultiplier;
            bonuses.Add($"Speed x{speedMultiplier:F1}");
        }

        // Combo bonus (multiple tricks in one jump)
        if (tricksPerformed.Count > 1)
        {
            finalMultiplier *= comboMultiplier;
            bonuses.Add($"Combo x{comboMultiplier:F1}");
        }

        // Clean landing bonus
        if (landingAngle <= landingAngleTolerance)
        {
            finalMultiplier *= cleanLandingMultiplier;
            bonuses.Add($"Clean Landing x{cleanLandingMultiplier:F1}");
        }

        // Calculate final score
        int finalScore = Mathf.RoundToInt(trickScore * finalMultiplier);

        // Award score if tricks were performed
        if (tricksPerformed.Count > 0)
        {
            AwardScore(tricksPerformed, finalScore, bonuses, airTime);
        }
    }

    void AwardScore(List<string> tricks, int score, List<string> bonuses, float airTime)
    {
        // Add to combo
        currentComboScore += score;
        currentComboTricks.AddRange(tricks);
        lastTrickTime = Time.time;

        // Build trick name
        string trickName = string.Join(" + ", tricks);

        // Log the trick
        if (showDebugLogs)
        {
            Debug.Log($"<color=lime>━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
            Debug.Log($"<color=lime>🎯 TRICK: {trickName}</color>");
            Debug.Log($"<color=lime>💰 SCORE: {score} points</color>");
            
            if (bonuses.Count > 0)
            {
                Debug.Log($"<color=yellow>✨ BONUSES: {string.Join(", ", bonuses)}</color>");
            }

            if (showDetailedStats)
            {
                Debug.Log($"<color=cyan>⏱ Air Time: {airTime:F2}s</color>");
                Debug.Log($"<color=cyan>🏆 Total Score: {totalScore + currentComboScore}</color>");
            }

            Debug.Log($"<color=lime>━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
        }
    }

    void OnGrindStart()
    {
        grindStartTime = Time.time;
        grindScore = 0;

        if (showDebugLogs && showDetailedStats)
        {
            Debug.Log($"<color=magenta>⚡ GRIND START</color>");
        }
    }

    void UpdateGrindScore()
    {
        if (grindController.IsGrinding)
        {
            float grindTime = Time.time - grindStartTime;
            grindScore = Mathf.RoundToInt(grindTime * grindScorePerSecond);
        }
    }

    void OnGrindEnd()
    {
        float grindTime = Time.time - grindStartTime;
        int finalGrindScore = grindScore;

        // Long grind bonus
        if (grindTime >= longGrindBonusThreshold)
        {
            finalGrindScore += longGrindBonus;
        }

        // Award grind score
        if (finalGrindScore > 0)
        {
            currentComboScore += finalGrindScore;
            currentComboTricks.Add($"Grind ({grindTime:F1}s)");
            lastTrickTime = Time.time;

            if (showDebugLogs)
            {
                Debug.Log($"<color=magenta>━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
                Debug.Log($"<color=magenta>🎯 TRICK: Rail Grind</color>");
                Debug.Log($"<color=magenta>💰 SCORE: {finalGrindScore} points</color>");
                Debug.Log($"<color=cyan>⏱ Grind Time: {grindTime:F2}s</color>");
                
                if (grindTime >= longGrindBonusThreshold)
                {
                    Debug.Log($"<color=yellow>✨ BONUS: Long Grind +{longGrindBonus}</color>");
                }
                
                if (showDetailedStats)
                {
                    Debug.Log($"<color=cyan>🏆 Total Score: {totalScore + currentComboScore}</color>");
                }
                
                Debug.Log($"<color=magenta>━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━</color>");
            }
        }

        grindScore = 0;
    }

    void CheckComboTimeout()
    {
        // If combo window expires, finalize the combo
        if (currentComboScore > 0 && Time.time - lastTrickTime >= comboTimeWindow)
        {
            FinalizeCombo();
        }
    }

    void FinalizeCombo()
    {
        totalScore += currentComboScore;

        if (showDebugLogs && currentComboTricks.Count > 1)
        {
            Debug.Log($"<color=orange>╔═══════════════════════════════════╗</color>");
            Debug.Log($"<color=orange>║  🔥 COMBO COMPLETE! 🔥           ║</color>");
            Debug.Log($"<color=orange>╚═══════════════════════════════════╝</color>");
            Debug.Log($"<color=orange>Tricks: {string.Join(" → ", currentComboTricks)}</color>");
            Debug.Log($"<color=orange>Combo Score: {currentComboScore} points</color>");
            Debug.Log($"<color=orange>═══════════════════════════════════</color>");
        }

        // Reset combo
        currentComboScore = 0;
        currentComboTricks.Clear();
    }

    string DetermineFlipDirection(float deltaRotationX)
    {
        // Determine if it was a frontflip or backflip based on the direction
        // Access current X rotation using reflection
        // float currentX = GetPrivateField<float>(trickSystem, "currentXRotation");
        float currentX = trickSystem.CurrentXRotation;

        // Positive rotation = Backflip, Negative = Frontflip (depends on your setup)
        if (currentX > 0 || (currentX > -180 && currentX < 0))
            return "Back";
        else
            return "Front";
    }

    float NormalizeAngle(float angle)
    {
        // Normalize angle to 0-360 range
        while (angle < 0f) angle += 360f;
        while (angle >= 360f) angle -= 360f;
        return angle;
    }

    // Public API for UI/external systems
    public int GetTotalScore() => totalScore;
    public int GetCurrentComboScore() => currentComboScore;
    public List<string> GetCurrentComboTricks() => new List<string>(currentComboTricks);
    public bool IsComboActive() => currentComboScore > 0;
    public float GetComboTimeRemaining() => Mathf.Max(0f, comboTimeWindow - (Time.time - lastTrickTime));

    // Debug visualization
    void OnGUI()
    {
        if (!showDebugLogs || !showDetailedStats) return;

        GUI.color = Color.white;
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(10, 10, 300, 25), $"Total Score: {totalScore}", style);
        
        if (currentComboScore > 0)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(10, 35, 300, 25), $"Combo: {currentComboScore} ({GetComboTimeRemaining():F1}s)", style);
        }
    }
}
