using UnityEngine;

[RequireComponent(typeof(TileMovement))]
public class PlayerController : MonoBehaviour
{
    private TileMovement tileMovement;

    public int TurnsFrozen { get; set; } = 0;

    void Start()
    {
        tileMovement = GetComponent<TileMovement>();
    }

    void Update()
    {
        // We allow the Update method to run if it's the player's turn OR if they are aiming a power-up.
        // If it's any other state (like AITurn or Paused), we exit immediately.

        if (GameManager.Instance == null ||
           (GameManager.Instance.CurrentState != GameManager.GameState.PlayerTurn &&
            GameManager.Instance.CurrentState != GameManager.GameState.PowerupTargeting))
        {
            return;
        }

        // --- State-Specific Input Handling ---

        // Power-up activation should only be checked during the main player turn.

        if (GameManager.Instance.CurrentState == GameManager.GameState.PlayerTurn)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) { GameManager.Instance.InitiateTargetingMode(0); }
            else if (Input.GetKeyDown(KeyCode.Alpha2)) { GameManager.Instance.InitiateTargetingMode(1); }
            else if (Input.GetKeyDown(KeyCode.Alpha3)) { GameManager.Instance.InitiateTargetingMode(2); }
        }

        // This switch correctly handles what WASD/Escape do based on the current state.

        switch (GameManager.Instance.CurrentState)
        {
            case GameManager.GameState.PlayerTurn:
                HandleMovementInput(); // If it's our turn, WASD moves the character.
                break;

            case GameManager.GameState.PowerupTargeting:
                HandleTargetingInput();   // If we are aiming, WASD confirms the target and Escape cancels.
                break;
        }
    }


    // In PlayerController.cs

    private void AttemptPlayerMove(Vector2Int direction)
    {
        GameManager.Instance.SetState(GameManager.GameState.PlayerAnimating);

        tileMovement.AttemptMove(direction, (success) => {
            if (success)
            {
                // After the player's move is successful, check if they landed on the AI.
                var aiController = FindObjectOfType<AIController>(); // Find the AI in the scene.
                if (aiController != null && tileMovement.GetCurrentGridPosition() == aiController.GetComponent<TileMovement>().GetCurrentGridPosition())
                {
                    Debug.Log("CAUGHT! The Player has moved onto the AI's tile.");
                    GameManager.Instance.EndGame(false); // Player loses.
                    return;
                }
                
                // If not caught, end the turn normally so the AI can move.
                GameManager.Instance.EndPlayerTurn();
            }
            else
            {
                // The move was invalid, so allow player input again.
                GameManager.Instance.SetState(GameManager.GameState.PlayerTurn);
            }
        });
    }


    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("EndGoal"))
        {
            Debug.Log("VICTORY! The player has reached the goal.");
            GameManager.Instance.EndGame(true); // Player wins
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<AIController>() != null) // Check if the object we collided with is the Enemy
        {
            Debug.Log("CAUGHT! The AI has caught the player.");
            GameManager.Instance.EndGame(false); // Player loses
        }
    }

    void HandleMovementInput()
    {
        if (Input.GetKeyDown(KeyCode.W)) { AttemptPlayerMove(Vector2Int.up); }
        else if (Input.GetKeyDown(KeyCode.S)) { AttemptPlayerMove(Vector2Int.down); }
        else if (Input.GetKeyDown(KeyCode.D)) { AttemptPlayerMove(Vector2Int.right); }
        else if (Input.GetKeyDown(KeyCode.A)) { AttemptPlayerMove(Vector2Int.left); }
    }


    // This method handles what happens when we press WASD while aiming.

    void HandleTargetingInput()
    {
        // The player presses WASD to confirm the direction of the power-up
        
        if (Input.GetKeyDown(KeyCode.W)) { GameManager.Instance.ConfirmPowerupTarget(Direction.Front); }
        else if (Input.GetKeyDown(KeyCode.S)) { GameManager.Instance.ConfirmPowerupTarget(Direction.Back); }
        else if (Input.GetKeyDown(KeyCode.D)) { GameManager.Instance.ConfirmPowerupTarget(Direction.Right); }
        else if (Input.GetKeyDown(KeyCode.A)) { GameManager.Instance.ConfirmPowerupTarget(Direction.Left); }
        else if (Input.GetKeyDown(KeyCode.Escape)) // Allow cancelling
        {
            GameManager.Instance.CancelTargetingMode();
        }
    }
}