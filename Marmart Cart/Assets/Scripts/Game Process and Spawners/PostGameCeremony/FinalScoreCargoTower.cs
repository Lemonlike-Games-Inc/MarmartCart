using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public sealed class MatchResultsScoreTokenUnityEvent :
    UnityEvent<int, int, bool>
{
}

/// <summary>
/// One player's physical final-score presentation.
///
/// - real submitted cargo prefabs represent ordinary score;
/// - random ordinary-cargo prefabs represent milestone/streak score;
/// - the ceremony number is presentation-scaled without changing gameplay score;
/// - authored first-layer slots repeat upward forever;
/// - the actual leading cart, score/rank kit, and camera target rise together;
/// - all animation uses unscaled time and no Rigidbody physics.
/// </summary>
[DisallowMultipleComponent]
public sealed class FinalScoreCargoTower : MonoBehaviour
{
    #region Authoring

    [Header("Player Identity")]
    [Range(1, CashScoreManager.MaxPlayers)]
    [SerializeField] private int playerIndex = 1;

    [Header("First Cargo Layer")]
    [Tooltip(
        "Author 2 or 3 slots around the footprint under the staged cart. " +
        "The complete slot pattern repeats upward by the profile Layer Height."
    )]
    [SerializeField] private Transform[] firstLayerSlots = new Transform[3];

    [Tooltip("Optional transform whose local UP defines tower growth. Defaults to world up.")]
    [SerializeField] private Transform stackDirectionReference;

    [Tooltip("Optional parent for spawned score cargo. A runtime child is created when empty.")]
    [SerializeField] private Transform runtimeVisualRoot;

    [Header("Moving Top Presentation")]
    [Tooltip(
        "External scene-kit root containing the live score and any always-visible decoration. " +
        "It follows the cart upward without needing objects added to the cart prefab."
    )]
    [SerializeField] private Transform topPresentationRoot;

    [Tooltip("Target assigned to this player's final-results Cinemachine camera.")]
    [SerializeField] private Transform cameraFocusTarget;

    [SerializeField] private TMP_Text liveScoreText;
    [SerializeField] private TMP_Text finalRankText;

    [Tooltip("Spotlight and any rank-finish effects enabled only when this player's reveal completes.")]
    [SerializeField] private GameObject completionVisualRoot;

    [Header("Feedback Events")]
    [SerializeField] private UnityEvent onRevealStarted;

    [Tooltip(
        "Arguments: player index, new unscaled gameplay score, is reward token."
    )]
    [SerializeField] private MatchResultsScoreTokenUnityEvent onScoreTokenLanded;

    [SerializeField] private UnityEvent onRevealCompleted;

    #endregion

    #region Runtime

    [Header("Runtime - Read Only")]
    [SerializeField] private bool presenting;
    [SerializeField] private bool complete;
    [SerializeField] private int finalScore;
    [SerializeField] private int displayedScore;
    [SerializeField] private int finalBaseScore;
    [SerializeField] private int finalBonusScore;
    [SerializeField] private int displayedBaseScore;
    [SerializeField] private int displayedBonusScore;
    [SerializeField] private int displayedPresentationScore;
    [SerializeField] private bool rewardDisplayCollapsed;
    [SerializeField] private int finalRank;
    [SerializeField] private int totalVisualTokens;
    [SerializeField] private int baseVisualTokens;
    [SerializeField] private int landedVisualTokens;
    [SerializeField] private float currentCartLift;

    private readonly List<Transform> validLayerSlots = new List<Transform>(4);
    private readonly List<ScoreVisualToken> revealTokens = new List<ScoreVisualToken>(256);
    private readonly List<ActiveTokenFlight> activeFlights = new List<ActiveTokenFlight>(32);
    private readonly List<GameObject> spawnedVisuals = new List<GameObject>(256);
    private readonly List<GameObject> rewardCargoPrefabPool = new List<GameObject>(32);

    private Transform stagedCart;
    private MatchResultsPresentationProfile activeProfile;
    private Coroutine revealRoutine;
    private Coroutine scoreTextPopRoutine;

