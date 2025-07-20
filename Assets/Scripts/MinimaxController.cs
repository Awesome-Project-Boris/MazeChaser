using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AIController))]
public class MinimaxController : MonoBehaviour
{
    // --- We will add the main Minimax logic here in later steps ---


    // --- STEP 3: GENERATE ALL POSSIBLE ACTIONS ---

    /// <summary>
    /// Looks at a game state and generates a complete list of all possible actions
    /// for the currently active character (AI or Player).
    /// </summary>
    /// <param name="state">The game state to analyze.</param>
    /// <param name="isAITurn">True if we are generating moves for the AI, false for the Player.</param>
    /// <returns>A list of all valid AIAction objects.</returns>
    private List<AIAction> GetPossibleActions(MinimaxGameState state, bool isAITurn)
    {
        var actions = new List<AIAction>();
        var characterPos = isAITurn ? state.AIPos : state.PlayerPos;
        var powerups = isAITurn ? state.AIPowerups : state.PlayerPowerups;

        if ((isAITurn && state.AITurnsFrozen > 0) || (!isAITurn && state.PlayerTurnsFrozen > 0))
        {
            return actions;
        }

        // --- A helper to perform the strict two-way wall check ---
        System.Func<Direction, bool> isPathClear = (dir) => {
            Vector2Int neighborPos = characterPos + GetVectorFromDirection(dir);
            if (neighborPos.x < 0 || neighborPos.x >= state.MazeColumns || neighborPos.y < 0 || neighborPos.y >= state.MazeRows)
                return false; // Out of bounds

            MazeCell currentCell = state.MazeGrid[characterPos.y, characterPos.x];
            MazeCell neighborCell = state.MazeGrid[neighborPos.y, neighborPos.x];

            if (dir == Direction.Front && !currentCell.WallFront && !neighborCell.WallBack) return true;
            if (dir == Direction.Back && !currentCell.WallBack && !neighborCell.WallFront) return true;
            if (dir == Direction.Right && !currentCell.WallRight && !neighborCell.WallLeft) return true;
            if (dir == Direction.Left && !currentCell.WallLeft && !neighborCell.WallRight) return true;

            return false;
        };

        // --- ACTION TYPE 1: Standard Moves ---
        if (isPathClear(Direction.Front)) actions.Add(new AIAction { Type = ActionType.Move, MoveDirection = Direction.Front });
        if (isPathClear(Direction.Back)) actions.Add(new AIAction { Type = ActionType.Move, MoveDirection = Direction.Back });
        if (isPathClear(Direction.Right)) actions.Add(new AIAction { Type = ActionType.Move, MoveDirection = Direction.Right });
        if (isPathClear(Direction.Left)) actions.Add(new AIAction { Type = ActionType.Move, MoveDirection = Direction.Left });

        // --- ACTION TYPE 2: Power-Up Usage ---
        for (int i = 0; i < powerups.Count; i++)
        {
            Powerup powerup = powerups[i];

            System.Action<Direction> addDirectionalPowerup = (dir) => {
                Vector2Int targetPos = characterPos + GetVectorFromDirection(dir);
                if (targetPos.x < 0 || targetPos.x >= state.MazeColumns || targetPos.y < 0 || targetPos.y >= state.MazeRows)
                    return; // Target is out of bounds

                bool wallExists = !isPathClear(dir);

                if (powerup.Type == PowerupType.BreakWall && wallExists)
                {
                    actions.Add(new AIAction { Type = ActionType.UsePowerup, PowerupSlot = i, PowerupType = powerup.Type, PowerupTargetDirection = dir });
                }
                else if (powerup.Type == PowerupType.Jump && wallExists)
                {
                    actions.Add(new AIAction { Type = ActionType.UsePowerup, PowerupSlot = i, PowerupType = powerup.Type, PowerupTargetDirection = dir });
                }
                else if (powerup.Type == PowerupType.Dash) // Dash can be used on open paths
                {
                    actions.Add(new AIAction { Type = ActionType.UsePowerup, PowerupSlot = i, PowerupType = powerup.Type, PowerupTargetDirection = dir });
                }
            };

            switch (powerup.Type)
            {
                case PowerupType.BreakWall:
                case PowerupType.Jump:
                case PowerupType.Dash:
                    addDirectionalPowerup(Direction.Front);
                    addDirectionalPowerup(Direction.Back);
                    addDirectionalPowerup(Direction.Right);
                    addDirectionalPowerup(Direction.Left);
                    break;

                case PowerupType.Freeze:
                case PowerupType.Teleport:
                    actions.Add(new AIAction { Type = ActionType.UsePowerup, PowerupSlot = i, PowerupType = powerup.Type });
                    break;
            }
        }
        return actions;
    }

