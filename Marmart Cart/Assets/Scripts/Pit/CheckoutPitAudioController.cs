using UnityEngine;

/// <summary>
/// Owns all sound feedback for one checkout pit.
///
/// The repeating cart beep uses a dedicated AudioSource because each accepted
/// cart needs an exact, increasing pitch. Checkout-complete and station-open
/// one-shots continue through the project's shared SfxManager.
/// </summary>
[DisallowMultipleComponent]
public sealed class CheckoutPitAudioController : MonoBehaviour
{
    #region Event Sources

    [Header("Pit Event Sources")]
    [SerializeField] private CheckOutManager checkOutManager;
    [SerializeField] private CartPitZone cartPitZone;
    [SerializeField] private CheckoutStationFlowController stationFlowController;

    #endregion

    #region Stacking Beep

    [Header("Stacking Checkout Beep")]
    [Tooltip("A dedicated source keeps checkout-beep pitch independent from all shared SFX.")]
    [SerializeField] private AudioSource checkoutBeepSource;

    [Tooltip("Creates a child AudioSource at runtime when Checkout Beep Source is unassigned.")]
    [SerializeField] private bool createMissingBeepSource = true;

    [SerializeField] private AudioClip checkoutBeepClip;

    [Range(0f, 1f)]
    [SerializeField] private float checkoutBeepVolume = 1f;

    [Tooltip("Pitch used by the first successfully checked-out cart in a session.")]
    [Range(0.01f, 3f)]
    [SerializeField] private float firstBeepPitch = 1f;

    [Tooltip("Pitch added for every later cart in the same checkout session.")]
    [Range(0f, 1f)]
    [SerializeField] private float pitchIncreasePerCart = 0.08f;

    [Tooltip("Hard ceiling for the rising checkout beep.")]
    [Range(0.01f, 3f)]
    [SerializeField] private float maximumBeepPitch = 1.6f;

    [Tooltip(
        "Stops a still-playing previous beep before the next one. Recommended " +
        "for a short, crisp scanner sound because one AudioSource cannot keep " +
        "different pitches on overlapping PlayOneShot voices."
    )]
    [SerializeField] private bool restartBeepIfStillPlaying = true;

    #endregion

    #region Shared One-Shots

    [Header("Shared SFX Manager One-Shots")]
    [SerializeField] private SfxManager sfxManager;

    [Tooltip("Played once when this pit leaves the checkout stop and begins auto-exit movement.")]
    [SerializeField] private string checkoutCompleteSfxKey = "CheckoutComplete";

    [Tooltip("Played once when MatchFlowDirector changes this station from closed to open.")]
    [SerializeField] private string stationOpenSfxKey = "CheckoutOpen";

    #endregion

    #region Runtime Diagnostics

