using UnityEngine;
using Unity.Netcode;
using TMPro;

/// <summary>
/// Game manager for tic tac toe game
/// Will handle game logic such as tracking player turns, checking for wins/draws, updating score, resetting game
/// Netcode components like from lectures to sync game state for clients/host, handles RPC calls from cell interactions and UI buttons
/// Will reference the cell interaction script to update visuals of cells when players make a move
/// 
/// </summary>
public class TicTacToeGameManager : NetworkBehaviour
{
    //board state set up with array, 0 means empty, 1 represents player 1 (O), 2 represents player 2 (X)
    private int[] boardState = new int[9];

    //current turn will be 1 for player 1 (O) and 2 for player 2 (X)
    private NetworkVariable<int> currentTurn = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //tracks which player goes first, alternates after each game, player 1 always goes first on initial game start
    private NetworkVariable<int> firstPlayer = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    //score tracking for each player, updated on the server when a win is detected and synced to clients
    private NetworkVariable<int> player1Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> player2Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Cell References")]
    [SerializeField] private CellInteraction[] cells = new CellInteraction[9]; //reference to cell interaction for each cell to update visuals when moves are made

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI playerTurnText; //text to show whose turn it is
    [SerializeField] private TextMeshProUGUI p1ScoreText; //text to show player 1 score
    [SerializeField] private TextMeshProUGUI p2ScoreText; //text to show player 2 score

    [Header("Results Display")]
    [SerializeField] private GameObject resultDisplay; //UI element to show the result of the game (win/draw) with different sprites for each outcome
    [SerializeField] private Sprite p1WinsSprite; //sprite to show when player 1 wins
    [SerializeField] private Sprite p2WinsSprite; //player2 sprite win
    [SerializeField] private Sprite drawSprite; //draw sprite

    //for all ways to win in tic tac toe, used to check for wins after each move
    private readonly int[][] winPatterns = new int[][]
    {
        new int[] { 0, 1, 2 }, //top row
        new int[] { 3, 4, 5 }, //middle row
        new int[] { 6, 7, 8 }, //bottom row
        new int[] { 0, 3, 6 }, //left column
        new int[] { 1, 4, 7 }, //middle column
        new int[] { 2, 5, 8 }, //right column
        new int[] { 0, 4, 8 }, //diagonal
        new int[] { 2, 4, 6 }  //other diagonal
    };

    //listeners for when network vars change to update visuals and ui for clients and host
    //will also set initial UI text and hide result display at the start of the game
    public override void OnNetworkSpawn()
    {
        //listener for when current turn changes to update the turn text in the UI
        currentTurn.OnValueChanged += (previousValue, newValue) =>
        {
            UpdateTurnText(newValue);
        };

        //listeners for when player scores change to update the score text in the UI
        player1Score.OnValueChanged += (previousValue, newValue) =>
        {
            p1ScoreText.text = "P1 Score: " + newValue;
        };

        //listener for player 2 score changes
        player2Score.OnValueChanged += (previousValue, newValue) =>
        {
            p2ScoreText.text = "P2 Score: " + newValue;
        };

        //set initial UI text and hide result display at the start of the game
        UpdateTurnText(currentTurn.Value);
        p1ScoreText.text = "P1 Score: " + player1Score.Value;
        p2ScoreText.text = "P2 Score: " + player2Score.Value;

        //hide result display at start of game
        if (resultDisplay != null)
            resultDisplay.SetActive(false);
    }

    //called locally when player clicks a cell, will send an RPC to the server to request the move, passing the index of the clicked cell
    public void OnCellClicked(int cellIndex)
    {
        Debug.Log("Cell clicked: " + cellIndex + " | IsSpawned: " + IsSpawned + " | IsClient: " + IsClient + " | IsHost: " + IsHost);
        if (!IsSpawned) return;

        RequestMoveServerRpc(cellIndex);
    }

