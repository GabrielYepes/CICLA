using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cullable : MonoBehaviour
{
    [Header("Fade Settings")]
    public float m_alphaChangeSpeed = 3f;
    public string m_shaderVariableName = "_PosSlider";
    public float m_fadeTo = -3f;    // Hidden state
    public float m_fadeFrom = 1f;   // Visible state

    [Header("Debug")]
    public bool showDebugLogs = true;

    private float m_currentAlpha = 1.0f;
    private Material m_mat;
    private bool m_occluding;
    private bool m_inCoroutine = false;

    public bool Occluding
    {
        get { return m_occluding; }
        set
        {
            if (m_occluding != value)
            {
                m_occluding = value;

                if (showDebugLogs)
                {
                    Debug.Log($"<color=orange>[{gameObject.name}] Occluding changed to: {value}</color>");
                }

                OnOccludingChanged();
            }
        }
    }

    void Start()
    {
        Debug.Log($"<color=cyan>[{gameObject.name}] Cullable Start()</color>");

        // Get the material
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError($"[{gameObject.name}] No Renderer component found!");
            enabled = false;
            return;
        }

        m_mat = renderer.material;

        // Check if shader has the property
        if (!m_mat.HasProperty(m_shaderVariableName))
        {
            Debug.LogError($"[{gameObject.name}] Material '{m_mat.name}' does not have property '{m_shaderVariableName}'! Shader: {m_mat.shader.name}");
            enabled = false;
            return;
        }

        // Set initial values
        m_currentAlpha = m_fadeFrom;
        m_mat.SetFloat(m_shaderVariableName, m_currentAlpha);

        Debug.Log($"<color=green>[{gameObject.name}] Initialized. Current alpha: {m_currentAlpha}, Shader var: {m_shaderVariableName}</color>");
    }

    private void OnOccludingChanged()
    {
        if (showDebugLogs)
        {
            Debug.Log($"[{gameObject.name}] OnOccludingChanged() - Target: {GetTargetAlpha()}, Current: {m_currentAlpha}");
        }

        if (!m_inCoroutine)
        {
            StartCoroutine(FadeAlphaRoutine());
            m_inCoroutine = true;
        }
    }

    private float GetTargetAlpha()
    {
        return m_occluding ? m_fadeTo : m_fadeFrom;
    }

    private IEnumerator FadeAlphaRoutine()
    {
        if (showDebugLogs)
        {
            Debug.Log($"<color=yellow>[{gameObject.name}] FadeAlphaRoutine STARTED. From {m_currentAlpha} to {GetTargetAlpha()}</color>");
        }

        int frameCount = 0;

        while (m_currentAlpha != GetTargetAlpha())
        {
            float alphaShift = m_alphaChangeSpeed * Time.deltaTime;
            float targetAlpha = GetTargetAlpha();

            if (m_currentAlpha < targetAlpha)
            {
                m_currentAlpha += alphaShift;
                if (m_currentAlpha > targetAlpha)
                {
                    m_currentAlpha = targetAlpha;
                }
            }
            else
            {
                m_currentAlpha -= alphaShift;
                if (m_currentAlpha < targetAlpha)
                {
                    m_currentAlpha = targetAlpha;
                }
            }

            // CRITICAL: Actually set the shader value
            if (m_mat != null)
            {
                m_mat.SetFloat(m_shaderVariableName, m_currentAlpha);

                // Debug every 10 frames
                if (frameCount % 10 == 0 && showDebugLogs)
                {
                    Debug.Log($"[{gameObject.name}] Frame {frameCount}: Alpha = {m_currentAlpha}, Target = {targetAlpha}");
                }
            }

            frameCount++;
            yield return null;
        }

        if (showDebugLogs)
        {
            Debug.Log($"<color=green>[{gameObject.name}] FadeAlphaRoutine COMPLETED at alpha: {m_currentAlpha}</color>");
        }

        m_inCoroutine = false;
    }

    // Debug: Show current state in Inspector
    void OnGUI()
    {
        if (showDebugLogs && m_mat != null)
        {
            float currentShaderValue = m_mat.GetFloat(m_shaderVariableName);
            GUI.Label(new Rect(10, 100, 400, 20),
                $"{gameObject.name}: Occluding={m_occluding}, Alpha={m_currentAlpha:F2}, Shader={currentShaderValue:F2}");
        }
    }
}