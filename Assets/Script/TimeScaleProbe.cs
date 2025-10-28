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
        var w = new WaitForSecondsRealtime(1f); // 不吃 timeScale 的等待
        while (true)
        {
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Debug.LogWarning("[TimeScaleProbe] Detected timeScale == 0, forcing back to 1.");
                Time.timeScale = 1f; // 临时守护，定位完问题后可移除
            }
            else
            {
                Debug.Log($"[TimeScaleProbe] timeScale={Time.timeScale}, GamePause.IsPaused={GamePause.IsPaused}");
            }
            yield return w;
        }
    }
}