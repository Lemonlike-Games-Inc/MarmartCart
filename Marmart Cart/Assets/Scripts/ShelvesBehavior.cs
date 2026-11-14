using UnityEngine;

public class ShelvesBehavior : MonoBehaviour
{
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;

    private bool isBeingSucked = false;
    private float targetY;
    private Vector3 rotateAxis;
    private float rotateSpeed;
    private Vector3 targetScale;
    private BoxCollider boxCollider;

    void Start()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        originalScale = transform.localScale;
        boxCollider = GetComponent<BoxCollider>();
    }
    public bool getIsBeingSucked()
    {
        return isBeingSucked;
    }
    public void BeginSuckEffect()
    {
        if (isBeingSucked) return;

        isBeingSucked = true;
        targetY = Random.Range(11f, 15f);
        rotateAxis = Random.onUnitSphere;
        rotateSpeed = Random.Range(5f, 15f);
        float scaleFactor = Random.Range(0.55f, 0.65f);
        targetScale = originalScale * scaleFactor;
        boxCollider.enabled = false;
    }

    public void StopAndRestore()
    {
        isBeingSucked = false;
        transform.position = originalPosition;
        transform.rotation = originalRotation;
        transform.localScale = originalScale;
        boxCollider.enabled = true;
    }
    void Update()
    {
        if (!isBeingSucked) return;

        // Move up to target height
        Vector3 pos = transform.position;
        pos.y = Mathf.MoveTowards(pos.y, targetY, Time.deltaTime * 2f);
        transform.position = pos;

        // Rotate slowly
        transform.Rotate(rotateAxis, rotateSpeed * Time.deltaTime);

        // Scale down slightly
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * 2f);
    }
}
