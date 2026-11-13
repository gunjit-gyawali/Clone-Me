using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public Rigidbody2D playerBody;
    public Rigidbody2D ghostPrefab;

    public float moveSpeed = 12f;

    [SerializeField] private int maxClones = 3;

    private InputAction moveUp;
    private InputAction moveDown;
    private InputAction moveLeft;
    private InputAction moveRight;
    private InputAction restartAction;

    private Vector2Int playerCell;
    private Vector2Int playerSpawn;

    private float attemptTime;

    private RaycastHit2D[] hitBuffer = new RaycastHit2D[4];

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
        public Rigidbody2D body;
        public List<RecordedCommand> recording;
        public Vector2Int cell;
        public int playbackIndex;
    }

    void Start()
    {
        playerSpawn = new Vector2Int(
            Mathf.RoundToInt(playerBody.position.x),
            Mathf.RoundToInt(playerBody.position.y));

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

    void FixedUpdate()
    {
        MoveBodyTowardsCell(playerBody, playerCell);

        foreach (GhostData ghost in ghosts)
        {
            MoveBodyTowardsCell(ghost.body, ghost.cell);
        }
    }

    void MoveBodyTowardsCell(Rigidbody2D body, Vector2Int cell)
    {
        Vector2 target = new Vector2(cell.x, cell.y);
        Vector2 nextPosition = Vector2.MoveTowards(body.position, target, moveSpeed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    bool HasArrived(Rigidbody2D body, Vector2Int cell)
    {
        Vector2 cellCenter = new Vector2(cell.x, cell.y);
        return (body.position - cellCenter).sqrMagnitude < 0.0001f;
    }

    bool IsBlocked(Rigidbody2D body, Vector2Int direction)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));

        int hitCount = body.Cast(new Vector2(direction.x, direction.y), filter, hitBuffer, 1f);

        return hitCount > 0;
    }

    void HandleInput()
    {
        if (moveUp == null)
        {
            return;
        }

        if (restartAction.WasPressedThisFrame())
        {
            RestartAndCreateGhost();
            return;
        }

        if (!HasArrived(playerBody, playerCell))
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
    }

    void TakeTurn(Vector2Int direction)
    {
        RecordedCommand command = new RecordedCommand();
        command.direction = direction;
        command.time = attemptTime;
        currentRecording.Add(command);

        if (IsBlocked(playerBody, direction))
        {
            Debug.Log(attemptTime.ToString("F2") + "s: " + DirectionName(direction)
                      + " blocked at " + (playerCell + direction) + " (still recorded)");
            return;
        }

        playerCell = playerCell + direction;

        Debug.Log(attemptTime.ToString("F2") + "s: " + DirectionName(direction)
                  + " -> player at " + playerCell);
    }

    void UpdateGhosts()
    {
        foreach (GhostData ghost in ghosts)
        {
            if (ghost.playbackIndex >= ghost.recording.Count)
            {
                continue;
            }

            RecordedCommand command = ghost.recording[ghost.playbackIndex];

            if (command.time > attemptTime)
            {
                continue;
            }

            if (!HasArrived(ghost.body, ghost.cell))
            {
                continue;
            }

            if (!IsBlocked(ghost.body, command.direction))
            {
                ghost.cell = ghost.cell + command.direction;
            }

            ghost.playbackIndex++;
        }
    }

    void RestartAndCreateGhost()
    {
        if (currentRecording.Count == 0)
        {
            Debug.Log("Nothing recorded yet, so no ghost was created.");
        }
        else if (savedRecordings.Count >= maxClones)
        {
            Debug.Log("Clone limit reached (" + maxClones + "). No more ghosts can be created.");
            return;
        }
        else
        {
            savedRecordings.Add(currentRecording);

            Debug.Log("Saved attempt " + savedRecordings.Count + " of " + maxClones + ": "
                      + RecordingToText(currentRecording));
        }

        StartAttempt();
    }

    void StartAttempt()
    {
        foreach (GhostData ghost in ghosts)
        {
            Destroy(ghost.body.gameObject);
        }
        ghosts.Clear();

        playerCell = playerSpawn;
        playerBody.position = new Vector2(playerCell.x, playerCell.y);
        currentRecording = new List<RecordedCommand>();

        attemptTime = 0f;

        foreach (List<RecordedCommand> recording in savedRecordings)
        {
            GhostData ghost = new GhostData();
            ghost.recording = recording;
            ghost.cell = playerSpawn;
            ghost.playbackIndex = 0;
            ghost.body = Instantiate(
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