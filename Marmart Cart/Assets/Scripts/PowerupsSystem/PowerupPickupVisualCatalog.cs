using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Presentation identities available to a world-space pickup spawner.
///
/// The first four numeric values deliberately match PowerupId so existing
/// serialized catalog entries keep their assignments. RandomPowerup is a
/// presentation-only identity and is never granted to a player.
/// </summary>
public enum PowerupPickupVisualKind
{
    Tomato = 0,
    IceCube = 1,
    ColaMentos = 2,
    StrongFan = 3,
    RandomPowerup = 4
}

/// <summary>
/// Shared mapping from pickup presentation identities to the world-space
/// visual prefabs shown by PowerupSpawner. Gameplay colliders remain on
/// PowerupPickup.
/// </summary>
[CreateAssetMenu(
    menuName = "Marmart Carts/Powerups/Pickup Visual Catalog",
    fileName = "PowerupPickupVisualCatalog"
)]
public class PowerupPickupVisualCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [FormerlySerializedAs("powerup")]
        [SerializeField] private PowerupPickupVisualKind visualKind;

        [Tooltip(
            "Visual-only world prefab. Do not put the gameplay pickup " +
            "trigger or PowerupPickup component in this prefab."
        )]
        [SerializeField] private GameObject visualPrefab;

        [Header("Socket Offset")]
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale;

        public PowerupPickupVisualKind VisualKind => visualKind;
        public GameObject VisualPrefab => visualPrefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;

        public Vector3 LocalScale
        {
            get
            {
                if (localScale == Vector3.zero) return Vector3.one;

                return new Vector3(
                    Mathf.Max(0.0001f, localScale.x),
                    Mathf.Max(0.0001f, localScale.y),
                    Mathf.Max(0.0001f, localScale.z)
                );
            }
        }

        public bool IsUsableFor(PowerupPickupVisualKind requestedKind)
        {
            return visualKind == requestedKind &&
                   IsDefined(visualKind) &&
                   visualPrefab != null;
        }

        public void Sanitize()
        {
            if (localScale == Vector3.zero)
            {
                localScale = Vector3.one;
                return;
            }

            localScale.x = Mathf.Max(0.0001f, localScale.x);
            localScale.y = Mathf.Max(0.0001f, localScale.y);
            localScale.z = Mathf.Max(0.0001f, localScale.z);
        }
    }

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    public bool TryGetEntry(
        PowerupPickupVisualKind visualKind,
        out Entry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (!entries[i].IsUsableFor(visualKind)) continue;

                entry = entries[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Compatibility overload for callers that need a concrete power-up
    /// visual rather than the shared RandomPowerup presentation.
    /// </summary>
    public bool TryGetEntry(PowerupId powerupId, out Entry entry)
    {
        if (!TryGetVisualKind(powerupId, out PowerupPickupVisualKind kind))
        {
            entry = default;
            return false;
        }

        return TryGetEntry(kind, out entry);
    }

    public static bool TryGetVisualKind(
        PowerupId powerupId,
        out PowerupPickupVisualKind visualKind)
    {
        switch (powerupId)
        {
            case PowerupId.Tomato:
                visualKind = PowerupPickupVisualKind.Tomato;
                return true;

            case PowerupId.IceCube:
                visualKind = PowerupPickupVisualKind.IceCube;
                return true;

            case PowerupId.ColaMentos:
                visualKind = PowerupPickupVisualKind.ColaMentos;
                return true;

            case PowerupId.StrongFan:
                visualKind = PowerupPickupVisualKind.StrongFan;
                return true;

            default:
                visualKind = default;
                return false;
        }
    }

    public static bool IsDefined(PowerupPickupVisualKind visualKind)
    {
        return visualKind == PowerupPickupVisualKind.Tomato ||
               visualKind == PowerupPickupVisualKind.IceCube ||
               visualKind == PowerupPickupVisualKind.ColaMentos ||
               visualKind == PowerupPickupVisualKind.StrongFan ||
               visualKind == PowerupPickupVisualKind.RandomPowerup;
    }

    private void OnValidate()
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Length; i++)
        {
            Entry entry = entries[i];
            entry.Sanitize();
            entries[i] = entry;
        }
    }
}