    [Header("Runtime - Read Only")]
    [SerializeField] private int lastCheckoutPlayerIndex;
    [SerializeField] private int lastBeepNumberInSession;
    [SerializeField] private float lastBeepPitch = 1f;
    [SerializeField] private int checkoutExitSoundCount;
    [SerializeField] private int stationOpenSoundCount;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveEventSources();
        PrepareCheckoutBeepSource();
    }

    private void OnEnable()
    {
        ResolveEventSources();

        if (checkOutManager != null)
        {
            checkOutManager.OnCartCheckedOut -= HandleCartCheckedOut;
            checkOutManager.OnCartCheckedOut += HandleCartCheckedOut;
        }

        if (cartPitZone != null)
        {
            cartPitZone.OnCheckoutExitStarted -= HandleCheckoutExitStarted;
            cartPitZone.OnCheckoutExitStarted += HandleCheckoutExitStarted;
        }

        if (stationFlowController != null)
        {
            stationFlowController.OnStationOpened -= HandleStationOpened;
            stationFlowController.OnStationOpened += HandleStationOpened;
        }
    }

    private void OnDisable()
    {
        if (checkOutManager != null)
        {
            checkOutManager.OnCartCheckedOut -= HandleCartCheckedOut;
        }

        if (cartPitZone != null)
        {
            cartPitZone.OnCheckoutExitStarted -= HandleCheckoutExitStarted;
        }

        if (stationFlowController != null)
        {
            stationFlowController.OnStationOpened -= HandleStationOpened;
        }

        if (checkoutBeepSource != null)
        {
            checkoutBeepSource.Stop();
            checkoutBeepSource.pitch = 1f;
        }

        lastCheckoutPlayerIndex = 0;
        lastBeepNumberInSession = 0;
        lastBeepPitch = 1f;
    }

    private void OnValidate()
    {
        checkoutBeepVolume = Mathf.Clamp01(checkoutBeepVolume);
        firstBeepPitch = Mathf.Clamp(firstBeepPitch, 0.01f, 3f);
        pitchIncreasePerCart = Mathf.Max(0f, pitchIncreasePerCart);
        maximumBeepPitch = Mathf.Clamp(maximumBeepPitch, firstBeepPitch, 3f);
    }

    #endregion

    #region Event Handling

    private void HandleCartCheckedOut(
        int playerIndex,
        int cartNumberInSession,
        int cargoEntryCount
    )
    {
        lastCheckoutPlayerIndex = playerIndex;
        lastBeepNumberInSession = Mathf.Max(1, cartNumberInSession);
        lastBeepPitch = CalculateBeepPitch(lastBeepNumberInSession);

        if (checkoutBeepSource == null || checkoutBeepClip == null) return;

        if (restartBeepIfStillPlaying && checkoutBeepSource.isPlaying)
        {
            checkoutBeepSource.Stop();
        }

        checkoutBeepSource.pitch = lastBeepPitch;
        checkoutBeepSource.PlayOneShot(checkoutBeepClip, checkoutBeepVolume);
    }

    private void HandleCheckoutExitStarted(int playerIndex)
    {
        lastCheckoutPlayerIndex = playerIndex;
        checkoutExitSoundCount++;

        PlaySharedSfx(checkoutCompleteSfxKey);
    }

    private void HandleStationOpened()
    {
        stationOpenSoundCount++;

        PlaySharedSfx(stationOpenSfxKey);
    }

    #endregion

    #region Setup

    private void ResolveEventSources()
    {
        if (checkOutManager == null)
        {
            checkOutManager = GetComponent<CheckOutManager>();
        }

        if (cartPitZone == null)
        {
            cartPitZone = GetComponent<CartPitZone>();
        }

        if (stationFlowController == null)
        {
            stationFlowController = GetComponent<CheckoutStationFlowController>();
        }
    }

    private void PrepareCheckoutBeepSource()
    {
        if (checkoutBeepSource == null && createMissingBeepSource)
        {
            GameObject sourceObject = new GameObject("Checkout Beep Audio Source");
            sourceObject.transform.SetParent(transform, false);

            checkoutBeepSource = sourceObject.AddComponent<AudioSource>();
            checkoutBeepSource.spatialBlend = 0f;
        }

        if (checkoutBeepSource == null) return;

        checkoutBeepSource.playOnAwake = false;
        checkoutBeepSource.loop = false;
        checkoutBeepSource.pitch = 1f;
    }

    #endregion

    #region Helpers

    private float CalculateBeepPitch(int cartNumberInSession)
    {
        int zeroBasedCartNumber = Mathf.Max(0, cartNumberInSession - 1);

        return Mathf.Min(
            maximumBeepPitch,
            firstBeepPitch + zeroBasedCartNumber * pitchIncreasePerCart
        );
    }

    private void PlaySharedSfx(string sfxKey)
    {
        if (sfxManager == null || string.IsNullOrWhiteSpace(sfxKey)) return;

        sfxManager.PlaySFX(sfxKey);
    }

    #endregion
}
