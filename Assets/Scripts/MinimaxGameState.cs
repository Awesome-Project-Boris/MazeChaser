using UnityEngine;
using System.Collections.Generic;


/// A snapshot of all relevant information for the Minimax algorithm to evaluate a possible future.
/// This class is designed to be cloned and modified during simulation without affecting the live game.


public class MinimaxGameState
{
    // Character States

    public Vector2Int AIPos { get; set; }
    public Vector2Int PlayerPos { get; set; }
    public int AITurnsFrozen { get; set; }
    public int PlayerTurnsFrozen { get; set; }

    // Inventories

    public List<Powerup> AIPowerups { get; set; }
    public List<Powerup> PlayerPowerups { get; set; }

    // Maze State

    public MazeCell[,] MazeGrid { get; set; }
    public int MazeRows { get; set; }
    public int MazeColumns { get; set; }


    /// Creates a deep copy of this game state for simulation.
    /// Essential for allowing the Minimax algorithm to explore different futures.

    public MinimaxGameState Clone()
    {
        // Create new lists for powerups to avoid modifying the original inventories.

        var aiPowerupsCopy = new List<Powerup>();
        foreach (var p in this.AIPowerups) { aiPowerupsCopy.Add(new Powerup(p)); }

        var playerPowerupsCopy = new List<Powerup>();
        foreach (var p in this.PlayerPowerups) { playerPowerupsCopy.Add(new Powerup(p)); }

        // Create a deep copy of the maze grid. This is crucial for simulating wall breaks.

        MazeCell[,] mazeGridCopy = new MazeCell[MazeRows, MazeColumns];
        for (int r = 0; r < MazeRows; r++)
        {
            for (int c = 0; c < MazeColumns; c++)
            {
                mazeGridCopy[r, c] = new MazeCell(this.MazeGrid[r, c]);
            }
        }

        return new MinimaxGameState
        {
            AIPos = this.AIPos,
            PlayerPos = this.PlayerPos,
            AITurnsFrozen = this.AITurnsFrozen,
            PlayerTurnsFrozen = this.PlayerTurnsFrozen,
            AIPowerups = aiPowerupsCopy,
            PlayerPowerups = playerPowerupsCopy,
            MazeGrid = mazeGridCopy,
            MazeRows = this.MazeRows,
            MazeColumns = this.MazeColumns
        };
    }
}