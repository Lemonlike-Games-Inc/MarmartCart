using UnityEngine;

public class RadioDishPowerup : MonoBehaviour, IPowerup
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Vector3 firePoint = new Vector3(0f, 1f, 0f);
    [SerializeField] private float bulletSpeed = 10f;

    public void ActivatePowerup()
    {
        DinoBehaviour dino = FindFirstObjectByType<DinoBehaviour>();
        if (dino == null)
        {
            Debug.LogWarning("No DinoUFO found in the scene!");
            return;
        }

        Vector3 spawnPos = transform.TransformPoint(firePoint); // local → world space

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        var bulletScript = bullet.GetComponent<RadioDishBullet>();
        if (bulletScript != null)
        {
            bulletScript.Initialize(dino.transform.position + new Vector3(0f,8f,0f), bulletSpeed);
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 worldFirePoint = transform.TransformPoint(firePoint);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(worldFirePoint, 0.2f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(worldFirePoint, worldFirePoint + transform.forward * 0.5f);
    }
}
