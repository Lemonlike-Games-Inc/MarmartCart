using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoxCollider))]
public class BothPlayersStartZone : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshPro statusText;

    [Header("Scene")]
    [SerializeField] private string sceneToLoad = "GameScene";

    [Header("Countdown")]
    [SerializeField] private float countdownSeconds = 5f;

    private bool _p1Inside;
    private bool _p2Inside;

    private float _timeLeft;
    private bool _countingDown;
    private bool _loading;

    private void Reset()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;

        SetRequireText();
    }

    private void Update()
    {
        if (_loading) return;

        bool bothInside = _p1Inside && _p2Inside;

        if (!bothInside)
        {
            // Cancel countdown if someone leaves
            _countingDown = false;
            _timeLeft = countdownSeconds;
            SetRequireText();
            return;
        }

        // Both inside -> start / continue countdown
        if (!_countingDown)
        {
            _countingDown = true;
            _timeLeft = countdownSeconds;
        }

        _timeLeft -= Time.deltaTime;

        int shown = Mathf.CeilToInt(_timeLeft);
        shown = Mathf.Max(0, shown);

        if (statusText != null)
            statusText.text = $"Ready! Game starts in {shown}...";

        if (_timeLeft <= 0f)
        {
            _loading = true;
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player1")) _p1Inside = true;
        else if (other.CompareTag("Player2")) _p2Inside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player1")) _p1Inside = false;
        else if (other.CompareTag("Player2")) _p2Inside = false;
    }

    private void SetRequireText()
    {
        if (statusText != null)
            statusText.text = "Require Both players to start.";
    }
}
