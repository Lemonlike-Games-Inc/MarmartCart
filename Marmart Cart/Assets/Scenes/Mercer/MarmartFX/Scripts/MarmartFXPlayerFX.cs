using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MarmartFX gameplay VFX for the test scene, all in player colours:
///  * boost trail (丝带 / 霓虹 / 速度线 / 残影)
///  * player cart highlight (描边 / 边缘发光 / 描边加发光 / 脚下光圈)
///  * "ate a new cart" effect (star burst, shock ring, punch, +1 popup)
/// Runtime only: listens to existing public events (CartControlScript.OnSpeedupHeld,
/// SnakeCartManager.OnOwnedFollowersChanged) and adds its own child objects; no game script,
/// prefab or material asset is modified. Settings live in a MarmartFXVFXSettings asset.
/// </summary>
[DisallowMultipleComponent]
public class MarmartFXPlayerFX : MonoBehaviour
{
    public MarmartFXVFXSettings settings;
    public Shader additiveShader;
    public Shader highlightShader;

    const string CopyPrefix = "MFX_";
    static Shader s_Additive;
    static Material s_StarMat;
    static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int ID_Shape = Shader.PropertyToID("_Shape");
    static readonly int ID_Softness = Shader.PropertyToID("_Softness");
    static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    static readonly int ID_GlowColor = Shader.PropertyToID("_GlowColor");
    static readonly int ID_GlowIntensity = Shader.PropertyToID("_GlowIntensity");
    static readonly int ID_GlowPower = Shader.PropertyToID("_GlowPower");
    static readonly int ID_FillIntensity = Shader.PropertyToID("_FillIntensity");
    static readonly int ID_PulseAmount = Shader.PropertyToID("_PulseAmount");
    static readonly int ID_PulseSpeed = Shader.PropertyToID("_PulseSpeed");
    static readonly int ID_WobbleAmount = Shader.PropertyToID("_WobbleAmount");
    static readonly int ID_WobbleFps = Shader.PropertyToID("_WobbleFps");
    static readonly int ID_WobbleFreq = Shader.PropertyToID("_WobbleFreq");
    static readonly int ID_Misregister = Shader.PropertyToID("_Misregister");
    static readonly int ID_UseSmoothNormal = Shader.PropertyToID("_UseSmoothNormal");
    static readonly int ID_StencilRef = Shader.PropertyToID("_StencilRef");
    static readonly int ID_StencilComp = Shader.PropertyToID("_StencilComp");
    static readonly int ID_MaskExpand = Shader.PropertyToID("_MaskExpand");
    static readonly int ID_OutlineBase = Shader.PropertyToID("_OutlineBase");
    static Shader s_MaskShader;
    static readonly Dictionary<Mesh, Mesh> s_SmoothMeshes = new Dictionary<Mesh, Mesh>();
    static readonly int ID_Fade = Shader.PropertyToID("_Fade");

    class PlayerState
    {
        public int id;
        public SnakeCartManager snake;
        public GameObject leader;
        public CartControlScript ctrl;
        public System.Action<bool> speedHandler;
        public System.Action followerHandler;
        public bool speedHeld;
        public int lastFollowerCount;

        public MarmartFXVFXSettings.TrailStyle builtTrailStyle = (MarmartFXVFXSettings.TrailStyle)(-1);
        public int builtLineCount;
        public readonly List<TrailRenderer> trails = new List<TrailRenderer>();
        public Material trailMat;
        public float ghostTimer;

        public bool highlightBuilt;
        public bool highlightFollowers;
        public bool highlightSmooth;
        public int highlightBodyCount;
        public readonly List<GameObject> highlightCopies = new List<GameObject>();
        public readonly List<Renderer> highlightSources = new List<Renderer>();
        public readonly List<Renderer> highlightCopyRenderers = new List<Renderer>();
        public Material highlightMat;
        public Material maskMat;
        public GameObject ring;
        public Material ringMat;
    }

    readonly Dictionary<SnakeCartManager, PlayerState> m_Players = new Dictionary<SnakeCartManager, PlayerState>();
    float m_NextScan;
    Material m_GhostMat;

