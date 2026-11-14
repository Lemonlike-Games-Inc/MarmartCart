using UnityEngine;

public class ControlsOverlayBinder : MonoBehaviour
{
    [SerializeField] private ControlsOverlayUI overlayP1;
    [SerializeField] private ControlsOverlayUI overlayP2;

    private void Start()
    {
        // Find cart controllers via tag (your current convention)
        Invoke(nameof(RegisterPlayer), 2f);
    }
    public void RegisterPlayer()
    {
        GameObject player1Ref;
        player1Ref = GameObject.FindGameObjectWithTag("Player1");
        GameObject player2Ref;
        player2Ref = GameObject.FindGameObjectWithTag("Player2");

        if (overlayP1) overlayP1.BindToCart(player1Ref.GetComponentInChildren<CartControlScript>());
        if (overlayP2) overlayP2.BindToCart(player2Ref.GetComponentInChildren<CartControlScript>());
    }
}