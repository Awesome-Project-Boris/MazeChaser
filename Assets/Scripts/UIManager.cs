using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{

    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject MainMenuPanel; 
    public GameObject EndgamePanel; 


    [Header("UI Sprites")]
    public Sprite EmptySlotSprite;
    public TextMeshProUGUI ChatText;
    public TextMeshProUGUI NotificationText;
    public TextMeshProUGUI EndSubtitleText;


    [Header("Power-up UI Slots")]
    public List<PowerupSlotUI> PlayerPowerupSlots;
    public List<PowerupSlotUI> AIPowerupSlots;


    private MazeSpawner mazeSpawner;
    private List<GameObject> highlightedObjects = new List<GameObject>();

    private List<string> chatHistory = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate UIManager found and destroyed.", this.gameObject);
            Destroy(this);
        }
        else
        {
            Instance = this;
            Debug.Log("UIManager Instance successfully set.", this.gameObject);
        }
    }

    private void Start()
    {
        mazeSpawner = FindFirstObjectByType<MazeSpawner>();
        if (mazeSpawner == null) Debug.LogError("[UIManager] MazeSpawner not found!");
        if (ChatText != null) ChatText.text = "";

        // Show/hide the main menu based on our static variable
        if (MainMenuPanel != null)
        {
            MainMenuPanel.SetActive(GameManager.showMainMenuOnLoad);
        }
        if (EndgamePanel != null)
        {
            EndgamePanel.SetActive(false); // Always start with the end panel hidden
        }

    }

    public void OnStartButtonPressed()
    {
        if (MainMenuPanel != null) MainMenuPanel.SetActive(false);
        GameManager.showMainMenuOnLoad = false; // Don't show on reset
        GameManager.Instance.StartGame();
    }

    public void ShowEndGameScreen(string message)
    {
        if (EndgamePanel != null)
        {
            EndgamePanel.SetActive(true);
            if (EndSubtitleText != null)
            {
                EndSubtitleText.text = message;
            }
        }
    }

    public void OnRestartButtonPressed()
    {
        // Restarting the game should not show the main menu
        GameManager.showMainMenuOnLoad = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnMainMenuButtonPressed()
    {
        // Going to the main menu should show it
        GameManager.showMainMenuOnLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Highlighting method for Break Wall 
    public void HighlightBreakableWalls(Vector2Int playerGridPos)
    {

        ClearHighlights();
        CheckAndHighlightWall(playerGridPos, Direction.Front);
        CheckAndHighlightWall(playerGridPos, Direction.Back);
        CheckAndHighlightWall(playerGridPos, Direction.Right);
        CheckAndHighlightWall(playerGridPos, Direction.Left);
    }

    //  Highlighting method for Jump 
    public void HighlightJumpableTiles(Vector2Int playerGridPos)
    {
        Debug.Log($"[UIManager] Starting to highlight JUMP targets from player position: {playerGridPos}");
        ClearHighlights();
        CheckAndHighlightJump(playerGridPos, Direction.Front, Vector2Int.up);
        CheckAndHighlightJump(playerGridPos, Direction.Back, Vector2Int.down);
        CheckAndHighlightJump(playerGridPos, Direction.Right, Vector2Int.right);
        CheckAndHighlightJump(playerGridPos, Direction.Left, Vector2Int.left);
    }

    // Highlighting method for Dash 
    public void HighlightDashPaths(Vector2Int playerGridPos)
    {
        ClearHighlights();
        var pathUp = PowerupManager.Instance.CalculateDashPath(playerGridPos, Direction.Front);
        var pathDown = PowerupManager.Instance.CalculateDashPath(playerGridPos, Direction.Back);
        var pathRight = PowerupManager.Instance.CalculateDashPath(playerGridPos, Direction.Right);
        var pathLeft = PowerupManager.Instance.CalculateDashPath(playerGridPos, Direction.Left);

        foreach (var tilePos in pathUp) { HighlightTile(tilePos); }
        foreach (var tilePos in pathDown) { HighlightTile(tilePos); }
        foreach (var tilePos in pathRight) { HighlightTile(tilePos); }
        foreach (var tilePos in pathLeft) { HighlightTile(tilePos); }
    }

    private void HighlightTile(Vector2Int gridPos)
    {
        GameObject floorTile = mazeSpawner.GetFloorTile(gridPos.y, gridPos.x);
        if (floorTile != null)
        {
            highlightedObjects.Add(floorTile);
            StartCoroutine(FlashObject(floorTile));
        }
    }

    private void CheckAndHighlightWall(Vector2Int pos, Direction dir)
    {
        // Determine the next position based on direction
        Vector2Int nextPos = pos;
        if (dir == Direction.Front) nextPos.y++;
        else if (dir == Direction.Back) nextPos.y--;
        else if (dir == Direction.Right) nextPos.x++;
        else if (dir == Direction.Left) nextPos.x--;

        // Check if the target is out of bounds (this also handles border walls)
        if (nextPos.y < 0 || nextPos.y >= mazeSpawner.Rows ||
            nextPos.x < 0 || nextPos.x >= mazeSpawner.Columns)
        {
            return;
        }

        // Two-Way check for wall data 
        MazeCell cell1 = mazeSpawner.MazeGenerator.GetMazeCell(pos.y, pos.x);
        MazeCell cell2 = mazeSpawner.MazeGenerator.GetMazeCell(nextPos.y, nextPos.x);

        bool wallInTheWay = false;
        if (dir == Direction.Front && (cell1.WallFront || cell2.WallBack)) { wallInTheWay = true; }
        else if (dir == Direction.Back && (cell1.WallBack || cell2.WallFront)) { wallInTheWay = true; }
        else if (dir == Direction.Right && (cell1.WallRight || cell2.WallLeft)) { wallInTheWay = true; }
        else if (dir == Direction.Left && (cell1.WallLeft || cell2.WallRight)) { wallInTheWay = true; }

        if (wallInTheWay)
        {
            // Getting the wall object
            GameObject wallObj = null;
            TileInfo tileInfo1 = mazeSpawner.GetTileInfo(pos.y, pos.x);
            TileInfo tileInfo2 = mazeSpawner.GetTileInfo(nextPos.y, nextPos.x);

            // Check if the first tile has the wall reference
            if (tileInfo1 != null && tileInfo1.WallObjects.ContainsKey(dir))
            {
                wallObj = tileInfo1.WallObjects[dir];
            }
            // If not, check if the neighboring tile has the wall reference from its perspective
            else if (tileInfo2 != null)
            {
                Direction oppositeDir = Direction.Start;
                if (dir == Direction.Front) oppositeDir = Direction.Back;
                else if (dir == Direction.Back) oppositeDir = Direction.Front;
                else if (dir == Direction.Right) oppositeDir = Direction.Left;
                else if (dir == Direction.Left) oppositeDir = Direction.Right;

                if (tileInfo2.WallObjects.ContainsKey(oppositeDir))
                {
                    wallObj = tileInfo2.WallObjects[oppositeDir];
                }
            }

            // If we found the wall object from either tile, highlight it.
            if (wallObj != null)
            {
                highlightedObjects.Add(wallObj);
                StartCoroutine(FlashObject(wallObj));
            }
            else
            {
                Debug.LogWarning($"Wall data exists for direction {dir} from {pos}, but no wall GameObject was found on either adjacent TileInfo.");
            }
        }
    }

    private void CheckAndHighlightJump(Vector2Int pos, Direction dir, Vector2Int dirVector)
    {
        Debug.Log($"--- Checking JUMP target in direction: {dir} ---");
        Vector2Int targetPos = pos + dirVector;
        if (targetPos.y < 0 || targetPos.y >= mazeSpawner.Rows || targetPos.x < 0 || targetPos.x >= mazeSpawner.Columns)
        {
            Debug.Log($"-- Jump target is out of bounds. Aborting check for {dir}.");
            return;
        }

        MazeCell cell1 = mazeSpawner.MazeGenerator.GetMazeCell(pos.y, pos.x);
        MazeCell cell2 = mazeSpawner.MazeGenerator.GetMazeCell(targetPos.y, targetPos.x);

        bool wallInTheWay = false;
        if (dir == Direction.Front && (cell1.WallFront || cell2.WallBack)) wallInTheWay = true;
        else if (dir == Direction.Back && (cell1.WallBack || cell2.WallFront)) wallInTheWay = true;
        else if (dir == Direction.Right && (cell1.WallRight || cell2.WallLeft)) wallInTheWay = true;
        else if (dir == Direction.Left && (cell1.WallLeft || cell2.WallRight)) wallInTheWay = true;

        Debug.Log($"-- Is a wall detected in the logical data? -> {wallInTheWay}");

        if (wallInTheWay)
        {
            Debug.Log($"-- Attempting to get floor tile GameObject at {targetPos}...");
            GameObject floorTile = mazeSpawner.GetFloorTile(targetPos.y, targetPos.x);
            if (floorTile != null)
            {
                Debug.Log($"-- SUCCESS: Found floor tile '{floorTile.name}'. Starting highlight coroutine.");
                highlightedObjects.Add(floorTile);
                StartCoroutine(FlashObject(floorTile));
            }
            else 
            {
                Debug.LogWarning($"-- FAILURE: A wall was detected, but GetFloorTile() returned NULL for position {targetPos}.");
            }
        }
    }

    public void ClearHighlights()
    {
        StopAllCoroutines(); 
        foreach (var obj in highlightedObjects)
        {
            if (obj != null)
            {
                var objRenderer = obj.GetComponentInChildren<Renderer>();
                if (objRenderer != null && originalColors.ContainsKey(obj))
                {
                    objRenderer.material.color = originalColors[obj];
                }
            }
        }
        highlightedObjects.Clear();
        originalColors.Clear(); // Clear the dictionary for the next use.
    }

    private IEnumerator FlashObject(GameObject obj)
    {
        Renderer objRenderer = obj.GetComponentInChildren<Renderer>();
        if (objRenderer == null)
        {
            Debug.LogError($"Could not find a Renderer on object '{obj.name}' or its children. Cannot highlight.", obj);
            yield break;
        }

        // Store the original color BEFORE we start flashing
        if (!originalColors.ContainsKey(obj))
        {
            originalColors[obj] = objRenderer.material.color;
        }

        Color highlightColor = Color.green;

        while (true)
        {
            objRenderer.material.color = highlightColor;
            yield return new WaitForSeconds(0.3f);
            // We get the original color from our dictionary.
            objRenderer.material.color = originalColors[obj];
            yield return new WaitForSeconds(0.3f);
        }
    }

    public void UpdatePowerupDisplay(List<Powerup> playerPowerups, List<Powerup> aiPowerups)
    {
        if (EmptySlotSprite == null) { Debug.LogError("EmptySlotSprite not assigned in UIManager!"); return; }
        for (int i = 0; i < PlayerPowerupSlots.Count; i++)
        {
            if (i < playerPowerups.Count) { PlayerPowerupSlots[i].DisplayPowerup(playerPowerups[i]); }
            else { PlayerPowerupSlots[i].ClearSlot(EmptySlotSprite); }
        }
        for (int i = 0; i < AIPowerupSlots.Count; i++)
        {
            if (i < aiPowerups.Count) { AIPowerupSlots[i].DisplayPowerup(aiPowerups[i]); }
            else { AIPowerupSlots[i].ClearSlot(EmptySlotSprite); }
        }
    }

    public void ShowNotification(string message, float duration) { StartCoroutine(NotificationCoroutine(message, duration)); }
    private IEnumerator NotificationCoroutine(string message, float duration)
    {
        NotificationText.text = message;
        NotificationText.enabled = true;
        yield return new WaitForSeconds(duration);
        NotificationText.enabled = false;
    }

    public void AddToChatHistory(string message)
    {
        if (ChatText == null) return;

        // Add the new message to the top of the list
        chatHistory.Insert(0, message);

        // If the list is too long, remove the oldest message
        if (chatHistory.Count > 3)
        {
            chatHistory.RemoveAt(3);
        }

        // Update the UI text display
        ChatText.text = string.Join("\n", chatHistory);
    }
}