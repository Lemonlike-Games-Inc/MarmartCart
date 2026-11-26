using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Per-player camera shake on one CinemachineCamera. CameraManager adds it at
/// runtime; call CameraManager.ShakePlayerCamera rather than this directly.
/// Offsets the camera in its own screen plane, fading out over the duration.
/// Time based, no coroutine.
/// </summary>
[AddComponentMenu("")]
public sealed class PlayerCameraShake : CinemachineExtension
{
    [Tooltip("Noise speed. Higher = more jittery.")]
    [Min(0.1f)]
    [SerializeField] private float frequency = 22f;

    private float amplitude;
    private float duration;
    private float startTime;
    private float seedX;
    private float seedY;
    private bool shaking;

    public bool IsShaking => shaking;

    public void Shake(float shakeAmplitude, float shakeDuration)
    {
        if (shakeAmplitude <= 0f || shakeDuration <= 0f) return;
        if (shaking && GetCurrentAmplitude() > shakeAmplitude) return;

        amplitude = shakeAmplitude;
        duration = shakeDuration;
        startTime = Time.time;
        seedX = UnityEngine.Random.Range(0f, 100f);
        seedY = UnityEngine.Random.Range(0f, 100f);
        shaking = true;
    }

    public void StopShake()
    {
        shaking = false;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (!shaking || stage != CinemachineCore.Stage.Noise) return;

        float current = GetCurrentAmplitude();

        if (current <= 0f)
        {
            shaking = false;
            return;
        }

        float t = Time.time * frequency;
        float x = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
        float y = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;

        state.PositionCorrection += state.RawOrientation * new Vector3(x * current, y * current, 0f);
    }

    private float GetCurrentAmplitude()
    {
        float progress = (Time.time - startTime) / duration;

        if (progress >= 1f) return 0f;

        float remaining = 1f - progress;
        return amplitude * remaining * remaining;
    }
}
