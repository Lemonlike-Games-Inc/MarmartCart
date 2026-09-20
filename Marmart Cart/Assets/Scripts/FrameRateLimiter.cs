using UnityEngine;

public sealed class FrameRateLimiter : MonoBehaviour
{
    private void Awake()
    {
        // Disable VSync so the monitor refresh rate does not override our cap.
        QualitySettings.vSyncCount = 0;

        // Cap the game at 120 FPS.
        Application.targetFrameRate = 165;
    }
}