    void Awake()
    {
        if (additiveShader == null) additiveShader = Shader.Find("MarmartFX/VFX/Additive");
        if (highlightShader == null) highlightShader = Shader.Find("MarmartFX/VFX/PlayerHighlight");
        s_Additive = additiveShader;
    }

    void OnDestroy()
    {
        foreach (var p in m_Players.Values) Unhook(p);
        m_Players.Clear();
    }

    void Start()
    {
        MarmartFXDebugLog.Write($"PlayerFX start: settings={(settings != null)} additive={(additiveShader != null)} highlight={(highlightShader != null)}");
    }

    void Update()
    {
        if (settings == null) return;
        if (Time.unscaledTime >= m_NextScan)
        {
            m_NextScan = Time.unscaledTime + 1f;
            Scan();
        }
        foreach (var p in m_Players.Values)
        {
            if (p.leader == null) continue;
            UpdateTrail(p);
            UpdateHighlight(p);
        }
    }

    // ------------------------------------------------------------------
    // Player discovery
    // ------------------------------------------------------------------

    string m_LastStatus;

    void Scan()
    {
        var sb = new System.Text.StringBuilder();
        var snakes = FindObjectsByType<SnakeCartManager>(FindObjectsSortMode.None);
        sb.Append($"PlayerFX scan: {snakes.Length} SnakeCartManager");
        foreach (var sn in snakes)
        {
            var b = sn.GetSnakeBody();
            var l = b != null && b.Count > 0 ? b[0] : null;
            sb.Append($" | P{sn.GetPlayerId()} body={b?.Count ?? -1} leader={(l != null ? l.name : "null")} ctrl={(l != null && l.GetComponentInChildren<CartControlScript>(true) != null)}");
        }
        string status = sb.ToString();
        if (status != m_LastStatus) { m_LastStatus = status; MarmartFXDebugLog.Write(status); }

        foreach (var snake in FindObjectsByType<SnakeCartManager>(FindObjectsSortMode.None))
        {
            var body = snake.GetSnakeBody();
            GameObject leader = body != null && body.Count > 0 ? body[0] : null;
            if (!m_Players.TryGetValue(snake, out var p))
            {
                p = new PlayerState { snake = snake, id = snake.GetPlayerId() };
                p.followerHandler = () => OnFollowersChanged(p);
                snake.OnOwnedFollowersChanged += p.followerHandler;
                p.lastFollowerCount = snake.GetOwnedFollowerCount();
                m_Players[snake] = p;
            }
            if (leader != p.leader)
            {
                ClearPlayerVisuals(p);
                if (p.ctrl != null && p.speedHandler != null) p.ctrl.OnSpeedupHeld -= p.speedHandler;
                p.leader = leader;
                p.ctrl = leader != null ? leader.GetComponentInChildren<CartControlScript>(true) : null;
                if (p.ctrl != null)
                {
                    p.speedHandler = held =>
                    {
                        if (held != p.speedHeld) MarmartFXDebugLog.Write($"P{p.id} speedup {(held ? "ON" : "off")}");
                        p.speedHeld = held;
                    };
                    p.ctrl.OnSpeedupHeld += p.speedHandler;
                }
            }
        }
    }

    void Unhook(PlayerState p)
    {
        if (p.snake != null && p.followerHandler != null) p.snake.OnOwnedFollowersChanged -= p.followerHandler;
        if (p.ctrl != null && p.speedHandler != null) p.ctrl.OnSpeedupHeld -= p.speedHandler;
        ClearPlayerVisuals(p);
    }

    void ClearPlayerVisuals(PlayerState p)
    {
        foreach (var t in p.trails) if (t != null) Destroy(t.gameObject);
        p.trails.Clear();
        p.builtTrailStyle = (MarmartFXVFXSettings.TrailStyle)(-1);
        ClearHighlight(p);
    }

    bool IsBoosting(PlayerState p)
    {
        if (p.ctrl == null) return false;
        return p.speedHeld || (settings.trail.includeColaBoost && p.ctrl.IsColaMentosBoostActive);
    }

