using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// MarmartFX test-scene effect: when a player's leading cart hits a shelf hard enough, products
/// "pop out" of the shelf (spawned copies of the products on that shelf, nothing is actually removed),
/// fly out, land on the floor and stay as physics props the carts can push around.
/// The oldest items shrink away once the maximum count is reached.
///
/// Runtime only. It hooks the carts by adding a small relay component to the cart colliders at
/// runtime (no prefab or game script is modified).
/// </summary>
[DisallowMultipleComponent]
public class MarmartFXShelfSpill : MonoBehaviour
{
    [Tooltip("特效设置（在 MarmartFX 控制面板的「特效」页里调）。")]
    public MarmartFXVFXSettings settings;
    public bool logHits = false;

    MarmartFXVFXSettings.SpillSettings S => settings.spill;

    // ------------------------------------------------------------------

    /// Either a whole product object to clone (template, e.g. Food_BaconBlock) or a single mesh.
    struct Product { public GameObject template; public Bounds localBounds; public Mesh mesh; public Material[] materials; public Vector3 scale; }

    readonly Dictionary<int, Transform> m_ShelfRootCache = new Dictionary<int, Transform>();
    readonly Dictionary<Transform, List<Product>> m_ShelfProducts = new Dictionary<Transform, List<Product>>();
    readonly Dictionary<Transform, float> m_ShelfNextTime = new Dictionary<Transform, float>();
    readonly List<Product> m_GlobalProducts = new List<Product>();
    readonly LinkedList<MarmartFXSpilledItem> m_Alive = new LinkedList<MarmartFXSpilledItem>();
    Transform m_Container;
    Transform m_Staging; // inactive parent: clones made under it don't run any game script's Awake
    float m_NextHookScan;
    int m_LoggedHits;
    bool m_GlobalBuilt;

    void Awake()
    {
        m_Container = new GameObject("MarmartFX Spilled Items").transform;
        m_Staging = new GameObject("MarmartFX Spill Staging").transform;
        m_Staging.gameObject.SetActive(false);
    }

    Material m_PaintMat;
    static readonly int[] ID_Paint = { Shader.PropertyToID("_Color0"), Shader.PropertyToID("_Color1"), Shader.PropertyToID("_Color2"), Shader.PropertyToID("_Color3"), Shader.PropertyToID("_Color4") };

    Material PaintMaterial()
    {
        if (m_PaintMat == null)
        {
            var sh = Shader.Find("MarmartFX/VFX/PaintDrip");
            if (sh == null) return null;
            m_PaintMat = new Material(sh) { name = "MFX PaintDrip" };
        }
        var m = m_PaintMat;
        m.SetFloat("_Width", S.paintWidth);
        m.SetFloat("_Blotch", S.paintBlotch);
        m.SetFloat("_PatchScale", S.paintPatchScale);
        m.SetFloat("_DripLength", S.dripLength);
        m.SetFloat("_DripDensity", S.dripDensity);
        m.SetFloat("_DripSpeed", S.dripSpeed);
        m.SetFloat("_BoilFps", S.paintBoilFps);
        if (S.paintColors != null)
            for (int i = 0; i < 5 && i < S.paintColors.Length; i++) m.SetColor(ID_Paint[i], S.paintColors[i]);
        return m;
    }