    private Vector3 cartBasePosition;
    private Quaternion cartBaseRotation;
    private Vector3 topPresentationBasePosition;
    private Vector3 cameraFocusBasePosition;
    private bool topPresentationMovesWithCartByHierarchy;
    private bool cameraFocusMovesWithCartByHierarchy;

    private float targetCartLift;
    private float cartLiftVelocity;
    private Vector3 scoreTextBaseScale = Vector3.one;
    private bool scoreTextBaseScaleCached;

    private sealed class ScoreVisualToken
    {
        public GameObject prefab;
        public int scoreValue;
        public bool isBonus;
    }

    private sealed class ActiveTokenFlight
    {
        public GameObject instance;
        public Vector3 startPosition;
        public Vector3 targetPosition;
        public Quaternion startRotation;
        public Quaternion targetRotation;
        public Vector3 targetLocalScale;
        public float elapsed;
        public int scoreValue;
        public bool isBonus;
    }

    public int PlayerIndex => playerIndex;
    public bool IsPresenting => presenting;
    public bool IsComplete => complete;
    public int DisplayedScore => displayedScore;
    public int DisplayedPresentationScore => displayedPresentationScore;
    public int FinalScore => finalScore;
    public int FinalRank => finalRank;
    public Transform CameraFocusTarget => cameraFocusTarget;

    public event Action<int> OnRevealStarted;
    public event Action<int, int, bool> OnScoreTokenLanded;
    public event Action<int> OnRevealCompleted;

    #endregion

    #region Unity

    private void Awake()
    {
        CacheScoreTextBaseScale();
        EnsureRuntimeVisualRoot();
        SetCompletionVisible(false);
        SetTextVisible(liveScoreText, false);
        SetTextVisible(finalRankText, false);
    }

    private void OnDisable()
    {
        StopRevealRoutine();
        StopScoreTextPop(true);
    }

    private void OnValidate()
    {
        playerIndex = Mathf.Clamp(playerIndex, 1, CashScoreManager.MaxPlayers);
    }

    #endregion

    #region Presentation API

    public bool BeginPresentation(
        Transform cartTransform,
        MatchResultsPlayerSnapshot snapshot,
        MatchResultsPresentationProfile profile)
    {
        if (cartTransform == null || snapshot == null || profile == null)
        {
            Debug.LogError(
                $"[FinalScoreCargoTower P{playerIndex}] Cart, snapshot, and profile are required.",
                this
            );
            return false;
        }

        RebuildValidSlotCache();

        if (validLayerSlots.Count == 0)
        {
            Debug.LogError(
                $"[FinalScoreCargoTower P{playerIndex}] Assign at least one First Cargo Layer slot.",
                this
            );
            return false;
        }

        ClearPresentation();
        EnsureRuntimeVisualRoot();

        stagedCart = cartTransform;
        activeProfile = profile;
        finalScore = snapshot.FinalScore;
        displayedScore = 0;
        finalBaseScore = snapshot.BaseScore;
        finalBonusScore = snapshot.BonusScore;
        displayedBaseScore = 0;
        displayedBonusScore = 0;
        displayedPresentationScore = 0;
        rewardDisplayCollapsed = false;
        finalRank = snapshot.Rank;
        landedVisualTokens = 0;
        currentCartLift = 0f;
        targetCartLift = 0f;
        cartLiftVelocity = 0f;
        complete = false;
        presenting = true;

        cartBasePosition = stagedCart.position;
        cartBaseRotation = stagedCart.rotation;

        if (topPresentationRoot != null)
        {
            topPresentationBasePosition = topPresentationRoot.position;
            topPresentationMovesWithCartByHierarchy =
                topPresentationRoot.IsChildOf(stagedCart);
        }

        if (cameraFocusTarget != null)
        {
            cameraFocusBasePosition = cameraFocusTarget.position;
            cameraFocusMovesWithCartByHierarchy =
                cameraFocusTarget.IsChildOf(stagedCart);
        }

        BuildRevealTokens(snapshot);
        totalVisualTokens = revealTokens.Count;

        SetCompletionVisible(false);
        SetTextVisible(liveScoreText, true);
        SetTextVisible(finalRankText, false);
        RefreshScoreText(false);

        onRevealStarted?.Invoke();
        OnRevealStarted?.Invoke(playerIndex);

        revealRoutine = StartCoroutine(RevealRoutine());
        return true;
    }