    Color PlayerColor(PlayerState p) => settings.PlayerColor(p.id);

    // ------------------------------------------------------------------
    // Boost trail
    // ------------------------------------------------------------------

    void UpdateTrail(PlayerState p)
    {
        var s = settings.trail;
        bool boosting = s.enabled && IsBoosting(p);

        if (!s.enabled)
        {
            if (p.trails.Count > 0) { foreach (var t in p.trails) if (t != null) Destroy(t.gameObject); p.trails.Clear(); p.builtTrailStyle = (MarmartFXVFXSettings.TrailStyle)(-1); }
            return;
        }

        if (p.builtTrailStyle != s.style || (s.style == MarmartFXVFXSettings.TrailStyle.速度线 && p.builtLineCount != s.speedLineCount))
            BuildTrails(p);

        Color c = PlayerColor(p);
        if (p.trailMat != null)
        {
            float k = s.style == MarmartFXVFXSettings.TrailStyle.丝带 ? 1f : s.intensity;
            p.trailMat.SetColor(ID_BaseColor, new Color(c.r * k, c.g * k, c.b * k, 1f));
            p.trailMat.SetFloat(ID_Softness, s.style == MarmartFXVFXSettings.TrailStyle.霓虹 ? 0.85f : 0.5f);
        }
        for (int i = 0; i < p.trails.Count; i++)
        {
            var t = p.trails[i];
            if (t == null) continue;
            t.emitting = boosting;
            t.time = s.time;
            bool lines = s.style == MarmartFXVFXSettings.TrailStyle.速度线;
            t.widthMultiplier = lines ? s.width * 0.18f : s.width;
            if (!lines) PlaceTrail(p, t.transform, i == 0 ? -1f : 1f);
        }

        if (s.style == MarmartFXVFXSettings.TrailStyle.残影 && boosting)
        {
            p.ghostTimer -= Time.deltaTime;
            if (p.ghostTimer <= 0f)
            {
                p.ghostTimer = Mathf.Max(0.01f, s.ghostInterval);
                SpawnGhost(p, c);
            }
        }
    }

    void PlaceTrail(PlayerState p, Transform t, float side)
    {
        var s = settings.trail;
        t.localPosition = new Vector3(side * s.sideSpacing * 0.5f, s.heightOffset, -s.backOffset);
    }