    /// Visual-only child copies of every mesh in the item, drawn with the paint-drip outline.
    void AddPaintHighlight(GameObject go)
    {
        var mat = PaintMaterial();
        if (mat == null) return;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.name == "MFX_Paint") continue;
            var c = new GameObject("MFX_Paint");
            c.layer = mf.gameObject.layer;
            c.transform.SetParent(mf.transform, false);
            c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = c.AddComponent<MeshRenderer>();
            var mats = new Material[mf.sharedMesh.subMeshCount];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    void Update()
    {
        if (settings == null) return;
        if (m_PaintMat != null) PaintMaterial(); // live-tweak from the control panel
        foreach (var it in m_Alive) if (it != null) it.SetPaintVisible(S.paintHighlight);
        if (Time.unscaledTime >= m_NextHookScan)
        {
            m_NextHookScan = Time.unscaledTime + 1f;
            HookCarts();
        }
        if (S.itemLifetime > 0f)
        {
            var node = m_Alive.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value == null) m_Alive.Remove(node);
                else if (!node.Value.IsDespawning && Time.time - node.Value.SpawnTime > S.itemLifetime) node.Value.Despawn();
                node = next;
            }
        }
    }

    /// <summary>Adds the relay to every leading-cart collider (runtime only, carts can spawn late).</summary>
    void HookCarts()
    {
        int added = 0, total = 0;
        foreach (var relay in FindObjectsByType<LeadingCartCollisionRelay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            total++;
            // Unity only sends OnCollisionEnter to the GameObject that owns the Rigidbody,
            // not to child colliders, so the relay goes on the cart's Rigidbody object.
            var c = relay.GetComponent<Collider>();
            var target = c != null && c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : relay.gameObject;
            if (target.GetComponent<MarmartFXCartHitRelay>() == null)
            {
                target.AddComponent<MarmartFXCartHitRelay>().spill = this;
                added++;
            }
        }
        if (added > 0) MarmartFXDebugLog.Write($"ShelfSpill hooked {added} new cart rigidbodies (relays seen {total})");
    }

    public void OnCartHit(Collision collision, Transform cart)
    {
        if (settings == null || !S.enabled) return;
        var col = collision.collider;
        if (m_LoggedHits < 100 && col != null && ((1 << col.gameObject.layer) & S.shelfLayers.value) != 0)
        {
            m_LoggedHits++;
            MarmartFXDebugLog.Write($"Cart hit '{col.name}' layer={LayerMask.LayerToName(col.gameObject.layer)}({col.gameObject.layer}) speed={collision.relativeVelocity.magnitude:0.0} shelfLayer={((1 << col.gameObject.layer) & S.shelfLayers.value) != 0}");
        }
        if (col == null || ((1 << col.gameObject.layer) & S.shelfLayers.value) == 0) return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < S.minImpactSpeed) return;

        var shelf = ShelfRoot(col);
        if (m_ShelfNextTime.TryGetValue(shelf, out float t) && Time.time < t) return;
        m_ShelfNextTime[shelf] = Time.time + S.shelfCooldown;

        var products = ProductsOf(shelf);
        if (products.Count == 0) products = GlobalProducts();
        if (products.Count == 0) return;

        float k = Mathf.InverseLerp(S.minImpactSpeed, Mathf.Max(S.minImpactSpeed + 0.01f, S.fullImpactSpeed), speed);
        int count = Mathf.RoundToInt(Mathf.Lerp(S.minItemsPerHit, S.maxItemsPerHit, k));

        Bounds sb = col.bounds;
        Vector3 contact = collision.contactCount > 0 ? collision.GetContact(0).point : sb.ClosestPoint(cart.position);
        // Items fly out on the side of the shelf that was hit (toward the cart).
        Vector3 outDir = cart.position - sb.ClosestPoint(cart.position);
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 1e-4f) { outDir = cart.position - sb.center; outDir.y = 0f; }
        outDir = outDir.sqrMagnitude > 1e-4f ? outDir.normalized : Vector3.forward;
        if (logHits) Debug.Log($"[MarmartFX] Shelf hit '{shelf.name}' speed {speed:0.0} → {count} items", shelf);

        if (S.shelfShake) ShakeShelf(shelf, outDir, speed > 0f ? k : 0f);

        if (S.hitBurst)
        {
            var snake = cart.GetComponentInParent<SnakeCartManager>();
            Color c = settings.PlayerColor(snake != null ? snake.GetPlayerId() : 1);
            MarmartFXPlayerFX.SpawnBurst(contact, c * 2.5f, Mathf.Clamp(count * 3, 6, 30), 4f, 0.5f);
        }

        // The hitting cart ignores the new items for a moment so they never shove it (no flying carts).
        var cartCols = cart.GetComponentsInChildren<Collider>();
        for (int i = 0; i < count; i++)
        {
            var p = products[Random.Range(0, products.Count)];
            // Spawn just outside the shelf face that was hit, at a random shelf height.
            Vector3 side = Vector3.Cross(Vector3.up, outDir);
            Vector3 along = side * (Random.Range(0.5f, 1.6f) * (Random.value < 0.5f ? -1f : 1f));
            Vector3 pos = sb.ClosestPoint(contact + outDir * 3f + along);
            pos += outDir * 0.12f;
            pos.y = Mathf.Lerp(sb.min.y + 0.7f, Mathf.Max(sb.min.y + 0.8f, sb.max.y - 0.2f), Random.value);

            Vector3 dir = Quaternion.Euler(0f, Random.Range(-S.spreadAngle, S.spreadAngle) * 0.5f, 0f) * outDir;
            Vector3 vel = dir * Mathf.Lerp(S.outSpeed.x, S.outSpeed.y, k * 0.6f + Random.value * 0.4f)
                        + Vector3.up * Random.Range(S.upSpeed.x, S.upSpeed.y);
            Spawn(p, pos, vel, cartCols);
        }
    }

    void Spawn(Product p, Vector3 pos, Vector3 velocity, Collider[] ignore)
    {
        while (m_Alive.Count > 0 && m_Alive.First.Value == null) m_Alive.RemoveFirst();
        int over = m_Alive.Count + 1 - Mathf.Max(1, S.maxItemsInScene);
        var node = m_Alive.First;
        while (over > 0 && node != null)
        {
            var next = node.Next;
            if (node.Value != null && !node.Value.IsDespawning) { node.Value.Despawn(); over--; }
            m_Alive.Remove(node);
            node = next;
        }

        GameObject go;
        Bounds lb;
        if (p.template != null)
        {
            go = Instantiate(p.template, m_Staging, false);
            go.name = p.template.name + " (spilled)";
            StripToVisual(go);
            SetLayerRecursive(go.transform, S.itemLayer);
            go.transform.SetParent(m_Container, false);
            go.SetActive(true);
            lb = p.localBounds;
        }
        else
        {
            go = new GameObject(p.mesh.name + " (spilled)");
            go.layer = S.itemLayer;
            go.transform.SetParent(m_Container, false);
            go.AddComponent<MeshFilter>().sharedMesh = p.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = p.materials;
            lb = p.mesh.bounds;
        }
        go.transform.SetPositionAndRotation(pos, Random.rotation);

        var box = go.AddComponent<BoxCollider>();
        box.center = lb.center;
        box.size = Vector3.Max(lb.size, Vector3.one * 0.02f);

        Vector3 finalScale = p.scale * S.itemScale;
        Vector3 worldSize = Vector3.Scale(lb.size, finalScale);
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Clamp(worldSize.x * worldSize.y * worldSize.z * S.density, S.massRange.x, S.massRange.y);
        rb.linearDamping = S.linearDamping;
        rb.angularDamping = S.angularDamping;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = velocity;
        rb.angularVelocity = Random.insideUnitSphere * S.spinSpeed;

        var item = go.AddComponent<MarmartFXSpilledItem>();
        AddPaintHighlight(go);
        box.hasModifiableContacts = true;
        MarmartFXOneWayContacts.Register(box);
        item.Init(finalScale, S.popInTime);
        item.IgnoreFor(box, ignore, 0.8f);
        m_Alive.AddLast(item);
    }

    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Shelf shake. Shelves are static (often static-batched), so moving them does nothing visually and
    // would touch the scene. Instead: hide the shelf's renderers, wobble a visual-only clone of its prefab
    // in the same spot, then remove the clone and show the originals again.
    // ------------------------------------------------------------------

    readonly HashSet<Transform> m_Shaking = new HashSet<Transform>();

    void ShakeShelf(Transform shelf, Vector3 outDir, float strength)
    {
        if (shelf == null) return;
        Transform root = shelf;
        GameObject src = null;
#if UNITY_EDITOR
        var inst = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(shelf.gameObject);
        if (inst != null)
        {
            src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(inst);
            if (src != null) root = inst.transform;
        }
#endif
        if (src == null || m_Shaking.Contains(root)) return;

        var clone = Instantiate(src, m_Staging, false);
        clone.name = root.name + " (shake)";
        StripToVisual(clone, false);

        // Match each original renderer to its clone counterpart: same materials (keeps the F8 toy swap)
        // and same lightmap, then hide the original.
        var hidden = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            string path = RelativePath(root, r.transform);
            var ct = path.Length == 0 ? clone.transform : clone.transform.Find(path);
            var cr = ct != null ? ct.GetComponent<Renderer>() : null;
            if (cr == null) continue;
            cr.sharedMaterials = r.sharedMaterials;
            cr.lightmapIndex = r.lightmapIndex;
            cr.lightmapScaleOffset = r.lightmapScaleOffset;
            cr.shadowCastingMode = r.shadowCastingMode;
            r.enabled = false;
            hidden.Add(r);
        }
        // Clone renderers with no original counterpart (removed in the scene via overrides) stay hidden.
        foreach (var cr in clone.GetComponentsInChildren<Renderer>(true))
        {
            string path = RelativePath(clone.transform, cr.transform);
            var ot = path.Length == 0 ? root : root.Find(path);
            var or = ot != null ? ot.GetComponent<Renderer>() : null;
            if (or == null || !hidden.Contains(or)) cr.enabled = false;
        }
        if (hidden.Count == 0) { Destroy(clone); return; }

        clone.transform.SetParent(m_Container, false);
        clone.transform.SetPositionAndRotation(root.position, root.rotation);
        clone.transform.localScale = root.lossyScale;
        clone.SetActive(true);
        m_Shaking.Add(root);
        StartCoroutine(ShakeRoutine(root, clone.transform, hidden, outDir, Mathf.Lerp(0.5f, 1f, strength)));
    }

    System.Collections.IEnumerator ShakeRoutine(Transform root, Transform clone, List<Renderer> hidden, Vector3 outDir, float strength)
    {
        Vector3 basePos = root.position;
        Quaternion baseRot = root.rotation;
        Vector3 tiltAxis = Vector3.Cross(Vector3.up, -outDir); // tips away from the cart first
        float T = Mathf.Max(0.05f, S.shakeTime);
        for (float t = 0f; t < T; t += Time.deltaTime)
        {
            if (clone == null) break;
            float decay = 1f - t / T;
            decay *= decay;
            float w = Mathf.Sin(t * S.shakeFrequency * Mathf.PI * 2f) * decay * strength;
            clone.position = basePos - outDir * (S.shakeDistance * w);
            clone.rotation = Quaternion.AngleAxis(S.shakeAngle * w, tiltAxis) * baseRot;
            yield return null;
        }
        if (clone != null) Destroy(clone.gameObject);
        foreach (var r in hidden) if (r != null) r.enabled = true;
        m_Shaking.Remove(root);
    }

    static string RelativePath(Transform root, Transform t)
    {
        if (t == root) return "";
        string path = t.name;
        for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        m_Shaking.Clear();
    }

    /// Keeps only transforms, mesh filters and renderers on a cloned product.
    static void StripToVisual(GameObject go, bool resetLightmaps = true)
    {
        // Scripts first (they may [RequireComponent] colliders/rigidbodies), then joints, then the rest.
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true)) if (c != null) DestroyImmediate(c);
        foreach (var c in go.GetComponentsInChildren<Joint>(true)) if (c != null) DestroyImmediate(c);
        for (int pass = 0; pass < 2; pass++)
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is Renderer || c is LODGroup) continue;
                DestroyImmediate(c);
            }
        if (resetLightmaps) foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.lightmapIndex = -1; // baked lightmap UVs mean nothing once it flies away
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
    }

    bool IsProductName(string n) => !string.IsNullOrEmpty(S.productNamePrefix) && n.StartsWith(S.productNamePrefix, System.StringComparison.OrdinalIgnoreCase);

    bool HasProductChild(Transform t)
    {
        for (int i = 0; i < t.childCount; i++) if (IsProductName(t.GetChild(i).name)) return true;
        return false;
    }

    Transform ShelfRoot(Collider col)
    {
        int id = col.GetInstanceID();
        if (m_ShelfRootCache.TryGetValue(id, out var root) && root != null) return root;

        // Prefer the nearest ancestor that directly holds Food_* objects (e.g. OpenFridgeShelf02_Meat01).
        int depth = 0;
        for (var t = col.transform; t != null && depth < 5; t = t.parent, depth++)
            if (HasProductChild(t)) { m_ShelfRootCache[id] = t; return t; }

        root = col.transform;
        // Climb while the parent is still "one piece of furniture" (smaller than ~6 m).
        while (root.parent != null)
        {
            var b = RendererBounds(root.parent);
            if (b.size.x > 6f || b.size.z > 6f) break;
            root = root.parent;
        }
        m_ShelfRootCache[id] = root;
        return root;
    }

    static Bounds RendererBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    List<Product> ProductsOf(Transform shelf)
    {
        if (m_ShelfProducts.TryGetValue(shelf, out var list)) return list;
        list = new List<Product>();
        AddNamedProducts(shelf, list);
        if (list.Count == 0)
        {
            var seen = new HashSet<Mesh>();
            foreach (var mf in shelf.GetComponentsInChildren<MeshFilter>())
                TryAddProduct(mf, list, seen);
        }
        MarmartFXDebugLog.Write($"ShelfSpill products of '{shelf.name}': {list.Count} ({(list.Count > 0 && list[0].template != null ? "Food objects" : "meshes")})");
        m_ShelfProducts[shelf] = list;
        return list;
    }

    List<Product> GlobalProducts()
    {
        if (m_GlobalBuilt) return m_GlobalProducts;
        m_GlobalBuilt = true;
        var seen = new HashSet<Mesh>();
        foreach (var col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (((1 << col.gameObject.layer) & S.shelfLayers.value) == 0) continue;
            var root = ShelfRoot(col);
            int before = m_GlobalProducts.Count;
            AddNamedProducts(root, m_GlobalProducts);
            if (m_GlobalProducts.Count == before)
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                    TryAddProduct(mf, m_GlobalProducts, seen);
            if (m_GlobalProducts.Count > 60) break;
        }
        return m_GlobalProducts;
    }

    /// Every Food_* object under the shelf (not nested in another Food_* object) becomes a clone template.
    void AddNamedProducts(Transform shelf, List<Product> list)
    {
        foreach (var t in shelf.GetComponentsInChildren<Transform>(true))
        {
            if (t == shelf || !IsProductName(t.name)) continue;
            bool nested = false;
            for (var a = t.parent; a != null && a != shelf; a = a.parent) if (IsProductName(a.name)) { nested = true; break; }
            if (nested) continue;
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) continue;
            // Bounds in the product's own local space (for the box collider).
            var inv = t.worldToLocalMatrix;
            bool first = true; Bounds b = default;
            foreach (var r in rs)
            {
                var wb = r.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var lp = inv.MultiplyPoint3x4(corner);
                    if (first) { b = new Bounds(lp, Vector3.zero); first = false; } else b.Encapsulate(lp);
                }
            }
            // In the editor, clone the original prefab asset when there is one: scene copies can be
            // static-batched/lightmapped and would render wrong once moved.
            GameObject template = t.gameObject;
