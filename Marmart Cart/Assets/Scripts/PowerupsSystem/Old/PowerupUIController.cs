using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerupUIController : MonoBehaviour
{
    [SerializeField] private PowerupsManager powerupManager;
    [SerializeField] private TextMeshProUGUI powerupText;
    [SerializeField] private TextMeshProUGUI powerupTextForEmpty;

    [Header("Powerup Icons")]
    [SerializeField] private GameObject noPowerupIcon;
    [SerializeField] private GameObject boostIcon;
    [SerializeField] private GameObject scaleIcon;
    [SerializeField] private GameObject projectileIcon;
    [SerializeField] private GameObject tomatoIcon;
    [SerializeField] private GameObject grabberIcon;

    void Update()
    {
        if (powerupManager == null)
            return;

        var current = powerupManager.GetCurrentPowerup();

        // Hide all icons before showing the active one
        SetAllIconsActive(false);

        string displayName = "";
        powerupTextForEmpty.text = "NO POWERUP!" + "\nLOOK FOR BASKETS";

        if (current != null)
        {
            switch (current)
            {
                case PowerupsManager.PowerupType.Boost:
                    displayName = "CHARGE AND DESTORY!";
                    powerupTextForEmpty.text = "";
                    SetIconActive(boostIcon, true);
                    break;

                case PowerupsManager.PowerupType.Scale:
                    displayName = "GROW THE CARTS";
                    powerupTextForEmpty.text = "";
                    SetIconActive(scaleIcon, true);
                    break;

                case PowerupsManager.PowerupType.BowlingBall:
                    displayName = "BOWLING STRIKE!";
                    powerupTextForEmpty.text = "";
                    SetIconActive(projectileIcon, true);
                    break;

                case PowerupsManager.PowerupType.Tomato:
                    displayName = "THROW A TOMATO";
                    powerupTextForEmpty.text = "";
                    SetIconActive(tomatoIcon, true);
                    break;

                case PowerupsManager.PowerupType.Grabber:
                    displayName = "GRABBER ARM";
                    powerupTextForEmpty.text = "";
                    SetIconActive(grabberIcon, true);
                    break;

                default:
                    displayName = "";
                    powerupTextForEmpty.text = "NO POWERUP!" +"\nLOOK FOR BASKETS";
                    SetIconActive(noPowerupIcon, true);
                    break;
            }
        }
        else
        {
            // Explicit no-powerup state
            SetIconActive(noPowerupIcon, true);
        }

        if (powerupText != null)
            powerupText.text = displayName;
    }

    private void SetAllIconsActive(bool state)
    {
        if (noPowerupIcon != null) noPowerupIcon.gameObject.SetActive(state);
        if (boostIcon != null) boostIcon.gameObject.SetActive(state);
        if (scaleIcon != null) scaleIcon.gameObject.SetActive(state);
        if (projectileIcon != null) projectileIcon.gameObject.SetActive(state);
        if (tomatoIcon != null) tomatoIcon.gameObject.SetActive(state);
        if (grabberIcon != null) grabberIcon.gameObject.SetActive(state);
    }

    private void SetIconActive(GameObject icon, bool state)
    {
        if (icon != null)
            icon.gameObject.SetActive(state);
    }
}