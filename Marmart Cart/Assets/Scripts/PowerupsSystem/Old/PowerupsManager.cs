using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;

public class PowerupsManager : MonoBehaviour
{
    public enum PowerupType { Boost, Scale, BowlingBall, Tomato, Grabber }

    [Header("Identity")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1; // 1..4
    public int PlayerIndex => playerIndex;

    [Header("References")]
    [SerializeField] private SnakeCartManager snakeCartManager;
    [SerializeField] private CartControlScript cartControlScript;

    [Header("Prefabs / Fire")]
    [SerializeField] private GameObject bowlingBallPrefab;
    [SerializeField] private GameObject tomatoPrefab;
    [SerializeField] private Transform firePoint;

    [Header("Boost")]
    [Tooltip("If you still use GameEvent for boost, wire it. Otherwise leave null and handle via CartControlScript directly.")]
    [SerializeField] private GameEvent boostEvent;
    [SerializeField] private GameObject visualBoostMore;

    [Header("Scaling Powerup")]
    [SerializeField] private float scaleDuration = 10f;
    private int scaleBuffCount = 0;

    [Header("SFX")]
    [SerializeField] private SfxManager sfxManager;

    [Header("Powerup Visuals")]
    [SerializeField] private GameObject visualBoost;
    [SerializeField] private GameObject visualScale;
    [SerializeField] private GameObject visualBowlingBall;
    [SerializeField] private GameObject visualTomato;
    [SerializeField] private GameObject visualGrabberRoot;

    //[Header("Grabber Powerup")]
    //[SerializeField] private GrabberAnimator grabberAnimatorInstance; // on GrabberRoot
    //[SerializeField] private Transform grabberRoot;

    private PowerupTier currentTier = PowerupTier.Normal;
    private PowerupType? currentPowerup = null;
    private bool powerupReady = false;

    private void Start()
    {
        Invoke(nameof(RegisterPlayer), 0.1f);
        UpdatePowerupVisuals();
    }

    public void RegisterPlayer()
    {
        if (!snakeCartManager)
            snakeCartManager = transform.parent.GetComponent<SnakeCartManager>();

        if (!cartControlScript)
        {
            cartControlScript = GetComponentInChildren<CartControlScript>();
            if (cartControlScript) cartControlScript.SetPowerupsManager(this);
        }

        // If you don’t want to set playerIndex manually,
        // you can infer it from tag: "Player1".."Player4"
        if (playerIndex <= 0 || playerIndex > 4)
            playerIndex = TagToPlayerIndex(gameObject.tag);
    }

    // ------------------ ROLL ------------------
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            RollRandomPowerup(PowerupTier.Normal);
        }
        if (Input.GetKeyDown(KeyCode.M))
        {
            RollRandomPowerup(PowerupTier.Gold);
        }

    }
    public void RollRandomPowerup(PowerupTier tier)
    {
        currentTier = tier;

        PowerupType rolled = RollFromTier(tier);

        currentPowerup = rolled;
        powerupReady = true;

        cartControlScript.AllowActivatePowerUp();

        // Aiming needed?
        bool needsAim = (rolled == PowerupType.BowlingBall || rolled == PowerupType.Tomato || rolled == PowerupType.Grabber);
        if (needsAim) cartControlScript.AllowAim();
        else cartControlScript.DisallowAim();

        UpdatePowerupVisuals();
        // Debug.Log($"P{playerIndex} rolled {tier} powerup: {currentPowerup}");
    }

    private PowerupType RollFromTier(PowerupTier tier)
    {
        if (tier == PowerupTier.Normal)
        {
            // Normal: Scale, Tomato
            int roll = Random.Range(0, 2); // 0,1
            return roll == 0 ? PowerupType.Scale : PowerupType.Tomato;
        }

        // Gold: Boost, BowlingBall
        int goldRoll = Random.Range(0, 2); // 0,1
        return goldRoll == 0 ? PowerupType.Boost : PowerupType.BowlingBall;
    }

    // ------------------ ACTIVATE ------------------

    public void ActivateStoredPowerup()
    {
        if (!powerupReady || currentPowerup == null) return;
        if (!cartControlScript.GetCanActivatePowerUp()) return;

        switch (currentPowerup.Value)
        {
            case PowerupType.Boost:
                ActivateBoost();
                break;

            case PowerupType.Scale:
                StartCoroutine(ScaleCartsTemporarily());
                break;

            case PowerupType.BowlingBall:
                FireProjectile(bowlingBallPrefab, ProjectileType.BowlingBall);
                break;

            case PowerupType.Tomato:
                FireProjectile(tomatoPrefab, ProjectileType.Tomato);
                break;
        }

        cartControlScript.DisallowAim();
        cartControlScript.DisallowActivatePowerUp();

        currentPowerup = null;
        powerupReady = false;
        UpdatePowerupVisuals();
    }

    private void ActivateBoost()
    {
        // Option A: keep your existing GameEvent pattern
        if (boostEvent != null)
        {
            boostEvent.Raise();
        }

        if (visualBoostMore)
        {
            visualBoostMore.SetActive(true);
            Invoke(nameof(DisableBoostMoreVisual), 1f);
        }
    }

    // ------------------ VISUALS ------------------

    private void UpdatePowerupVisuals()
    {
        if (visualBoost) visualBoost.SetActive(false);
        if (visualScale) visualScale.SetActive(false);
        if (visualBowlingBall) visualBowlingBall.SetActive(false);
        if (visualTomato) visualTomato.SetActive(false);
        if (visualGrabberRoot) visualGrabberRoot.SetActive(false);

        if (!powerupReady || currentPowerup == null)
            return;

        switch (currentPowerup.Value)
        {
            case PowerupType.Boost:
                if (visualBoost) visualBoost.SetActive(true);
                break;
            case PowerupType.Scale:
                if (visualScale) visualScale.SetActive(true);
                break;
            case PowerupType.BowlingBall:
                if (visualBowlingBall) visualBowlingBall.SetActive(true);
                break;
            case PowerupType.Tomato:
                if (visualTomato) visualTomato.SetActive(true);
                break;
            case PowerupType.Grabber:
                if (visualGrabberRoot) visualGrabberRoot.SetActive(true);
                break;
        }
    }

    // ------------------ GRABBER ------------------

    //private void UseGrabber()
    //{
    //    if (!grabberAnimatorInstance || !grabberRoot || !firePoint) return;

    //    grabberRoot.gameObject.SetActive(true);
    //    grabberRoot.position = firePoint.position;

    //    Vector3 dir = cartControlScript.AimDirection;
    //    if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
    //    dir.y = 0f;
    //    if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
    //    dir.Normalize();

    //    float angleDeg = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
    //    float yawOffset = 0f;
    //    float finalY = angleDeg + yawOffset;

    //    grabberRoot.localEulerAngles = new Vector3(90f, finalY, 180f);

    //    var grabberBehavior = grabberRoot.GetComponentInChildren<GrabberBehavior>();
    //    if (grabberBehavior != null)
    //    {
    //        grabberBehavior.SetOwner(this, snakeCartManager);
    //        grabberBehavior.SetOwnerPlayerIndex(playerIndex); // add this method (recommended)
    //    }

    //    grabberAnimatorInstance.PlayGrabber();
    //}

    // ------------------ SCALE ------------------

    private IEnumerator ScaleCartsTemporarily()
    {
        scaleBuffCount++;

        if (scaleBuffCount == 1)
        {
            // send needed scale signal to SnakeCartManager to manage rest of bodies
            var leadingcart = snakeCartManager.GetSnakeBody()[0];
            leadingcart.transform.localScale = Vector3.one * 10f;
            snakeCartManager.needScaleup = true;
        }

        yield return new WaitForSeconds(scaleDuration);

        scaleBuffCount--;

        if (scaleBuffCount == 0)
        {
            // send reset scale signal to SnakeCartManager to manage rest of bodies
            var leadingcart = snakeCartManager.GetSnakeBody()[0];
            leadingcart.transform.localScale = Vector3.one * 5f;
            snakeCartManager.needScaleup = false;
        }
    }

    // ------------------ PROJECTILES ------------------

    private enum ProjectileType { BowlingBall, Tomato }

    private void FireProjectile(GameObject prefab, ProjectileType type)
    {
        if (!prefab || !firePoint) return;

        Quaternion rot = (type == ProjectileType.Tomato) ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.identity;
        GameObject projectile = Instantiate(prefab, firePoint.position, rot);

        Vector3 dir = cartControlScript.AimDirection;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        var proj = projectile.GetComponent<PowerupProjectile>();
        if (proj != null)
            proj.Launch(dir, playerIndex); // change projectile to accept int ownerIndex
    }

    private void DisableBoostMoreVisual()
    {
        if (visualBoostMore) visualBoostMore.SetActive(false);
    }

    public PowerupType? GetCurrentPowerup() => currentPowerup;

    private int TagToPlayerIndex(string tag)
    {
        if (!tag.StartsWith("Player")) return 1;
        if (int.TryParse(tag.Substring(6), out int num)) return Mathf.Clamp(num, 1, 4);
        return 1;
    }
}
