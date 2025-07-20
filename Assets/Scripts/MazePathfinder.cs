using System.Collections.Generic;
using UnityEngine;


// We're using BFS to find the optimal placements for enemy and player.

public static class MazePathfinder
{
    public static Vector2Int FindFurthestCell(int startRow, int startCol, BasicMazeGenerator generator)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int?> visited = new Dictionary<Vector2Int, Vector2Int?>();
        Vector2Int startNode = new Vector2Int(startCol, startRow);
        queue.Enqueue(startNode);
        visited[startNode] = null;
        Vector2Int furthestNode = startNode;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            furthestNode = currentNode;
            MazeCell currentCell = generator.GetMazeCell(currentNode.y, currentNode.x);

            // Check neighbors
            if (!currentCell.WallRight && !visited.ContainsKey(new Vector2Int(currentNode.x + 1, currentNode.y)))
            {
                Vector2Int neighbor = new Vector2Int(currentNode.x + 1, currentNode.y);
                visited[neighbor] = currentNode;
                queue.Enqueue(neighbor);
            }
            if (!currentCell.WallLeft && !visited.ContainsKey(new Vector2Int(currentNode.x - 1, currentNode.y)))
            {
                Vector2Int neighbor = new Vector2Int(currentNode.x - 1, currentNode.y);
                visited[neighbor] = currentNode;
                queue.Enqueue(neighbor);
            }
            if (!currentCell.WallFront && !visited.ContainsKey(new Vector2Int(currentNode.x, currentNode.y + 1)))
            {
                Vector2Int neighbor = new Vector2Int(currentNode.x, currentNode.y + 1);
                visited[neighbor] = currentNode;
                queue.Enqueue(neighbor);
            }
            if (!currentCell.WallBack && !visited.ContainsKey(new Vector2Int(currentNode.x, currentNode.y - 1)))
            {
                Vector2Int neighbor = new Vector2Int(currentNode.x, currentNode.y - 1);
                visited[neighbor] = currentNode;
                queue.Enqueue(neighbor);
            }
        }
        return furthestNode;
    }


    // Finds the single shortest path between a start and end point.
    // It returns a List of coordinates representing the cells on the path.

    public static List<Vector2Int> FindShortestPath(int startRow, int startCol, int endRow, int endCol, BasicMazeGenerator generator)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int?> visited = new Dictionary<Vector2Int, Vector2Int?>();
        Vector2Int startNode = new Vector2Int(startCol, startRow);
        Vector2Int endNode = new Vector2Int(endCol, endRow);

        if (startNode == endNode) return new List<Vector2Int> { startNode };

        queue.Enqueue(startNode);
        visited[startNode] = null;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            if (currentNode == endNode) break;

            MazeCell currentCell = generator.GetMazeCell(currentNode.y, currentNode.x);

            // Helper to perform a strict two-way check
            System.Action<Direction> checkNeighbor = (dir) => {
                Vector2Int neighborNode;
                bool isPathClear = false;

                // Get neighbor position and check its corresponding wall
                switch (dir)
                {
                    case Direction.Front:
                        neighborNode = new Vector2Int(currentNode.x, currentNode.y + 1);
                        if (neighborNode.y < generator.RowCount && !currentCell.WallFront && !generator.GetMazeCell(neighborNode.y, neighborNode.x).WallBack) isPathClear = true;
                        break;
                    case Direction.Back:
                        neighborNode = new Vector2Int(currentNode.x, currentNode.y - 1);
                        if (neighborNode.y >= 0 && !currentCell.WallBack && !generator.GetMazeCell(neighborNode.y, neighborNode.x).WallFront) isPathClear = true;
                        break;
                    case Direction.Right:
                        neighborNode = new Vector2Int(currentNode.x + 1, currentNode.y);
                        if (neighborNode.x < generator.ColumnCount && !currentCell.WallRight && !generator.GetMazeCell(neighborNode.y, neighborNode.x).WallLeft) isPathClear = true;
                        break;
                    case Direction.Left:
                        neighborNode = new Vector2Int(currentNode.x - 1, currentNode.y);
                        if (neighborNode.x >= 0 && !currentCell.WallLeft && !generator.GetMazeCell(neighborNode.y, neighborNode.x).WallRight) isPathClear = true;
                        break;
                    default:
                        return;
                }

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

        // Backtrack from the end node to the start node to build the path.
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int? pathNode = endNode;
        while (pathNode != null && visited.ContainsKey(pathNode.Value))
        {
            path.Add(pathNode.Value);
            pathNode = visited[pathNode.Value];
        }
        path.Reverse();

        // Return the path only if it's valid (starts at the start node)
        return (path.Count > 0 && path[0] == startNode) ? path : null;
    }

    // Calculates the distance from every cell in the maze to the NEAREST cell on a given path.
    // This uses a "multi-source" BFS starting from all path cells at once.

    public static Dictionary<Vector2Int, int> CalculateDistancesFromPath(List<Vector2Int> path, BasicMazeGenerator generator)
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        // Start the search from EVERY cell on the path, with a distance of 0.

        foreach (var pathCell in path)
        {
            if (!distances.ContainsKey(pathCell))
            {
                distances[pathCell] = 0;
                queue.Enqueue(pathCell);
            }
        }

        // Standard BFS

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            MazeCell currentCell = generator.GetMazeCell(currentNode.y, currentNode.x);

            System.Action<Vector2Int> checkNeighbor = (neighborNode) => {
                if (!distances.ContainsKey(neighborNode))
                {
                    distances[neighborNode] = distances[currentNode] + 1;
                    queue.Enqueue(neighborNode);
                }
            };

            if (!currentCell.WallRight) checkNeighbor(new Vector2Int(currentNode.x + 1, currentNode.y));
            if (!currentCell.WallLeft) checkNeighbor(new Vector2Int(currentNode.x - 1, currentNode.y));
            if (!currentCell.WallFront) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y + 1));
            if (!currentCell.WallBack) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y - 1));
        }
        return distances;
    }

    // This method returns a map of distances from the start cell to all other reachable cells.

    public static Dictionary<Vector2Int, int> CalculateAllDistances(int startRow, int startCol, BasicMazeGenerator generator)
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int startNode = new Vector2Int(startCol, startRow);

        queue.Enqueue(startNode);
        distances[startNode] = 0;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            MazeCell currentCell = generator.GetMazeCell(currentNode.y, currentNode.x);

            // Check neighbors
            System.Action<Vector2Int> checkNeighbor = (neighborNode) => {
                if (!distances.ContainsKey(neighborNode))
                {
                    distances[neighborNode] = distances[currentNode] + 1;
                    queue.Enqueue(neighborNode);
                }
            };

            if (!currentCell.WallRight) checkNeighbor(new Vector2Int(currentNode.x + 1, currentNode.y));
            if (!currentCell.WallLeft) checkNeighbor(new Vector2Int(currentNode.x - 1, currentNode.y));
            if (!currentCell.WallFront) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y + 1));
            if (!currentCell.WallBack) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y - 1));
        }

        return distances;
    }

    // This is a new, overloaded version of FindShortestPath.
    // It allows us to run a path search on a temporary, modified copy of the maze data.

    public static List<Vector2Int> FindShortestPath(int startRow, int startCol, int endRow, int endCol, MazeCell[,] mazeData, int rows, int columns)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int?> visited = new Dictionary<Vector2Int, Vector2Int?>();
        Vector2Int startNode = new Vector2Int(startCol, startRow);
        Vector2Int endNode = new Vector2Int(endCol, endRow);

        queue.Enqueue(startNode);
        visited[startNode] = null;

        while (queue.Count > 0)
        {
            Vector2Int currentNode = queue.Dequeue();
            if (currentNode == endNode) break;

            // The only change is here: we access the mazeData array directly.

            MazeCell currentCell = mazeData[currentNode.y, currentNode.x];

            System.Action<Vector2Int> checkNeighbor = (neighborNode) => {
                if (neighborNode.y >= 0 && neighborNode.y < rows && neighborNode.x >= 0 && neighborNode.x < columns && !visited.ContainsKey(neighborNode))
                {
                    visited[neighborNode] = currentNode;
                    queue.Enqueue(neighborNode);
                }
            };

            if (!currentCell.WallRight) checkNeighbor(new Vector2Int(currentNode.x + 1, currentNode.y));
            if (!currentCell.WallLeft) checkNeighbor(new Vector2Int(currentNode.x - 1, currentNode.y));
            if (!currentCell.WallFront) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y + 1));
            if (!currentCell.WallBack) checkNeighbor(new Vector2Int(currentNode.x, currentNode.y - 1));
        }

        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int? pathNode = endNode;
        while (pathNode != null && visited.ContainsKey(pathNode.Value))
        {
            path.Add(pathNode.Value);
            pathNode = visited[pathNode.Value];
        }
        path.Reverse();
        return path;
    }
}