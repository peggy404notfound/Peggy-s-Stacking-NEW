using UnityEngine;
using System.Collections;

public class TimeScaleProbe : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(Probe());
    }
    IEnumerator Probe()
    {
        var w = new WaitForSecondsRealtime(1f);
        while (true)
        {
            // 新增：若已结算，停止干预 timeScale（或直接 break 退出循环也可）
            if (GameEndManager.Instance != null && GameEndManager.Instance.HasEnded)
            {
                Debug.Log("[TimeScaleProbe] Game ended; probe will not force timeScale.");
                yield return w;
                continue;
            }

            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Debug.LogWarning("[TimeScaleProbe] Detected timeScale == 0, forcing back to 1.");
                Time.timeScale = 1f;
            }
            else
            {
                Debug.Log($"[TimeScaleProbe] timeScale={Time.timeScale}, GamePause.IsPaused={GamePause.IsPaused}");
            }
            yield return w;
        }
    }
}