using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime ownership / loose-state behavior for one follower cart.
///
/// Current responsibilities:
/// - player-owned vs loose state;
/// - vulnerable state;
/// - direct existing-instance recollection into SnakeCartManager;
/// - team color / team outline;
/// - collect VFX;
/// - loose-cart disappearance warning;
/// - central owned -> loose cargo normalization and overload spill.
///
/// IMPORTANT:
/// Cargo itself is authoritative on ChainCartCargo.
/// A loose cart keeps all safe cargo. Only LOCAL overload is spilled.
/// Cargo contents are authoritative exclusively on ChainCartCargo.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class ChainedCartManager : MonoBehaviour, ISpawnerHoldable
{
    private const int MaxSupportedPlayers = 4;

    #region Ownership State

    [Header("Ownership State")]
    [field: SerializeField]
    public bool isCollectedByPlayer { get; private set; }

    [Header("Runtime - Read Only")]
    [SerializeField] private bool isVulnerable;
    [SerializeField] private bool collectionCommitted;
    [SerializeField] private bool collectionWaitingForNextFixedUpdate;

    private SnakeCartManager pendingCollectingSnake;

    public bool IsVulnerable => isVulnerable;

    /// <summary>
    /// True while this cart is either fully player-owned OR has already been
    /// claimed and staged into a player's chain but has not yet finalized
    /// isCollectedByPlayer on the next physics step.
    ///
    /// SnakeCartManager uses this for topology ownership only.
    /// Battle logic should continue using isCollectedByPlayer.
    /// </summary>
    public bool IsOwnedOrCollectionPending => isCollectedByPlayer || collectionCommitted;

    public bool isAvailable => !isCollectedByPlayer && !collectionCommitted;

    /// <summary>
    /// Raised after this cart completes an owned/loose state transition.
    /// Semantic consumers such as PowerupCartTarget can refresh immediately
    /// without polling every cart in Update.
    /// </summary>
    public event System.Action<ChainedCartManager> OnCollectionStateChanged;

    #endregion

    #region References

    [Header("References")]
    [SerializeField] private ParticleSystem collectVFX;
    [SerializeField] private Renderer cartRenderer;
    [SerializeField] private CartMaterialManager cartMaterialManager;
    [SerializeField] private CartTeamOutlineController teamOutlineController;
    [SerializeField] private ChainCartCargo chainCartCargo;

    [Header("Optional SFX")]
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private string collectCartSfxKey = "";
    [SerializeField] private string cargoSpillSfxKey = "";

    private Rigidbody rb;
    private RigidbodyInterpolation looseInterpolation;
    #endregion

    #region Cargo Spill

    [Header("Cargo Spill")]
    [Tooltip("Generic GroceryLootPickup prefab used when the GroceryLootDefinition has no dedicated World Pickup Prefab.")]
    [SerializeField] private GroceryLootPickup fallbackSpillPickupPrefab;

    [Min(0f)]
    [SerializeField] private float spillSpawnHeight = 0.6f;

    [Min(0f)]
    [SerializeField] private float spillSpawnRadius = 1.0f;

    [Min(0f)]
    [SerializeField] private float spillImpulseMin = 2f;

    [Min(0f)]
    [SerializeField] private float spillImpulseMax = 5f;

    [Min(0f)]
    [SerializeField] private float spillUpwardImpulse = 2f;

    [Tooltip("Prevents freshly spilled groceries from being immediately recollected by overlapping player/cart colliders.")]
    [Min(0f)]
    [SerializeField] private float spilledPickupCollectionDelay = 0.35f;

    [Header("Cargo Spill Runtime - Read Only")]
    [SerializeField] private int lastSpilledCargoCount;

    private readonly List<CargoEntry> spillBuffer = new List<CargoEntry>(16);

    #endregion

    #region Self-Destruct

    [Header("Loose Cart Self-Destruct")]
    [Tooltip("Loose carts disappear after this duration. 0 disables self-destruction.")]
    [Min(0f)]
    [SerializeField] private float disappearTime = 15f;

    [Tooltip("How long before disappearing the CartMaterialManager warning begins.")]
    [Min(0f)]
    [SerializeField] private float disappearWarningDuration = 3f;

    [Header("Runtime - Read Only")]
    [SerializeField] private bool disappearWarningStarted;
    [SerializeField] private bool heldBySpawner;

    private Coroutine disappearRoutine;

    #endregion

    #region Team Color

    [Header("Team Color")]
    [SerializeField] private Color defaultColor = Color.white;

    [SerializeField]
    private Color[] playerTeamColors = new Color[MaxSupportedPlayers]
    {
        Color.blue,
        Color.red,
        Color.green,
        Color.yellow
    };

    [Tooltip("Material slot whose color represents the owning player's team.")]
    [Min(0)]
    [SerializeField] private int teamColorMaterialIndex = 1;

    #endregion


    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        looseInterpolation = rb.interpolation;

        if (cartMaterialManager == null) cartMaterialManager = GetComponentInChildren<CartMaterialManager>(true);
        if (teamOutlineController == null) teamOutlineController = GetComponentInChildren<CartTeamOutlineController>(true);
        if (chainCartCargo == null) chainCartCargo = GetComponentInChildren<ChainCartCargo>(true);

        if (collectVFX == null) Debug.LogWarning("[ChainedCartManager] Collect VFX is not assigned.", this);
        if (cartRenderer == null) Debug.LogWarning("[ChainedCartManager] Cart Renderer is not assigned.", this);
        if (chainCartCargo == null) Debug.LogWarning("[ChainedCartManager] ChainCartCargo is not assigned/found.", this);

        SetCartTeamColor();
    }

    private void Start()
    {
        if (isAvailable) RestartDisappearCountdown();
    }

    private void FixedUpdate()
    {
        FinalizeStagedCollectionIfReady();
    }

    private void OnDisable()
    {
        StopDisappearCountdown();
    }

    #endregion

    #region Vulnerable State

    public void SetVulnerable(bool vulnerable)
    {
        bool validVulnerableState = vulnerable && isCollectedByPlayer;

        if (isVulnerable == validVulnerableState) return;

        isVulnerable = validVulnerableState;

        if (cartMaterialManager != null) cartMaterialManager.SetVulnerableMode(isVulnerable);
    }

    #endregion

    #region Loose Collection

    private void OnTriggerEnter(Collider other)
    {
        if (!isAvailable || other == null) return;

        if (!TryResolveCollectingPlayer(
                other,
                out SnakeCartManager collectingSnake,
                out LeadingCartBattleController battleController))
        {
            return;
        }

        if (battleController != null && battleController.IsInGhostMode) return;

        TryCommitExistingInstanceCollection(collectingSnake);
    }

    private bool TryResolveCollectingPlayer(
        Collider other,
        out SnakeCartManager collectingSnake,
        out LeadingCartBattleController battleController)
    {
        collectingSnake = other.GetComponentInParent<SnakeCartManager>();
        battleController = null;

        if (collectingSnake == null) return false;

        List<GameObject> snakeBody = collectingSnake.GetSnakeBody();

        if (snakeBody != null && snakeBody.Count > 0 && snakeBody[0] != null)
        {
            battleController = snakeBody[0].GetComponentInChildren<LeadingCartBattleController>(true);
        }

        return true;
    }

    private bool TryCommitExistingInstanceCollection(SnakeCartManager collectingSnake)
    {
        if (collectionCommitted || collectingSnake == null) return false;

        // Lock this loose cart immediately so another player cannot also collect
        // it during the same physics step.
        collectionCommitted = true;
        StopDisappearCountdown();

        // Stage first: move the same physical cart behind the collector while
        // intentionally keeping isCollectedByPlayer == false.
        if (!collectingSnake.TryCollectExistingFollower(this))
        {
            collectionCommitted = false;
            RestartDisappearCountdown();
            return false;
        }

        // It is now physically owned, even though the logical state finishes
        // on the next FixedUpdate.
        SetCollectedPhysicsState(true);

        pendingCollectingSnake = collectingSnake;
        collectionWaitingForNextFixedUpdate = true;

        return true;
    }

    private void FinalizeStagedCollectionIfReady()
    {
        if (!collectionWaitingForNextFixedUpdate) return;

        collectionWaitingForNextFixedUpdate = false;

        SnakeCartManager collectingSnake = pendingCollectingSnake;
        pendingCollectingSnake = null;

        if (collectingSnake == null ||
            !collectingSnake.FinalizeExistingFollowerCollection(this))
        {
            RestoreLooseStateAfterFailedCollection();
            return;
        }

        if (!string.IsNullOrEmpty(collectCartSfxKey) && sfxManager != null)
        {
            sfxManager.PlaySFX(collectCartSfxKey);
        }
    }

    #endregion

    #region Player Ownership

    public void CollectByPlayer()
    {
        SetCollectedPhysicsState(true);

        SetVulnerable(false);

        isCollectedByPlayer = true;
        collectionCommitted = false;
        collectionWaitingForNextFixedUpdate = false;
        pendingCollectingSnake = null;

        StopDisappearCountdown();
        disappearWarningStarted = false;

        SetCartTeamColor();

        if (teamOutlineController != null)
        {
            int playerId = TagToPlayerIndex(gameObject.tag) + 1;

            if (playerId >= 1 && playerId <= MaxSupportedPlayers) teamOutlineController.SetTeam(playerId);
            else teamOutlineController.ClearTeam();
        }

        PublishCollectionStateChanged();
    }

    /// <summary>
    /// Used only if SnakeCartManager fails to adopt this loose instance after
    /// collection has already been committed.
    /// </summary>
    public void RestoreLooseStateAfterFailedCollection()
    {
        SetCollectedPhysicsState(false);
        SetVulnerable(false);

        isCollectedByPlayer = false;
        collectionCommitted = false;
        collectionWaitingForNextFixedUpdate = false;
        pendingCollectingSnake = null;
        gameObject.tag = "Item";

        if (teamOutlineController != null) teamOutlineController.ClearTeam();

        SetCartTeamColor();
        RestartDisappearCountdown();
        PublishCollectionStateChanged();
    }

    public void ResetDisappearCountDown()
    {
        if (isAvailable) RestartDisappearCountdown();
        else StopDisappearCountdown();
    }

    #endregion

    #region Detach / Loose State

    public void OnDetach()
    {
        Detach(GetRandomPlanarDirection(), Random.Range(10f, 30f), 0f);
    }

    public void OnDetach(Vector3 hitDirection)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(hitDirection, Vector3.up);

        if (planarDirection.sqrMagnitude < 0.0001f) planarDirection = GetRandomPlanarDirection();
        else planarDirection.Normalize();

        Detach(planarDirection, Random.Range(30f, 50f), 30f);
    }

    private void Detach(Vector3 baseDirection, float forceMagnitude, float randomDirectionAngle)
    {
        if (rb == null) return;

        // CENTRAL RULE:
        // every owned -> loose transition normalizes this physical cart first.
        PrepareForLooseState();

        // Must happen before AddForce/AddTorque.
        SetCollectedPhysicsState(false);

        SetVulnerable(false);

        gameObject.tag = "Item";

        if (teamOutlineController != null) teamOutlineController.ClearTeam();

        isCollectedByPlayer = false;
        collectionCommitted = false;
        collectionWaitingForNextFixedUpdate = false;
        pendingCollectingSnake = null;

        SetCartTeamColor();
        RestartDisappearCountdown();
        PublishCollectionStateChanged();

        Vector3 forceDirection = baseDirection;

        if (randomDirectionAngle > 0f)
        {
            float randomAngle = Random.Range(-randomDirectionAngle, randomDirectionAngle);
            forceDirection = Quaternion.Euler(0f, randomAngle, 0f) * forceDirection;
        }

        rb.AddForce(forceDirection.normalized * forceMagnitude, ForceMode.Impulse);

        Vector3 randomTorque = Random.insideUnitSphere * Random.Range(20f, 30f); 
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }

    /// <summary>
    /// Converts an owned cart into a valid loose-cart cargo state.
    ///
    /// Safe cargo remains authoritative on this physical cart.
    /// Only LOCAL overload is removed and respawned as world grocery loot.
    ///
    /// Returns the number of successfully spawned spilled pickups.
    /// </summary>
    public int PrepareForLooseState()
    {
        lastSpilledCargoCount = 0;

        if (chainCartCargo == null || !chainCartCargo.IsOverloaded) return 0;

        spillBuffer.Clear();
        chainCartCargo.RemoveOverloadCargo(spillBuffer);

        for (int i = 0; i < spillBuffer.Count; i++)
        {
            CargoEntry entry = spillBuffer[i];
            if (entry == null) continue;

            if (TrySpawnSpilledCargo(entry))
            {
                lastSpilledCargoCount++;
                continue;
            }

            // Never silently delete cargo because a spill prefab was misconfigured.
            // Restore the original CargoEntry to this cart instead.
            chainCartCargo.TryAddCargoEntry(entry);
        }

        spillBuffer.Clear();

        if (lastSpilledCargoCount > 0 &&
            sfxManager != null &&
            !string.IsNullOrEmpty(cargoSpillSfxKey))
        {
            sfxManager.PlaySFX(cargoSpillSfxKey);
        }

        return lastSpilledCargoCount;
    }

    private bool TrySpawnSpilledCargo(CargoEntry entry)
    {
        if (entry == null || entry.SourceLoot == null) return false;

        GameObject sourcePrefab = entry.SourceLoot.WorldPickupPrefab;

        if (sourcePrefab == null && fallbackSpillPickupPrefab != null)
        {
            sourcePrefab = fallbackSpillPickupPrefab.gameObject;
        }

        if (sourcePrefab == null)
        {
            Debug.LogError(
                $"[ChainedCartManager] Cannot spill '{entry.SourceLoot.DisplayName}': no World Pickup Prefab and no Fallback Spill Pickup Prefab.",
                this
            );

            return false;
        }

        Vector2 randomCircle = Random.insideUnitCircle;
        if (randomCircle.sqrMagnitude < 0.0001f) randomCircle = Vector2.right;
        randomCircle.Normalize();

        float radius = Random.Range(0.25f * spillSpawnRadius, spillSpawnRadius);

        Vector3 planarOffset = new Vector3(randomCircle.x, 0f, randomCircle.y) * radius;
        Vector3 spawnPosition = transform.position + Vector3.up * spillSpawnHeight + planarOffset;
        Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject spawnedObject = Instantiate(sourcePrefab, spawnPosition, spawnRotation);

        GroceryLootPickup pickup = spawnedObject.GetComponent<GroceryLootPickup>();

        if (pickup == null)
        {
            Debug.LogError(
                $"[ChainedCartManager] Spill prefab '{sourcePrefab.name}' must have GroceryLootPickup on its root GameObject.",
                spawnedObject
            );

            Destroy(spawnedObject);
            return false;
        }

        pickup.Initialize(entry.SourceLoot);
        pickup.ArmCollectionAfterDelay(spilledPickupCollectionDelay);

        Rigidbody pickupBody = spawnedObject.GetComponent<Rigidbody>();

        if (pickupBody != null)
        {
            Vector3 outward = planarOffset.sqrMagnitude > 0.0001f ? planarOffset.normalized : GetRandomPlanarDirection();
            float impulse = Random.Range(spillImpulseMin, spillImpulseMax);

            Vector3 spillImpulse = outward * impulse + Vector3.up * spillUpwardImpulse;
            pickupBody.AddForce(spillImpulse, ForceMode.Impulse);
        }

        return true;
    }

    private Vector3 GetRandomPlanarDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(Random.insideUnitSphere, Vector3.up);

        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;

        return direction.normalized;
    }

    #endregion

    #region Spawner Hold

    public void OnSpawnerHoldStart()
    {
        heldBySpawner = true;
        StopDisappearCountdown();
    }

    public void OnSpawnerHoldEnd()
    {
        heldBySpawner = false;

        if (isAvailable) RestartDisappearCountdown();
    }

    #endregion

    #region Loose Cart Lifetime

    private void RestartDisappearCountdown()
    {
        StopDisappearCountdown();

        disappearWarningStarted = false;

        if (!Application.isPlaying || !isAvailable || heldBySpawner || disappearTime <= 0f) return;

        disappearRoutine = StartCoroutine(DisappearRoutine());
    }

    private void StopDisappearCountdown()
    {
        if (disappearRoutine != null)
        {
            StopCoroutine(disappearRoutine);
            disappearRoutine = null;
        }

        // The disappearing-warning ghost visual lives independently inside
        // CartMaterialManager, so stopping the countdown coroutine alone is not
        // enough. Restore the cart's real/base materials as well.
        if (disappearWarningStarted)
        {
            disappearWarningStarted = false;

            if (cartMaterialManager != null &&
                cartMaterialManager.IsGhostVisualActive)
            {
                cartMaterialManager.ClearVisualMode();
            }
        }
    }

    private IEnumerator DisappearRoutine()
    {
        float warningDuration = Mathf.Clamp(disappearWarningDuration, 0f, disappearTime);
        float normalDuration = Mathf.Max(0f, disappearTime - warningDuration);

        if (normalDuration > 0f) yield return new WaitForSeconds(normalDuration);

        if (!isAvailable || heldBySpawner)
        {
            disappearRoutine = null;
            yield break;
        }

        if (warningDuration > 0f)
        {
            disappearWarningStarted = true;

            if (cartMaterialManager != null) cartMaterialManager.SetGhostMode(warningDuration);

            yield return new WaitForSeconds(warningDuration);
        }

        disappearRoutine = null;

        if (isAvailable && !heldBySpawner) Destroy(gameObject);
    }

    #endregion

    #region Team Color

    public void SetCartTeamColor()
    {
        if (cartRenderer == null) return;

        Material[] materials = cartRenderer.materials;

        if (teamColorMaterialIndex < 0 ||
            teamColorMaterialIndex >= materials.Length ||
            materials[teamColorMaterialIndex] == null)
        {
            return;
        }

        Color targetColor = defaultColor;

        if (isCollectedByPlayer)
        {
            int playerIndex = TagToPlayerIndex(gameObject.tag);

            if (playerIndex >= 0 && playerIndex < playerTeamColors.Length)
            {
                targetColor = playerTeamColors[playerIndex];
            }
        }

        materials[teamColorMaterialIndex].color = targetColor;
        cartRenderer.materials = materials;

        if (cartMaterialManager != null) cartMaterialManager.RefreshBaseMaterials();
    }

    private int TagToPlayerIndex(string objectTag)
    {
        switch (objectTag)
        {
            case "Player1": return 0;
            case "Player2": return 1;
            case "Player3": return 2;
            case "Player4": return 3;
            default: return -1;
        }
    }

    #endregion

    #region Collect VFX

    public void PlayVFX()
    {
        if (collectVFX == null) return;

        collectVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        collectVFX.Play();
    }

    #endregion

    #region Ownership Notifications

    private void PublishCollectionStateChanged()
    {
        OnCollectionStateChanged?.Invoke(this);
    }

    #endregion
    private void SetCollectedPhysicsState(bool collected)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (rb == null) return;

        if (collected)
        {
            // Clear loose-cart falling/spinning before making it kinematic.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = true;
        }
        else
        {
            // Must become dynamic before gravity or detach impulses can work.
            rb.isKinematic = false;
            rb.interpolation = looseInterpolation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    #region Validation

    private void OnValidate()
    {
        disappearTime = Mathf.Max(0f, disappearTime);
        disappearWarningDuration = Mathf.Clamp(disappearWarningDuration, 0f, disappearTime);
        teamColorMaterialIndex = Mathf.Max(0, teamColorMaterialIndex);

        spillSpawnHeight = Mathf.Max(0f, spillSpawnHeight);
        spillSpawnRadius = Mathf.Max(0f, spillSpawnRadius);
        spillImpulseMin = Mathf.Max(0f, spillImpulseMin);
        spillImpulseMax = Mathf.Max(spillImpulseMin, spillImpulseMax);
        spillUpwardImpulse = Mathf.Max(0f, spillUpwardImpulse);
        spilledPickupCollectionDelay = Mathf.Max(0f, spilledPickupCollectionDelay);
    }

    #endregion
}