#if UNITY_EDITOR
            var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
            if (src != null && src.transform.parent == null) template = src;
#endif
            list.Add(new Product { template = template, localBounds = b, scale = t.lossyScale });
        }
    }

    void TryAddProduct(MeshFilter mf, List<Product> list, HashSet<Mesh> seen)
    {
        var mesh = mf.sharedMesh;
        if (mesh == null || seen.Contains(mesh)) return;
        var mr = mf.GetComponent<MeshRenderer>();
        if (mr == null || !mr.enabled || mr.sharedMaterials.Length == 0) return;
        if (((1 << mf.gameObject.layer) & S.shelfLayers.value) != 0 && mf.GetComponent<Collider>() != null) return;
        float size = Mathf.Max(mr.bounds.size.x, mr.bounds.size.y, mr.bounds.size.z);
        if (size > S.maxProductSize || size < S.minProductSize) return;
        if (!mesh.isReadable && mesh.vertexCount == 0) return;
        seen.Add(mesh);
        list.Add(new Product { mesh = mesh, materials = mr.sharedMaterials, scale = mf.transform.lossyScale });
    }

    /// <summary>Removes every spilled item (button in the inspector / control panel).</summary>
    [ContextMenu("清空所有掉落物")]
    public void ClearAll()
    {
        foreach (var it in m_Alive) if (it != null) Destroy(it.gameObject);
        m_Alive.Clear();
    }
}

