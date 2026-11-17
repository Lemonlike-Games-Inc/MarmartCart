using System;
using UnityEngine;

/// <summary>
/// All gameplay-VFX settings for the MarmartFX test scene (shelf spill, boost trail, player highlight,
/// "ate a new cart" effect). Stored as an asset so tweaks made in Play mode are kept.
/// Edited from Tools > MarmartFX > 控制面板 > 特效.
/// </summary>
[CreateAssetMenu(menuName = "MarmartFX/VFX Settings", fileName = "MarmartFX_VFX")]
public class MarmartFXVFXSettings : ScriptableObject
{
    [Tooltip("玩家 1~4 的颜色（轨迹、高亮、吃车特效都用这个）。")]
    public Color[] playerColors =
    {
        new Color(0.20f, 0.62f, 1.00f, 1f), // P1 blue
        new Color(1.00f, 0.30f, 0.32f, 1f), // P2 red
        new Color(0.28f, 0.90f, 0.45f, 1f), // P3 green
        new Color(1.00f, 0.78f, 0.18f, 1f), // P4 yellow
    };

    public SpillSettings spill = new SpillSettings();
    public TrailSettings trail = new TrailSettings();
    public HighlightSettings highlight = new HighlightSettings();
    public EatSettings eat = new EatSettings();
    public BubbleSettings bubble = new BubbleSettings();

    public Color PlayerColor(int playerId)
    {
        int i = Mathf.Clamp(playerId - 1, 0, playerColors.Length - 1);
        return playerColors.Length > 0 ? playerColors[i] : Color.white;
    }

    // ------------------------------------------------------------------

    [Serializable]
    public class SpillSettings
    {
        public bool enabled = true;

        [Header("触发 (Trigger)")]
        [Tooltip("货架碰撞体所在的图层（项目里货架是 Obstacles）。")]
        public LayerMask shelfLayers = 1 << 9;
        [Tooltip("撞击速度低于这个值不掉东西。")]
        public float minImpactSpeed = 2f;
        [Tooltip("同一个货架两次掉落之间的最短间隔（秒）。")]
        public float shelfCooldown = 0.6f;

        [Header("掉落数量 (Amount)")]
        public int minItemsPerHit = 2;
        public int maxItemsPerHit = 7;
        [Tooltip("达到这个撞击速度时掉最多的数量。")]
        public float fullImpactSpeed = 18f;
        [Tooltip("场上最多同时存在多少个掉落物，超过时最早的会缩小消失。")]
        public int maxItemsInScene = 80;
        [Tooltip("掉落物存在多少秒后自动消失，0 = 一直存在（只受数量上限限制）。")]
        public float itemLifetime = 0f;

        [Header("弹出 (Pop Out)")]
        public Vector2 outSpeed = new Vector2(2f, 5f);
        public Vector2 upSpeed = new Vector2(2.5f, 5f);
        [Tooltip("左右散开的角度（度）。")]
        public float spreadAngle = 70f;
        public float spinSpeed = 10f;
        [Tooltip("出现时从 0 放大的时间（秒），做出凭空冒出来的感觉。")]
        public float popInTime = 0.12f;
        [Tooltip("撞击处冒出一圈小星星（玩家颜色）。")]
        public bool hitBurst = true;

        [Header("货架抖动 (Shelf Shake)")]
        [Tooltip("被撞的货架晃一下（用一个临时的可动副本来晃，原货架不会被移动）。")]
        public bool shelfShake = true;
        [Tooltip("晃动位移（米）。")]
        public float shakeDistance = 0.08f;
        [Tooltip("前后倾斜角度（度）。")]
        public float shakeAngle = 3f;
        [Tooltip("晃动持续时间（秒）。")]
        public float shakeTime = 0.4f;
        [Tooltip("晃动频率（每秒来回次数）。")]
        public float shakeFrequency = 9f;

