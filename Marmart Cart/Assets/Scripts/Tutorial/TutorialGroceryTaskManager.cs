using UnityEngine;

public class TutorialGroceryTaskManager : MonoBehaviour
{
    public static TutorialGroceryTaskManager Instance { get; private set; }

    [Header("Tasks")]
    [SerializeField] private CollectItemTask player1Task;
    [SerializeField] private CollectItemTask player2Task;
    [SerializeField] private CollectItemTask player3Task;
    [SerializeField] private CollectItemTask player4Task;

    private void Awake()
    {
        Instance = this;
    }

    public void NotifyGroceryCollected(string playerTag)
    {
        switch (playerTag)
        {
            case "Player1": player1Task?.IncreaseCount(); break;
            case "Player2": player2Task?.IncreaseCount(); break;
            case "Player3": player3Task?.IncreaseCount(); break;
            case "Player4": player4Task?.IncreaseCount(); break;
        }
    }
}