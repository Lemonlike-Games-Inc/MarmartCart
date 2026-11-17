using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MarmartFX test-scene effect: every pickup loot bubble (the game's "Fresnel Sphere Outline" sphere on
/// GroceryLoot_* prefabs) gets a hand-drawn paint-drip look. At runtime the original sphere renderer is
/// hidden and a visual-only sphere with MarmartFX/VFX/PaintBubble is added next to it. No prefab, material
/// or game script is modified; turning the effect off restores the original renderer.
///
/// Starts itself in any scene that has MarmartFXPlayerFX (i.e. only the MarmartFX test scene).
/// </summary>
public class MarmartFXLootBubble : MonoBehaviour
{
    public MarmartFXVFXSettings settings;

    class Entry { public Renderer original; public GameObject copy; public Material mat; public Color baseColor; }

    readonly Dictionary<Renderer, Entry> m_Entries = new Dictionary<Renderer, Entry>();
    readonly List<Renderer> m_Dead = new List<Renderer>();
    Shader m_Shader;
    float m_NextScan;
    bool m_WasEnabled;

    static readonly int ID_Color = Shader.PropertyToID("_Color");
    static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");
    static readonly int ID_FillAlpha = Shader.PropertyToID("_FillAlpha");
    static readonly int ID_RimWidth = Shader.PropertyToID("_RimWidth");
    static readonly int ID_RimJitter = Shader.PropertyToID("_RimJitter");
    static readonly int ID_HighlightSize = Shader.PropertyToID("_HighlightSize");
    static readonly int ID_Wobble = Shader.PropertyToID("_Wobble");
    static readonly int ID_DripLength = Shader.PropertyToID("_DripLength");
    static readonly int ID_DripCount = Shader.PropertyToID("_DripCount");
    static readonly int ID_DripSpeed = Shader.PropertyToID("_DripSpeed");
    static readonly int ID_BoilFps = Shader.PropertyToID("_BoilFps");
    static readonly int ID_GlowColor = Shader.PropertyToID("_GlowColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<MarmartFXLootBubble>() != null) return;
        var fx = FindAnyObjectByType<MarmartFXPlayerFX>();
        if (fx == null || fx.settings == null) return;
        var go = new GameObject("MarmartFX Loot Bubbles");
        go.AddComponent<MarmartFXLootBubble>().settings = fx.settings;
    }

    MarmartFXVFXSettings.BubbleSettings S => settings.bubble;

    void Update()
    {
        if (settings == null) return;
        if (m_Shader == null) m_Shader = Shader.Find("MarmartFX/VFX/PaintBubble");
        if (m_Shader == null) return;

        bool on = S.enabled;
        if (on != m_WasEnabled)
        {
            m_WasEnabled = on;
            if (!on) RestoreAll();
        }
        if (!on) return;

        if (Time.unscaledTime >= m_NextScan)
        {
            m_NextScan = Time.unscaledTime + 0.5f;
            Scan();
        }

        m_Dead.Clear();
        foreach (var kv in m_Entries)
        {
            var e = kv.Value;
            if (kv.Key == null || e.copy == null) { m_Dead.Add(kv.Key); if (e.copy != null) Destroy(e.copy); continue; }
            // Follow the original's visibility (the game may hide/pop the bubble).
            bool vis = kv.Key.gameObject.activeInHierarchy;
            if (e.copy.activeSelf != vis) e.copy.SetActive(vis);
            if (kv.Key.enabled) kv.Key.enabled = false;
            Apply(e);
        }
        foreach (var r in m_Dead) m_Entries.Remove(r);
    }

    void Scan()
    {
        string name = string.IsNullOrEmpty(S.bubbleObjectName) ? "Fresnel Sphere Outline" : S.bubbleObjectName;
        foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mf.name != name) continue;
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || m_Entries.ContainsKey(r) || mf.sharedMesh == null) continue;
            if (!r.enabled) continue; // only bubbles the game actually shows

            var e = new Entry { original = r, baseColor = S.paintColor };
            var src = r.sharedMaterial;
            if (src != null && src.HasProperty(ID_GlowColor)) e.baseColor = src.GetColor(ID_GlowColor);
            else if (src != null && src.HasProperty(ID_Color)) e.baseColor = src.GetColor(ID_Color);

            var pickup = mf.GetComponentInParent<GroceryLootPickup>();
            if (pickup != null && pickup.GetComponent<MarmartFXLootPickupProbe>() == null)
                pickup.gameObject.AddComponent<MarmartFXLootPickupProbe>();

            e.copy = new GameObject("MFX_PaintBubble");
            e.copy.layer = mf.gameObject.layer;
            e.copy.transform.SetParent(mf.transform, false);
            e.copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var cr = e.copy.AddComponent<MeshRenderer>();
            e.mat = new Material(m_Shader) { name = "MFX PaintBubble" };
            cr.sharedMaterial = e.mat;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cr.receiveShadows = false;
            r.enabled = false;
            m_Entries[r] = e;
            Apply(e);
        }
    }

    void Apply(Entry e)
    {
        var m = e.mat;
        Color c = S.useOriginalColor ? e.baseColor : S.paintColor;
        c.a = 1f;
        m.SetColor(ID_Color, c);
        m.SetColor(ID_RimColor, new Color(c.r * S.rimDarken, c.g * S.rimDarken, c.b * S.rimDarken, 1f));
        m.SetFloat(ID_FillAlpha, S.fillAlpha);
        m.SetFloat(ID_RimWidth, S.rimWidth);
        m.SetFloat(ID_RimJitter, S.rimJitter);
        m.SetFloat(ID_HighlightSize, S.highlightSize);
        m.SetFloat(ID_Wobble, S.wobble);
        m.SetFloat(ID_DripLength, S.dripLength);
        m.SetFloat(ID_DripCount, S.dripColumns);
        m.SetFloat(ID_DripSpeed, S.dripSpeed);
        m.SetFloat(ID_BoilFps, S.boilFps);
    }

    void RestoreAll()
    {
        foreach (var kv in m_Entries)
        {
            if (kv.Key != null) kv.Key.enabled = true;
            if (kv.Value.copy != null) Destroy(kv.Value.copy);
            if (kv.Value.mat != null) Destroy(kv.Value.mat);
        }
        m_Entries.Clear();
    }

    void OnDisable() => RestoreAll();
}