        [Header("物理 (Physics)")]
        [Tooltip("掉落物图层。Default 会和车、地面、货架都碰撞；不要用 Collectibles。")]
        public int itemLayer = 0;
        [Tooltip("每立方米质量，越小越容易被推走。")]
        public float density = 40f;
        public Vector2 massRange = new Vector2(0.05f, 0.4f);
        public float linearDamping = 0.6f;
        public float angularDamping = 0.6f;
        [Tooltip("货架里商品物体的名字前缀（如 Food_BaconBlock）。撞到货架时会随机复制这些物体弹出；找不到时退回按尺寸挑模型。")]
        public string productNamePrefix = "Food";
        [Tooltip("商品尺寸范围（米），超出的模型不会被当成商品。")]
        public float maxProductSize = 0.9f;
        public float minProductSize = 0.06f;
        public float itemScale = 1f;

        [Header("物品高亮：彩色滴颜料 (Paint Drip)")]
        [Tooltip("掉落物品外面一圈五颜六色的滴颜料描边（后处理之后画，不受场景滤镜影响）。")]
        public bool paintHighlight = false;
        [Range(0, 20)] public float paintWidth = 5f;
        [Tooltip("描边粗细不均匀的程度。")]
        [Range(0, 1)] public float paintBlotch = 0.6f;
        [Tooltip("颜色色块大小，越大色块越碎。")]
        [Range(0.5f, 40)] public float paintPatchScale = 6f;
        [Tooltip("颜料往下滴的长度。")]
        [Range(0, 60)] public float dripLength = 18f;
        [Tooltip("滴落的密度。")]
        [Range(1, 60)] public float dripDensity = 14f;
        [Tooltip("滴落重新生长的速度。")]
        [Range(0, 3)] public float dripSpeed = 0.4f;
        [Tooltip("每秒重画几次（手绘抖动），0 = 不抖。")]
        [Range(0, 24)] public float paintBoilFps = 6f;
        [Tooltip("五种颜料颜色。")]
        public Color[] paintColors =
        {
            new Color(1f, 0.15f, 0.35f), new Color(1f, 0.8f, 0.05f), new Color(0.1f, 0.85f, 0.35f),
            new Color(0.15f, 0.55f, 1f), new Color(0.75f, 0.25f, 1f)
        };
    }

    public enum TrailStyle { 丝带 = 0, 霓虹 = 1, 速度线 = 2, 残影 = 3 }

    [Serializable]
    public class TrailSettings
    {
        public bool enabled = true;
        public TrailStyle style = TrailStyle.霓虹;
        [Tooltip("轨迹持续时间（秒）。")]
        public float time = 0.45f;
        public float width = 0.35f;
        [Tooltip("HDR 亮度，配合 Bloom 发光。")]
        public float intensity = 2.5f;
        [Tooltip("从车中心往后、往下偏移（米）。")]
        public float backOffset = 0.6f;
        public float heightOffset = 0.15f;
        [Tooltip("两条轮迹的左右间距（丝带 / 霓虹）。")]
        public float sideSpacing = 0.45f;
        [Tooltip("速度线条数。")]
        public int speedLineCount = 6;
        [Tooltip("残影生成间隔（秒）。")]
        public float ghostInterval = 0.05f;
        public float ghostLifetime = 0.3f;
        [Tooltip("可乐曼妥思道具加速时也显示轨迹。")]
        public bool includeColaBoost = true;
    }

    public enum HighlightStyle { 描边 = 0, 边缘发光 = 1, 描边加发光 = 2, 脚下光圈 = 3, 手绘描边 = 4 }