    //RPC function called on the server when a client requests to make a move by clicking a cell
    //will validate the move, update the board state, check for wins/draws, and update turn and scores accordingly
    [Rpc(SendTo.Server)]
    private void RequestMoveServerRpc(int cellIndex, RpcParams rpcParams = default)
    {
        //dont allow moves until both players are connected
        if (NetworkManager.Singleton.ConnectedClients.Count < 2) return;

        //get which player sent the RPC request based on client ID
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        int playerNumber = GetPlayerNumber(senderClientId);

        //check if its player's turn
        if (playerNumber != currentTurn.Value) return;

        //check if cell is already marked
        if (boardState[cellIndex] != 0) return;

        //update the board state on the server
        boardState[cellIndex] = playerNumber;

        //tell all clients and host to update the visual of this cell
        UpdateCellVisualClientRpc(cellIndex, playerNumber);

        //check for win to update scores for players
        if (CheckWin(playerNumber))
        {
            if (playerNumber == 1)
                player1Score.Value++;
            else
                player2Score.Value++;

            //announce the result to clients and host with player number of winner or draw
            AnnounceResultClientRpc(playerNumber);
            return;
        }

        //check for a draw
        if (CheckDraw())
        {
            //0 indicates draw
            AnnounceResultClientRpc(0);
            return;
        }

        //update turn to the other player after a successful move
        currentTurn.Value = (currentTurn.Value == 1) ? 2 : 1;
    }

    //client RPC used to update the visual of a cell when a move is made
    //called by the server after validating a move so all players see the same board state
    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateCellVisualClientRpc(int cellIndex, int playerNumber)
    {
        if (cells != null && cellIndex >= 0 && cellIndex < cells.Length)
        {
            cells[cellIndex].SetVisual(playerNumber);
        }
    }

    //client RPC sent from server to all clients and host to announce the result of the game (win/draw)
    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceResultClientRpc(int winner)
    {
        if (resultDisplay != null)
        {
            resultDisplay.SetActive(true);
            UnityEngine.UI.Image img = resultDisplay.GetComponent<UnityEngine.UI.Image>();

            if (img != null)
            {
                if (winner == 1) img.sprite = p1WinsSprite;
                else if (winner == 2) img.sprite = p2WinsSprite;
                else img.sprite = drawSprite;
            }
        }

        if (playerTurnText != null)
            playerTurnText.text = winner == 0 ? "Draw!" : "Player " + winner + " Wins!";

        //if is the server, reset game after short delay so players can see result
        if (IsServer)
            Invoke(nameof(ResetGame), 3f);
    }

    //function to reset game state on server, clear board, reset turn (alternate turns)
    private void ResetBoardServer()
    {
        //reset board state to empty
        for (int i = 0; i < 9; i++)
            boardState[i] = 0;

        //alternate starting player for new game
        firstPlayer.Value = (firstPlayer.Value == 1) ? 2 : 1;
        currentTurn.Value = firstPlayer.Value;

        ResetVisualsClientRpc();
    }

    //client RPC to reset the visuals of the cells and UI for all clients and host when a new game starts
    [Rpc(SendTo.ClientsAndHost)]
    private void ResetVisualsClientRpc()
    {
        if (resultDisplay != null)
            resultDisplay.SetActive(false);

        foreach (var cell in cells)
            cell.CellReset();

        UpdateTurnText(firstPlayer.Value);
    }

    //helper func for getting player number based on client ID
    private int GetPlayerNumber(ulong clientId)
    {
        return clientId == 0 ? 1 : 2;
    }

    //helper func to check for wins by checking board state for all possible win conditions
    private bool CheckWin(int playerNumber)
    {
        foreach (var pattern in winPatterns)
        {
            if (boardState[pattern[0]] == playerNumber &&
                boardState[pattern[1]] == playerNumber &&
                boardState[pattern[2]] == playerNumber)
            {
                return true;
            }
        }

        return false;
    }

    //helper func that checks for a draw
    private bool CheckDraw()
    {
        for (int i = 0; i < 9; i++)
        {
            if (boardState[i] == 0)
                return false;
        }
        return true;
    }

    //helper func to update turn txt
    private void UpdateTurnText(int turn)
    {
        if (playerTurnText != null)
            playerTurnText.text = "Turn: Player " + turn;
    }

    //resets game after result display timer
    private void ResetGame()
    {
        if (IsServer)
            ResetBoardServer();
    }
}