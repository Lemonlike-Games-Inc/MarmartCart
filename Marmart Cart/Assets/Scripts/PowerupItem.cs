using UnityEngine;
public enum PowerupTier
{
    Normal,
    Gold
}
public class PowerupItem : MonoBehaviour, ISpawnerHoldable
{
    [SerializeField] private SfxManager sfxManager;
    [SerializeField] private float selfCleanTime = 20f;
    [Header("Powerup Tier")]
    [SerializeField] private PowerupTier powerupTier = PowerupTier.Normal;  // NEW

    private bool _heldBySpawner = false;
    public void OnSpawnerHoldStart()
    {
        _heldBySpawner = true;
    }

    public void OnSpawnerHoldEnd()
    {
        _heldBySpawner = false;
    }
    private void Start()
    {
        // Automatically destroy the powerup item after X seconds if not collected
        if (_heldBySpawner) { return; }
        Destroy(this.gameObject, selfCleanTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player1"))
        {
            PowerupsManager p1PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p1PowerupManager == null)
            {
                // hit a chained cart instead of the leading cart
                return;
            }

            p1PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }

        if (other.gameObject.CompareTag("Player2"))
        {
            PowerupsManager p2PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p2PowerupManager == null)
            {
                return;
            }

            p2PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }

        if (other.gameObject.CompareTag("Player3"))
        {
            PowerupsManager p3PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p3PowerupManager == null)
            {
                return;
            }

            p3PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }

        if (other.gameObject.CompareTag("Player4"))
        {
            PowerupsManager p4PowerupManager = other.GetComponentInChildren<PowerupsManager>();
            if (p4PowerupManager == null)
            {
                return;
            }

            p4PowerupManager.RollRandomPowerup(powerupTier);   // NEW: pass tier
            sfxManager.PlaySFX("CollectPowerup");
            Destroy(this.gameObject);
        }
    }
}
