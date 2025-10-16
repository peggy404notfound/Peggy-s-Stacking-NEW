using UnityEngine;

public static class GamePause
{
    public static bool IsPaused { get; private set; }

    public static void Pause()
    {
        if (IsPaused) return;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        IsPaused = true;
    }

    public static void Resume()
    {
        if (!IsPaused) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
    }
}