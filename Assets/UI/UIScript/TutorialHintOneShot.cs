using UnityEngine;
using System.Collections.Generic;

public class TutorialHintOneShot : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject hintRoot;

    [Header("唯一键（同一条提示共享此键）")]
    public string prefsKey = "tut_key";

    public enum Scope { PerSession, PerDevice }
    [Header("出现频率")]
    public Scope showScope = Scope.PerSession;

    [Header("是否在场景开始就自动弹")]
    public bool showOnSceneStart = false;

    // 新增：可选的倒计时引用（没拖就自动找）
    [Header("可选：要暂停/恢复的倒计时")]
    public CountdownTimer countdown;

    private static readonly HashSet<string> seenThisSession = new HashSet<string>();
    private static int s_activeHints = 0; // 新增：并发弹窗计数，最后一个关闭时再恢复
    bool _showing = false;

    void Start()
    {
        if (!countdown) countdown = FindObjectOfType<CountdownTimer>();
        if (showOnSceneStart) TriggerIfNeeded();
    }

    void Update()
    {
        if (!_showing) return;
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
            CloseHint();
    }

    public void TriggerIfNeeded()
    {
        if (string.IsNullOrEmpty(prefsKey)) return;

        bool seen =
            (showScope == Scope.PerSession && seenThisSession.Contains(prefsKey)) ||
            (showScope == Scope.PerDevice && PlayerPrefs.GetInt(prefsKey, 0) == 1);
        if (seen) return;

        if (!hintRoot) { Debug.LogWarning("[TutorialHintOneShot] hintRoot 未设置，跳过弹窗"); return; }

        hintRoot.SetActive(true);
        hintRoot.transform.SetAsLastSibling();
        var cv = hintRoot.GetComponentInParent<Canvas>(true);
        if (cv != null)
        {
            cv.overrideSorting = true;
            cv.sortingOrder = 200;
            if (cv.renderMode == RenderMode.ScreenSpaceCamera && cv.worldCamera == null && Camera.main)
                cv.worldCamera = Camera.main;
        }

        // 移除：Time.timeScale = 0f;  AudioListener.pause = true;
        // 改为：逻辑暂停 + 暂停倒计时
        s_activeHints++;
        GamePause.Pause();               // 仅设置逻辑暂停标志（供尊重它的系统参考）
        countdown?.Pause();              // 只暂停倒计时（BGM 不受影响）

        _showing = true;
    }

    void CloseHint()
    {
        if (!_showing) return;
        _showing = false;
        hintRoot.SetActive(false);

        // 移除：Time.timeScale = 1f;  AudioListener.pause = false;
        // 只有最后一个提示关闭时，才恢复“逻辑暂停”
        s_activeHints = Mathf.Max(0, s_activeHints - 1);
        if (s_activeHints == 0) GamePause.Resume();

        // 恢复倒计时
        countdown?.Resume();

        if (showScope == Scope.PerSession) seenThisSession.Add(prefsKey);
        else { PlayerPrefs.SetInt(prefsKey, 1); PlayerPrefs.Save(); }

        Debug.Log("[TutorialHint] Hint closed by any key/click.");
    }
}