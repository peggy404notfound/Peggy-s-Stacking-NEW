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
        var w = new WaitForSecondsRealtime(1f); // ���� timeScale �ĵȴ�
        while (true)
        {
            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Debug.LogWarning("[TimeScaleProbe] Detected timeScale == 0, forcing back to 1.");
                Time.timeScale = 1f; // ��ʱ�ػ�����λ���������Ƴ�
            }
            else
            {
                Debug.Log($"[TimeScaleProbe] timeScale={Time.timeScale}, GamePause.IsPaused={GamePause.IsPaused}");
            }
            yield return w;
        }
    }
}