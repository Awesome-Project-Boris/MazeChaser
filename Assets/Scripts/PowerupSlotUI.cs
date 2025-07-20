using UnityEngine;
using UnityEngine.UI;
// No longer needs TMPro since we removed the uses text

public class PowerupSlotUI : MonoBehaviour
{
    // These are now private. Only this script needs to know about them.
    private Image powerupIcon;

    // The Awake method runs when the object is first created.
    private void Awake()
    {
        // Get the Image component on this GameObject automatically.
        powerupIcon = GetComponent<Image>();
        if (powerupIcon == null)
        {
            Debug.LogError("PowerupSlotUI could not find an Image component on this GameObject!");
        }
    }

    // This method is now 'internal'. Only scripts within the same assembly (like our UIManager) can call it.
    internal void DisplayPowerup(Powerup powerup)
    {
        if (powerup == null || powerup.Icon == null)
        {
            // If the powerup or its icon is null, disable the image.
            powerupIcon.enabled = false;
            return;
        }

        powerupIcon.enabled = true;
        powerupIcon.sprite = powerup.Icon;
    }

    // This method is also 'internal' and now correctly displays the empty sprite.
    internal void ClearSlot(Sprite emptySprite)
    {
        powerupIcon.enabled = true;
        powerupIcon.sprite = emptySprite;
    }
}