    [ContextMenu("Clear Final Score Tower")]
    public void ClearPresentation()
    {
        StopRevealRoutine();
        StopScoreTextPop(true);

        for (int i = spawnedVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = spawnedVisuals[i];
            if (visual != null) Destroy(visual);
        }

        spawnedVisuals.Clear();
        activeFlights.Clear();
        revealTokens.Clear();
        rewardCargoPrefabPool.Clear();

        presenting = false;
        complete = false;
        displayedScore = 0;
        finalScore = 0;
        finalBaseScore = 0;
        finalBonusScore = 0;
        displayedBaseScore = 0;
        displayedBonusScore = 0;
        displayedPresentationScore = 0;
        rewardDisplayCollapsed = false;
        finalRank = 0;
        totalVisualTokens = 0;
        baseVisualTokens = 0;
        landedVisualTokens = 0;
        currentCartLift = 0f;
        targetCartLift = 0f;
        cartLiftVelocity = 0f;
        stagedCart = null;
        activeProfile = null;

        SetCompletionVisible(false);
        SetTextVisible(liveScoreText, false);
        SetTextVisible(finalRankText, false);
    }

    #endregion

    #region Token Planning

    private void BuildRevealTokens(MatchResultsPlayerSnapshot snapshot)
    {
        revealTokens.Clear();
        rewardCargoPrefabPool.Clear();

        int basePointsPerVisual = activeProfile.BaseScorePointsPerVisual;
        int bonusPointsPerVisual = activeProfile.BonusScorePointsPerVisual;

        int baseScoreRemaining = snapshot.BaseScore;
        IReadOnlyList<MatchResultsCargoRecord> records = snapshot.CargoRecords;

        if (records != null)
        {
            for (int i = 0; i < records.Count && baseScoreRemaining > 0; i++)
            {
                MatchResultsCargoRecord record = records[i];
                if (record == null) continue;

                int recordScoreRemaining = Mathf.Min(
                    Mathf.Max(0, record.ScoreValue),
                    baseScoreRemaining
                );

                GameObject prefab =
                    record.CargoVisual != null
                        ? record.CargoVisual.CargoVisualPrefab
                        : null;

                if (prefab == null) prefab = activeProfile.FallbackCargoVisualPrefab;

                while (recordScoreRemaining > 0)
                {
                    int representedScore = Mathf.Min(
                        basePointsPerVisual,
                        recordScoreRemaining
                    );

                    revealTokens.Add(new ScoreVisualToken
                    {
                        prefab = prefab,
                        scoreValue = representedScore,
                        isBonus = false
                    });

                    recordScoreRemaining -= representedScore;
                    baseScoreRemaining -= representedScore;
                }
            }
        }

        // Reconciliation fallback: if old saves/tests changed score without a
        // recorded CargoEntry, the final score still receives a complete tower.
        while (baseScoreRemaining > 0)
        {
            int representedScore = Mathf.Min(basePointsPerVisual, baseScoreRemaining);

            revealTokens.Add(new ScoreVisualToken
            {
                prefab = activeProfile.FallbackCargoVisualPrefab,
                scoreValue = representedScore,
                isBonus = false
            });

            baseScoreRemaining -= representedScore;
        }

        BuildRewardCargoPrefabPool(records);

        int bonusScoreRemaining = snapshot.BonusScore;

        while (bonusScoreRemaining > 0)
        {
            int representedScore = Mathf.Min(bonusPointsPerVisual, bonusScoreRemaining);

            revealTokens.Add(new ScoreVisualToken
            {
                prefab = GetRandomRewardCargoPrefab(),
                scoreValue = representedScore,
                isBonus = true
            });

            bonusScoreRemaining -= representedScore;
        }

        int cap = activeProfile.MaximumVisualTokensPerPlayer;

        if (cap > 0 && revealTokens.Count > cap)
        {
            int originalCount = revealTokens.Count;
            CompressTokenPlanToMaximum(cap);

            Debug.LogWarning(
                $"[FinalScoreCargoTower P{playerIndex}] Score requires {originalCount} visuals, " +
                $"above the safety cap {cap}. The plan was compressed to {revealTokens.Count} " +
                "sampled cargo/reward visuals; the displayed score remains exact.",
                this
            );
        }

        baseVisualTokens = 0;

        for (int i = 0; i < revealTokens.Count; i++)
        {
            if (!revealTokens[i].isBonus) baseVisualTokens++;
        }
    }

