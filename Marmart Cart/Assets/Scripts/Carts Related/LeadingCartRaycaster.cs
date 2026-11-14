using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LeadingCartRaycaster : MonoBehaviour
{
    [SerializeField] CartControlScript cartControlInput;
    [SerializeField] SnakeCartManager snakeCartManager;

    [Header("Raycast Settings")]
    [SerializeField] LayerMask layerMask;
    [SerializeField] float distance;
    [SerializeField] private float raycastYOffset = 0.5f;
    [SerializeField] private float raycastZOffset = 0.5f;

    [field: SerializeField]
    public Vector3 hitDirection { get; private set; }

    [Header("Others")]
    [SerializeField] private float detachCooldown = 5f;
    [SerializeField] private float cooldownTimer = 0f;
    private bool cartInGhostMode = false;

    [Header("Events")]
    [SerializeField] GameEvent disableDetachEvent;
    [SerializeField] SfxManager sfxManager;
    [SerializeField] GameObject chargingVFX;

    [Header("Tomato Splash")]
    [SerializeField] private Image tomatoSplashEffect;
    [SerializeField] private float splashFadeDelay = 2f;
    [SerializeField] private float splashFadeDuration = 3f;
    private Coroutine tomatoSplashRoutine;

    void Start()
    {
        snakeCartManager = this.transform.parent.GetComponent<SnakeCartManager>();
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        if (cooldownTimer > 0f)
        {
            if (!cartInGhostMode)
            {
                this.gameObject.GetComponent<CartMaterialManager>()?.SetGhostMode(cooldownTimer);
                cartInGhostMode = true;
            }
        }
        else
        {
            cartInGhostMode = false;
        }

        if (cartControlInput.IsSpeedingUp() || cartControlInput.IsCharing()) chargingVFX.SetActive(true);
        else chargingVFX.SetActive(false);
    }

    void FixedUpdate()
    {
        Vector3 rayStartPosition = transform.position + transform.forward * raycastZOffset + transform.up * raycastYOffset;
        RaycastHit hit;

        if (Physics.Raycast(rayStartPosition, transform.forward, out hit, distance, layerMask))
        {
            if (hit.transform.gameObject.GetComponent<ChainedCartManager>() != null)
            {
                GameObject hitObject = hit.transform.gameObject;
                ChainedCartManager hitCartInfo = hitObject.GetComponent<ChainedCartManager>();

                if (hitCartInfo.isCollectedByPlayer && !hitCartInfo.CompareTag(this.tag))
                {
                    if (hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>() != null)
                    {
                        CartControlScript hitCartController = hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>();

                        if (hitCartController.GetIsInPit())
                        {
                            DetachSelfCompletely();
                            return;
                        }
                    }
                }

                if (cartControlInput.IsCharing())
                {
                    if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f)
                    {
                        hitDirection = -1 * hit.normal;
                        hitCartInfo.OnDetach(hitDirection);
                        sfxManager.PlaySFX("Detach");
                    }
                }
                else
                {
                    if (hitCartInfo.isCollectedByPlayer && cooldownTimer <= 0f) DetachSelfCompletely();
                }
            }
            else if (hit.transform.gameObject.GetComponent<LeadingCartRaycaster>() != null && !hit.transform.gameObject.CompareTag(this.tag))
            {
                if (hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>() != null)
                {
                    CartControlScript hitCartController = hit.transform.parent.GetChild(0).GetComponentInChildren<CartControlScript>();

                    if (hitCartController.GetIsInPit())
                    {
                        DetachSelfCompletely();
                        return;
                    }
                }

                if (cartControlInput.IsCharing())
                {
                    if (hit.transform.parent.GetComponent<SnakeCartManager>().GetSnakeBody().Count >= 2)
                    {
                        ChainedCartManager secondCartOnOtherPlayer = hit.transform.parent.GetComponent<SnakeCartManager>().GetSnakeBody()[1].GetComponent<ChainedCartManager>();

                        if (cooldownTimer <= 0f)
                        {
                            hitDirection = -1 * hit.normal;
                            secondCartOnOtherPlayer.OnDetach(hitDirection);
                            sfxManager.PlaySFX("Detach");
                        }
                    }
                }
                else
                {
                    if (cooldownTimer <= 0f && !hit.transform.gameObject.GetComponent<LeadingCartRaycaster>().getIfInGhostMode())
                        DetachSelfCompletely();
                }
            }

            if (hit.transform.gameObject.CompareTag("Obstacles") && cartControlInput.IsCharing())
                Destroy(hit.transform.gameObject);
        }
    }

    public void TemporarilyDisableDetaching()
    {
        cooldownTimer = detachCooldown;
    }

    public void SetInGhostModeWithTime(float time)
    {
        cooldownTimer = time;
    }

    public void DetachSelfCompletely()
    {
        cooldownTimer = 4f;
        cartControlInput.DisallowActivatePowerUp();

        if (snakeCartManager.GetSnakeBody().Count >= 2)
        {
            snakeCartManager.GetSnakeBody()[1].GetComponent<ChainedCartManager>().OnDetach();
            sfxManager.PlaySFX("Detach");
        }

        LeadingCartBehaviour leadingCartBehaviour0 = this.gameObject.transform.GetChild(0).GetChild(0).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour1 = this.gameObject.transform.GetChild(0).GetChild(1).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour2 = this.gameObject.transform.GetChild(0).GetChild(2).GetComponent<LeadingCartBehaviour>();
        LeadingCartBehaviour leadingCartBehaviour3 = this.gameObject.transform.GetChild(0).GetChild(3).GetComponent<LeadingCartBehaviour>();

        //leadingCartBehaviour0.SetSpeedToZero(2f);
        //leadingCartBehaviour1.SetSpeedToZero(2f);
        //leadingCartBehaviour2.SetSpeedToZero(2f);
        //leadingCartBehaviour3.SetSpeedToZero(2f);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 rayStartPosition = transform.position + transform.forward * raycastZOffset + transform.up * raycastYOffset;
        Gizmos.DrawRay(rayStartPosition, transform.forward * distance);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            sfxManager.PlaySFX("CrashWalls");
            //cartControlInput.DisallowActivatePowerUp();
            //cartControlInput.AllowMoveBackward();

            if (cartControlInput.IsCharing()) Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Walls"))
        {
            sfxManager.PlaySFX("CrashWalls");
            //cartControlInput.DisallowActivatePowerUp();
            //cartControlInput.AllowMoveBackward();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles") || collision.gameObject.CompareTag("Walls"))
        {
            //cartControlInput.AllowActivatePowerUp();
            //cartControlInput.DisallowMoveBackward();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            //if (cartControlInput.IsCharing()) Destroy(collision.gameObject);
            //cartControlInput.DisallowActivatePowerUp();
            //cartControlInput.AllowMoveBackward();
        }

        if (collision.gameObject.CompareTag("Walls"))
        {
            //cartControlInput.AllowMoveBackward();
            //cartControlInput.DisallowActivatePowerUp();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Obstacles"))
        {
            cartControlInput.DisallowActivatePowerUp();
            cartControlInput.AllowMoveBackward();
        }

        if (other.gameObject.CompareTag("Walls"))
        {
            cartControlInput.DisallowActivatePowerUp();
            cartControlInput.AllowMoveBackward();
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.CompareTag("Obstacles"))
        {
            cartControlInput.DisallowActivatePowerUp();
            cartControlInput.AllowMoveBackward();
        }

        if (other.gameObject.CompareTag("Walls"))
        {
            cartControlInput.DisallowActivatePowerUp();
            cartControlInput.AllowMoveBackward();
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Obstacles"))
        {
            cartControlInput.AllowActivatePowerUp();
            cartControlInput.DisallowMoveBackward();
        }

        if (other.gameObject.CompareTag("Walls"))
        {
            cartControlInput.AllowActivatePowerUp();
            cartControlInput.DisallowMoveBackward();
        }
    }
    public bool getIfInGhostMode()
    {
        return cartInGhostMode;
    }

    public SnakeCartManager GetmySnakeCartManager()
    {
        return snakeCartManager;
    }

    public void TriggerTomatoSplash()
    {
        if (tomatoSplashEffect == null) return;

        if (tomatoSplashRoutine != null) StopCoroutine(tomatoSplashRoutine);
        tomatoSplashRoutine = StartCoroutine(TomatoSplashCoroutine());
    }

    private IEnumerator TomatoSplashCoroutine()
    {
        tomatoSplashEffect.gameObject.SetActive(true);

        Color c = tomatoSplashEffect.color;
        c.a = 1f;
        tomatoSplashEffect.color = c;

        yield return new WaitForSeconds(splashFadeDelay);

        float elapsed = 0f;

        while (elapsed < splashFadeDuration)
        {
            float t = elapsed / splashFadeDuration;
            c.a = Mathf.Lerp(1f, 0f, t);
            tomatoSplashEffect.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }

        c.a = 0f;
        tomatoSplashEffect.color = c;
        tomatoSplashEffect.gameObject.SetActive(false);
        tomatoSplashRoutine = null;
    }
}
