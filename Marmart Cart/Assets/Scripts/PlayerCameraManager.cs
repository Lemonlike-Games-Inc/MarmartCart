using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Serialization;

public class PlayerCameraManager : MonoBehaviour
{
    [Header("Player identity")]
    [Tooltip("1-based player number (P1..P4). P3/P4 always use the 4P checkout camera set.")]
    [Range(1, 4)]
    [SerializeField] private int playerIndex = 1;

    [Header("Gameplay Camera")]
    [SerializeField] private CinemachineCamera followCamera;

    [Header("2P Layout - Checkout Cameras (P1/P2 only)")]
    [Tooltip("Leave this complete set empty on the P3 and P4 camera managers.")]
    [SerializeField] private CinemachineCamera twoPlayerCheckoutCameraLane1;
    [SerializeField] private CinemachineCamera twoPlayerCheckoutCameraLane2;
    [SerializeField] private CinemachineCamera twoPlayerCheckoutCameraLane3;
    [SerializeField] private CinemachineCamera twoPlayerCheckoutCameraLane4;

    [Header("4P Layout - Checkout Cameras (P1..P4)")]
    [SerializeField] private CinemachineCamera fourPlayerCheckoutCameraLane1;
    [SerializeField] private CinemachineCamera fourPlayerCheckoutCameraLane2;
    [SerializeField] private CinemachineCamera fourPlayerCheckoutCameraLane3;
    [SerializeField] private CinemachineCamera fourPlayerCheckoutCameraLane4;

    // Preserve the two references authored before the layout-specific camera
    // sets existed. P1/P2 migrate them into the 2P set; P3/P4 migrate them into
    // the 4P set. They remain hidden so existing scene/prefab data is not lost.
    [FormerlySerializedAs("checkoutCameraLane1")]
    [SerializeField, HideInInspector] private CinemachineCamera legacyCheckoutCameraLane1;

    [FormerlySerializedAs("checkoutCameraLane2")]
    [SerializeField, HideInInspector] private CinemachineCamera legacyCheckoutCameraLane2;

    [Header("Priority settings")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int idlePriority = 10;

    private CinemachineCamera _current;

    private void Awake()
    {
        MigrateLegacyCheckoutCameras();
    }

    private void Start()
    {
        // Make startup deterministic even if a checkout camera was accidentally
        // saved with an active priority in the Inspector.
        SetAllCheckoutCameraPriorities(idlePriority);
        SetActiveCamera(followCamera);
    }

    // -------- Gameplay follow setup --------

    public void SetFollowTarget(Transform target)
    {
        if (followCamera != null)
        {
            followCamera.Follow = target;
            // If you use LookAt on this camera:
            // followCamera.LookAt = target;
        }
    }

    // -------- Checkout entry/exit --------

    public void EnterCheckoutLane(int laneIndex)
    {
        if (laneIndex < 1 || laneIndex > 4)
        {
            Debug.LogWarning($"[PlayerCameraManager P{playerIndex}] Invalid lane index {laneIndex}. Expected 1..4.", this);
            return;
        }

        int activePlayerCount = GetActivePlayerCount();
        bool useFourPlayerLayout = activePlayerCount > 2;
        CinemachineCamera targetCam = GetCheckoutCamera(laneIndex, useFourPlayerLayout);

        if (targetCam == null)
        {
            string layoutName = useFourPlayerLayout ? "4P" : "2P";
            Debug.LogWarning(
                $"[PlayerCameraManager P{playerIndex}] {layoutName} checkout camera for lane {laneIndex} is not assigned.",
                this
            );
            return;
        }

        SetActiveCamera(targetCam);
    }

    public void ExitCheckout()
    {
        if (followCamera == null)
        {
            Debug.LogWarning($"[PlayerCameraManager P{playerIndex}] Follow camera not assigned.");
            return;
        }

        SetActiveCamera(followCamera);
    }

    // -------- Core: priority switching for THIS player only --------

    private int GetActivePlayerCount()
    {
        if (GMode.Instance != null)
        {
            return GMode.Instance.PlayerCount();
        }

        // Direct-scene fallback: P3/P4 cannot exist in a 2P layout. P1/P2 keep
        // the historical 2P fallback used elsewhere in the match scene.
        return playerIndex >= 3 ? 4 : 2;
    }

    private CinemachineCamera GetCheckoutCamera(int laneIndex, bool useFourPlayerLayout)
    {
        if (useFourPlayerLayout)
        {
            return laneIndex switch
            {
                1 => fourPlayerCheckoutCameraLane1,
                2 => fourPlayerCheckoutCameraLane2,
                3 => fourPlayerCheckoutCameraLane3,
                4 => fourPlayerCheckoutCameraLane4,
                _ => null
            };
        }

        return laneIndex switch
        {
            1 => twoPlayerCheckoutCameraLane1,
            2 => twoPlayerCheckoutCameraLane2,
            3 => twoPlayerCheckoutCameraLane3,
            4 => twoPlayerCheckoutCameraLane4,
            _ => null
        };
    }

    private void SetAllCheckoutCameraPriorities(int priority)
    {
        SetCameraPriority(twoPlayerCheckoutCameraLane1, priority);
        SetCameraPriority(twoPlayerCheckoutCameraLane2, priority);
        SetCameraPriority(twoPlayerCheckoutCameraLane3, priority);
        SetCameraPriority(twoPlayerCheckoutCameraLane4, priority);

        SetCameraPriority(fourPlayerCheckoutCameraLane1, priority);
        SetCameraPriority(fourPlayerCheckoutCameraLane2, priority);
        SetCameraPriority(fourPlayerCheckoutCameraLane3, priority);
        SetCameraPriority(fourPlayerCheckoutCameraLane4, priority);
    }

    private static void SetCameraPriority(CinemachineCamera camera, int priority)
    {
        if (camera != null) camera.Priority = priority;
    }

    private void MigrateLegacyCheckoutCameras()
    {
        if (playerIndex <= 2)
        {
            if (twoPlayerCheckoutCameraLane1 == null)
                twoPlayerCheckoutCameraLane1 = legacyCheckoutCameraLane1;

            if (twoPlayerCheckoutCameraLane2 == null)
                twoPlayerCheckoutCameraLane2 = legacyCheckoutCameraLane2;
        }
        else
        {
            if (fourPlayerCheckoutCameraLane1 == null)
                fourPlayerCheckoutCameraLane1 = legacyCheckoutCameraLane1;

            if (fourPlayerCheckoutCameraLane2 == null)
                fourPlayerCheckoutCameraLane2 = legacyCheckoutCameraLane2;
        }
    }

    private void OnValidate()
    {
        playerIndex = Mathf.Clamp(playerIndex, 1, 4);
        MigrateLegacyCheckoutCameras();
    }

    private void SetActiveCamera(CinemachineCamera cam)
    {
        if (cam == null) return;

        // raise new cam
        cam.Priority = activePriority;

        // lower old cam
        if (_current != null && _current != cam)
            _current.Priority = idlePriority;

        _current = cam;
    }
}