    private void BuildRewardCargoPrefabPool(
        IReadOnlyList<MatchResultsCargoRecord> records)
    {
        GameObject[] authoredPrefabs = activeProfile.RewardCargoVisualPrefabs;

        if (authoredPrefabs != null)
        {
            for (int i = 0; i < authoredPrefabs.Length; i++)
                AddUniqueRewardCargoPrefab(authoredPrefabs[i]);
        }

        // An explicitly authored list is authoritative. Otherwise the reward
        // phase automatically reuses ordinary cargo this player really submitted.
        if (rewardCargoPrefabPool.Count == 0 && records != null)
        {
            for (int i = 0; i < records.Count; i++)
            {
                MatchResultsCargoRecord record = records[i];

                if (record == null || record.CargoVisual == null) continue;

                AddUniqueRewardCargoPrefab(
                    record.CargoVisual.CargoVisualPrefab
                );
            }
        }

        if (rewardCargoPrefabPool.Count == 0)
            AddUniqueRewardCargoPrefab(activeProfile.FallbackCargoVisualPrefab);
    }

    private void AddUniqueRewardCargoPrefab(GameObject prefab)
    {
        if (prefab == null || rewardCargoPrefabPool.Contains(prefab)) return;
        rewardCargoPrefabPool.Add(prefab);
    }

    private GameObject GetRandomRewardCargoPrefab()
    {
        if (rewardCargoPrefabPool.Count == 0) return null;

        return rewardCargoPrefabPool[
            UnityEngine.Random.Range(0, rewardCargoPrefabPool.Count)
        ];
    }

    private void CompressTokenPlanToMaximum(int requestedMaximum)
    {
        int baseCount = 0;
        int bonusCount = 0;

        for (int i = 0; i < revealTokens.Count; i++)
        {
            if (revealTokens[i].isBonus) bonusCount++;
            else baseCount++;
        }

        bool hasBothChannels = baseCount > 0 && bonusCount > 0;
        int maximum = Mathf.Max(hasBothChannels ? 2 : 1, requestedMaximum);

        int baseBudget;
        int bonusBudget;

        if (!hasBothChannels)
        {
            baseBudget = baseCount > 0 ? maximum : 0;
            bonusBudget = bonusCount > 0 ? maximum : 0;
        }
        else
        {
            baseBudget = Mathf.Clamp(
                Mathf.RoundToInt((float)maximum * baseCount / revealTokens.Count),
                1,
                maximum - 1
            );
            bonusBudget = maximum - baseBudget;
        }

        List<ScoreVisualToken> compressed =
            new List<ScoreVisualToken>(maximum);

        AppendCompressedChannel(false, baseCount, baseBudget, compressed);
        AppendCompressedChannel(true, bonusCount, bonusBudget, compressed);

        revealTokens.Clear();
        revealTokens.AddRange(compressed);
    }