/// <summary>Debug only: logs why a loot pickup does or doesn't get collected (reads game state, changes nothing).</summary>
public class MarmartFXLootPickupProbe : MonoBehaviour
{
    static readonly System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    float m_LastLog;

    void OnEnable()
    {
        var p = GetComponent<GroceryLootPickup>();
        if (p != null) p.OnCollected += (pk, cc) => MarmartFXDebugLog.Write($"LootProbe COLLECTED '{name}' by '{cc.name}'");
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time - m_LastLog < 0.5f) return;
        var cc = other.GetComponentInParent<CargoCapacityController>();
        if (cc == null) return;
        m_LastLog = Time.time;
        var p = GetComponent<GroceryLootPickup>();
        object armed = p != null ? typeof(GroceryLootPickup).GetField("collectionArmed", F)?.GetValue(p) : null;
        object collected = p != null ? typeof(GroceryLootPickup).GetField("collected", F)?.GetValue(p) : null;
        object def = p != null ? typeof(GroceryLootPickup).GetField("lootDefinition", F)?.GetValue(p) : null;
        var list = typeof(CargoCapacityController).GetField("orderedCargoCarts", F)?.GetValue(cc) as System.Collections.ICollection;
        var col = GetComponent<Collider>();
        MarmartFXDebugLog.Write($"LootProbe touch '{name}' by '{other.name}' ctrl='{cc.name}' armed={armed} collected={collected} def={(def != null)} cargoCarts={(list != null ? list.Count.ToString() : "?")} triggerEnabled={(col != null && col.enabled)}");
    }
}
