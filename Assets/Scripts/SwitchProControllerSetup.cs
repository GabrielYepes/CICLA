using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;

public class SwitchProControllerSetup : MonoBehaviour
{
    void Awake()
    {
        // Log all connected devices for debugging
        Debug.Log("=== Connected Input Devices ===");
        foreach (var device in InputSystem.devices)
        {
            Debug.Log($"Device: {device.name} | Layout: {device.layout} | Description: {device.description}");
        }
    }
}