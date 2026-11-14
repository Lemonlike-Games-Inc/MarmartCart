using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ComboDeal : MonoBehaviour
{
    public enum ComboRewardType { Points, Powerup }
    [Header("Combo Deal UIs")]
    [SerializeField] private GameObject comboDealUI;
    [SerializeField] private TextMeshProUGUI dealReward;
    [SerializeField] private TextMeshProUGUI commonCartRemainingText;
    [SerializeField] private TextMeshProUGUI rareCartRemainingText;
    [SerializeField] private TextMeshProUGUI epicCartRemainingText;
    [SerializeField] private TextMeshProUGUI legendaryCartRemainingText;

    [SerializeField] private GameObject commonCartCompleteMark;
    [SerializeField] private GameObject rareCartCompleteMark;
    [SerializeField] private GameObject epicCartRCompleteMark;
    [SerializeField] private GameObject legendaryCartCompleteMark;
    [SerializeField] private Slider timerSlider;
    public int DealID;
    public float RewardPoints { get; private set; }
    public ComboRewardType rewardType;
    public bool rewardIsPowerup;

    [Header("UI Controller Management")]
    public int stackIndex;
    public float PanelWidth => GetComponent<RectTransform>().rect.width;
    [SerializeField] public ComboDealUIController uiController;
    private Dictionary<CartRarity, int> requirements;
    private float duration;
    private float startTime;
    private bool completed = false;
    private int finalContributorIndex = 0;

    public void Initialize(int id, float rewardPoints, Dictionary<CartRarity, int> reqs, float durationSeconds)
    {
        DealID = id;
        RewardPoints = rewardPoints;
        requirements = new Dictionary<CartRarity, int>(reqs);
        duration = durationSeconds;
        startTime = Time.time;

        // Use controller to set start position
        RectTransform rect = GetComponent<RectTransform>();
        Vector2 startPos = uiController.GetSpawnStartPosition();
        rect.anchoredPosition = startPos;

        rewardType = ComboRewardType.Points; // default to points
        //rewardType = (Random.value < 0.5f) ? ComboRewardType.Points : ComboRewardType.Powerup;
        rewardPoints = rewardType == ComboRewardType.Points ? rewardPoints : 0f;
        //rewardIsPowerup = rewardType == ComboRewardType.Powerup;

        UpdateUI();
        uiController.Register(this);

        commonCartCompleteMark.SetActive(false);
        rareCartCompleteMark.SetActive(false);
        epicCartRCompleteMark.SetActive(false);
        legendaryCartCompleteMark.SetActive(false);
    }
    void Update()
    {
        if (comboDealUI == null || completed) return;

        float remainingTime = TimeRemaining;
        timerSlider.value = remainingTime / duration;

        if (IsExpired)
        {
            uiController.Unregister(this);
            comboDealUI.SetActive(false);
        }

        if(requirements[CartRarity.Common] == 0 && commonCartCompleteMark != null)
        {
            commonCartRemainingText.text = "";
            commonCartCompleteMark.SetActive(true);
        }
        if (requirements[CartRarity.Rare] == 0 && rareCartCompleteMark != null)
        {
            rareCartRemainingText.text = "";
            rareCartCompleteMark.SetActive(true);
        }
        if (requirements[CartRarity.Epic] == 0 && epicCartRCompleteMark != null)
        {
            epicCartRemainingText.text = "";
            epicCartRCompleteMark.SetActive(true);
        }
        if (requirements[CartRarity.Legendary] == 0 && legendaryCartCompleteMark != null)
        {
            legendaryCartRemainingText.text = "";
            legendaryCartCompleteMark.SetActive(true);
        }

    }

    public bool IsExpired => Time.time > startTime + duration;
    public bool IsCompleted => completed;
    public float TimeRemaining => Mathf.Max(0, (startTime + duration) - Time.time);
    public void SetCoreData(float rewardPoints, Dictionary<CartRarity, int> reqs, float durationSeconds)
    {
        RewardPoints = rewardPoints;
        requirements = new Dictionary<CartRarity, int>(reqs);
        duration = durationSeconds;
        startTime = Time.time;
    }
    public void InitializeFromLogic(ComboDeal source, Vector2 startPosition)
    {
        DealID = source.DealID;
        RewardPoints = source.RewardPoints;
        requirements = new Dictionary<CartRarity, int>(source.GetRemainingRequirements());
        duration = source.duration;
        startTime = Time.time;

        RectTransform rect = GetComponent<RectTransform>();
        rect.anchoredPosition = startPosition;

        UpdateUI();
        uiController.Register(this);
    }
    public bool SubmitCart(CartRarity rarity, int playerIndex)
    {
        if (completed || !requirements.ContainsKey(rarity) || requirements[rarity] <= 0)
            return false;

        requirements[rarity]--;
        finalContributorIndex = playerIndex; // store the latest contributor

        UpdateUI();
        CheckCompletion();
        return true;
    }

    private void CheckCompletion()
    {
        foreach (var cartLeft in requirements)
        {
            if (cartLeft.Value > 0)
                return;
        }
        // get to this point means all requirements are met, all carts value are zero
        completed = true;
        OnCompleted();
    }

    private void OnCompleted()
    {
        if (finalContributorIndex != 0)
        {
            if (rewardType == ComboRewardType.Powerup)
            {
                if(finalContributorIndex ==1)
                {
                    PowerupsManager p1PowerupManager = GameObject.FindGameObjectWithTag("Player1").GetComponentInChildren<PowerupsManager>();
                    //p1PowerupManager.RollRandomPowerup();
                }
                else if(finalContributorIndex ==2)
                {
                    PowerupsManager p2PowerupManager = GameObject.FindGameObjectWithTag("Player2").GetComponentInChildren<PowerupsManager>();
                    //p2PowerupManager.RollRandomPowerup();
                }
            }
            CashScoreManager scoreManager = FindFirstObjectByType<CashScoreManager>();
            if (scoreManager != null)
            {
                Debug.Log($"Combo Deal {DealID} completed by Player {finalContributorIndex}! Reward: {RewardPoints} points");
                //scoreManager.AddComboRewardToPlayer(RewardPoints, finalContributorIndex);
            }
            else
            {
                Debug.LogError("CashScoreManager not found in the scene.");
            }
        }
        uiController.Unregister(this);
        comboDealUI.SetActive(false);
    }
    private void UpdateUI()
    {
        if (dealReward != null)
            dealReward.text = rewardIsPowerup ? "POWERUP" : $"{RewardPoints}" + " PTS";

        if (commonCartRemainingText != null && requirements.ContainsKey(CartRarity.Common))
            commonCartRemainingText.text = requirements[CartRarity.Common].ToString();

        if (rareCartRemainingText != null && requirements.ContainsKey(CartRarity.Rare))
            rareCartRemainingText.text = requirements[CartRarity.Rare].ToString();

        if (epicCartRemainingText != null && requirements.ContainsKey(CartRarity.Epic))
            epicCartRemainingText.text = requirements[CartRarity.Epic].ToString();

        if (legendaryCartRemainingText != null && requirements.ContainsKey(CartRarity.Legendary))
            legendaryCartRemainingText.text = requirements[CartRarity.Legendary].ToString();
    }
    public Dictionary<CartRarity, int> GetRemainingRequirements()
    {
        return new Dictionary<CartRarity, int>(requirements);
    }
}