    [Serializable]
    public class HighlightSettings
    {
        public bool enabled = true;
        public HighlightStyle style = HighlightStyle.手绘描边;
        [Range(0, 20)] public float outlineWidth = 6f;
        [Header("手绘描边")]
        [Tooltip("描边粗细的抖动程度，越大越像手涂得不均匀。")]
        [Range(0, 1)] public float sketchWobble = 0.6f;
        [Tooltip("每秒重画几次（越低越像逐帧手绘的抖动）。")]
        [Range(1, 24)] public float sketchFps = 8f;
        [Tooltip("粗细变化的密度。")]
        [Range(0.5f, 30)] public float sketchNoiseScale = 6f;
        [Tooltip("整条描边每帧偏出车身的距离，像涂色涂出界。")]
        [Range(0, 5)] public float sketchOffset = 1.5f;
        [Tooltip("描边用玩家颜色的满饱和、满亮度版本。")]
        public bool vividOutline = true;
        [Tooltip("只描车的外轮廓，车内部（轮子、车筐等零件之间）不画线。")]
        public bool silhouetteOnly = true;
        [Tooltip("把车身遮罩向外扩多少，用来填住铁丝网车筐的空隙，避免整台车被涂满。描边会画在扩出来的这圈外面。")]
        [Range(0, 40)] public float gapFill = 10f;
        [Tooltip("用平滑法线，避免硬边处描边断开。")]
        public bool smoothNormals = true;
        [Header("发光")]
        [Range(0, 5)] public float glowIntensity = 1.2f;
        [Range(0.5f, 8)] public float glowPower = 2.5f;
        [Range(0, 1)] public float fillIntensity = 0.05f;
        [Range(0, 1)] public float pulseAmount = 0.25f;
        public float pulseSpeed = 3f;
        [Tooltip("加速时高亮变亮的倍数。")]
        public float boostMultiplier = 1.8f;
        [Tooltip("光圈大小（米）。")]
        public float ringRadius = 1.3f;
        [Tooltip("跟随的车也一起高亮。")]
        public bool includeFollowers = true;
        [Tooltip("被捡到的小车保持车本身的颜色（游戏默认会把它染成玩家色；这里只在测试场景运行时改回来）。")]
        public bool keepFollowerOwnColor = true;
    }

    [Serializable]
    public class BubbleSettings
    {
        [Tooltip("把可拾取商品外面的 bubble 换成手绘滴颜料风格（运行时替换，原 prefab/材质不动）。")]
        public bool enabled = true;
        [Tooltip("用原 bubble 材质的颜色；关掉就用下面的颜色。")]
        public bool useOriginalColor = true;
        public Color paintColor = new Color(0.35f, 0.8f, 1f);
        [Tooltip("描边颜色 = 颜料色乘这个亮度。")]
        [Range(0, 1)] public float rimDarken = 0.45f;
        [Range(0, 1)] public float fillAlpha = 0.18f;
        [Range(0, 0.8f)] public float rimWidth = 0.28f;
        [Tooltip("描边粗细不均匀程度。")]
        [Range(0, 1)] public float rimJitter = 0.5f;
        [Tooltip("高光大小。")]
        [Range(0, 0.5f)] public float highlightSize = 0.16f;
        [Tooltip("形状抖动。")]
        [Range(0, 0.3f)] public float wobble = 0.06f;
        [Tooltip("底部滴颜料长度。")]
        [Range(0, 1.5f)] public float dripLength = 0.45f;
        [Range(3, 40)] public float dripColumns = 14f;
        [Range(0, 3)] public float dripSpeed = 0.35f;
        [Tooltip("每秒重画几次（手绘抖动），0 = 不抖。")]
        [Range(0, 24)] public float boilFps = 6f;
        [Tooltip("原 bubble 物体的名字。")]
        public string bubbleObjectName = "Fresnel Sphere Outline";
    }

    [Serializable]
    public class EatSettings
    {
        public bool enabled = true;
        [Tooltip("新车位置爆出星星粒子。")]
        public bool burst = true;
        public int burstCount = 24;
        public float burstSpeed = 5f;
        [Tooltip("地面冲击波光环。")]
        public bool shockRing = true;
        public float ringRadius = 2.5f;
        public float ringTime = 0.4f;
        [Tooltip("新车和车头弹一下（只缩放模型，不影响物理）。")]
        public bool punch = true;
        public float punchScale = 0.35f;
        public float punchTime = 0.3f;
        [Tooltip("头顶冒出 +1。")]
        public bool popupText = true;
        public float popupHeight = 2.2f;
        [Tooltip("HDR 亮度。")]
        public float intensity = 3f;
    }
}
