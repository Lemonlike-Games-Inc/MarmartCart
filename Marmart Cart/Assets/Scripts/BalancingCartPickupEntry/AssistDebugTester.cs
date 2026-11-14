#if UNITY_EDITOR
using UnityEngine;

public class AssistDebugTester : MonoBehaviour
{
    [SerializeField] private MatchBalanceManager matchBalanceManager;
    [SerializeField] private int testPlayerId = 1;
    [SerializeField] private int fakeZoneStock = 5;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            AssistEvaluationResult result = matchBalanceManager.EvaluatePlayerForAssist(testPlayerId, fakeZoneStock);
            Debug.Log($"Assist Eval Player {testPlayerId}: {result}");
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            matchBalanceManager.MarkPlayerClaimedAssist(testPlayerId);
            Debug.Log($"Marked Player {testPlayerId} as having claimed assist.");
        }
    }
}
#endif