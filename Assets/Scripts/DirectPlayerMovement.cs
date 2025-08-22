using UnityEngine;

public class DirectPlayerMovement : MonoBehaviour
{
    public StudyClient studyClient;
    public string playerId;
    public float moveCooldown = 0.1f; // 100ms cooldown
    private float lastMoveTime = 0f;

    void Awake()
    {
        if (studyClient == null)
        {
            studyClient = FindFirstObjectByType<StudyClient>();
        }
        if (studyClient == null)
        {
            Debug.LogError("DirectPlayerMovement could not find a StudyClient in the scene!");
        }
    }

    void Update()
    {
        if (Time.time - lastMoveTime < moveCooldown)
        {
            return;
        }

        Vector2 move = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
        {
            move.y = 1.0f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            move.y = -1.0f;
        }
        if (Input.GetKey(KeyCode.A))
        {
            move.x = -1.0f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            move.x = 1.0f;
        }

        if (move != Vector2.zero)
        {
            SendMoveAction(move);
            lastMoveTime = Time.time;
        }
    }

    private void SendMoveAction(Vector2 move)
    {
        if (studyClient == null) return;
        Action action = new Action
        {
            player = playerId,
            action_type = "movement",
            action_data = new System.Collections.Generic.List<float> { move.x, move.y }
        };
        studyClient.SendAction(action);
        Debug.Log("Sent Move Action: " + move);
    }
}
