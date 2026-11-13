using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public Transform player;
    public GameObject ghostPrefab;

    public float ghostGlideSpeed = 12f;

    private InputAction moveUp;
    private InputAction moveDown;
    private InputAction moveLeft;
    private InputAction moveRight;
    private InputAction restartAction;

    private Vector2Int playerCell;
    private Vector2Int playerSpawn;

    private float attemptTime;

    private class RecordedCommand
    {
        public Vector2Int direction;
        public float time;
    }

    private List<RecordedCommand> currentRecording = new List<RecordedCommand>();

    private List<List<RecordedCommand>> savedRecordings = new List<List<RecordedCommand>>();

    private List<GhostData> ghosts = new List<GhostData>();

    private class GhostData
    {
        public GameObject visual;
        public List<RecordedCommand> recording;
        public Vector2Int cell;
        public int playbackIndex;
    }

    void Start()
    {
        playerSpawn = new Vector2Int(
            Mathf.RoundToInt(player.position.x),
            Mathf.RoundToInt(player.position.y));

        SetUpInput();
        StartAttempt();
    }

    void SetUpInput()
    {
        if (InputSystem.actions == null)
        {
            Debug.LogError("No project-wide Input Actions asset found. " +
                           "Go to Edit > Project Settings > Input System Package and create one.");
            return;
        }

        moveUp = InputSystem.actions.FindAction("MoveUp");
        moveDown = InputSystem.actions.FindAction("MoveDown");
        moveLeft = InputSystem.actions.FindAction("MoveLeft");
        moveRight = InputSystem.actions.FindAction("MoveRight");
        restartAction = InputSystem.actions.FindAction("Restart");

        if (moveUp == null || moveDown == null || moveLeft == null ||
            moveRight == null || restartAction == null)
        {
            Debug.LogError("One or more actions were not found. Check that you created " +
                           "MoveUp, MoveDown, MoveLeft, MoveRight and Restart " +
                           "with exactly these names.");
            moveUp = null;
        }
    }

    void Update()
    {
        attemptTime += Time.deltaTime;

        HandleInput();
        UpdateGhosts();
    }

    void HandleInput()
    {
        if (moveUp == null)
        {
            return;
        }

        if (moveUp.WasPressedThisFrame())
        {
            TakeTurn(Vector2Int.up);
        }
        else if (moveDown.WasPressedThisFrame())
        {
            TakeTurn(Vector2Int.down);
        }
        else if (moveLeft.WasPressedThisFrame())
        {
            TakeTurn(Vector2Int.left);
        }
        else if (moveRight.WasPressedThisFrame())
        {
            TakeTurn(Vector2Int.right);
        }
        else if (restartAction.WasPressedThisFrame())
        {
            RestartAndCreateGhost();
        }
    }

    void TakeTurn(Vector2Int direction)
    {
        playerCell = playerCell + direction;
        player.position = new Vector3(playerCell.x, playerCell.y, 0);

        RecordedCommand command = new RecordedCommand();
        command.direction = direction;
        command.time = attemptTime;
        currentRecording.Add(command);

        Debug.Log(attemptTime.ToString("F2") + "s: " + DirectionName(direction)
                  + " -> player at " + playerCell);
    }

    void UpdateGhosts()
    {
        foreach (GhostData ghost in ghosts)
        {
            while (ghost.playbackIndex < ghost.recording.Count
                   && ghost.recording[ghost.playbackIndex].time <= attemptTime)
            {
                ghost.cell = ghost.cell + ghost.recording[ghost.playbackIndex].direction;
                ghost.playbackIndex++;
            }

            Vector3 target = new Vector3(ghost.cell.x, ghost.cell.y, 0);
            ghost.visual.transform.position = Vector3.MoveTowards(
                ghost.visual.transform.position, target, ghostGlideSpeed * Time.deltaTime);
        }
    }

    void RestartAndCreateGhost()
    {
        if (currentRecording.Count > 0)
        {
            savedRecordings.Add(currentRecording);

            Debug.Log("Saved attempt " + savedRecordings.Count + ": "
                      + RecordingToText(currentRecording));
        }
        else
        {
            Debug.Log("Nothing recorded yet, so no ghost was created.");
        }

        StartAttempt();
    }

    void StartAttempt()
    {
        foreach (GhostData ghost in ghosts)
        {
            Destroy(ghost.visual);
        }
        ghosts.Clear();

        playerCell = playerSpawn;
        player.position = new Vector3(playerCell.x, playerCell.y, 0);
        currentRecording = new List<RecordedCommand>();

        attemptTime = 0f;

        foreach (List<RecordedCommand> recording in savedRecordings)
        {
            GhostData ghost = new GhostData();
            ghost.recording = recording;
            ghost.cell = playerSpawn;
            ghost.playbackIndex = 0;
            ghost.visual = Instantiate(
                ghostPrefab,
                new Vector3(playerSpawn.x, playerSpawn.y, 0),
                Quaternion.identity);

            ghosts.Add(ghost);
        }

        Debug.Log("New attempt started with " + ghosts.Count + " ghost(s).");
    }

    string DirectionName(Vector2Int direction)
    {
        if (direction == Vector2Int.up) return "Up";
        if (direction == Vector2Int.down) return "Down";
        if (direction == Vector2Int.left) return "Left";
        if (direction == Vector2Int.right) return "Right";
        return "None";
    }

    string RecordingToText(List<RecordedCommand> recording)
    {
        List<string> names = new List<string>();
        foreach (RecordedCommand command in recording)
        {
            names.Add(DirectionName(command.direction) + "@" + command.time.ToString("F2") + "s");
        }
        return string.Join(", ", names);
    }
}