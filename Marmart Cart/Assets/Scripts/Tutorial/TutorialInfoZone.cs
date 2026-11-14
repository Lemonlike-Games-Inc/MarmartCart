using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class TutorialInfoZone : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private TutorialInfoPanel p1Panel;
    [SerializeField] private TutorialInfoPanel p2Panel;

    [Header("Behavior")]
    [SerializeField] private bool oneShot = false;
    private bool _used;

    [SerializeField] private ControlsOverlayUI ControlsOverlayUIscript;
    [SerializeField] int caseSwitch = 0;
    private void Reset()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneShot && _used) return;

        // Must be the leading cart collider with the tag
        TutorialInfoPanel panel = null;
        if (other.CompareTag("Player1")) panel = p1Panel;
        else if (other.CompareTag("Player2")) panel = p2Panel;
        else return;

        if (panel == null || panel.IsOpen) return;

        var raycaster = other.GetComponent<LeadingCartRaycaster>();
        var cartCtrl = other.GetComponentInChildren<CartControlScript>();

        if (raycaster == null || cartCtrl == null) return;

        _used = true;
        panel.Open(cartCtrl, raycaster);

        if (ControlsOverlayUIscript != null)
        {
            switch
                 (caseSwitch)
            {
                case 0:
                    ControlsOverlayUIscript.IntroduceCheckout();
                    ControlsOverlayUIscript.IntroduceExit();
                    break;
                case 1:
                    ControlsOverlayUIscript.IntroduceAim();
                    ControlsOverlayUIscript.IntroduceShoot();
                    ControlsOverlayUIscript.IntroduceCharge();
                    break;
            }
        }
    }
}