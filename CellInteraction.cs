using UnityEngine;

/// <summary>
/// Script for the interaction of each cell in the tic tac toe board
/// Will handle the click events of the cells and update the visuals of the cells based on what players do
/// Will reference game manager script to handle game logic when cells are clicked
/// </summary>
public class CellInteraction : MonoBehaviour
{
    //the index for each cell in board (9 total, 0-8)
    [SerializeField] private int cellIndex;
    //reference to x sprite child object under each cell
    [SerializeField] private GameObject xSprite;
    //same as above but for o sprite
    [SerializeField] private GameObject oSprite;

    //reference to the game manager script to call functions from it
    private TicTacToeGameManager gameManager;

    //prevents clicking cells before game has started/players have connected
    private bool gameStarted = false;

    private void Start()
    {
        //get reference to game manager script
        gameManager = FindFirstObjectByType<TicTacToeGameManager>();

        //so that sprites dont show at the start of the game
        if (oSprite != null)
        {
            oSprite.SetActive(false);
        }

        if (xSprite != null)
        {
            xSprite.SetActive(false);
        }
    }

    //called by lobby manager when session starts to allow cell clicks
    public void SetGameStarted(bool started)
    {
        gameStarted = started;
    }

    //with new input system, use raycast to detect clicks on cells (remember u attached colliders to them so should work)
    private void Update()
    {
        //check if mouse clicked, if not return 
        if (UnityEngine.InputSystem.Mouse.current == null) return;

        //check if left mouse button clicked
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!gameStarted) return; //so cant click cells before game starts/players connect

            //get mouse pos in game world space and raycast to see if hitting cell
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            //raycast to see if hitting cell
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            //if raycast hits a collider on cell, pass cell index to game manager for the logic
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                Debug.Log("Cell clicked via raycast: " + cellIndex);

                //handle the cell click
                if (gameManager != null)
                    gameManager.OnCellClicked(cellIndex);
            }
        }
    }

    //function to set the visual of the cell based on the player index (1 reps O, 2 for X), represents the move made by the player in that cell
    public void SetVisual(int playerIndex)
    {
        if (oSprite != null)
        {
            oSprite.SetActive(playerIndex == 1);
        }

        if (xSprite != null)
        {
            xSprite.SetActive(playerIndex == 2);
        }
    }

    //function to reset the cell visuals back to default/hidden
    public void CellReset()
    {
        if (oSprite != null)
        {
            oSprite.SetActive(false);
        }

        if (xSprite != null)
        {
            xSprite.SetActive(false);
        }
    }
}
