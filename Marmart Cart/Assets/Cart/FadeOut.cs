using UnityEngine;

public class FadeOut : MonoBehaviour
{
    public float lifetime = 1.5f;       // 完全消失需要多久
    public float fadeDuration = 1.0f;   // 从开始淡出到透明所需时间

    private float timer = 0f;
    private Renderer rend;
    private Material mat;
    private Color originalColor;

    void Start()
    {
        rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            // 注意：自动生成实例化材质，避免影响 prefab
            mat = rend.material;
            originalColor = mat.color;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 开始淡出
        if (timer >= lifetime - fadeDuration)
        {
            float t = 1 - ((lifetime - timer) / fadeDuration);
            if (mat != null)
            {
                Color c = originalColor;
                c.a = Mathf.Lerp(1f, 0f, t);
                mat.color = c;
            }
        }

        // 到时间销毁
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
