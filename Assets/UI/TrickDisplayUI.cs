using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Displays current tricks and combo information
/// </summary>
public class TrickDisplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI trickText;
    [SerializeField] private TrickScoringSystem scoringSystem;

    [Header("Display Settings")]
    [SerializeField] private float fadeOutDelay = 1f; // Time to show trick after combo ends
    [SerializeField] private bool showComboMultiplier = true;
    [SerializeField] private bool showScore = true;

    [Header("Animation (Optional)")]
    [SerializeField] private bool useAnimation = true;
    [SerializeField] private float scaleUpAmount = 1.2f;
    [SerializeField] private float animationSpeed = 5f;

    private bool wasComboActive = false;
    private string lastTrickDisplay = "";
    private bool isFadingOut = false;
    private RectTransform rectTransform;
    private Vector3 normalScale;

    void Start()
    {
        // Auto-find references
        if (scoringSystem == null)
        {
            scoringSystem = FindObjectOfType<TrickScoringSystem>();
            if (scoringSystem == null)
            {
                Debug.LogError("TrickDisplayUI: TrickScoringSystem not found!");
                enabled = false;
                return;
            }
        }

        if (trickText == null)
        {
            Debug.LogError("TrickDisplayUI: Trick Text not assigned!");
            enabled = false;
            return;
        }

        rectTransform = trickText.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            normalScale = rectTransform.localScale;
        }

        // Start with empty text
        trickText.text = "";
    }

    void Update()
    {
        bool isComboActive = scoringSystem.IsComboActive();

        // Combo just started or is ongoing
        if (isComboActive)
        {
            isFadingOut = false;
            UpdateTrickDisplay();
            wasComboActive = true;

            // Scale animation when combo is active
            if (useAnimation && rectTransform != null)
            {
                float targetScale = 1f + (Mathf.Sin(Time.time * animationSpeed) * 0.1f);
                rectTransform.localScale = normalScale * targetScale;
            }
        }
        // Combo just ended
        else if (wasComboActive && !isFadingOut)
        {
            wasComboActive = false;
            StartCoroutine(FadeOutTrick());
        }
    }

    void UpdateTrickDisplay()
    {
        List<string> tricks = scoringSystem.GetCurrentComboTricks();
        int comboScore = scoringSystem.GetCurrentComboScore();

        if (tricks.Count == 0)
        {
            trickText.text = "";
            return;
        }

        // Build display string
        string display = "";

        // Show trick names
        display = string.Join(" + ", tricks);

        // Add combo multiplier indicator
        if (showComboMultiplier && tricks.Count > 1)
        {
            display += "\nCOMBO!";
        }

        // Add score
        if (showScore)
        {
            display += $"\n{comboScore} pts";
        }

        // Add combo timer (optional)
        float timeRemaining = scoringSystem.GetComboTimeRemaining();
        if (timeRemaining > 0f && timeRemaining < 2f)
        {
            display += $"\n({timeRemaining:F1}s)";
        }

        trickText.text = display;
        lastTrickDisplay = display;
    }

    IEnumerator FadeOutTrick()
    {
        isFadingOut = true;

        // Keep displaying the last trick for a moment
        yield return new WaitForSeconds(fadeOutDelay);

        // Reset scale
        if (rectTransform != null)
        {
            rectTransform.localScale = normalScale;
        }

        // Clear text
        trickText.text = "";
        lastTrickDisplay = "";
        isFadingOut = false;
    }
}