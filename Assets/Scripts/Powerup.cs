using UnityEngine;

public enum PowerupType
{
    BreakWall,
    Teleport,
    Freeze,
    Jump,
    Dash
}

[System.Serializable]
public class Powerup // Just a data type class
{
    public PowerupType Type;
    public string Name;
    public Sprite Icon;

    [Tooltip("Only used by the Freeze power-up.")]
    public int FreezeDuration;

    // The copy constructor no longer needs to copy 'Uses'
    public Powerup(Powerup original)
    {
        this.Type = original.Type;
        this.Name = original.Name;
        this.Icon = original.Icon;
        this.FreezeDuration = original.FreezeDuration;
    }
}