/// <summary>Runtime-added to leading-cart colliders; forwards hits to MarmartFXShelfSpill.</summary>
public class MarmartFXCartHitRelay : MonoBehaviour
{
    public MarmartFXShelfSpill spill;

    void OnCollisionEnter(Collision collision)
    {
        if (spill != null && spill.isActiveAndEnabled) spill.OnCartHit(collision, transform);
    }
}

/// <summary>A spawned product: pops in from scale 0, shrinks away when removed.</summary>
public class MarmartFXSpilledItem : MonoBehaviour
{
    Vector3 m_Scale;
    float m_PopTime;
    float m_Age;
    float m_DespawnAge = -1f;
    public float SpawnTime { get; private set; }
    public bool IsDespawning => m_DespawnAge >= 0f;

    public void Init(Vector3 scale, float popTime)
    {
        m_Scale = scale;
        m_PopTime = Mathf.Max(0.001f, popTime);
        SpawnTime = Time.time;
        transform.localScale = popTime > 0f ? scale * 0.01f : scale;
    }

    Collider m_Self;
    Collider[] m_Ignored;
    float m_IgnoreUntil = -1f;

    List<Renderer> m_Paint;
    bool m_PaintVisible = true;

    public void SetPaintVisible(bool on)
    {
        if (m_Paint == null)
        {
            m_Paint = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.name == "MFX_Paint") m_Paint.Add(r);
            m_PaintVisible = !on;
        }
        if (on == m_PaintVisible) return;
        m_PaintVisible = on;
        foreach (var r in m_Paint) if (r != null) r.enabled = on;
    }

    public void IgnoreFor(Collider self, Collider[] others, float seconds)
    {
        if (self == null || others == null) return;
        m_Self = self; m_Ignored = others; m_IgnoreUntil = Time.time + seconds;
        foreach (var o in others) if (o != null) Physics.IgnoreCollision(self, o, true);
    }

    void RestoreCollisions()
    {
        if (m_Ignored != null && m_Self != null)
            foreach (var o in m_Ignored) if (o != null) Physics.IgnoreCollision(m_Self, o, false);
        m_Ignored = null; m_IgnoreUntil = -1f;
    }

    void OnDestroy()
    {
        var c = GetComponent<Collider>();
        if (c != null) MarmartFXOneWayContacts.Unregister(c.GetInstanceID());
    }

    public void Despawn()
    {
        if (IsDespawning) return;
        m_DespawnAge = 0f;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    void Update()
    {
        if (m_IgnoreUntil > 0f && Time.time >= m_IgnoreUntil) RestoreCollisions();
        if (IsDespawning)
        {
            m_DespawnAge += Time.deltaTime;
            float s = 1f - m_DespawnAge / 0.25f;
            if (s <= 0f) { Destroy(gameObject); return; }
            transform.localScale = m_Scale * s;
            return;
        }
        if (m_Age < m_PopTime)
        {
            m_Age += Time.deltaTime;
            float x = Mathf.Clamp01(m_Age / m_PopTime);
            float overshoot = 1f + Mathf.Sin(x * Mathf.PI) * 0.25f; // little bounce
            transform.localScale = m_Scale * Mathf.Min(x * overshoot, 1.25f);
            if (x >= 1f) transform.localScale = m_Scale;
        }
    }
}

