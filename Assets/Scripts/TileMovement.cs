using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TileMovement : MonoBehaviour
{
    public float MoveSpeed = 5f;

    private MazeSpawner mazeSpawner;
    private Vector2Int currentPosition;
    private bool isMoving = false;

    void Start()
    {
        mazeSpawner = FindObjectOfType<MazeSpawner>();
        currentPosition = WorldToGrid(transform.position);
    }

    public bool IsMoving()
    {
        return isMoving;
    }

    public void AttemptMove(Vector2Int direction, System.Action<bool> onMoveAttempted)
    {
        if (isMoving)
        {
            onMoveAttempted?.Invoke(false); // Report that the move did not start
            return;
        }

        if (IsValidMove(direction))
        {
            // When we start the coroutine, we provide a callback that will end the turn.
            StartCoroutine(MoveToTile(currentPosition + direction, () => {
            }));
            onMoveAttempted?.Invoke(true); // Report that the move successfully started
        }
        else
        {
            onMoveAttempted?.Invoke(false); // Report that the move did not start
        }
    }

    private bool IsValidMove(Vector2Int direction)
    {
        MazeCell currentCell = mazeSpawner.MazeGenerator.GetMazeCell(currentPosition.y, currentPosition.x);
        Vector2Int targetPosition = currentPosition + direction;

        if (targetPosition.x < 0 || targetPosition.x >= mazeSpawner.Columns ||
            targetPosition.y < 0 || targetPosition.y >= mazeSpawner.Rows)
        {
            return false;
        }

        MazeCell targetCell = mazeSpawner.MazeGenerator.GetMazeCell(targetPosition.y, targetPosition.x);

        if (direction == Vector2Int.up && !currentCell.WallFront && !targetCell.WallBack) return true;
        if (direction == Vector2Int.down && !currentCell.WallBack && !targetCell.WallFront) return true;
        if (direction == Vector2Int.right && !currentCell.WallRight && !targetCell.WallLeft) return true;
        if (direction == Vector2Int.left && !currentCell.WallLeft && !targetCell.WallRight) return true;

        return false;
    }

    private IEnumerator MoveToTile(Vector2Int targetGridPos, System.Action onMoveComplete)
    {
        isMoving = true;
        Vector3 targetWorldPos = GridToWorld(targetGridPos);

        while (Vector3.Distance(transform.position, targetWorldPos) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, MoveSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = targetWorldPos;
        currentPosition = targetGridPos;
        isMoving = false;

        onMoveComplete?.Invoke();
    }

    private Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(gridPos.x * (mazeSpawner.CellWidth + (mazeSpawner.AddGaps ? 0.2f : 0)), 1, gridPos.y * (mazeSpawner.CellHeight + (mazeSpawner.AddGaps ? 0.2f : 0)));
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        int col = Mathf.RoundToInt(worldPos.x / (mazeSpawner.CellWidth + (mazeSpawner.AddGaps ? 0.2f : 0)));
        int row = Mathf.RoundToInt(worldPos.z / (mazeSpawner.CellHeight + (mazeSpawner.AddGaps ? 0.2f : 0)));
        return new Vector2Int(col, row);
    }

    public Vector2Int GetCurrentGridPosition()
    {
        return currentPosition;
    }

    public void TeleportToTile(Vector2Int targetGridPos)
    {
        // Update the logical position
        currentPosition = targetGridPos;
        // Update the visual position instantly
        transform.position = GridToWorld(targetGridPos);
    }

    // This method takes a list of tiles and moves the character along them sequentially.
    public void ExecuteDash(List<Vector2Int> path, System.Action onDashComplete)
    {
        StartCoroutine(DashCoroutine(path, onDashComplete));
    }

    private IEnumerator DashCoroutine(List<Vector2Int> path, System.Action onDashComplete)
    {
        isMoving = true;

        // Loop through each "waypoint" in the provided path.
        foreach (var waypoint in path)
        {
            Vector3 targetWorldPos = GridToWorld(waypoint);
            // Use the existing smooth movement logic to go to the next waypoint.
            while (Vector3.Distance(transform.position, targetWorldPos) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, MoveSpeed * Time.deltaTime * 2f); // Move a bit faster for a "dash" feel
                yield return null;
            }
            // Ensure we end exactly on the tile.
            transform.position = targetWorldPos;
            currentPosition = waypoint;
        }

        isMoving = false;
        // When the entire path is complete, call the onComplete action.
        onDashComplete?.Invoke();
    }

    public void StopAllMovement()
    {
        StopAllCoroutines();
        isMoving = false;
    }
}