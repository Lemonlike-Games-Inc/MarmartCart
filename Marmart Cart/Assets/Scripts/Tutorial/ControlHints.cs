using UnityEngine;
using TMPro;

public class ControlHints : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;

    [Header("Visual")]
    [SerializeField] private float inactiveAlpha = 0.5f;
    [SerializeField] private float activeAlpha = 1f;
    [SerializeField] private float pulseTime = 0.5f;
    [SerializeField] private float pulseScale = 1f;

    private bool _isHeld;
    private float _pulseTimer;
    private Vector3 _baseScale;

    private void Awake()
    {
        _baseScale = transform.localScale;
        SetHeld(false);
    }

    public void SetIntroduced(bool introduced)
    {
        gameObject.SetActive(introduced);
        if (introduced) SetHeld(false);
    }

    // 🔹 NEW: sustained highlight
    public void SetHeld(bool held)
    {
        _isHeld = held;
        group.alpha = held ? activeAlpha : inactiveAlpha;
    }

    // 🔹 Optional: momentary feedback on press
    public void Pulse()
    {
        _pulseTimer = pulseTime;
        transform.localScale = _baseScale * pulseScale;
        group.alpha = activeAlpha;
    }

    private void Update()
    {
        if (_pulseTimer <= 0f) return;

        _pulseTimer -= Time.unscaledDeltaTime;
        if (_pulseTimer <= 0f)
        {
            transform.localScale = _baseScale;
            // revert to held or idle state
            group.alpha = _isHeld ? activeAlpha : inactiveAlpha;
        }
    }
}
