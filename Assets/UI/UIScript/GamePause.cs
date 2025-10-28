using UnityEngine;

public static class GamePause
{
    // ����ͣ�������߼��ж�ʹ�ã����� Time.timeScale������ȫ����Ƶ
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