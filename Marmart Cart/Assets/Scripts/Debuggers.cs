using UnityEngine;
using UnityEngine.SceneManagement;

public class Debuggers : MonoBehaviour
{
    [Header("Restart Game")]
    [SerializeField] private bool enableRestartHotkey = true;
    [SerializeField] private KeyCode restartKey = KeyCode.F9;

    [Header("Player Roots (auto-assigned)")]
    [SerializeField] private Transform p1Root;
    [SerializeField] private Transform p2Root;
    [SerializeField] private Transform p3Root;
    [SerializeField] private Transform p4Root;

    [Header("Player Rigidbodies (auto-assigned)")]
    [SerializeField] private Rigidbody p1Rigidbody;
    [SerializeField] private Rigidbody p2Rigidbody;
    [SerializeField] private Rigidbody p3Rigidbody;
    [SerializeField] private Rigidbody p4Rigidbody;

    [Header("Player CartControl (auto-assigned)")]
    [SerializeField] private CartControlScript p1CartControl;
    [SerializeField] private CartControlScript p2CartControl;
    [SerializeField] private CartControlScript p3CartControl;
    [SerializeField] private CartControlScript p4CartControl;

    [Header("Teleport Targets (set in Inspector)")]
    [SerializeField] private Transform p1TeleportPoint;
    [SerializeField] private Transform p2TeleportPoint;
    [SerializeField] private Transform p3TeleportPoint;
    [SerializeField] private Transform p4TeleportPoint;

    [Header("Debug Hotkeys")]
    [SerializeField] private KeyCode p1TeleportKey = KeyCode.Alpha1; // 1
    [SerializeField] private KeyCode p2TeleportKey = KeyCode.Alpha2; // 2
    [SerializeField] private KeyCode p3TeleportKey = KeyCode.Alpha3; // 3
    [SerializeField] private KeyCode p4TeleportKey = KeyCode.Alpha4; // 4

    [SerializeField] private KeyCode p1AllowFlipKey = KeyCode.Q;
    [SerializeField] private KeyCode p2AllowFlipKey = KeyCode.W;
    [SerializeField] private KeyCode p3AllowFlipKey = KeyCode.E;
    [SerializeField] private KeyCode p4AllowFlipKey = KeyCode.R;

    [Header("Add Empty Cart Events")]
    [SerializeField] private GameEvent p1AddEmptyCartEvent;
    [SerializeField] private GameEvent p2AddEmptyCartEvent;
    [SerializeField] private GameEvent p3AddEmptyCartEvent;
    [SerializeField] private GameEvent p4AddEmptyCartEvent;

    [Header("Add Empty Cart Hotkeys")]
    [SerializeField] private KeyCode p1AddCartKey = KeyCode.Z;
    [SerializeField] private KeyCode p2AddCartKey = KeyCode.X;
    [SerializeField] private KeyCode p3AddCartKey = KeyCode.C;
    [SerializeField] private KeyCode p4AddCartKey = KeyCode.V;

    private void Start()
    {
        Invoke(nameof(RegisterPlayers), 2f);
    }

    private void RegisterPlayers()
    {
        RegisterOnePlayer(1, out p1Root, out p1Rigidbody, out p1CartControl);
        RegisterOnePlayer(2, out p2Root, out p2Rigidbody, out p2CartControl);
        RegisterOnePlayer(3, out p3Root, out p3Rigidbody, out p3CartControl);
        RegisterOnePlayer(4, out p4Root, out p4Rigidbody, out p4CartControl);

        Debug.Log("[Debuggers] Player refs registered.");
    }

