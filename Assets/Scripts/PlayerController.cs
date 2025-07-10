using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        float h = Input.GetAxis("Horizontal"); // A/D or Left/Right
        float v = Input.GetAxis("Vertical");   // W/S or Up/Down
        Vector3 move = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
        transform.position += move;
    }
}