    /// <summary>
    /// The simulation engine. Takes a state and an action, and returns the resulting new state.
    /// </summary>
    public MinimaxGameState GetStateAfterAction(MinimaxGameState previousState, AIAction action, bool isAITurn)
    {
        // Start by creating a deep copy of the state. This is our sandbox to play in.
        MinimaxGameState newState = previousState.Clone();

        // Identify who is acting and who is the opponent
        var actingPowerups = isAITurn ? newState.AIPowerups : newState.PlayerPowerups;

        if (action.Type == ActionType.Move)
        {
            Vector2Int newPos = (isAITurn ? newState.AIPos : newState.PlayerPos) + GetVectorFromDirection(action.MoveDirection);
            if (isAITurn) newState.AIPos = newPos;
            else newState.PlayerPos = newPos;
        }
        else if (action.Type == ActionType.UsePowerup)
        {
            // The powerup is consumed
            Powerup usedPowerup = actingPowerups[action.PowerupSlot];
            actingPowerups.RemoveAt(action.PowerupSlot);

            switch (action.PowerupType)
            {
                case PowerupType.Jump:
                    Vector2Int jumpPos = (isAITurn ? newState.AIPos : newState.PlayerPos) + GetVectorFromDirection(action.PowerupTargetDirection);
                    if (isAITurn) newState.AIPos = jumpPos;
                    else newState.PlayerPos = jumpPos;
                    break;

                case PowerupType.Freeze:
                    if (isAITurn) newState.PlayerTurnsFrozen = usedPowerup.FreezeDuration;
                    else newState.AITurnsFrozen = usedPowerup.FreezeDuration;
                    break;

                case PowerupType.BreakWall:
                    Vector2Int breakPos = isAITurn ? newState.AIPos : newState.PlayerPos;
                    SimulateWallBreak(newState, breakPos, action.PowerupTargetDirection);
                    break;

                case PowerupType.Teleport:
                    Vector2Int goalPos = new Vector2Int(newState.MazeColumns - 1, newState.MazeRows - 1);
                    // For a deterministic simulation, we assume Teleport sends the opponent
                    // to the worst possible spot for them (furthest from the goal).
                    Vector2Int newTeleportPos = FindFurthestCellSimulated(goalPos, newState);

                    if (isAITurn) newState.PlayerPos = newTeleportPos; // AI teleports Player
                    else newState.AIPos = newTeleportPos; // Player teleports AI
                    break;

                case PowerupType.Dash:
                    Vector2Int startPos = isAITurn ? newState.AIPos : newState.PlayerPos;
                    List<Vector2Int> path = CalculateDashPathSimulated(startPos, action.PowerupTargetDirection, newState);
                    Vector2Int endPos = (path.Count > 0) ? path.Last() : startPos;

                    if (isAITurn) newState.AIPos = endPos;
                    else newState.PlayerPos = endPos;
                    break;
            }
        }

        // Return the modified state, representing the future.
        return newState;
    }

    // --- Helper methods for simulation ---

