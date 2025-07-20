using UnityEngine;
using UnityEngine.UI;

public class PowerupSlotUI : MonoBehaviour
{
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