using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }


    // Game State Machine (with all states) 

    public enum GameState
    {
        MainMenu,
        Starting,
        PlayerTurn,
        AITurn,
        PowerupTargeting,
        PlayerAnimating,
        AIAnimating,
        Paused,
        GameOver
    }

    public GameState CurrentState { get; private set; }

    [Header("Scene References")]
    public MazeSpawner mazeSpawner;

    [Header("UI References")]
    public TextMeshProUGUI RewardKeeperUI;
    public TextMeshProUGUI TurnText;

    //  Turn Management 

    private int turnNumber = 1;
    private int activePowerupSlot = -1;
    private Coroutine turnTransitionCoroutine;

    public int GetTurnNumber()
    {
        return turnNumber;
    }

    public static bool showMainMenuOnLoad = true;

    [Header("Game Feel Settings")]
    [Tooltip("The short pause in seconds between the player's and AI's turns.")]
    public float TurnTransitionDelay = 0.5f;

    // Object References
    private PowerupManager powerupManager;
    public PlayerController player { get; private set; }
    public AIController ai { get; private set; }

    //private void Awake()
    //{
    //    if (Instance != null && Instance != this) { Destroy(gameObject); }
    //    else { Instance = this; }

    //    CurrentState = GameState.MainMenu;
    //}

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void StartGame()
    {
        // Set the state to prevent anything else from happening while spawning

        SetState(GameState.Starting);

        // We're telling the maze spawner to spawn the maze, and then the maze spawner nudges the manager to start the game

        if (mazeSpawner != null)
        {
            mazeSpawner.BeginSpawning();
        }
        else
        {
            Debug.LogError("MazeSpawner reference has not been set in the GameManager!");
        }
    }

    private void Start()
    {
        if (showMainMenuOnLoad)
        {
            // This is a fresh launch. Setting the state to MainMenu and waiting for the player.

            CurrentState = GameState.MainMenu;
        }
        else
        {
            // This is a Reset. Bypassing the menu and commanding the game to start immediately.
            StartGame();
        }
    }

    // Called by MazeSpawner after all objects are created.

    public void InitializeGame(PlayerController playerController, AIController aiController)
    {
        this.player = playerController;
        this.ai = aiController;

        this.ai.Initialize();

        powerupManager = PowerupManager.Instance;
        powerupManager.InitializeAndDealStartingPowerups();

        turnNumber = 1;
        UpdateTurnUI();
        StartPlayerTurn();
    }

    //  Turn Flow Management 

    private void StartPlayerTurn()
    {
        CurrentState = GameState.PlayerTurn;
        UIManager.Instance.ShowNotification("Player's Turn", 1.5f); 
        UpdateRewardCounterUI();
        UpdateTurnUI();
        Debug.Log($"--- Turn {turnNumber}: Player's Turn ---");
    }

    private void UpdateTurnUI()
    {
        if (TurnText != null)
        {
            TurnText.text = $"Turn number {turnNumber}";
        }
    }

    public void EndPlayerTurn()
    {
        if (CurrentState != GameState.PlayerTurn && CurrentState != GameState.PowerupTargeting && CurrentState != GameState.PlayerAnimating) return;
        Debug.Log("Player ends their turn.");
        CheckForPowerupAward();
        TransitionToState(GameState.AITurn);
    }

    private void StartAITurn()
    {
        CurrentState = GameState.AITurn;
        UIManager.Instance.ShowNotification("Opponent's Turn", 1.5f); 
        UpdateRewardCounterUI();
        Debug.Log("AI's Turn...");
        ai.TakeTurn();
    }

    public void EndAITurn()
    {
        if (CurrentState != GameState.AITurn && CurrentState != GameState.AIAnimating) return;
        Debug.Log("AI ends its turn.");

        // We check if the player is frozen BEFORE advancing the turn number.
        bool playerIsFrozen = player.TurnsFrozen > 0;

        if (playerIsFrozen)
        {
            // If the player is frozen, we still advance the turn counter to represent their "skipped" turn.

            turnNumber++;
            Debug.Log($"--- Turn {turnNumber}: Player's Turn (SKIPPED) ---");

            player.TurnsFrozen--; // Decrement the freeze counter.
            UIManager.Instance.ShowNotification($"Player is frozen! {player.TurnsFrozen} turn(s) left.", 1.5f);

            // Because the player's turn was skipped, we transition right back to the AI's turn.

            TransitionToState(GameState.AITurn);
        }
        else
        {
            // Normal turn progression.

            turnNumber++;
            TransitionToState(GameState.PlayerTurn);
        }
    }

    private void UpdateRewardCounterUI()
    {
        if (RewardKeeperUI != null)
        {
            int turnsRemaining = 10 - (turnNumber % 10);
            if (turnNumber % 10 == 0) { turnsRemaining = 10; } // Show 10 on the turn of the reward
            RewardKeeperUI.text = $"Power-up in: {turnsRemaining}";
        }
    }

    private void TransitionToState(GameState newState)
    {
        if (turnTransitionCoroutine != null) { StopCoroutine(turnTransitionCoroutine); }
        turnTransitionCoroutine = StartCoroutine(TurnTransitionCoroutine(newState));
    }

    private IEnumerator TurnTransitionCoroutine(GameState newState)
    {
        yield return new WaitForSeconds(TurnTransitionDelay);
        if (newState == GameState.AITurn) { StartAITurn(); }
        else if (newState == GameState.PlayerTurn) { StartPlayerTurn(); }
        turnTransitionCoroutine = null;
    }

    //  Power-up & Game State Logic 

    public void InitiateTargetingMode(int slotIndex)
    {
        if (CurrentState != GameState.PlayerTurn || powerupManager.PlayerPowerups.Count <= slotIndex) return;
        Powerup powerup = powerupManager.PlayerPowerups[slotIndex];
        Vector2Int playerPos = player.GetComponent<TileMovement>().GetCurrentGridPosition();

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null) { Debug.LogError("FATAL: Could not find an active UIManager in the scene!"); return; }

        activePowerupSlot = slotIndex;

        if (powerup.Type == PowerupType.BreakWall) { CurrentState = GameState.PowerupTargeting; uiManager.HighlightBreakableWalls(playerPos); }
        else if (powerup.Type == PowerupType.Jump) { CurrentState = GameState.PowerupTargeting; uiManager.HighlightJumpableTiles(playerPos); }
        else if (powerup.Type == PowerupType.Dash) { CurrentState = GameState.PowerupTargeting; uiManager.HighlightDashPaths(playerPos); }
        else if (powerup.Type == PowerupType.Freeze)
        {
            powerupManager.ExecuteFreeze(powerupManager.PlayerPowerups, slotIndex, ai);
            EndPlayerTurn();
        }
        else if (powerup.Type == PowerupType.Teleport)
        {
            powerupManager.ExecuteTeleport(powerupManager.PlayerPowerups, slotIndex, ai, player);
            EndPlayerTurn();
        }
    }

    public void ConfirmPowerupTarget(Direction targetDirection)
    {
        if (activePowerupSlot < 0 || activePowerupSlot >= powerupManager.PlayerPowerups.Count)
        {
            CancelTargetingMode(); return;
        }

        if (CurrentState != GameState.PowerupTargeting) return;

        Vector2Int playerPos = player.GetComponent<TileMovement>().GetCurrentGridPosition();
        Powerup activePowerup = powerupManager.PlayerPowerups[activePowerupSlot];
        bool success = false;
        bool turnEndsImmediately = false;

        if (activePowerup.Type == PowerupType.BreakWall)
        {
            success = powerupManager.ExecuteBreakWall(powerupManager.PlayerPowerups, activePowerupSlot, playerPos, targetDirection);
            if (success) turnEndsImmediately = true;
        }
        else if (activePowerup.Type == PowerupType.Jump)
        {
            success = powerupManager.ExecuteJump(powerupManager.PlayerPowerups, activePowerupSlot, playerPos, targetDirection, player);
            if (success) turnEndsImmediately = true;
        }
        else if (activePowerup.Type == PowerupType.Dash)
        {
            List<Vector2Int> dashPath = powerupManager.ExecuteDash(activePowerupSlot, playerPos, targetDirection);
            if (dashPath.Count > 0)
            {
                success = true;
                player.GetComponent<TileMovement>().ExecuteDash(dashPath, () => { EndPlayerTurn(); });
            }
        }

        FindObjectOfType<UIManager>()?.ClearHighlights();
        activePowerupSlot = -1;

        if (success)
        {
            if (turnEndsImmediately) { EndPlayerTurn(); }
            else { CurrentState = GameState.PlayerAnimating; }
        }
        else { CurrentState = GameState.PlayerTurn; }
    }

    public void CancelTargetingMode()
    {
        FindObjectOfType<UIManager>()?.ClearHighlights();
        activePowerupSlot = -1;
        CurrentState = GameState.PlayerTurn;
    }

    private void CheckForPowerupAward()
    {
        if (turnNumber > 0 && turnNumber % 10 == 0)
        {
            powerupManager.AttemptToAwardTurnBasedPowerups();
        }
    }

    public void EndGame(bool playerWon)
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;

        string endMessage = playerWon ?
            $"You Won after {turnNumber} turns!" :
            $"You Lost after {turnNumber} turns!";

        UIManager.Instance.ShowEndGameScreen(endMessage);
    }

    public void SetState(GameState newState)
    {
        CurrentState = newState;
    }
}