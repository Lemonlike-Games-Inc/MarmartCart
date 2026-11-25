using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-local darkness over each player's view: everything is blocked except
/// a lit circle around that player's own leading cart. Systems ask for it per
/// player through handles (the Blackout Policy; later a personal Blackout that
/// leaves its user out), each with a strength for fades and a lit radius.
/// PlayerDarknessView draws it. Without this component nothing goes dark.
///
/// Per player the strongest request wins, and the smallest lit radius among
/// the requests that are on. Each request also carries DarknessHudRules for
/// what the HUD does while dark; HUD drawers ask through GetHudMode, CanvasAlpha
/// and HudShapes.TryGetDarknessPass.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerViewDarkness : MonoBehaviour
{
    public const int MaxPlayers = PlayerViewService.MaxViews;

    private struct Request
    {
        public int Handle;
        public int PlayerIndex;
        public float Strength;
        public float LitRadius;
        public DarknessHudRules HudRules;
    }

    private readonly List<Request> requests = new List<Request>(MaxPlayers * 2);
    private int lastHandle;

    public static PlayerViewDarkness Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    /// <summary>
    /// Darkens this player's view until removed, starting at strength 0.
    /// The lit radius is in world metres around their leading cart. Hud Rules are read live
    /// (null = DarknessHudRules.Default). Returns a handle; 0 means nothing was added.
    /// </summary>
    public int Add(int playerIndex, float litRadius, DarknessHudRules hudRules = null)
    {
        if (playerIndex < 1 || playerIndex > MaxPlayers) return 0;

        lastHandle++;
        if (lastHandle <= 0) lastHandle = 1;

        requests.Add(new Request
        {
            Handle = lastHandle,
            PlayerIndex = playerIndex,
            Strength = 0f,
            LitRadius = Mathf.Max(0f, litRadius),
            HudRules = hudRules ?? DarknessHudRules.Default
        });

        return lastHandle;
    }

    /// <summary>0 = no darkness, 1 = full darkness outside the lit circle.</summary>
    public void SetStrength(int handle, float strength01)
    {
        int index = IndexOf(handle);
        if (index < 0) return;

        Request request = requests[index];
        request.Strength = Mathf.Clamp01(strength01);
        requests[index] = request;
    }

    public void SetLitRadius(int handle, float litRadius)
    {
        int index = IndexOf(handle);
        if (index < 0) return;

        Request request = requests[index];
        request.LitRadius = Mathf.Max(0f, litRadius);
        requests[index] = request;
    }

    public void Remove(int handle)
    {
        int index = IndexOf(handle);
        if (index >= 0) requests.RemoveAt(index);
    }

    /// <summary>This player's darkness now. False when their view is not dark at all.</summary>
    public bool TryGet(int playerIndex, out float strength01, out float litRadius)
    {
        strength01 = 0f;
        litRadius = float.MaxValue;

        for (int i = 0; i < requests.Count; i++)
        {
            Request request = requests[i];
            if (request.PlayerIndex != playerIndex || request.Strength <= 0f) continue;

            strength01 = Mathf.Max(strength01, request.Strength);
            litRadius = Mathf.Min(litRadius, request.LitRadius);
        }

        if (strength01 > 0f) return true;

        litRadius = 0f;
        return false;
    }

    /// <summary>
    /// This player's darkness strength now (0-1), 0 without a PlayerViewDarkness in the scene.
    /// For HUD that should fade out while its viewer is in the dark.
    /// </summary>
    public static float StrengthFor(int playerIndex)
    {
        return Instance != null && Instance.TryGet(playerIndex, out float strength, out _) ? strength : 0f;
    }

    /// <summary>
    /// How a HUD part should look for this viewer now: the stricter setting among the
    /// requests that are on. Covered with strength 0 when the view is not dark.
    /// </summary>
    public static DarknessHudMode GetHudMode(int playerIndex, DarknessHudElement element, out float strength01)
    {
        strength01 = 0f;
        if (Instance == null) return DarknessHudMode.Covered;

        List<Request> list = Instance.requests;
        DarknessHudMode mode = DarknessHudMode.OnTop;

        for (int i = 0; i < list.Count; i++)
        {
            Request request = list[i];
            if (request.PlayerIndex != playerIndex || request.Strength <= 0f) continue;

            strength01 = Mathf.Max(strength01, request.Strength);
            mode = DarknessHudRules.Stricter(mode, request.HudRules.Get(element));
        }

        return strength01 > 0f ? mode : DarknessHudMode.Covered;
    }

    /// <summary>
    /// Alpha for a canvas HUD part, which always draws above the darkness: 1 when its
    /// setting is On Top or the view is not dark, otherwise it fades out with the darkness.
    /// </summary>
    public static float CanvasAlpha(int playerIndex, DarknessHudElement element)
    {
        DarknessHudMode mode = GetHudMode(playerIndex, element, out float strength);
        return mode == DarknessHudMode.OnTop ? 1f : 1f - strength;
    }

    private int IndexOf(int handle)
    {
        if (handle == 0) return -1;

        for (int i = 0; i < requests.Count; i++)
        {
            if (requests[i].Handle == handle) return i;
        }

        return -1;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[UI] More than one PlayerViewDarkness in the scene; the newest one is used.", this);

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