/// <summary>
/// Makes spilled items one-way: anything with a Rigidbody (carts, follower carts) pushes the items,
/// but the items act as if the cart had infinite mass, so they can never shove or launch a cart.
/// Uses Unity's contact modification; only colliders registered here are affected.
/// </summary>
public static class MarmartFXOneWayContacts
{
    static readonly HashSet<int> s_Items = new HashSet<int>();
    static readonly object s_Lock = new object();
    static bool s_Hooked;

    public static void Register(Collider c)
    {
        if (c == null) return;
        lock (s_Lock) s_Items.Add(c.GetInstanceID());
        if (!s_Hooked)
        {
            s_Hooked = true;
            Physics.ContactModifyEvent += OnModify;
            Physics.ContactModifyEventCCD += OnModify;
            Application.quitting += Unhook;
        }
    }

    public static void Unregister(int id)
    {
        lock (s_Lock) s_Items.Remove(id);
    }

    static void Unhook()
    {
        Physics.ContactModifyEvent -= OnModify;
        Physics.ContactModifyEventCCD -= OnModify;
        s_Hooked = false;
        lock (s_Lock) s_Items.Clear();
    }

    // Runs on physics threads: only touches the id set (under the lock) and the contact data.
    static void OnModify(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
    {
        lock (s_Lock)
        {
            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i];
                bool aItem = s_Items.Contains(pair.colliderInstanceID);
                bool bItem = s_Items.Contains(pair.otherColliderInstanceID);
                if (aItem == bItem) continue; // item-item or unrelated
                if (aItem && pair.otherBodyInstanceID == 0) continue; // static geometry: nothing to protect
                if (bItem && pair.bodyInstanceID == 0) continue;
                var mp = pair.massProperties;
                if (aItem) { mp.otherInverseMassScale = 0f; mp.otherInverseInertiaScale = 0f; mp.inverseMassScale = 1f; mp.inverseInertiaScale = 1f; }
                else { mp.inverseMassScale = 0f; mp.inverseInertiaScale = 0f; mp.otherInverseMassScale = 1f; mp.otherInverseInertiaScale = 1f; }
                pair.massProperties = mp;
                pairs[i] = pair;
            }
        }
    }
}
