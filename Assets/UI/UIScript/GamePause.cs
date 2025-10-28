using UnityEngine;

public static class GamePause
{
    // 软暂停：仅供逻辑判断使用，不改 Time.timeScale，不改全局音频
    public static bool IsPaused { get; private set; }

    public static void Pause()
    {
        IsPaused = true;
    }

    public static void Resume()
    {
        IsPaused = false;
    }
}