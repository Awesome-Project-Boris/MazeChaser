using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(TileMovement))]
[RequireComponent(typeof(MinimaxController))]

public class AIController : MonoBehaviour
{
    private List<GameObject> highlightedPathTiles = new List<GameObject>(); // Main path to consider
    private List<GameObject> highlightedStrategicPathTiles = new List<GameObject>(); // Alternative wildcard path to consider

    private Dictionary<GameObject, Color> originalTileColors = new Dictionary<GameObject, Color>(); // Original color of tiles  

    public int TurnsFrozen { get; set; } = 0;

    [Header("AI Configuration")]
    [Tooltip("How many turns ahead the AI will think. 2 is a good starting point.")]
    [Range(0, 4)]
    public int AIDepth = 2;

    private TileMovement tileMovement;
    private MinimaxController minimaxController;
    private PlayerController player;
    private MazeSpawner mazeSpawner;
    private PowerupManager powerupManager;

    public void Initialize()
    {
        tileMovement = GetComponent<TileMovement>();
        minimaxController = GetComponent<MinimaxController>();

        // Getting component references

        player = GameManager.Instance.player;
        mazeSpawner = FindObjectOfType<MazeSpawner>();
        powerupManager = PowerupManager.Instance;
    }

    public void TakeTurn()
    {
        if (tileMovement.GetCurrentGridPosition() == player.GetComponent<TileMovement>().GetCurrentGridPosition())
        {
            Debug.Log("CAUGHT! The AI has moved onto the player's tile.");
            GameManager.Instance.EndGame(false); // Player loses
            return;
        }

        ClearAllHighlights();

        if (TurnsFrozen > 0)
        {
            Debug.Log("[AI] Turn skipped (frozen).");
            TurnsFrozen--;
            ClearPreviousPath(); // Clear the path if frozen
            GameManager.Instance.EndAITurn();
            return;
        }

        // --- VISUALIZER CALL ---

        // At the start of the turn, this shows the path the AI is considering.

        vvar chasePath = MazePathfinder.FindShortestPath(
            tileMovement.GetCurrentGridPosition().y, tileMovement.GetCurrentGridPosition().x,
            player.GetComponent<TileMovement>().GetCurrentGridPosition().y, player.GetComponent<TileMovement>().GetCurrentGridPosition().x,
            mazeSpawner.MazeGenerator
        );
        // Draw the main chase path in blue.
        VisualizePath(chasePath, new Color(0.9f, 0.9f, 1.0f));


        // 1. We figure out how's the game going and the parameters for Minimax's condsideration

        MinimaxGameState currentState = new MinimaxGameState
        {
            AIPos = tileMovement.GetCurrentGridPosition(),
            PlayerPos = player.GetComponent<TileMovement>().GetCurrentGridPosition(),
            AITurnsFrozen = this.TurnsFrozen,
            PlayerTurnsFrozen = player.TurnsFrozen,
            AIPowerups = new List<Powerup>(powerupManager.AIPowerups),
            PlayerPowerups = new List<Powerup>(powerupManager.PlayerPowerups),
            MazeGrid = mazeSpawner.MazeGenerator.MazeGrid,
            MazeRows = mazeSpawner.Rows,
            MazeColumns = mazeSpawner.Columns
        };

        // 2. We ask the Minimax brain for the best action.

        AIAction bestAction = minimaxController.GetBestAction(currentState, AIDepth);

        // 2.5. If we don't get a suitable "best" action, we just tell the AI to move down the path to the player.

        if (bestAction == null)
        {
            Debug.LogWarning("[AI] Minimax returned no best action. Using fallback: move towards player.");
            var path = MazePathfinder.FindShortestPath(currentState.AIPos.y, currentState.AIPos.x, currentState.PlayerPos.y, currentState.PlayerPos.x, mazeSpawner.MazeGenerator);
            if (path != null && path.Count > 1)
            {
                // Create a simple move action from the path
                bestAction = new AIAction
                {
                    Type = ActionType.Move,
                    MoveDirection = GetDirectionFromVector(path[1] - currentState.AIPos)
                };
            }
            else
            {
                // If there's truly no action and no path, end the turn.
                Debug.LogError("[AI] Fallback failed: No path to player. AI is trapped. Ending turn.");
                GameManager.Instance.EndAITurn();
                return;
            }
        }

         Color boldColor = new Color(0.6f, 0.6f, 1.0f);
 Color dimColor = new Color(0.9f, 0.9f, 1.0f);

 // Determine the final, chosen path based on the AI's action.
 List<Vector2Int> finalPath;
 if (bestAction.Type == ActionType.UsePowerup && (bestAction.PowerupType == PowerupType.BreakWall || bestAction.PowerupType == PowerupType.Jump))
 {
     // The best action was strategic, so the "final path" is the one AFTER the power-up.
     var stateAfterAction = minimaxController.GetStateAfterAction(currentState, bestAction, true);
     finalPath = minimaxController.FindShortestPathSimulated(stateAfterAction.AIPos, stateAfterAction.PlayerPos, stateAfterAction);

     // In this case, we also want to show the "alternative" dumb path.
     var alternativePath = MazePathfinder.FindShortestPath(
         currentState.AIPos.y, currentState.AIPos.x,
         currentState.PlayerPos.y, currentState.PlayerPos.x,
         mazeSpawner.MazeGenerator
     );
     // Draw the alternative path first with the dim color.
     VisualizePath(alternativePath, dimColor);
 }
 else
 {
     // The best action was a simple move, so the final path is the direct chase path.
     finalPath = MazePathfinder.FindShortestPath(
         currentState.AIPos.y, currentState.AIPos.x,
         currentState.PlayerPos.y, currentState.PlayerPos.x,
         mazeSpawner.MazeGenerator
     );
 }

 // Draw the final, chosen path with the BOLD color.
 // The VisualizePath logic will automatically prevent it from overwriting tiles
 // that were already colored by the dim "alternative" path if they overlap.
 // To ensure the main path is always prominent, we will draw it last.
 
 VisualizePath(finalPath, boldColor);

        Debug.Log($"[AI DECISION]: {bestAction}");

        if (bestAction.Type == ActionType.UsePowerup && (bestAction.PowerupType == PowerupType.BreakWall || bestAction.PowerupType == PowerupType.Jump))
        {
            var stateAfterAction = minimaxController.GetStateAfterAction(currentState, bestAction, true);
            var strategicPath = minimaxController.FindShortestPathSimulated(stateAfterAction.AIPos, stateAfterAction.PlayerPos, stateAfterAction);
            // Draw the strategic path in a different color, on top of the blue path.
            VisualizePath(strategicPath, new Color(0.6f, 0.6f, 1.0f));
        }

        if (bestAction.Type == ActionType.UsePowerup && (bestAction.PowerupType == PowerupType.BreakWall || bestAction.PowerupType == PowerupType.Jump))
        {
            // Create a temporary clone of the maze to simulate the wall break
            var stateAfterAction = minimaxController.GetStateAfterAction(currentState, bestAction, true);
            var strategicPath = minimaxController.FindShortestPathSimulated(stateAfterAction.AIPos, stateAfterAction.PlayerPos, stateAfterAction);
            VisualizeStrategicPath(strategicPath);
        }

        if (bestAction.Type == ActionType.UsePowerup)
        {
            string powerupName = System.Text.RegularExpressions.Regex.Replace(bestAction.PowerupType.ToString(), "(\\B[A-Z])", " $1"); // We get the name of the powerup being used
            string chatMessage = $"Agent used {powerupName} at turn {GameManager.Instance.GetTurnNumber()}!";
            UIManager.Instance.AddToChatHistory(chatMessage);
        }

        // 3. The Agent executes the move

        if (bestAction.Type == ActionType.Move)
        {
            tileMovement.AttemptMove(GetVectorFromDirection(bestAction.MoveDirection), (success) =>
            {
                GameManager.Instance.EndAITurn();
            });
        }
        else if (bestAction.Type == ActionType.UsePowerup)
        {
            ExecutePowerupAction(bestAction);
            GameManager.Instance.EndAITurn();
        }
    }



