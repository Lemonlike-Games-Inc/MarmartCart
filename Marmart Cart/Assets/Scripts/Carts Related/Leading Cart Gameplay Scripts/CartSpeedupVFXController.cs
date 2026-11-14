using UnityEngine;

public class CartSpeedupVFXController : MonoBehaviour
{
    [SerializeField] private CartControlScript cartControl;
    [SerializeField] private GameObject speedupVFX;

    private void OnEnable()
    {
        if (cartControl != null)
            cartControl.OnSpeedupHeld += SetSpeedupVFX;
    }

    private void OnDisable()
    {
        if (cartControl != null)
            cartControl.OnSpeedupHeld -= SetSpeedupVFX;

        if (speedupVFX != null)
            speedupVFX.SetActive(false);
    }

    private void SetSpeedupVFX(bool active)
    {
        if (speedupVFX != null)
            speedupVFX.SetActive(active);
    }
}