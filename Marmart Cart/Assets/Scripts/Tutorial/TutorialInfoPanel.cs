using TMPro;
using UnityEngine;

public class TutorialInfoPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text pageText; // optional

    [Header("Pages")]
    [TextArea][SerializeField] private string[] pages;

    private int _index;
    private bool _open;

    private CartControlScript _cart;
    private LeadingCartRaycaster _raycaster;

    public bool IsOpen => _open;

    private void Awake()
    {
        if (panelRoot) panelRoot.SetActive(false);
        _open = false;
    }

    public void Open(CartControlScript cart, LeadingCartRaycaster raycaster)
    {
        if (_open) return;
        if (pages == null || pages.Length == 0) return;

        _open = true;
        _cart = cart;
        _raycaster = raycaster;
        _index = 0;

        // Freeze this player's cart only
        FreezeCart();

        // Subscribe to THIS cart’s D-pad events
        if (_cart != null)
        {
            _cart.OnTutorialPrev += PrevPage;
            _cart.OnTutorialNext += NextPage;

            // Also block gameplay input (cheap + safe)
            _cart.DisableControl();
            _cart.DisallowSpeedingUp();
            _cart.DisallowActivatePowerUp();
        }

        panelRoot.SetActive(true);
        //Debug.Log("panel set active to true");
        Refresh();
    }

    public void Close()
    {
        if (!_open) return;

        // Unsubscribe
        if (_cart != null)
        {
            _cart.OnTutorialPrev -= PrevPage;
            _cart.OnTutorialNext -= NextPage;

            // Restore control
            _cart.EnableControl();
            _cart.AllowSpeedingUp();
            _cart.AllowActivatePowerUp();
        }

        // Unfreeze wheels
        UnfreezeCart();

        _open = false;
        _cart = null;
        _raycaster = null;

        panelRoot.SetActive(false);
    }

    private void NextPage()
    {
        if (!_open) return;

        _index++;
        if (_index >= pages.Length)
        {
            Close();
            return;
        }
        Refresh();
    }

    private void PrevPage()
    {
        if (!_open) return;

        _index = Mathf.Max(0, _index - 1);
        Refresh();
    }

    private void Refresh()
    {
        bodyText.text = pages[_index];
        if (pageText) pageText.text = $"{_index + 1}/{pages.Length}";
    }

    private void FreezeCart()
    {
        CartFreezeUtil.Freeze(_raycaster);
        // optional: ensure after physics tick like your pit zone does
        Invoke(nameof(FreezeAgain), 0.05f);
    }

    private void FreezeAgain()
    {
        CartFreezeUtil.Freeze(_raycaster);
    }

    private void UnfreezeCart()
    {
        CartFreezeUtil.Unfreeze(_raycaster);
    }
}