    private void ExecutePowerupAction(AIAction action)
    {
        // Use a direct reference to the AI's inventory
        var aiInventory = powerupManager.AIPowerups;

        switch (action.PowerupType)
        {
            case PowerupType.BreakWall:
                powerupManager.ExecuteBreakWall(aiInventory, action.PowerupSlot, tileMovement.GetCurrentGridPosition(), action.PowerupTargetDirection);
                break;
            case PowerupType.Jump:
                powerupManager.ExecuteJump(aiInventory, action.PowerupSlot, tileMovement.GetCurrentGridPosition(), action.PowerupTargetDirection, this);
                break;
            case PowerupType.Dash:
                // Dash requires a callback to end the turn after the animation.
                List<Vector2Int> dashPath = powerupManager.CalculateDashPath(tileMovement.GetCurrentGridPosition(), action.PowerupTargetDirection);
                aiInventory.RemoveAt(action.PowerupSlot); // Consume powerup
                UIManager.Instance.UpdatePowerupDisplay(powerupManager.PlayerPowerups, powerupManager.AIPowerups);
                tileMovement.ExecuteDash(dashPath, () => { GameManager.Instance.EndAITurn(); });
                return; // Return early because the callback will end the turn.
            case PowerupType.Freeze:
                powerupManager.ExecuteFreeze(aiInventory, action.PowerupSlot, player);
                break;
            case PowerupType.Teleport:
                powerupManager.ExecuteTeleport(aiInventory, action.PowerupSlot, player, this);
                break;
        }
    }