    private void AppendCompressedChannel(
        bool bonusChannel,
        int sourceCount,
        int targetCount,
        List<ScoreVisualToken> destination)
    {
        if (sourceCount <= 0 || targetCount <= 0) return;

        List<ScoreVisualToken> channel =
            new List<ScoreVisualToken>(sourceCount);

        for (int i = 0; i < revealTokens.Count; i++)
        {
            ScoreVisualToken token = revealTokens[i];
            if (token.isBonus == bonusChannel) channel.Add(token);
        }

        targetCount = Mathf.Min(targetCount, channel.Count);

        for (int group = 0; group < targetCount; group++)
        {
            int start = Mathf.FloorToInt((float)group * channel.Count / targetCount);
            int end = Mathf.FloorToInt((float)(group + 1) * channel.Count / targetCount);
            end = Mathf.Max(start + 1, end);

            int combinedScore = 0;

            for (int i = start; i < end && i < channel.Count; i++)
                combinedScore += Mathf.Max(0, channel[i].scoreValue);

            ScoreVisualToken representative = channel[start];

            destination.Add(new ScoreVisualToken
            {
                prefab = representative.prefab,
                scoreValue = combinedScore,
                isBonus = bonusChannel
            });
        }
    }

    private static int DivideRoundUp(int value, int divisor)
    {
        if (value <= 0) return 0;
        return (value + Mathf.Max(1, divisor) - 1) / Mathf.Max(1, divisor);
    }

    #endregion

    #region Reveal Runtime

    private IEnumerator RevealRoutine()
    {
        yield return RunTokenPhase(0, baseVisualTokens);

        if (baseVisualTokens < revealTokens.Count)
        {
            // Make the ordinary total readable for at least one rendered frame
            // before reward cargo begins adding the parenthesized score.
            UpdateMovingTop(Time.unscaledDeltaTime, false);
            yield return null;

            yield return RunTokenPhase(baseVisualTokens, revealTokens.Count);
        }

        displayedBaseScore = finalBaseScore;
        displayedBonusScore = finalBonusScore;
        displayedScore = finalScore;

        if (finalBonusScore > 0)
        {
            // The reward phase has already shown Base (+Reward). Collapse it
            // to the final multiplied total on the following rendered frame.
            rewardDisplayCollapsed = true;
            RefreshScoreText(true);
        }

        // Let the cart/camera/kit reach the exact final height even when the
        // last several token flights completed during the same rendered frame.
        while (Mathf.Abs(currentCartLift - targetCartLift) > 0.001f)
        {
            UpdateMovingTop(Time.unscaledDeltaTime, false);
            yield return null;
        }

        UpdateMovingTop(0f, true);

        if (activeProfile.CompletionRevealDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                activeProfile.CompletionRevealDelay
            );
        }

        RefreshRankText();
        SetTextVisible(finalRankText, true);
        SetCompletionVisible(true);

        presenting = false;
        complete = true;
        revealRoutine = null;

