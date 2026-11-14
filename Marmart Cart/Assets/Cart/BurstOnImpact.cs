using UnityEngine;

public class BurstOnImpact : MonoBehaviour
{
    [Header("碰撞触发 Tag（默认 Player）")]
    public string triggerTag = "Player";

    [Header("要生成的 Prefab（必须带 Rigidbody）")]
    public GameObject itemPrefab;

    [Header("生成多少个")]
    public int spawnCount = 10;

    [Header("生成半径")]
    public float spawnRadius = 0.4f;

    [Header("喷力大小（向上烟花效果）")]
    public float upwardForce = 10f;

    [Header("左右散开程度（0=直上）")]
    public float spread = 0.3f;

    [Header("是否只触发一次")]
    public bool triggerOnce = true;

    private bool _hasBurst = false;



    private void OnCollisionEnter(Collision collision)
    {
        if (triggerOnce && _hasBurst) return;
        if (!collision.collider.CompareTag(triggerTag)) return;

        Burst();
    }


    private void Burst()
    {
        _hasBurst = true;

        for (int i = 0; i < spawnCount; i++)
        {
            // ① 生成 prefab
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            Vector3 spawnPos = transform.position + offset;

            GameObject obj = Instantiate(itemPrefab, spawnPos, Random.rotation);

            // ② 检查 / 设定 tag 必须为 Item（否则不喷）
            // 如果你的 Prefab 本身就有 Tag = "Item"，可以删除这行
            obj.tag = "Item";

            if (!obj.CompareTag("Item"))
            {
                // 不是 Item → 直接跳过，不给力
                continue;
            }

            // ③ 给力（烟花效果）
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 dir = (Vector3.up + Random.insideUnitSphere * spread).normalized;
                rb.AddForce(dir * upwardForce, ForceMode.Impulse);
            }
            FadeOut fade = obj.AddComponent<FadeOut>();
            fade.lifetime = 2f;
            fade.fadeDuration = 1f;
        }
    }
}
