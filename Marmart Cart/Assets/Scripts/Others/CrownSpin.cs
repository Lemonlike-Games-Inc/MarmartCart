using UnityEngine;

public class CrownSpin : MonoBehaviour
{
    [Tooltip("Degrees per second")]
    [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 180f, 0f);

    private void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