        onRevealCompleted?.Invoke();
        OnRevealCompleted?.Invoke(playerIndex);
    }

    private IEnumerator RunTokenPhase(int startIndex, int endIndex)
    {
        int nextTokenIndex = Mathf.Clamp(startIndex, 0, revealTokens.Count);
        int exclusiveEnd = Mathf.Clamp(endIndex, nextTokenIndex, revealTokens.Count);
        float spawnCountdown = 0f;

        while (nextTokenIndex < exclusiveEnd || activeFlights.Count > 0)
        {
            float deltaTime = Time.unscaledDeltaTime;
            spawnCountdown -= deltaTime;

            while (nextTokenIndex < exclusiveEnd && spawnCountdown <= 0f)
            {
                SpawnTokenFlight(revealTokens[nextTokenIndex], nextTokenIndex);
                nextTokenIndex++;
                spawnCountdown += activeProfile.TokenSpawnInterval;
            }

            UpdateTokenFlights(deltaTime);
            UpdateMovingTop(deltaTime, false);

            yield return null;
        }
    }

    private void SpawnTokenFlight(ScoreVisualToken token, int tokenIndex)
    {
        if (token == null || stagedCart == null) return;

        GetTokenTargetPose(
            tokenIndex,
            token.isBonus,
            out Vector3 targetPosition,
            out Quaternion targetRotation
        );

        Vector3 startPosition =
            stagedCart.position +
            transform.TransformDirection(activeProfile.SpawnOffsetFromCart);

        GameObject instance = token.prefab != null
            ? Instantiate(token.prefab, runtimeVisualRoot)
            : CreatePrototypeVisual();

        if (instance == null)
        {
            // A missing visual must never lose score. CreatePrototypeVisual has
            // no dependency other than Unity primitives, so this is defensive.
            LandTokenWithoutVisual(token);
            return;
        }

        instance.name =
            token.isBonus
                ? $"FinalBonusScore_P{playerIndex}_{tokenIndex}"
                : $"FinalCargoScore_P{playerIndex}_{tokenIndex}";

        instance.transform.SetPositionAndRotation(startPosition, stagedCart.rotation);
        PrepareAsVisualOnly(instance);

        Vector3 targetScale = instance.transform.localScale;
        instance.transform.localScale =
            targetScale * activeProfile.TokenStartScaleMultiplier;

        spawnedVisuals.Add(instance);

        activeFlights.Add(new ActiveTokenFlight
        {
            instance = instance,
            startPosition = startPosition,
            targetPosition = targetPosition,
            startRotation = stagedCart.rotation,
            targetRotation = targetRotation,
            targetLocalScale = targetScale,
            elapsed = 0f,
            scoreValue = token.scoreValue,
            isBonus = token.isBonus
        });
    }

    private void UpdateTokenFlights(float deltaTime)
    {
        float duration = activeProfile.TokenTravelDuration;
        Vector3 stackDirection = GetStackDirection();

        for (int i = activeFlights.Count - 1; i >= 0; i--)
        {
            ActiveTokenFlight flight = activeFlights[i];

            if (flight.instance == null)
            {
                activeFlights.RemoveAt(i);
                LandScoreValue(flight.scoreValue, flight.isBonus);
                continue;
            }

            flight.elapsed += deltaTime;

            float normalizedTime = Mathf.Clamp01(flight.elapsed / duration);
            float travelT = activeProfile.EvaluateTokenTravel(normalizedTime);
            float arc =
                4f * normalizedTime * (1f - normalizedTime) *
                activeProfile.TokenTravelArcHeight;

            Transform visualTransform = flight.instance.transform;
            visualTransform.position =
                Vector3.Lerp(
                    flight.startPosition,
                    flight.targetPosition,
                    travelT
                ) +
                stackDirection * arc;

            Quaternion travelRotation = Quaternion.Slerp(
                flight.startRotation,
                flight.targetRotation,
                travelT
            );

            visualTransform.rotation =
                travelRotation *
                Quaternion.AngleAxis(
                    activeProfile.TokenSpinDegrees * travelT,
                    Vector3.up
                );

            visualTransform.localScale = Vector3.Lerp(
                flight.targetLocalScale * activeProfile.TokenStartScaleMultiplier,
                flight.targetLocalScale,
                travelT
            );

            if (normalizedTime < 1f) continue;

            visualTransform.SetPositionAndRotation(
                flight.targetPosition,
                flight.targetRotation
            );
            visualTransform.localScale = flight.targetLocalScale;

            activeFlights.RemoveAt(i);
            LandScoreValue(flight.scoreValue, flight.isBonus);
        }
    }

    private void LandTokenWithoutVisual(ScoreVisualToken token)
    {
        LandScoreValue(token.scoreValue, token.isBonus);
    }

    private void LandScoreValue(int scoreValue, bool isBonus)
    {
        landedVisualTokens++;
        int safeScoreValue = Mathf.Max(0, scoreValue);

        if (isBonus)
        {
            displayedBonusScore = Mathf.Min(
                finalBonusScore,
                displayedBonusScore + safeScoreValue
            );
        }
        else
        {
            displayedBaseScore = Mathf.Min(
                finalBaseScore,
                displayedBaseScore + safeScoreValue
            );
        }

        displayedScore = Mathf.Min(
            finalScore,
            displayedBaseScore + displayedBonusScore
        );

        int slotsPerLayer = Mathf.Max(1, validLayerSlots.Count);
        int completedOrStartedLayers = DivideRoundUp(landedVisualTokens, slotsPerLayer);
        targetCartLift = completedOrStartedLayers * activeProfile.LayerHeight;

        RefreshScoreText(true);

        onScoreTokenLanded?.Invoke(playerIndex, displayedScore, isBonus);
        OnScoreTokenLanded?.Invoke(playerIndex, displayedScore, isBonus);
    }

    private void UpdateMovingTop(float deltaTime, bool snap)
    {
        if (stagedCart == null) return;

        if (snap || deltaTime <= 0f)
        {
            currentCartLift = targetCartLift;
            cartLiftVelocity = 0f;
        }
        else
        {
            currentCartLift = Mathf.SmoothDamp(
                currentCartLift,
                targetCartLift,
                ref cartLiftVelocity,
                activeProfile.CartRiseSmoothTime,
                Mathf.Infinity,
                deltaTime
            );
        }

        Vector3 lift = GetStackDirection() * currentCartLift;
        stagedCart.SetPositionAndRotation(cartBasePosition + lift, cartBaseRotation);

        if (topPresentationRoot != null && !topPresentationMovesWithCartByHierarchy)
            topPresentationRoot.position = topPresentationBasePosition + lift;

        if (cameraFocusTarget != null && !cameraFocusMovesWithCartByHierarchy)
            cameraFocusTarget.position = cameraFocusBasePosition + lift;
    }

    #endregion

    #region Layout / Visual Helpers

    private void GetTokenTargetPose(
        int tokenIndex,
        bool isBonus,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        int slotCount = Mathf.Max(1, validLayerSlots.Count);
        int slotIndex = tokenIndex % slotCount;
        int layerIndex = tokenIndex / slotCount;

        Transform slot = validLayerSlots[slotIndex];
        worldPosition =
            slot.position +
            GetStackDirection() * (activeProfile.LayerHeight * layerIndex);
        worldRotation = slot.rotation;

        if (!isBonus && activeProfile.RandomizeOrdinaryCargoFinalYaw)
        {
            Vector2 yawRange = activeProfile.OrdinaryCargoFinalYawRange;
            float randomYaw = UnityEngine.Random.Range(yawRange.x, yawRange.y);

            worldRotation *= Quaternion.AngleAxis(randomYaw, Vector3.up);
        }
    }

    private Vector3 GetStackDirection()
    {
        Vector3 direction =
            stackDirectionReference != null
                ? stackDirectionReference.up
                : Vector3.up;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.up;
    }

    private void RebuildValidSlotCache()
    {
        validLayerSlots.Clear();
        if (firstLayerSlots == null) return;

        for (int i = 0; i < firstLayerSlots.Length; i++)
        {
            Transform slot = firstLayerSlots[i];
            if (slot != null) validLayerSlots.Add(slot);
        }
    }

    private void EnsureRuntimeVisualRoot()
    {
        if (runtimeVisualRoot != null) return;

        Transform existing = transform.Find("Runtime Final Score Cargo");

        if (existing != null)
        {
            runtimeVisualRoot = existing;
            return;
        }

        GameObject root = new GameObject("Runtime Final Score Cargo");
        root.transform.SetParent(transform, false);
        runtimeVisualRoot = root.transform;
    }

    private GameObject CreatePrototypeVisual()
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(runtimeVisualRoot, false);
        sphere.transform.localScale =
            Vector3.one * activeProfile.PrototypeFallbackScale;

        Renderer renderer = sphere.GetComponent<Renderer>();

        if (renderer != null)
        {
            if (activeProfile.PrototypeFallbackMaterial != null)
                renderer.sharedMaterial = activeProfile.PrototypeFallbackMaterial;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            Color color = Color.white;

            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        return sphere;
    }

    private static void PrepareAsVisualOnly(GameObject visual)
    {
        Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = false;
        }

        Rigidbody[] bodies = visual.GetComponentsInChildren<Rigidbody>(true);

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null) continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private void RefreshScoreText(bool animate)
    {
        if (liveScoreText == null || activeProfile == null) return;

        int multiplier = activeProfile.ScoreDisplayMultiplier;
        int multipliedBaseScore = displayedBaseScore * multiplier;
        int multipliedBonusScore = displayedBonusScore * multiplier;
        displayedPresentationScore = displayedScore * multiplier;

        try
        {
            liveScoreText.text =
                displayedBonusScore > 0 && !rewardDisplayCollapsed
                    ? string.Format(
                        activeProfile.RewardScoreTextFormat,
                        multipliedBaseScore,
                        multipliedBonusScore
                    )
                    : string.Format(
                        activeProfile.ScoreTextFormat,
                        displayedPresentationScore
                    );
        }
        catch (FormatException)
        {
            liveScoreText.text =
                displayedBonusScore > 0 && !rewardDisplayCollapsed
                    ? $"{multipliedBaseScore} (+{multipliedBonusScore})"
                    : displayedPresentationScore.ToString();
        }

        if (animate) PlayScoreTextPop();
    }

    private void PlayScoreTextPop()
    {
        if (liveScoreText == null ||
            activeProfile == null ||
            !activeProfile.AnimateScoreTextChanges)
        {
            return;
        }

        CacheScoreTextBaseScale();
        StopScoreTextPop(true);
        scoreTextPopRoutine = StartCoroutine(AnimateScoreTextPop());
    }

    private IEnumerator AnimateScoreTextPop()
    {
        float duration = activeProfile.ScoreTextPopDuration;
        float peakMultiplier = activeProfile.ScoreTextPopScaleMultiplier;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalizedTime * Mathf.PI);
            float scaleMultiplier = Mathf.Lerp(1f, peakMultiplier, pulse);

            liveScoreText.transform.localScale =
                scoreTextBaseScale * scaleMultiplier;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        liveScoreText.transform.localScale = scoreTextBaseScale;
        scoreTextPopRoutine = null;
    }

    private void CacheScoreTextBaseScale()
    {
        if (scoreTextBaseScaleCached || liveScoreText == null) return;

        scoreTextBaseScale = liveScoreText.transform.localScale;
        scoreTextBaseScaleCached = true;
    }

    private void StopScoreTextPop(bool restoreScale)
    {
        if (scoreTextPopRoutine != null)
        {
            StopCoroutine(scoreTextPopRoutine);
            scoreTextPopRoutine = null;
        }

        if (restoreScale && liveScoreText != null && scoreTextBaseScaleCached)
            liveScoreText.transform.localScale = scoreTextBaseScale;
    }

    private void RefreshRankText()
    {
        if (finalRankText == null || activeProfile == null) return;

        string ordinal = GetOrdinalRank(finalRank);

        try
        {
            finalRankText.text = string.Format(
                activeProfile.RankTextFormat,
                ordinal
            );
        }
        catch (FormatException)
        {
            finalRankText.text = ordinal;
        }
    }

    private static string GetOrdinalRank(int rank)
    {
        switch (rank)
        {
            case 1: return "1ST";
            case 2: return "2ND";
            case 3: return "3RD";
            case 4: return "4TH";
            default: return $"#{Mathf.Max(1, rank)}";
        }
    }

    private void SetCompletionVisible(bool visible)
    {
        if (completionVisualRoot != null)
            completionVisualRoot.SetActive(visible);
    }

    private static void SetTextVisible(TMP_Text text, bool visible)
    {
        if (text != null) text.gameObject.SetActive(visible);
    }

    private void StopRevealRoutine()
    {
        if (revealRoutine == null) return;
        StopCoroutine(revealRoutine);
        revealRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (firstLayerSlots == null) return;

        Vector3 stackDirection = GetStackDirection();

        for (int i = 0; i < firstLayerSlots.Length; i++)
        {
            Transform slot = firstLayerSlots[i];
            if (slot == null) continue;

            Gizmos.DrawWireSphere(slot.position, 0.07f);
            Gizmos.DrawLine(slot.position, slot.position + slot.forward * 0.16f);

            float previewHeight =
                activeProfile != null
                    ? activeProfile.LayerHeight
                    : 0.35f;

            Gizmos.DrawWireCube(
                slot.position + stackDirection * previewHeight,
                Vector3.one * 0.1f
            );
        }
    }

    #endregion
}
