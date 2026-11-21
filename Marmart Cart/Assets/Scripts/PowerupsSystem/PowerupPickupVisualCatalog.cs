using System;
using UnityEngine;

/// <summary>
/// Which world-space visual a map pickup shows: one per item it can give,
/// plus one for Random Pool pickups. Gameplay colliders remain on
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
        [Tooltip("The item this visual shows. Ignored on the Random Pickup Visual.")]
        [SerializeField] private ItemDefinition item;

        [Tooltip(
            "Visual-only world prefab. Do not put the gameplay pickup " +
            "trigger or PowerupPickup component in this prefab."
        )]
        [SerializeField] private GameObject visualPrefab;

        [Header("Socket Offset")]
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale;

        public ItemDefinition Item => item;
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

    [Tooltip("One visual per item a map pickup can give.")]
    [SerializeField] private Entry[] itemVisuals = Array.Empty<Entry>();

    [Tooltip("Shown by Random Pool pickups, whatever item they will give.")]
    [SerializeField] private Entry randomPickupVisual;

    public bool TryGetItemVisual(ItemDefinition item, out Entry entry)
    {
        if (item != null && itemVisuals != null)
        {
            for (int i = 0; i < itemVisuals.Length; i++)
            {
                if (itemVisuals[i].Item != item ||
                    itemVisuals[i].VisualPrefab == null)
                {
                    continue;
                }

                entry = itemVisuals[i];
                return true;
            }
        }

        entry = default;
        return false;
    }

    public bool TryGetRandomVisual(out Entry entry)
    {
        entry = randomPickupVisual;
        return entry.VisualPrefab != null;
    }

    private void OnValidate()
    {
        if (itemVisuals != null)
        {
            for (int i = 0; i < itemVisuals.Length; i++)
            {
                Entry entry = itemVisuals[i];
                entry.Sanitize();
                itemVisuals[i] = entry;
            }
        }

        randomPickupVisual.Sanitize();
    }
}
