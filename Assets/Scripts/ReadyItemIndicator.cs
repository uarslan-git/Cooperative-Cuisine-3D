using UnityEngine;

public class ReadyItemIndicator : MonoBehaviour
{
    public float bobHeight = 0.1f;
    public float bobSpeed = 2f;
    
    private Vector3 startPos;
    private float randomOffset;
    
    void Start()
    {
        startPos = transform.localPosition;
        randomOffset = Random.Range(0f, 2f * Mathf.PI); // Random starting phase
    }
    
    void Update()
    {
        // Create a gentle bobbing animation to indicate the item is ready
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed + randomOffset) * bobHeight;
        transform.localPosition = new Vector3(startPos.x, newY, startPos.z);
    }
    
    void OnDestroy()
    {
        // Reset position when destroyed
        if (transform != null)
        {
            transform.localPosition = startPos;
        }
    }
}
