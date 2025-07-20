using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using System;

public class PowerupManager : MonoBehaviour
{
    // Singleton pattern for easy access
    public static PowerupManager Instance { get; private set; }

    [Header("Component References")]
    public Image PowerupIcon;

    private MazeSpawner mazeSpawner;

    private PlayerController player;

    public event Action OnMazeChanged;

    [Header("Power-up Definitions")]
    [Tooltip("Define all 5 possible power-ups here. This is the master list.")]
    public List<Powerup> AllPossiblePowerups;

    [Header("Current Inventories")]
    public List<Powerup> PlayerPowerups { get; private set; }
    public List<Powerup> AIPowerups { get; private set; }

    private int MaxPowerups = 3;


    private void Awake()
    {
        // Enforce Singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); }
        else { Instance = this; }

        mazeSpawner = FindFirstObjectByType<MazeSpawner>();
        player = GetComponent<PlayerController>();
    }

    // UI

    public void DisplayPowerup(Powerup powerup)
    {
        if (powerup == null)
        {
            PowerupIcon.enabled = false;
            return;
        }

        PowerupIcon.enabled = true;
        PowerupIcon.sprite = powerup.Icon;
    }

    // This method now displays the "Empty" sprite.
    public void ClearSlot(Sprite emptySprite)
    {
        PowerupIcon.enabled = true;
        PowerupIcon.sprite = emptySprite;
    }