    void BuildTrails(PlayerState p)
    {
        foreach (var t in p.trails) if (t != null) Destroy(t.gameObject);
        p.trails.Clear();
        var s = settings.trail;
        p.builtTrailStyle = s.style;
        p.builtLineCount = s.speedLineCount;
        if (s.style == MarmartFXVFXSettings.TrailStyle.残影) return;

        if (p.trailMat == null)
        {
            p.trailMat = new Material(additiveShader) { name = "MFX Trail P" + p.id };
            p.trailMat.SetFloat(ID_Shape, 0f);
        }

        int n = s.style == MarmartFXVFXSettings.TrailStyle.速度线 ? Mathf.Max(1, s.speedLineCount) : 2;
        var bounds = LeaderBounds(p);
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"{CopyPrefix}Trail {i}");
            go.transform.SetParent(p.leader.transform, false);
            if (s.style == MarmartFXVFXSettings.TrailStyle.速度线)
            {
                // Spread the thin lines over the cart's silhouette.
                Vector3 ext = bounds.extents;
                Vector3 local = p.leader.transform.InverseTransformPoint(bounds.center);
                go.transform.localPosition = local + new Vector3(Random.Range(-ext.x, ext.x) * 0.9f, Random.Range(-ext.y * 0.3f, ext.y), -ext.z * 0.6f);
            }
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = p.trailMat;
            tr.time = s.time;
            tr.minVertexDistance = 0.08f;
            tr.emitting = false;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.alignment = s.style == MarmartFXVFXSettings.TrailStyle.速度线 ? LineAlignment.View : LineAlignment.TransformZ;
            tr.textureMode = LineTextureMode.Stretch;
            tr.numCapVertices = 2;
            if (tr.alignment == LineAlignment.TransformZ)
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // ribbon lies flat on the floor
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, s.style == MarmartFXVFXSettings.TrailStyle.霓虹 ? 0.15f : 0.4f));
            tr.widthCurve = curve;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            p.trails.Add(tr);
        }
    }

    void SpawnGhost(PlayerState p, Color c)
    {
        if (m_GhostMat == null) m_GhostMat = new Material(highlightShader) { name = "MFX Ghost" };
        var s = settings.trail;
        var root = new GameObject($"{CopyPrefix}Ghost P{p.id}");
        var mpb = new MaterialPropertyBlock();
        Color hc = c * s.intensity;
        mpb.SetColor(ID_GlowColor, hc);
        mpb.SetColor(ID_OutlineColor, new Color(0, 0, 0, 0));
        mpb.SetFloat(ID_OutlineWidth, 0f);
        mpb.SetFloat(ID_GlowIntensity, 1.2f);
        mpb.SetFloat(ID_GlowPower, 1.5f);
        mpb.SetFloat(ID_FillIntensity, 0.35f);
        mpb.SetFloat(ID_PulseAmount, 0f);
        var rends = new List<Renderer>();
        foreach (var mf in p.leader.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null || mf.name.StartsWith(CopyPrefix) || mf.gameObject.layer == 5) continue;
            var g = new GameObject("part");
            g.transform.SetParent(root.transform, false);
            g.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            g.transform.localScale = mf.transform.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>();
            var mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = m_GhostMat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.SetPropertyBlock(mpb);
            rends.Add(r);
        }
        StartCoroutine(FadeGhost(root, rends, mpb, s.ghostLifetime));
    }

    IEnumerator FadeGhost(GameObject root, List<Renderer> rends, MaterialPropertyBlock mpb, float life)
    {
        float t = 0f;
        while (t < life && root != null)
        {
            t += Time.deltaTime;
            mpb.SetFloat(ID_Fade, 1f - t / Mathf.Max(0.01f, life));
            foreach (var r in rends) if (r != null) r.SetPropertyBlock(mpb);
            yield return null;
        }
        if (root != null) Destroy(root);
    }

    Bounds LeaderBounds(PlayerState p)
    {
        Bounds b = new Bounds(p.leader.transform.position, Vector3.one);
        bool has = false;
        foreach (var r in CartRenderers(p.leader))
        {
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static IEnumerable<Renderer> CartRenderers(GameObject root, bool includeDisabled = false)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (r.name.StartsWith(CopyPrefix) || r.gameObject.layer == 5) continue;
            if (r.transform.parent != null && r.transform.parent.name.StartsWith(CopyPrefix)) continue;
            if (!includeDisabled && (!r.enabled || !r.gameObject.activeInHierarchy)) continue;
            // Transparent parts are kept on purpose: the game swaps trailing carts to see-through
            // GhostMode/VunlerableMode materials. The stencil mask keeps the outline off them anyway.
            yield return r;
        }
    }

    // ------------------------------------------------------------------
    // Highlight
    // ------------------------------------------------------------------

    // The game tints collected carts with the team color (ChainedCartManager.SetCartTeamColor).
    // Mercer wants them to keep their own color, so in this test scene we put defaultColor back.
    static readonly System.Reflection.BindingFlags s_Priv = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
    static System.Reflection.FieldInfo s_FiRenderer, s_FiDefault, s_FiIndex;

    void KeepOwnColor(PlayerState p)
    {
        var body = p.snake.GetSnakeBody();
        if (body == null) return;
        if (s_FiRenderer == null)
        {
            var t = typeof(ChainedCartManager);
            s_FiRenderer = t.GetField("cartRenderer", s_Priv);
            s_FiDefault = t.GetField("defaultColor", s_Priv);
            s_FiIndex = t.GetField("teamColorMaterialIndex", s_Priv);
            if (s_FiRenderer == null || s_FiDefault == null || s_FiIndex == null) return;
        }
        for (int i = 1; i < body.Count; i++)
        {
            if (body[i] == null) continue;
            var ccm = body[i].GetComponentInChildren<ChainedCartManager>();
            if (ccm == null) continue;
            var r = s_FiRenderer.GetValue(ccm) as Renderer;
            if (r == null) continue;
            int idx = (int)s_FiIndex.GetValue(ccm);
            var mats = r.sharedMaterials;
            if (idx < 0 || idx >= mats.Length || mats[idx] == null) continue;
            var own = (Color)s_FiDefault.GetValue(ccm);
            if (!mats[idx].name.EndsWith("(Instance)")) continue; // never touch a material asset
            if (mats[idx].color != own) mats[idx].color = own;
        }
    }

    void UpdateHighlight(PlayerState p)
    {
        if (settings.highlight.keepFollowerOwnColor && p.snake != null) KeepOwnColor(p);
        var s = settings.highlight;
        bool ringStyle = s.style == MarmartFXVFXSettings.HighlightStyle.脚下光圈;
        int bodyCount = s.includeFollowers ? (p.snake.GetSnakeBody()?.Count ?? 1) : 1;

        if (!s.enabled) { ClearHighlight(p); return; }
        if (ringStyle && p.highlightCopies.Count > 0) ClearHighlight(p);
        if (!ringStyle && p.ring != null) { Destroy(p.ring); p.ring = null; }

        bool boost = IsBoosting(p);
        float mult = boost ? s.boostMultiplier : 1f;
        Color c = PlayerColor(p);

        if (ringStyle)
        {
            if (p.ring == null) BuildRing(p);
            var b = LeaderBounds(p);
            p.ring.transform.position = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
            p.ring.transform.rotation = Quaternion.Euler(90f, Time.time * 40f, 0f);
            float pulse = 1f + Mathf.Sin(Time.time * s.pulseSpeed) * 0.08f * s.pulseAmount * 4f;
            p.ring.transform.localScale = Vector3.one * s.ringRadius * 2f * pulse;
            float k = Mathf.Max(0.3f, s.glowIntensity) * 2f * mult;
            p.ringMat.SetColor(ID_BaseColor, new Color(c.r * k, c.g * k, c.b * k, 1f));
            return;
        }

        if (!p.highlightBuilt || p.highlightFollowers != s.includeFollowers || p.highlightBodyCount != bodyCount)
            BuildHighlight(p, bodyCount);

        bool outline = s.style != MarmartFXVFXSettings.HighlightStyle.边缘发光;
        bool glow = s.style == MarmartFXVFXSettings.HighlightStyle.边缘发光 || s.style == MarmartFXVFXSettings.HighlightStyle.描边加发光;
        bool sketch = s.style == MarmartFXVFXSettings.HighlightStyle.手绘描边;
        for (int i = 0; i < p.highlightSources.Count; i++)
        {
            var src = p.highlightSources[i];
            var cr = p.highlightCopyRenderers[i];
            if (cr != null) { bool on = src != null && src.enabled; if (cr.enabled != on) cr.enabled = on; }
        }
        var m = p.highlightMat;
        m.SetColor(ID_OutlineColor, c * (boost ? 1.5f : 1f));
        bool masked = s.silhouetteOnly && p.maskMat != null;
        float gap = masked ? s.gapFill : 0f;
        m.SetFloat(ID_OutlineWidth, outline ? s.outlineWidth : 0f);
        m.SetFloat(ID_OutlineBase, outline ? gap : 0f);
        m.SetColor(ID_GlowColor, c);
        m.SetFloat(ID_GlowIntensity, glow ? s.glowIntensity * mult : 0f);
        m.SetFloat(ID_GlowPower, s.glowPower);
        m.SetFloat(ID_FillIntensity, glow ? s.fillIntensity * mult : 0f);
        m.SetFloat(ID_PulseAmount, s.pulseAmount);
        m.SetFloat(ID_PulseSpeed, s.pulseSpeed);
        m.SetFloat(ID_WobbleAmount, sketch ? s.sketchWobble : 0f);
        m.SetFloat(ID_WobbleFps, s.sketchFps);
        m.SetFloat(ID_WobbleFreq, s.sketchNoiseScale);
        m.SetFloat(ID_Misregister, sketch ? s.sketchOffset : 0f);
        m.SetFloat(ID_UseSmoothNormal, s.smoothNormals && p.highlightSmooth ? 1f : 0f);
        int stencilRef = (p.id & 0xF) << 4;
        m.SetInt(ID_StencilRef, stencilRef);
        m.SetInt(ID_StencilComp, s.silhouetteOnly && p.maskMat != null ? (int)UnityEngine.Rendering.CompareFunction.NotEqual : (int)UnityEngine.Rendering.CompareFunction.Always);
        if (p.maskMat != null)
        {
            p.maskMat.SetInt(ID_StencilRef, stencilRef);
            p.maskMat.SetFloat(ID_MaskExpand, gap);
            p.maskMat.SetFloat(ID_UseSmoothNormal, s.smoothNormals && p.highlightSmooth ? 1f : 0f);
        }
        if (sketch || s.vividOutline)
        {
            // Drawn after post-processing, so plain 0..1 colors show exactly as picked.
            Color oc = c;
            if (s.vividOutline) { Color.RGBToHSV(c, out float h, out float sat, out float val); oc = Color.HSVToRGB(h, 1f, 1f); }
            if (boost) oc = Color.Lerp(oc, Color.white, 0.35f);
            oc.a = 1f;
            m.SetColor(ID_OutlineColor, oc);
        }
        m.SetFloat(ID_Fade, 1f);
    }

    void BuildHighlight(PlayerState p, int bodyCount)
    {
        ClearHighlight(p);
        if (p.highlightMat == null) p.highlightMat = new Material(highlightShader) { name = "MFX Highlight P" + p.id };
        if (s_MaskShader == null) s_MaskShader = Shader.Find("Hidden/MarmartFX/VFX/HighlightMask");
        if (p.maskMat == null && s_MaskShader != null)
            p.maskMat = new Material(s_MaskShader) { name = "MFX Highlight Mask P" + p.id, renderQueue = 2999 };
        p.highlightMat.renderQueue = 3000;
        var targets = new List<GameObject> { p.leader };
        if (settings.highlight.includeFollowers)
        {
            var body = p.snake.GetSnakeBody();
            if (body != null) for (int i = 1; i < body.Count; i++) if (body[i] != null) targets.Add(body[i]);
        }
        foreach (var root in targets)
        {
            // Disabled renderers too: the game toggles cart visuals (normal/ghost); copies follow below.
            foreach (var r in new List<Renderer>(CartRenderers(root, true)))
            {
                var copy = new GameObject($"{CopyPrefix}Highlight");
                copy.transform.SetParent(r.transform, false);
                if (r is SkinnedMeshRenderer smr)
                {
                    var c = copy.AddComponent<SkinnedMeshRenderer>();
                    c.sharedMesh = smr.sharedMesh;
                    c.bones = smr.bones;
                    c.rootBone = smr.rootBone;
                    c.sharedMaterials = Repeat(p.highlightMat, smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1);
                    c.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { Destroy(copy); continue; }
                    var sm = SmoothNormalMesh(mf.sharedMesh);
                    if (sm != mf.sharedMesh) p.highlightSmooth = true;
                    copy.AddComponent<MeshFilter>().sharedMesh = sm;
                    var c = copy.AddComponent<MeshRenderer>();
                    c.sharedMaterials = Repeat(p.highlightMat, mf.sharedMesh.subMeshCount);
                    c.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    c.receiveShadows = false;
                }
                p.highlightCopies.Add(copy);
                p.highlightSources.Add(r);
                p.highlightCopyRenderers.Add(copy.GetComponent<Renderer>());
                if (p.maskMat != null)
                {
                    // Stencil mask on its own renderer (queue 2999) so it is drawn before every outline.
                    var mask = Instantiate(copy, r.transform, false);
                    mask.name = $"{CopyPrefix}HighlightMask";
                    var mr = mask.GetComponent<Renderer>();
                    mr.sharedMaterials = Repeat(p.maskMat, mr.sharedMaterials.Length);
                    p.highlightCopies.Add(mask);
                    p.highlightSources.Add(r);
                    p.highlightCopyRenderers.Add(mr);
                }
            }
        }
        p.highlightBuilt = true;
        p.highlightFollowers = settings.highlight.includeFollowers;
        p.highlightBodyCount = bodyCount;
    }

    /// Copy of the mesh with position-averaged normals in UV4, so the outline doesn't crack at hard edges.
    /// Only possible for Read/Write-enabled meshes; otherwise the original mesh is used.
    static Mesh SmoothNormalMesh(Mesh src)
    {
        if (src == null || !src.isReadable) return src;
        if (s_SmoothMeshes.TryGetValue(src, out var cached) && cached != null) return cached;
        var verts = src.vertices;
        var norms = src.normals;
        if (norms == null || norms.Length != verts.Length) return src;
        var sum = new Dictionary<Vector3Int, Vector3>();
        Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 10000f), Mathf.RoundToInt(v.y * 10000f), Mathf.RoundToInt(v.z * 10000f));
        for (int i = 0; i < verts.Length; i++)
        {
            var k = Key(verts[i]);
            sum.TryGetValue(k, out var acc);
            sum[k] = acc + norms[i];
        }
        var smooth = new List<Vector3>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            var n = sum[Key(verts[i])];
            smooth.Add(n.sqrMagnitude > 1e-8f ? n.normalized : norms[i]);
        }
        var m = Instantiate(src);
        m.name = src.name + " (MFX smooth)";
        m.SetUVs(3, smooth);
        s_SmoothMeshes[src] = m;
        return m;
    }

    static Material[] Repeat(Material m, int n)
    {
        var a = new Material[Mathf.Max(1, n)];
        for (int i = 0; i < a.Length; i++) a[i] = m;
        return a;
    }

    void BuildRing(PlayerState p)
    {
        p.ring = CreateQuad($"{CopyPrefix}Ring P{p.id}", null);
        if (p.ringMat == null)
        {
            p.ringMat = new Material(additiveShader) { name = "MFX Ring P" + p.id };
            p.ringMat.SetFloat(ID_Shape, 2f);
        }
        p.ring.GetComponent<MeshRenderer>().sharedMaterial = p.ringMat;
    }

    void ClearHighlight(PlayerState p)
    {
        foreach (var g in p.highlightCopies) if (g != null) Destroy(g);
        p.highlightCopies.Clear();
        p.highlightSources.Clear();
        p.highlightCopyRenderers.Clear();
        p.highlightBuilt = false;
        if (p.ring != null) { Destroy(p.ring); p.ring = null; }
    }

    // ------------------------------------------------------------------
    // Ate a new cart
    // ------------------------------------------------------------------

    void OnFollowersChanged(PlayerState p)
    {
        if (p.snake == null) return;
        int now = p.snake.GetOwnedFollowerCount();
        int gained = now - p.lastFollowerCount;
        p.lastFollowerCount = now;
        MarmartFXDebugLog.Write($"P{p.id} followers changed → {now} (gained {gained})");
        if (settings == null || !settings.eat.enabled || gained <= 0) return;

        var body = p.snake.GetSnakeBody();
        GameObject target = body != null && body.Count > 0 ? body[body.Count - 1] : p.leader;
        if (target == null) target = p.leader;
        if (target == null) return;
        PlayEatEffect(p, target, gained, now);
    }

    void PlayEatEffect(PlayerState p, GameObject target, int gained, int total)
    {
        var s = settings.eat;
        Color c = PlayerColor(p);
        Color hdr = new Color(c.r * s.intensity, c.g * s.intensity, c.b * s.intensity, 1f);
        Vector3 pos = target.transform.position;

        if (s.burst) SpawnBurst(pos + Vector3.up * 0.6f, hdr, s.burstCount, s.burstSpeed, 0.45f);
        if (s.shockRing) StartCoroutine(ShockRing(pos, hdr, s.ringRadius, s.ringTime));
        if (s.punch)
        {
            StartCoroutine(Punch(target, s.punchScale, s.punchTime));
            if (p.leader != null && p.leader != target) StartCoroutine(Punch(p.leader, s.punchScale * 0.5f, s.punchTime));
        }
        if (s.popupText) StartCoroutine(Popup(pos + Vector3.up * s.popupHeight, "+" + gained, c));
    }

    IEnumerator ShockRing(Vector3 pos, Color color, float radius, float time)
    {
        var mat = new Material(additiveShader);
        mat.SetFloat(ID_Shape, 2f);
        var go = CreateQuad($"{CopyPrefix}ShockRing", mat);
        go.transform.SetPositionAndRotation(pos + Vector3.up * 0.08f, Quaternion.Euler(90f, 0f, 0f));
        float t = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / Mathf.Max(0.01f, time));
            float e = 1f - (1f - x) * (1f - x);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius * 2f, e);
            mat.SetColor(ID_BaseColor, new Color(color.r, color.g, color.b, 1f - x));
            yield return null;
        }
        Destroy(go);
        Destroy(mat);
    }

    IEnumerator Punch(GameObject target, float amount, float time)
    {
        // Scale only pure visual transforms (no collider / rigidbody) so physics is untouched.
        var parts = new List<Transform>();
        var orig = new List<Vector3>();
        foreach (var r in CartRenderers(target))
        {
            var t = r.transform;
            if (t.GetComponent<Collider>() != null || t.GetComponent<Rigidbody>() != null || parts.Contains(t)) continue;
            parts.Add(t);
            orig.Add(t.localScale);
        }
        float e = 0f;
        while (e < time)
        {
            e += Time.deltaTime;
            float x = Mathf.Clamp01(e / Mathf.Max(0.01f, time));
            float k = 1f + Mathf.Sin(x * Mathf.PI) * amount * (1f - x * 0.5f);
            Vector3 squash = new Vector3(k, 2f - k, k);
            for (int i = 0; i < parts.Count; i++) if (parts[i] != null) parts[i].localScale = Vector3.Scale(orig[i], squash);
            yield return null;
        }
        for (int i = 0; i < parts.Count; i++) if (parts[i] != null) parts[i].localScale = orig[i];
    }

    IEnumerator Popup(Vector3 pos, string text, Color color)
    {
        var go = new GameObject($"{CopyPrefix}Popup");
        var tm = go.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        float t = 0f, life = 0.9f;
        while (t < life)
        {
            t += Time.deltaTime;
            float x = t / life;
            go.transform.position = pos + Vector3.up * (x * 1.2f);
            if (cam != null) go.transform.rotation = cam.transform.rotation;
            float s = x < 0.15f ? Mathf.Lerp(0.3f, 1.3f, x / 0.15f) : Mathf.Lerp(1.3f, 1f, (x - 0.15f) / 0.2f);
            go.transform.localScale = Vector3.one * s;
            tm.color = new Color(color.r, color.g, color.b, 1f - Mathf.Clamp01((x - 0.6f) / 0.4f));
            yield return null;
        }
        Destroy(go);
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    static GameObject CreateQuad(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (mat != null) mr.sharedMaterial = mat;
        return go;
    }

    /// <summary>One-shot burst of little stars (used by the eat effect and the shelf hit).</summary>
    public static void SpawnBurst(Vector3 pos, Color hdrColor, int count, float speed, float size)
    {
        if (s_Additive == null) s_Additive = Shader.Find("MarmartFX/VFX/Additive");
        if (s_Additive == null) return;
        if (s_StarMat == null)
        {
            s_StarMat = new Material(s_Additive) { name = "MFX Star" };
            s_StarMat.SetFloat(ID_Shape, 3f);
            s_StarMat.SetColor(ID_BaseColor, Color.white);
        }

        var go = new GameObject($"{CopyPrefix}Burst");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = hdrColor;
        main.gravityModifier = 0.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = Mathf.Max(count, 1);
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(count, 1)) });
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = s_StarMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }
}