    private void RegisterOnePlayer(int index, out Transform root, out Rigidbody rb, out CartControlScript control)
    {
        root = null;
        rb = null;
        control = null;

        var go = GameObject.FindGameObjectWithTag($"Player{index}");
        if (!go)
        {
            // In 2P mode P3/P4 may not exist—don't warn loudly.
            return;
        }

        root = go.transform;
        rb = go.GetComponentInChildren<Rigidbody>();
        control = go.GetComponentInChildren<CartControlScript>();

        if (!rb) Debug.LogWarning($"[Debuggers] Missing Rigidbody on Player{index}.");
        if (!control) Debug.LogWarning($"[Debuggers] Missing CartControlScript on Player{index}.");
    }

    private void Update()
    {
        RestartDebug();
        TeleportDebug();
        FlipDebug();
        AddEmptyCartDebug();
    }

    // ----------------- RESTART GAME -----------------

    private void RestartDebug()
    {
        if (!enableRestartHotkey) return;

        if (Input.GetKeyDown(restartKey))
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.LoadScene(current.buildIndex);
        }
    }

    // ----------------- TELEPORT PLAYERS -----------------

    private void TeleportDebug()
    {
        TryTeleport(p1TeleportKey, p1Root, p1Rigidbody, p1TeleportPoint, 1, p1CartControl);
        TryTeleport(p2TeleportKey, p2Root, p2Rigidbody, p2TeleportPoint, 2, p2CartControl);
        TryTeleport(p3TeleportKey, p3Root, p3Rigidbody, p3TeleportPoint, 3, p3CartControl);
        TryTeleport(p4TeleportKey, p4Root, p4Rigidbody, p4TeleportPoint, 4, p4CartControl);
    }

    private void TryTeleport(KeyCode key, Transform root, Rigidbody rb, Transform target, int index, CartControlScript control)
    {
        if (!Input.GetKeyDown(key)) return;

        if (root != null && target != null)
        {
            root.position = target.position;
            root.rotation = target.rotation;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            control.SetOutPit();
            LeadingCartBehaviour[] behaviours = control.gameObject.transform.parent.GetComponentsInChildren<LeadingCartBehaviour>();
            if(behaviours.Length > 0)
            {
                for(int i = 0; i < behaviours.Length; i++)
                {
                    behaviours[i].ResetSpeed();
                }
            }
        }
        else
        {
            Debug.LogWarning($"[Debuggers] P{index} teleport refs missing.");
        }
    }

    // ----------------- ALLOW FLIP -----------------

    private void FlipDebug()
    {
        TryAllowMoveBackward(p1AllowFlipKey, p1CartControl, 1);
        TryAllowMoveBackward(p2AllowFlipKey, p2CartControl, 2);
        TryAllowMoveBackward(p3AllowFlipKey, p3CartControl, 3);
        TryAllowMoveBackward(p4AllowFlipKey, p4CartControl, 4);
    }

    private void TryAllowMoveBackward(KeyCode key, CartControlScript control, int index)
    {
        if (!Input.GetKeyDown(key)) return;

        if (control != null)
        {
            control.AllowMoveBackward();
            Debug.Log($"[Debuggers] P{index} allowed to move backward.");
        }
        else
        {
            Debug.LogWarning($"[Debuggers] p{index}CartControl not assigned.");
        }
    }

    // ----------------- ADD EMPTY CART (EVENT) -----------------

    private void AddEmptyCartDebug()
    {
        TryRaiseAddCart(p1AddCartKey, p1AddEmptyCartEvent, 1);
        TryRaiseAddCart(p2AddCartKey, p2AddEmptyCartEvent, 2);
        TryRaiseAddCart(p3AddCartKey, p3AddEmptyCartEvent, 3);
        TryRaiseAddCart(p4AddCartKey, p4AddEmptyCartEvent, 4);
    }

    private void TryRaiseAddCart(KeyCode key, GameEvent evt, int index)
    {
        if (!Input.GetKeyDown(key)) return;

        if (evt != null)
        {
            evt.Raise();
            Debug.Log($"[Debuggers] Raised P{index} AddEmptyCart event.");
        }
        else
        {
            Debug.LogWarning($"[Debuggers] P{index} AddEmptyCart event not assigned.");
        }
    }
}