    // This method is called by the GameManager at the start of the game.
    public void InitializeAndDealStartingPowerups()
    {
        PlayerPowerups = new List<Powerup>();
        AIPowerups = new List<Powerup>();

        Debug.Log("Dealing starting power-ups...");
        AwardStartingPowerups(PlayerPowerups);
        AwardStartingPowerups(AIPowerups);

        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);
    }

    // This handles the logic for dealing 3 unique, random powerups.
    private void AwardStartingPowerups(List<Powerup> targetInventory)
    {
        // Create a temporary list of all available powerups to draw from.
        List<Powerup> availablePool = new List<Powerup>(AllPossiblePowerups);

        // Award 3 unique power-ups
        for (int i = 0; i < 3; i++)
        {
            if (availablePool.Count == 0) break; // Stop if we run out of power-ups to give

            // Pick a random power-up from the pool
            int randomIndex = UnityEngine.Random.Range(0, availablePool.Count);
            Powerup chosenPowerup = availablePool[randomIndex];

            // Add a *copy* of the chosen power-up to the target's inventory.
            targetInventory.Add(new Powerup(chosenPowerup));

            // Remove it from the pool to ensure it's not picked again.
            availablePool.RemoveAt(randomIndex);
        }
    }

    // This will be called by the GameManager every 10 turns.
    public void AwardRandomPowerupTo(List<Powerup> targetInventory)
    {

        if (targetInventory.Count >= 3)
        {
            // We can add a log message for debugging purposes.
            // This is helpful to confirm the logic is working correctly.
            string inventoryOwner = (targetInventory == PlayerPowerups) ? "Player" : "AI";
            Debug.Log($"{inventoryOwner} inventory is full. No new power-up awarded.");
            return; // Exit the method early.
        }

        if (AllPossiblePowerups.Count == 0) return;

        // Pick a random power-up from the master list and add a copy to the inventory.
        Powerup randomPowerup = AllPossiblePowerups[UnityEngine.Random.Range(0, AllPossiblePowerups.Count)];
        targetInventory.Add(new Powerup(randomPowerup));

        // We will add UI update calls here later.
        Debug.Log("Awarded a random power-up!");

        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);
    }

    public bool ExecuteBreakWall(List<Powerup> userInventory, int slotIndex, Vector2Int userGridPos, Direction targetDirection)
    {
        GameObject wallObj = null;
        TileInfo currentTileInfo = mazeSpawner.GetTileInfo(userGridPos.y, userGridPos.x);

        // --- NEW: Two-Way Lookup for the Wall GameObject ---

        // First, try to find the wall reference on the current tile.
        if (currentTileInfo != null && currentTileInfo.WallObjects.ContainsKey(targetDirection))
        {
            wallObj = currentTileInfo.WallObjects[targetDirection];
        }
        // If we didn't find it, check the neighboring tile from the opposite direction.
        else
        {
            Vector2Int neighborGridPos = userGridPos;
            Direction oppositeDirection = Direction.Start;

            if (targetDirection == Direction.Front) { neighborGridPos.y++; oppositeDirection = Direction.Back; }
            else if (targetDirection == Direction.Back) { neighborGridPos.y--; oppositeDirection = Direction.Front; }
            else if (targetDirection == Direction.Right) { neighborGridPos.x++; oppositeDirection = Direction.Left; }
            else if (targetDirection == Direction.Left) { neighborGridPos.x--; oppositeDirection = Direction.Right; }

            TileInfo neighborTileInfo = mazeSpawner.GetTileInfo(neighborGridPos.y, neighborGridPos.x);
            if (neighborTileInfo != null && neighborTileInfo.WallObjects.ContainsKey(oppositeDirection))
            {
                wallObj = neighborTileInfo.WallObjects[oppositeDirection];
            }
        }


        // --- The rest of the logic proceeds only if a wall object was found ---
        if (wallObj != null)
        {
            // Get the neighbor's position and opposite direction again for updating data
            Vector2Int neighborGridPos = userGridPos;
            Direction oppositeDirection = Direction.Start;
            if (targetDirection == Direction.Front) { neighborGridPos.y++; oppositeDirection = Direction.Back; }
            else if (targetDirection == Direction.Back) { neighborGridPos.y--; oppositeDirection = Direction.Front; }
            else if (targetDirection == Direction.Right) { neighborGridPos.x++; oppositeDirection = Direction.Left; }
            else if (targetDirection == Direction.Left) { neighborGridPos.x--; oppositeDirection = Direction.Right; }

            // Update the logical maze data for BOTH cells.
            mazeSpawner.MazeGenerator.GetMazeCell(userGridPos.y, userGridPos.x).SetWall(targetDirection, false);
            mazeSpawner.MazeGenerator.GetMazeCell(neighborGridPos.y, neighborGridPos.x).SetWall(oppositeDirection, false);

            // Remove the wall reference from BOTH TileInfo components.
            currentTileInfo.WallObjects.Remove(targetDirection);
            TileInfo neighborTileInfo = mazeSpawner.GetTileInfo(neighborGridPos.y, neighborGridPos.x);
            if (neighborTileInfo != null)
            {
                neighborTileInfo.WallObjects.Remove(oppositeDirection);
            }

            // Destroy the visual GameObject and consume the power-up.
            Destroy(wallObj);
            userInventory.RemoveAt(slotIndex);
            UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);

            OnMazeChanged?.Invoke();

            return true; // Report success
        }

        // If no physical wall was found, the action fails.
        Debug.LogWarning($"Break Wall failed: No wall GameObject found in direction {targetDirection} from {userGridPos}");
        return false;
    }

    // This method handles the logic of jumping over a wall.
    public bool ExecuteJump(List<Powerup> userInventory, int slotIndex, Vector2Int userGridPos, Direction targetDirection, MonoBehaviour user)
    {
        // Get the TileMovement component from the correct user (player or AI)
        TileMovement userTileMovement = user.GetComponent<TileMovement>();

        // --- SIMPLIFIED LOGIC ---
        // We no longer need to check for walls here. We trust that if this method
        // was called, the move is valid because the UIManager already highlighted it.

        // 1. Calculate the destination tile based on the confirmed direction.
        Vector2Int directionVector = Vector2Int.zero;
        if (targetDirection == Direction.Front) { directionVector = Vector2Int.up; }
        else if (targetDirection == Direction.Back) { directionVector = Vector2Int.down; }
        else if (targetDirection == Direction.Right) { directionVector = Vector2Int.right; }
        else if (targetDirection == Direction.Left) { directionVector = Vector2Int.left; }

        // 2. Execute the move using the TeleportToTile method.
        userTileMovement.TeleportToTile(userGridPos + directionVector);

        // 3. Consume the power-up.
        Powerup powerup = userInventory[slotIndex];
        userInventory.RemoveAt(slotIndex);
        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);

        return true; // Always report success.
    }

    // This method applies the freeze effect to a target.
    public void ExecuteFreeze(List<Powerup> userInventory, int slotIndex, MonoBehaviour target)
    {
        Powerup powerup = userInventory[slotIndex];
        int freezeDuration = powerup.FreezeDuration;

        // Apply the freeze duration to the correct controller type
        if (target is PlayerController)
        {
            (target as PlayerController).TurnsFrozen = freezeDuration;
            UIManager.Instance.ShowNotification($"{(target is PlayerController ? "Player" : "AI")} frozen for {freezeDuration} turns!", 2.5f);
        }
        else if (target is AIController)
        {
            (target as AIController).TurnsFrozen = freezeDuration;
            Debug.Log($"AI has been frozen for {freezeDuration} turns.");
        }

        // Decrement uses and update UI
        userInventory.RemoveAt(slotIndex);
        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);
    }

    // This method handles the logic of teleporting a target to a random tile.
    public bool ExecuteTeleport(List<Powerup> userInventory, int slotIndex, MonoBehaviour targetToTeleport, MonoBehaviour otherCharacter)
    {
        Powerup powerup = userInventory[slotIndex];

        // --- Find a valid random tile ---
        List<Vector2Int> validTiles = new List<Vector2Int>();

        // Get the grid positions of both characters to avoid teleporting on top of someone.
        Vector2Int targetCurrentPos = targetToTeleport.GetComponent<TileMovement>().GetCurrentGridPosition();
        Vector2Int otherCharCurrentPos = otherCharacter.GetComponent<TileMovement>().GetCurrentGridPosition();

        // Loop through every tile in the maze to build a list of valid spots.
        for (int row = 0; row < mazeSpawner.Rows; row++)
        {
            for (int col = 0; col < mazeSpawner.Columns; col++)
            {
                Vector2Int potentialPos = new Vector2Int(col, row);
                // A tile is valid if it's not where the target already is, and not where the other character is.
                if (potentialPos != targetCurrentPos && potentialPos != otherCharCurrentPos)
                {
                    validTiles.Add(potentialPos);
                }
            }
        }

        if (validTiles.Count > 0)
        {
            // Pick a random tile from our list of valid candidates.
            Vector2Int destination = validTiles[UnityEngine.Random.Range(0, validTiles.Count)];

            TileMovement targetMovement = targetToTeleport.GetComponent<TileMovement>();

            targetMovement.StopAllMovement();
            targetMovement.TeleportToTile(destination);

            // Use the TeleportToTile method we already created for the Jump power-up!
            targetToTeleport.GetComponent<TileMovement>().TeleportToTile(destination);

            // Update power-up uses and UI.
            userInventory.RemoveAt(slotIndex);
            UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);

            // Show a UI notification.
            string targetName = (targetToTeleport is PlayerController) ? "Player" : "Opponent";
            UIManager.Instance.ShowNotification($"{targetName} was teleported!", 2.5f);

            return true; // Report success
        }

        // This would only happen in a tiny 1x2 maze, but it's good practice to handle failure.
        Debug.LogWarning("Teleport failed: No valid tiles to teleport to.");
        return false; // Report failure
    }

    // This is a helper method that calculates the full path of a dash in one direction.
    public List<Vector2Int> CalculateDashPath(Vector2Int startPos, Direction direction)
    {
        var path = new List<Vector2Int>();
        var currentPos = startPos;
        int maxDashLength = Mathf.Max(mazeSpawner.Rows, mazeSpawner.Columns);

        for (int i = 0; i < maxDashLength; i++)
        {
            // 1. Determine the next potential position.
            Vector2Int nextPos = currentPos;
            if (direction == Direction.Front) { nextPos.y++; }
            else if (direction == Direction.Back) { nextPos.y--; }
            else if (direction == Direction.Right) { nextPos.x++; }
            else if (direction == Direction.Left) { nextPos.x--; }

            // 2. Check if the next position is out of the maze bounds.
            if (nextPos.y < 0 || nextPos.y >= mazeSpawner.Rows ||
                nextPos.x < 0 || nextPos.x >= mazeSpawner.Columns)
            {
                break; // Stop if we would go off the map.
            }

            // 3. --- The Bulletproof Two-Way Wall Check ---
            // Get the data for both the current tile and the one we want to move to.
            MazeCell currentCell = mazeSpawner.MazeGenerator.GetMazeCell(currentPos.y, currentPos.x);
            MazeCell nextCell = mazeSpawner.MazeGenerator.GetMazeCell(nextPos.y, nextPos.x);

            bool wallInTheWay = false;
            // Check for a wall from BOTH perspectives.
            // e.g., To move Front (Up), check for a WallFront on the current cell OR a WallBack on the next cell.
            if (direction == Direction.Front && (currentCell.WallFront || nextCell.WallBack)) { wallInTheWay = true; }
            else if (direction == Direction.Back && (currentCell.WallBack || nextCell.WallFront)) { wallInTheWay = true; }
            else if (direction == Direction.Right && (currentCell.WallRight || nextCell.WallLeft)) { wallInTheWay = true; }
            else if (direction == Direction.Left && (currentCell.WallLeft || nextCell.WallRight)) { wallInTheWay = true; }

            if (wallInTheWay)
            {
                break; // If either cell reports a wall between them, stop.
            }

            // 4. If the path is clear, add the next tile and continue.
            path.Add(nextPos);
            currentPos = nextPos;
        }
        return path;
    }

    // You will need this helper method in PowerupManager as well.
    private Vector3 GridToWorld(Vector2Int gridPos)
    {
        // This function should ideally be public in MazeSpawner to avoid duplication,
        // but for now, we can have a copy here.
        return new Vector3(
            gridPos.x * (mazeSpawner.CellWidth + (mazeSpawner.AddGaps ? 0.2f : 0)),
            1, // Y-position of the check
            gridPos.y * (mazeSpawner.CellHeight + (mazeSpawner.AddGaps ? 0.2f : 0))
        );
    }

    // This method consumes the power-up use. Notice it just returns the calculated path.
    // The GameManager will be responsible for telling the player to move along it.
    public List<Vector2Int> ExecuteDash(int slotIndex, Vector2Int playerGridPos, Direction targetDirection)
    {
        Powerup powerup = PlayerPowerups[slotIndex];

        // Decrement uses and update UI first.
        PlayerPowerups.RemoveAt(slotIndex);
        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);

        // Calculate and return the path for the GameManager to use.
        return CalculateDashPath(playerGridPos, targetDirection);
    }

    // Add this new public method. The GameManager calls this every 10 turns.
    public void AttemptToAwardTurnBasedPowerups()
    {
        // Check player's inventory and award if not full
        if (PlayerPowerups.Count < MaxPowerups)
        {
            AwardRandomPowerupTo(PlayerPowerups, "Player");
        }
        else
        {
            Debug.Log("Player inventory is full. No new power-up awarded.");
        }

        // Check AI's inventory and award if not full
        if (AIPowerups.Count < MaxPowerups)
        {
            AwardRandomPowerupTo(AIPowerups, "AI");
        }
        else
        {
            Debug.Log("AI inventory is full. No new power-up awarded.");
        }
    }

    // This is the helper method that does the actual work of giving one power-up.
    private void AwardRandomPowerupTo(List<Powerup> targetInventory, string inventoryOwner)
    {
        if (AllPossiblePowerups.Count == 0) return;

        Powerup randomPowerup = AllPossiblePowerups[UnityEngine.Random.Range(0, AllPossiblePowerups.Count)];
        targetInventory.Add(new Powerup(randomPowerup));

        Debug.Log($"Awarded a '{randomPowerup.Name}' power-up to {inventoryOwner}!");
        UIManager.Instance.UpdatePowerupDisplay(PlayerPowerups, AIPowerups);
    }
}
