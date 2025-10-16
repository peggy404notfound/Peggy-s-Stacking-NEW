using UnityEngine;
using UnityEngine.Events;

public class GameEndManager : MonoBehaviour
{
    public static GameEndManager Instance { get; private set; }

    [Tooltip("����ʱ�Ƿ���ͣʱ����")]
    public bool freezeTimeOnGameOver = true;

    public UnityEvent onGameOver;

    public bool HasEnded { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void GameOver()
    {
        if (HasEnded) return;
        HasEnded = true;
        if (freezeTimeOnGameOver) Time.timeScale = 0f;
        onGameOver?.Invoke();
        Debug.Log("[GameEnd] Game Over");
    }
}