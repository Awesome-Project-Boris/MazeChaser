using UnityEngine;
using System.Collections.Generic;

public class MazeSpawner : MonoBehaviour
{
    // --- Public Fields for Unity Inspector ---
    public enum MazeGenerationAlgorithm { PureRecursive, RecursiveTree, RandomTree, OldestTree, RecursiveDivision, }
    public MazeGenerationAlgorithm Algorithm = MazeGenerationAlgorithm.PureRecursive;
    public bool FullRandom;
    public int RandomSeed;
    public int Rows;
    public int Columns;
    public float CellWidth;
    public float CellHeight;
    public bool AddGaps;

    [Header("Prefabs")]
    public GameObject Floor = null;
    public GameObject Wall = null;
    public GameObject Pillar = null;
    public GameObject PlayerPrefab = null;
    public GameObject EnemyPrefab = null;
    public GameObject EndGoalPrefab = null;

    // --- Public Properties ---
    public BasicMazeGenerator MazeGenerator { get; private set; }

    // --- Private Fields ---
    private Dictionary<Vector2Int, TileInfo> tileInfos = new Dictionary<Vector2Int, TileInfo>();

    public void BeginSpawning()
    {
        // Set the random seed if not using a fully random maze
        if (!FullRandom)
        {
            Random.seed = RandomSeed;
        }
        switch (Algorithm)
        {
            case MazeGenerationAlgorithm.PureRecursive:
                MazeGenerator = new RecursiveMazeGenerator(Rows, Columns);
                break;
            case MazeGenerationAlgorithm.RecursiveTree:
                MazeGenerator = new RecursiveTreeMazeGenerator(Rows, Columns);
                break;
            case MazeGenerationAlgorithm.RandomTree:
                MazeGenerator = new RandomTreeMazeGenerator(Rows, Columns);
                break;
            case MazeGenerationAlgorithm.OldestTree:
                MazeGenerator = new OldestTreeMazeGenerator(Rows, Columns);
                break;
            case MazeGenerationAlgorithm.RecursiveDivision:
                MazeGenerator = new DivisionMazeGenerator(Rows, Columns);
                break;
        }
        MazeGenerator.GenerateMaze();

        // --- PASS 1: Instantiate all Floor Tiles ---
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                float x = column * (CellWidth + (AddGaps ? 0.2f : 0));
                float z = row * (CellHeight + (AddGaps ? 0.2f : 0));

                GameObject tmp_floor = Instantiate(Floor, new Vector3(x, 0, z), Quaternion.Euler(0, 0, 0));
                tmp_floor.transform.parent = transform;

                TileInfo currentTileInfo = tmp_floor.GetComponent<TileInfo>();
                if (currentTileInfo != null)
                {
                    tileInfos[new Vector2Int(column, row)] = currentTileInfo;
                }
            }
        }

        // --- PASS 2: Instantiate Walls and link them to the existing TileInfos ---
        // --- PASS 2: Instantiate Walls and link them to the existing TileInfos ---
        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                MazeCell cell = MazeGenerator.GetMazeCell(row, column);
                TileInfo currentTileInfo = tileInfos[new Vector2Int(column, row)];
                float x = column * (CellWidth + (AddGaps ? 0.2f : 0));
                float z = row * (CellHeight + (AddGaps ? 0.2f : 0));

                // This logic for WallRight is correct and should stay.
                if (cell.WallRight)
                {
                    GameObject tmp_wall = Instantiate(Wall, new Vector3(x + CellWidth / 2, 0, z) + Wall.transform.position, Quaternion.Euler(0, 90, 0));
                    tmp_wall.transform.parent = transform;

                    // This part is new from our refactoring and is correct.
                    currentTileInfo.WallObjects[Direction.Right] = tmp_wall;
                    var neighborKey = new Vector2Int(column + 1, row);
                    if (tileInfos.ContainsKey(neighborKey)) { tileInfos[neighborKey].WallObjects[Direction.Left] = tmp_wall; }
                }

                // This logic for WallFront is correct and should stay.
                if (cell.WallFront)
                {
                    GameObject tmp_wall = Instantiate(Wall, new Vector3(x, 0, z + CellHeight / 2) + Wall.transform.position, Quaternion.Euler(0, 0, 0));
                    tmp_wall.transform.parent = transform;

                    currentTileInfo.WallObjects[Direction.Front] = tmp_wall;
                    var neighborKey = new Vector2Int(column, row + 1);
                    if (tileInfos.ContainsKey(neighborKey)) { tileInfos[neighborKey].WallObjects[Direction.Back] = tmp_wall; }
                }

                if (cell.WallLeft)
                {
                    GameObject tmp_wall = Instantiate(Wall, new Vector3(x - CellWidth / 2, 0, z) + Wall.transform.position, Quaternion.Euler(0, 270, 0));
                    tmp_wall.transform.parent = transform;

                    // The linking logic for this wall is already handled by the WallRight of the neighbor,
                    // but we can add it here for completeness if needed in the future.
                    currentTileInfo.WallObjects[Direction.Left] = tmp_wall;
                }

                if (cell.WallBack)
                {
                    GameObject tmp_wall = Instantiate(Wall, new Vector3(x, 0, z - CellHeight / 2) + Wall.transform.position, Quaternion.Euler(0, 180, 0));
                    tmp_wall.transform.parent = transform;

                    currentTileInfo.WallObjects[Direction.Back] = tmp_wall;
                }
            }
        }

        // --- 3. Instantiate Pillars (Optional) ---
        if (Pillar != null)
        {
            for (int row = 0; row < Rows + 1; row++)
            {
                for (int column = 0; column < Columns + 1; column++)
                {
                    float x = column * (CellWidth + (AddGaps ? 0.2f : 0));
                    float z = row * (CellHeight + (AddGaps ? 0.2f : 0));
                    // CORRECTED INSTANTIATION
                    GameObject tmp_pillar = Instantiate(Pillar, new Vector3(x - CellWidth / 2, 0, z - CellHeight / 2), Quaternion.identity);
                    tmp_pillar.transform.parent = transform;
                }
            }
        }

        // --- 4. Strategically Place and Instantiate Entities ---
        Vector2Int goalPosition = new Vector2Int(Columns - 1, Rows - 1);
        MazeGenerator.GetMazeCell(goalPosition.y, goalPosition.x).IsGoal = true;
        Vector2Int playerStartPos = MazePathfinder.FindFurthestCell(goalPosition.y, goalPosition.x, MazeGenerator);
        Vector2Int enemyStartPos = FindBestEnemySpawn(playerStartPos, goalPosition);

        System.Func<int, int, Vector3> GetWorldPos = (row, col) => {
            return new Vector3(col * (CellWidth + (AddGaps ? 0.2f : 0)), 1, row * (CellHeight + (AddGaps ? 0.2f : 0)));
        };

        // --- 5. Spawn Player, AI, and Goal ---
        if (EndGoalPrefab != null) { Instantiate(EndGoalPrefab, GetWorldPos(goalPosition.y, goalPosition.x), Quaternion.identity, transform); }
        GameObject playerObj = Instantiate(PlayerPrefab, GetWorldPos(playerStartPos.y, playerStartPos.x), Quaternion.identity, transform);
        GameObject aiObj = Instantiate(EnemyPrefab, GetWorldPos(enemyStartPos.y, enemyStartPos.x), Quaternion.identity, transform);

        if (playerObj != null && aiObj != null)
        {
            GameManager.Instance.InitializeGame(playerObj.GetComponent<PlayerController>(), aiObj.GetComponent<AIController>());
        }
    }

    // This is the enemy placement logic we settled on, encapsulated in its own method for clarity.
    private Vector2Int FindBestEnemySpawn(Vector2Int playerPos, Vector2Int goalPos)
    {
        var playerDistanceMap = MazePathfinder.CalculateAllDistances(playerPos.y, playerPos.x, MazeGenerator);
        var goalDistanceMap = MazePathfinder.CalculateAllDistances(goalPos.y, goalPos.x, MazeGenerator);
        var playerToGoalPath = MazePathfinder.FindShortestPath(playerPos.y, playerPos.x, goalPos.y, goalPos.x, MazeGenerator);
        var playerToGoalPathLookup = new HashSet<Vector2Int>(playerToGoalPath);

        List<Vector2Int> candidateCells = new List<Vector2Int>();
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Columns; c++)
            {
                var currentPos = new Vector2Int(c, r);
                if (playerDistanceMap.ContainsKey(currentPos) && goalDistanceMap.ContainsKey(currentPos) && !playerToGoalPathLookup.Contains(currentPos) &&
                    playerDistanceMap[currentPos] >= 10 && goalDistanceMap[currentPos] >= 8)
                {
                    candidateCells.Add(currentPos);
                }
            }
        }

        if (candidateCells.Count > 0)
        {
            return candidateCells[Random.Range(0, candidateCells.Count)];
        }
        // Fallback if no candidates are found
        return MazePathfinder.FindFurthestCell(playerPos.y, playerPos.x, MazeGenerator);
    }

    public TileInfo GetTileInfo(int row, int col)
    {
        var key = new Vector2Int(col, row);
        return tileInfos.ContainsKey(key) ? tileInfos[key] : null;
    }

    // Add this method back into your MazeSpawner.cs script.
    public GameObject GetFloorTile(int row, int col)
    {
        // This method relies on the 'tileInfos' dictionary and the 'TileInfo' component.
        // It gets the TileInfo for a grid position and returns its GameObject.
        TileInfo tileInfo = GetTileInfo(row, col);
        if (tileInfo != null)
        {
            return tileInfo.gameObject;
        }
        return null;
    }
}