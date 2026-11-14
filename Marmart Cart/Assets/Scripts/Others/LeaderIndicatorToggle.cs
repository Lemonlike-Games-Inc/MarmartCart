using UnityEngine;

public class LeaderIndicatorToggle : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CashScoreManager cashScoreManager;

    [Header("Owner")]
    [Range(1, 4)]
    [SerializeField] private int localPlayerIndex = 1;

    [Header("Toggle Target")]
    [SerializeField] private GameObject leaderOnlyObject;

    [Header("Update")]
    [SerializeField] private float refreshRate = 0.10f;

    private float _timer;

    private void Start()
    {
        if (!cashScoreManager)
            cashScoreManager = FindFirstObjectByType<CashScoreManager>();
    }

    private void OnEnable()
    {
        ForceUpdate();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshRate) return;
        _timer = 0f;

        ForceUpdate();
    }

    private void ForceUpdate()
    {
        if (!leaderOnlyObject) return;
        if (!cashScoreManager || !GMode.Instance)
        {
            leaderOnlyObject.SetActive(false);
            return;
        }

        bool isLeader = IsLocalPlayerLeaderOrTied();
        leaderOnlyObject.SetActive(isLeader);
    }

    private bool IsLocalPlayerLeaderOrTied()
    {
        int count = GMode.Instance.PlayerCount();
        int myScore = cashScoreManager.GetPlayerScore(localPlayerIndex);

        int topScore = int.MinValue;
        for (int i = 1; i <= count; i++)
            topScore = Mathf.Max(topScore, cashScoreManager.GetPlayerScore(i));
        if(myScore == 0)
            return false;

        return myScore == topScore; // ties included (including 0 at game start)
    }

    public void SetLocalPlayerIndex(int idx)
    {
        localPlayerIndex = Mathf.Clamp(idx, 1, 4);
        ForceUpdate();
    }
}