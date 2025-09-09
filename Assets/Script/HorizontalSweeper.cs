using UnityEngine;

public class HorizontalSweeper : MonoBehaviour
{
    public float moveSpeed = 3f;
    [HideInInspector] public Transform leftBound;
    [HideInInspector] public Transform rightBound;

    public bool isActive = false;

    private float dir = 1f;

    private void Update()
    {
        if (!isActive || leftBound == null || rightBound == null) return;

        transform.position += Vector3.right * dir * moveSpeed * Time.deltaTime;

        if (transform.position.x <= leftBound.position.x) dir = 1f;
        if (transform.position.x >= rightBound.position.x) dir = -1f;
    }
}