using UnityEngine;
using System.Collections;

public enum Direction{
	Start,
	Right,
	Front,
	Left,
	Back,
};
//<summary>
//Class for representing concrete maze cell.
//</summary>
public class MazeCell
{
    public bool IsVisited = false;
    public bool WallRight = false;
    public bool WallFront = false;
    public bool WallLeft = false;
    public bool WallBack = false;
    public bool IsGoal = false;

    // Default constructor (already exists, no changes needed)
    public MazeCell() { }

    // --- ADD THIS NEW CONSTRUCTOR ---
    // This is a "copy constructor". It creates a new MazeCell
    // instance with the same values as an existing one.
    public MazeCell(MazeCell original)
    {
        this.IsVisited = original.IsVisited;
        this.WallRight = original.WallRight;
        this.WallFront = original.WallFront;
        this.WallLeft = original.WallLeft;
        this.WallBack = original.WallBack;
        this.IsGoal = original.IsGoal;
    }

    public void SetWall(Direction direction, bool state)
    {
        switch (direction)
        {
            case Direction.Front: WallFront = state; break;
            case Direction.Back: WallBack = state; break;
            case Direction.Left: WallLeft = state; break;
            case Direction.Right: WallRight = state; break;
        }
    }
}