    private void SimulateWallBreak(MinimaxGameState state, Vector2Int pos, Direction dir)
    {
        Vector2Int neighborPos = pos;
        Direction oppositeDir = Direction.Start;

        switch (dir)
        {
            case Direction.Front: neighborPos.y++; oppositeDir = Direction.Back; break;
            case Direction.Back: neighborPos.y--; oppositeDir = Direction.Front; break;
            case Direction.Right: neighborPos.x++; oppositeDir = Direction.Left; break;
            case Direction.Left: neighborPos.x--; oppositeDir = Direction.Right; break;
        }

        // Check bounds
        if (neighborPos.x < 0 || neighborPos.x >= state.MazeColumns || neighborPos.y < 0 || neighborPos.y >= state.MazeRows) return;

        // Update walls in the cloned maze grid
        state.MazeGrid[pos.y, pos.x].SetWall(dir, false);
        state.MazeGrid[neighborPos.y, neighborPos.x].SetWall(oppositeDir, false);
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

    // ADD THE FOLLOWING THREE HELPER METHODS FOR SIMULATING COMPLEX POWERUPS
    // Note: These are simplified versions of your MazePathfinder logic that work on our GameState object.

    // In MinimaxController.cs

    public List<Vector2Int> FindShortestPathSimulated(Vector2Int startNode, Vector2Int endNode, MinimaxGameState state)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int?> visited = new Dictionary<Vector2Int, Vector2Int?>();

        if (startNode == endNode) return new List<Vector2Int> { startNode };

        queue.Enqueue(startNode);
        visited[startNode] = null;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            if (currentNode == endNode) break;

            MazeCell currentCell = state.MazeGrid[currentNode.y, currentNode.x];

            // Helper to perform a strict two-way check within the simulated state
            System.Action<Direction> checkNeighbor = (dir) => {
                Vector2Int neighborNode = currentNode + GetVectorFromDirection(dir);

                // Check bounds first
                if (neighborNode.x < 0 || neighborNode.x >= state.MazeColumns || neighborNode.y < 0 || neighborNode.y >= state.MazeRows) return;

                MazeCell neighborCell = state.MazeGrid[neighborNode.y, neighborNode.x];
                bool isPathClear = false;

                if (dir == Direction.Front && !currentCell.WallFront && !neighborCell.WallBack) isPathClear = true;
                else if (dir == Direction.Back && !currentCell.WallBack && !neighborCell.WallFront) isPathClear = true;
                else if (dir == Direction.Right && !currentCell.WallRight && !neighborCell.WallLeft) isPathClear = true;
                else if (dir == Direction.Left && !currentCell.WallLeft && !neighborCell.WallRight) isPathClear = true;

                if (isPathClear && !visited.ContainsKey(neighborNode))
                {
                    visited[neighborNode] = currentNode;
                    queue.Enqueue(neighborNode);
                }
            };

            checkNeighbor(Direction.Front);
            checkNeighbor(Direction.Back);
            checkNeighbor(Direction.Right);
            checkNeighbor(Direction.Left);
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int? pathNode = endNode;
        while (pathNode != null && visited.ContainsKey(pathNode.Value))
        {
            path.Add(pathNode.Value);
            pathNode = visited[pathNode.Value];
        }
        path.Reverse();

        return (path.Count > 0 && path[0] == startNode) ? path : null;
    }

    private Vector2Int FindFurthestCellSimulated(Vector2Int startNode, MinimaxGameState state)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        queue.Enqueue(startNode);
        visited.Add(startNode);
        Vector2Int furthestNode = startNode;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            furthestNode = currentNode;
            MazeCell currentCell = state.MazeGrid[currentNode.y, currentNode.x];

