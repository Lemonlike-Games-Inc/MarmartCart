using UnityEngine;

public static class CartFreezeUtil
{
    public static void Freeze(LeadingCartRaycaster raycaster)
    {
        if (!raycaster) return;

        var root = raycaster.gameObject.transform.GetChild(0);
        root.GetChild(0).GetComponent<LeadingCartBehaviour>().SetSpeedToZero();
        root.GetChild(1).GetComponent<LeadingCartBehaviour>().SetSpeedToZero();
        root.GetChild(2).GetComponent<LeadingCartBehaviour>().SetSpeedToZero();
        root.GetChild(3).GetComponent<LeadingCartBehaviour>().SetSpeedToZero();
    }

    public static void Unfreeze(LeadingCartRaycaster raycaster)
    {
        if (!raycaster) return;

        var root = raycaster.gameObject.transform.GetChild(0);
        root.GetChild(0).GetComponent<LeadingCartBehaviour>().ResetSpeed();
        root.GetChild(1).GetComponent<LeadingCartBehaviour>().ResetSpeed();
        root.GetChild(2).GetComponent<LeadingCartBehaviour>().ResetSpeed();
        root.GetChild(3).GetComponent<LeadingCartBehaviour>().ResetSpeed();
    }
}
