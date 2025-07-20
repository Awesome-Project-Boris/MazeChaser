using UnityEngine;

public enum ActionType { Move, UsePowerup }

/// Represents a single, potential action a character can take,
/// complete with all necessary information for execution and evaluation.

public class AIAction
{
    public ActionType Type { get; set; }
-
    public Direction MoveDirection { get; set; }

    //  Fields for a 'UsePowerup' action 

    public int PowerupSlot { get; set; } // The inventory index (0, 1, or 2)
    public PowerupType PowerupType { get; set; }
    public Direction PowerupTargetDirection { get; set; } // For directional powerups (BreakWall, Jump, Dash)

    public float Score { get; set; }

    // Helper for debugging to easily see what action the AI chose.

    public override string ToString()
    {
        if (Type == ActionType.Move)
        {
            return $"Action: Move {MoveDirection}, Score: {Score:F2}";
        }
        else
        {
            string target = (PowerupTargetDirection != Direction.Start && PowerupTargetDirection != 0) ? $" towards {PowerupTargetDirection}" : "";
            return $"Action: Use {PowerupType}{target} (from slot {PowerupSlot}), Score: {Score:F2}";
        }
    }
}