            // Check neighbors (abbreviated for clarity)
            Vector2Int[] neighbors = new Vector2Int[] {
                new Vector2Int(currentNode.x + 1, currentNode.y), new Vector2Int(currentNode.x - 1, currentNode.y),
                new Vector2Int(currentNode.x, currentNode.y + 1), new Vector2Int(currentNode.x, currentNode.y - 1)
            };
            if (!currentCell.WallRight && !visited.Contains(neighbors[0])) { visited.Add(neighbors[0]); queue.Enqueue(neighbors[0]); }
            if (!currentCell.WallLeft && !visited.Contains(neighbors[1])) { visited.Add(neighbors[1]); queue.Enqueue(neighbors[1]); }
            if (!currentCell.WallFront && !visited.Contains(neighbors[2])) { visited.Add(neighbors[2]); queue.Enqueue(neighbors[2]); }
            if (!currentCell.WallBack && !visited.Contains(neighbors[3])) { visited.Add(neighbors[3]); queue.Enqueue(neighbors[3]); }
        }
        return furthestNode;
    }

    /// <summary>
    /// The AI's "brain". Analyzes a game state and returns a score.
    /// Higher scores are better for the AI.
    /// </summary>

    // In MinimaxController.cs

    // In MinimaxController.cs

    private float EvaluateState(MinimaxGameState state)
    {
        Vector2Int goalPos = new Vector2Int(state.MazeColumns - 1, state.MazeRows - 1);

        // --- Terminal State Check (Game Over Conditions are Absolute) ---
        if (state.AIPos == state.PlayerPos) return 10000f;
        if (state.PlayerPos == goalPos) return -10000f;
        if (state.AIPos == goalPos) return -5000f;

        // --- Path Calculations ---
        int aiPathToPlayer = FindShortestPathSimulated(state.AIPos, state.PlayerPos, state)?.Count ?? 999;
        int playerPathToGoal = FindShortestPathSimulated(state.PlayerPos, goalPos, state)?.Count ?? 999;

        // --- Core Chase Scoring ---
        float finalScore = -aiPathToPlayer * 10f;

        // --- NEW: URGENCY HEURISTIC ---
        // If the player is very close to winning, the AI must panic.
        if (playerPathToGoal < 5)
        {
            // The penalty increases exponentially the closer the player gets.
            // 4 steps away = -100 penalty. 1 step away = -400 penalty.
            // This will force the AI to prioritize stopping the player above all else.
            float urgencyPenalty = (5 - playerPathToGoal) * 100f;
            finalScore -= urgencyPenalty;
        }

        // --- Strategic Modifiers for Power-ups ---
        if (state.PlayerTurnsFrozen > 0)
        {
            finalScore += 80f;
        }
        if (state.AITurnsFrozen > 0)
        {
            finalScore -= 80f;
        }

        return finalScore;
    }



    // --- PUBLIC ENTRY POINT ---

    /// <summary>
    /// The main entry point for the AI. It sets up and starts the Minimax evaluation.
    /// </summary>
    /// <returns>The single best action for the AI to take right now.</returns>

    // In MinimaxController.cs


    public AIAction GetBestAction(MinimaxGameState currentState, int depth)
    {
        AIAction bestAction = null;
        float bestScore = float.MinValue;

        // --- Check for an immediate win before any complex thinking ---
        // (This logic remains the same)
        foreach (AIAction immediateAction in GetPossibleActions(currentState, true))
        {
            if (immediateAction.Type == ActionType.Move)
            {
                if (currentState.AIPos + GetVectorFromDirection(immediateAction.MoveDirection) == currentState.PlayerPos)
                {
                    Debug.Log("[AI STRATEGY]: Found an immediate kill shot. Overriding Minimax.");
                    immediateAction.Score = 10000f;
                    return immediateAction;
                }
            }
        }

        // --- Setup for Tie-breaker and NEW Willingness Factor ---
        List<Vector2Int> idealPath = FindShortestPathSimulated(currentState.AIPos, currentState.PlayerPos, currentState);
        Vector2Int idealNextStep = (idealPath != null && idealPath.Count > 1) ? idealPath[1] : currentState.AIPos;

        // --- NEW WILLINGNESS CALCULATION ---
        Vector2Int goalPos = new Vector2Int(currentState.MazeColumns - 1, currentState.MazeRows - 1);
        int playerPathToGoal = FindShortestPathSimulated(currentState.PlayerPos, goalPos, currentState)?.Count ?? 999;

        // The AI's willingness to use power-ups scales from 20% to 100% based on the player's proximity to the goal.
        float willingness = Mathf.InverseLerp(30f, 5f, playerPathToGoal); // Ranges from 0.0 (far) to 1.0 (close)
        float willingnessModifier = 0.2f + (willingness * 0.8f); // Scales from a base of 0.2 up to 1.0

        // --- Main Evaluation Loop ---
        foreach (AIAction action in GetPossibleActions(currentState, true))
        {
            MinimaxGameState newState = GetStateAfterAction(currentState, action, true);
            float score = Minimax(newState, depth - 1, false);
            float strategicBonus = 0f;

            if (action.Type == ActionType.UsePowerup)
            {
                // ... (Strategic Bonus Calculation is the same as before) ...
                switch (action.PowerupType)
                {
                    case PowerupType.BreakWall:
                    case PowerupType.Jump:
                        int pathLengthBefore = idealPath?.Count ?? 999;
                        int pathLengthAfter = FindShortestPathSimulated(newState.AIPos, newState.PlayerPos, newState)?.Count ?? 999;
                        int pathImprovement = pathLengthBefore - pathLengthAfter;
                        if (pathImprovement >= 4) { strategicBonus = pathImprovement * 15f; }
                        break;
                    case PowerupType.Teleport:
                        int playerPathToGoalBefore = playerPathToGoal;
                        int playerPathToGoalAfter = FindShortestPathSimulated(newState.PlayerPos, goalPos, newState)?.Count ?? 999;
                        int defensiveImprovement = playerPathToGoalAfter - playerPathToGoalBefore;
                        if (defensiveImprovement > 0) { strategicBonus = defensiveImprovement * 10f; }
                        break;
                    case PowerupType.Freeze:
                        strategicBonus = 45f;
                        break;
                }

                // --- APPLY THE WILLINGNESS MODIFIER ---
                // The calculated bonus is now scaled by the AI's current "willingness" to act.
                score += strategicBonus * willingnessModifier;
            }

            // Apply tie-breaker for moves
            if (action.Type == ActionType.Move && (currentState.AIPos + GetVectorFromDirection(action.MoveDirection) == idealNextStep))
            {
                score += 0.01f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                action.Score = score;
                bestAction = action;
            }
        }
        return bestAction;
    }

    // --- The Core Recursive Algorithm ---
    private float Minimax(MinimaxGameState state, int depth, bool isMaximizingPlayer)
    {
        // --- Base Case: If we've reached max depth or the game is over, return the state's value ---
        if (depth == 0)
        {
            return EvaluateState(state);
        }
        // A quick check to see if the game ended prematurely
        float terminalScore = EvaluateState(state);
        if (Mathf.Abs(terminalScore) >= 1000f)
        {
            return terminalScore;
        }

        List<AIAction> possibleActions = GetPossibleActions(state, isMaximizingPlayer);
        if (possibleActions.Count == 0)
        {
            return EvaluateState(state);
        }

        // --- Recursive Step ---
        if (isMaximizingPlayer) // The AI's turn
        {
            float bestScore = float.MinValue;
            foreach (var action in possibleActions)
            {
                MinimaxGameState newState = GetStateAfterAction(state, action, true);
                float score = Minimax(newState, depth - 1, false);
                bestScore = Mathf.Max(bestScore, score);
            }
            return bestScore;
        }
        else // The Player's turn
        {
            float bestScore = float.MaxValue;
            foreach (var action in possibleActions)
            {
                MinimaxGameState newState = GetStateAfterAction(state, action, false);
                float score = Minimax(newState, depth - 1, true);
                bestScore = Mathf.Min(bestScore, score);
            }
            return bestScore;
        }
    }

    private List<Vector2Int> CalculateDashPathSimulated(Vector2Int startPos, Direction direction, MinimaxGameState state)
    {
        var path = new List<Vector2Int>();
        var currentPos = startPos;

        // Loop for a maximum distance to prevent infinite loops in odd cases
        for (int i = 0; i < Mathf.Max(state.MazeRows, state.MazeColumns); i++)
        {
            Vector2Int neighborPos = currentPos + GetVectorFromDirection(direction);

            // Stop if the next tile is out of bounds
            if (neighborPos.x < 0 || neighborPos.x >= state.MazeColumns || neighborPos.y < 0 || neighborPos.y >= state.MazeRows)
            {
                break;
            }

            // Perform the same strict, two-way wall check
            MazeCell currentCell = state.MazeGrid[currentPos.y, currentPos.x];
            MazeCell neighborCell = state.MazeGrid[neighborPos.y, neighborPos.x];
            bool isPathClear = false;

            if (direction == Direction.Front && !currentCell.WallFront && !neighborCell.WallBack) isPathClear = true;
            else if (direction == Direction.Back && !currentCell.WallBack && !neighborCell.WallFront) isPathClear = true;
            else if (direction == Direction.Right && !currentCell.WallRight && !neighborCell.WallLeft) isPathClear = true;
            else if (direction == Direction.Left && !currentCell.WallLeft && !neighborCell.WallRight) isPathClear = true;

            // If a wall is in the way, the dash stops.
            if (!isPathClear)
            {
                break;
            }

            // Otherwise, add the tile to the path and continue from the new position.
            path.Add(neighborPos);
            currentPos = neighborPos;
        }
        return path;
    }
}