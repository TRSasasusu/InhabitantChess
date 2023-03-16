﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BoardGameController : MonoBehaviour
{
    /**
     * TODO:
     * - replace highlight materials (space and beam) with SIM in-game
     * - replace piece-teleporting with animated slerp or smth
     * - add toggleable overview camera or adjust seat
     */

    public FirstPersonManipulator PlayerManip;
    public GameObject StartText;
    public bool Playing { get; private set; }

    private float _CPUTurnTime = 1.0f;
    private int _currPlyrIdx;
    private BoardController _board;
    private BoardState _boardState = BoardState.Idle;
    private SpaceController _selectedSpace;

    private enum BoardState
    {
        WaitingForInput,
        InputReceived,
        Moving,
        Idle,
        GameOver
    }

    void Start()
    {
        _board = transform.Find("BoardGame_Board").gameObject.GetComponent<BoardController>();
        _board.Init();
        _currPlyrIdx = -1;
    }

    void Update()
    {
        // check for user input - should probably add a prompt to show space under cursor
        if (_boardState == BoardState.WaitingForInput && OWInput.IsNewlyPressed(InputLibrary.interact, InputMode.All))
        {
            CastRay();
        }
        else if (_boardState == BoardState.GameOver)
        {
            // resume if player selects prompt to start new game
        }

        void CastRay()
        {
            Transform manipTrans = PlayerManip.transform;
            RaycastHit hit;
            if (Physics.Raycast(manipTrans.position, manipTrans.forward, out hit, 75f, OWLayerMask.blockableInteractMask)) 
            {
                SpaceController hitSpc = hit.collider.gameObject.GetComponent<SpaceController>();
                if (hitSpc != null) 
                { 
                    // allow PlayerTurn to proceed
                    _boardState = BoardState.InputReceived;
                    _selectedSpace = hitSpc;
                }
            }
        }
    }

    public void EnterGame()
    {
        StartText.SetActive(false);
        _board.ResetBoard();
        Playing = true;
        StartCoroutine(Play());
    }

    public void ExitGame()
    {
        Playing = false;
        if (_currPlyrIdx != -1)
        {
            var currPlayer = _board.Players[_currPlyrIdx];
            _board.ToggleHighlight(currPlayer.g);
            _board.ToggleSpaces(_board.GetAdjacent(currPlayer.pos.up, currPlayer.pos.across));
            _board.UpdateBeam(true);
        }
        StartText.SetActive(true);
    }

    // loop controlling turns, game state
    private IEnumerator Play()
    {
        int turnCount = 0;
        // turn on beam at start
        _board.UpdateBeam();

        while (Playing)
        {
            for (int i = 0; i < _board.Players.Count && Playing; i++)
            {
                _currPlyrIdx = i;
                if (_board.Players[i].type == PieceType.Eye)
                {
                    StartCoroutine(CPUTurn(i));
                }
                else
                {
                    StartCoroutine(PlayerTurn(i));
                }
                // wait until turn finishes
                yield return new WaitUntil(() => _boardState == BoardState.Idle);
                // deleted flagged players
                var removed = _board.CheckBeam();
                foreach (int r in removed)
                {
                    var temp = _board.Players[r];
                    _board.Players.RemoveAt(r);
                    Destroy(temp.g);
                    if (r <= i) i--;
                    Debug.Log($"Removed {temp.g.name}, i = {i}, list length {_board.Players.Count}");
                    Playing = _board.Players.Count > 1;
                }
            }
            Debug.Log($"Turn {turnCount++} complete");
        }
        Debug.Log("Game Over!");
        _boardState = BoardState.GameOver;
    }

    private IEnumerator PlayerTurn(int pIdx) 
    {
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.Players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);

        _board.ToggleSpaces(adj);
        _board.ToggleHighlight(player.g);
        // wait for input, then move
        while (_selectedSpace == null || !adj.Contains(_selectedSpace.Space)) {
            _boardState = BoardState.WaitingForInput;
            yield return new WaitUntil(() => _boardState == BoardState.InputReceived);
        }
        // we're ready to move
        // might use moving to check animation status later idk
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.Space);
        // reset highlighting/visibility and finish
        _board.ToggleHighlight(player.g);
        _board.ToggleSpaces(adj);
        // blocker piece should update beam on move
        if (player.type == PieceType.Blocker)
        {
            _board.UpdateBeam();
        }
        _boardState = BoardState.Idle;
    }

    private IEnumerator CPUTurn(int pIdx)
    {
        (GameObject g, (int up, int across) pos, PieceType type) player = _board.Players[pIdx];
        List<(int, int)> adj = _board.GetAdjacent(player.pos.up, player.pos.across);
        _board.ToggleHighlight(player.g);
        // add artificial wait
        _boardState = BoardState.WaitingForInput;
        yield return new WaitForSecondsRealtime(_CPUTurnTime);
        _boardState = BoardState.InputReceived;
        // randomly choose an adjacent space
        // in future - could replace this w a call to a function that uses AI rules
        (int, int) randPos = adj[Random.Range(0, adj.Count)];
        _selectedSpace = _board.SpaceDict[randPos].GetComponent<SpaceController>();
        // move to space
        _boardState = BoardState.Moving;
        _board.TryMove(pIdx, _selectedSpace.Space);
        _board.UpdateBeam();
        // reset
        _board.ToggleHighlight(player.g);
        _boardState = BoardState.Idle;
    }
}
