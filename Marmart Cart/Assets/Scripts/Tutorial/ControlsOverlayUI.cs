using UnityEngine;

/// <summary>
/// One player's tutorial control-hint panel.
/// Existing ControlHints components still own the reveal / held / pulse animations.
/// </summary>
[DisallowMultipleComponent]
public class ControlsOverlayUI : MonoBehaviour
{
    [Header("Rows - Display Order")]
    [SerializeField] private ControlHints movementRow;
    [SerializeField] private ControlHints driftRow;
    [SerializeField] private ControlHints speedupRow;
    [SerializeField] private ControlHints reverseRow;
    [SerializeField] private ControlHints checkoutRow;
    [SerializeField] private ControlHints aimRow;
    [SerializeField] private ControlHints activatePowerupRow;

    [Header("Runtime - Read Only")]
    [SerializeField, Range(1, 4)] private int playerIndex = 1;
    [SerializeField] private CartControlScript cart;

    public int PlayerIndex => playerIndex;
    public CartControlScript BoundCart => cart;

    private void Awake()
    {
        EnforceRowOrder();
        ApplyInitialVisibility();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void ConfigurePlayer(int index)
    {
        playerIndex = Mathf.Clamp(index, 1, 4);
    }

    public void BindToCart(CartControlScript newCart)
    {
        if (cart == newCart) return;

        Unbind();
        cart = newCart;
        if (cart == null) return;

        cart.OnMoveHeld += HandleMovementHeld;
        cart.OnDriftHeld += HandleDriftHeld;
        cart.OnSpeedupHeld += HandleSpeedupHeld;
        cart.OnMoveBackwardPressed += HandleReversePressed;
        cart.OnCheckoutReleased += HandleCheckoutPressed;
        cart.OnAimHeld += HandleAimHeld;

        // Use the existing post-activation feedback event. Do NOT subscribe the
        // UI to OnPowerupUsePressed because that event also participates in
        // gameplay's power-up routing.
        cart.OnShootPressed += HandleActivatePowerupPressed;
    }

    public void Unbind()
    {
        if (cart == null) return;

        cart.OnMoveHeld -= HandleMovementHeld;
        cart.OnDriftHeld -= HandleDriftHeld;
        cart.OnSpeedupHeld -= HandleSpeedupHeld;
        cart.OnMoveBackwardPressed -= HandleReversePressed;
        cart.OnCheckoutReleased -= HandleCheckoutPressed;
        cart.OnAimHeld -= HandleAimHeld;
        cart.OnShootPressed -= HandleActivatePowerupPressed;

        SetAllHeld(false);
        cart = null;
    }

    public void ApplyInitialVisibility()
    {
        SetBaseHintsIntroduced(true);
        SetCheckoutIntroduced(false);
        SetAdvancedHintsIntroduced(false);
    }

    public void SetBaseHintsIntroduced(bool introduced)
    {
        movementRow?.SetIntroduced(introduced);
        driftRow?.SetIntroduced(introduced);
        speedupRow?.SetIntroduced(introduced);
        reverseRow?.SetIntroduced(introduced);

        if (!introduced)
        {
            movementRow?.SetHeld(false);
            driftRow?.SetHeld(false);
            speedupRow?.SetHeld(false);
        }
    }

    public void SetCheckoutIntroduced(bool introduced)
    {
        checkoutRow?.SetIntroduced(introduced);
    }

    public void SetAdvancedHintsIntroduced(bool introduced)
    {
        aimRow?.SetIntroduced(introduced);
        activatePowerupRow?.SetIntroduced(introduced);

        if (!introduced)
            aimRow?.SetHeld(false);
    }

    public void IntroduceMovement() => movementRow?.SetIntroduced(true);
    public void IntroduceDrift() => driftRow?.SetIntroduced(true);
    public void IntroduceSpeedup() => speedupRow?.SetIntroduced(true);
    public void IntroduceReverse() => reverseRow?.SetIntroduced(true);
    public void IntroduceCheckout() => checkoutRow?.SetIntroduced(true);
    public void IntroduceAim() => aimRow?.SetIntroduced(true);
    public void IntroduceActivatePowerup() => activatePowerupRow?.SetIntroduced(true);

    // Backward-compatible aliases for old tutorial calls.
    public void IntroduceMove() => IntroduceMovement();
    public void IntroduceMoveBackward() => IntroduceReverse();
    public void IntroduceCharge() => IntroduceSpeedup();
    public void IntroduceShoot() => IntroduceActivatePowerup();
    public void IntroduceExit() => IntroduceDrift();

    private void HandleMovementHeld(bool held) => movementRow?.SetHeld(held);
    private void HandleDriftHeld(bool held) => driftRow?.SetHeld(held);
    private void HandleSpeedupHeld(bool held) => speedupRow?.SetHeld(held);
    private void HandleAimHeld(bool held) => aimRow?.SetHeld(held);

    private void HandleReversePressed() => reverseRow?.Pulse();
    private void HandleCheckoutPressed() => checkoutRow?.Pulse();
    private void HandleActivatePowerupPressed() => activatePowerupRow?.Pulse();

    private void SetAllHeld(bool held)
    {
        movementRow?.SetHeld(held);
        driftRow?.SetHeld(held);
        speedupRow?.SetHeld(held);
        aimRow?.SetHeld(held);
    }

    [ContextMenu("Enforce Hint Row Order")]
    public void EnforceRowOrder()
    {
        SetSiblingIndex(movementRow, 0);
        SetSiblingIndex(driftRow, 1);
        SetSiblingIndex(speedupRow, 2);
        SetSiblingIndex(reverseRow, 3);
        SetSiblingIndex(checkoutRow, 4);
        SetSiblingIndex(aimRow, 5);
        SetSiblingIndex(activatePowerupRow, 6);
    }

    private static void SetSiblingIndex(ControlHints row, int index)
    {
        if (row == null) return;
        row.transform.SetSiblingIndex(index);
    }
}