    private Vector2Int GetVectorFromDirection(Direction dir)
    {
        switch (dir)
        {
            case Direction.Front: return Vector2Int.up;
            case Direction.Back: return Vector2Int.down;
            case Direction.Right: return Vector2Int.right;
            case Direction.Left: return Vector2Int.left;
            default: return Vector2Int.zero;
        }
    }

    private Direction GetDirectionFromVector(Vector2Int dir)
    {
        if (dir == Vector2Int.up) return Direction.Front;
        if (dir == Vector2Int.down) return Direction.Back;
        if (dir == Vector2Int.right) return Direction.Right;
        if (dir == Vector2Int.left) return Direction.Left;
        return Direction.Start;
    }


    // Path debugging


// In AIController.cs

    private void ClearPreviousPath()
    {
        foreach (var tile in highlightedPathTiles)
        {
            // SAFETY CHECK: Only try to revert the color if the tile still exists
            // and we have its original color stored in our dictionary.
            if (tile != null && originalTileColors.ContainsKey(tile))
            {
                tile.GetComponent<Renderer>().material.color = originalTileColors[tile];
            }
        }
        foreach (var tile in highlightedStrategicPathTiles)
        {
            // Add the same safety check here for the strategic path.
            if (tile != null && originalTileColors.ContainsKey(tile))
            {
                tile.GetComponent<Renderer>().material.color = originalTileColors[tile];
            }
        }

        highlightedPathTiles.Clear();
        highlightedStrategicPathTiles.Clear();
        
    }

    
private void ClearAllHighlights()
{
    foreach (var entry in originalTileColors)
    {
        // Key is the GameObject, Value is the original Color
        if (entry.Key != null) 
        {
            entry.Key.GetComponent<Renderer>().material.color = entry.Value;
        }
    }
    // After reverting all colors, we clear the dictionary for the next turn.
    originalTileColors.Clear();
}

private void VisualizePath(List<Vector2Int> path, Color highlightColor)
{
    if (path == null) return;

    foreach (var pos in path)
    {
        GameObject tileObject = mazeSpawner.GetFloorTile(pos.y, pos.x);
        if (tileObject != null)
        {
            var tileRenderer = tileObject.GetComponent<Renderer>();
            if (tileRenderer != null)
            {
                // IMPORTANT: Only store the tile's color if it's the first time
                // we are highlighting it this turn.
                if (!originalTileColors.ContainsKey(tileObject))
                {
                    originalTileColors.Add(tileObject, tileRenderer.material.color);
                }
                // Apply the highlight color.
                tileRenderer.material.color = highlightColor;
            }
        }
    }
}

void OnTriggerEnter(Collider other)
    {
        // Check if the object we touched has a PlayerController script on it.
        if (other.GetComponent<PlayerController>() != null)
        {
            Debug.Log("CAUGHT! The AI has collided with the player.");
            // Tell the GameManager the player lost.
            GameManager.Instance.EndGame(false);
        }
    }
}