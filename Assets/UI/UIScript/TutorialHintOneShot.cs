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
    public Scope showScope = Scope.PerSession;  // 默认：每次启动游戏弹一次

    [Header("是否在场景开始就自动弹")]
    public bool showOnSceneStart = false;

    // —— 会话内“已看过”的键集合（进程退出即清空）——
    private static readonly HashSet<string> seenThisSession = new HashSet<string>();

    bool _showing = false;

    void Start()
    {
        if (showOnSceneStart) TriggerIfNeeded();
    }

    void Update()
    {
        if (!_showing) return;

        // 按任意键或鼠标左键/触摸关闭
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            CloseHint();
        }
    }

    public void TriggerIfNeeded()
    {
        if (string.IsNullOrEmpty(prefsKey)) return;

        // 判定是否已看过
        bool seen =
            (showScope == Scope.PerSession && seenThisSession.Contains(prefsKey)) ||
            (showScope == Scope.PerDevice && PlayerPrefs.GetInt(prefsKey, 0) == 1);

        if (seen) return;

        if (!hintRoot)
        {
            Debug.LogWarning("[TutorialHintOneShot] hintRoot 未设置，跳过弹窗");
            return;
        }

        // 显示并置顶
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

        // 暂停游戏
        Time.timeScale = 0f;
        AudioListener.pause = true;

        _showing = true;
    }

    void CloseHint()
    {
        if (!_showing) return;

        _showing = false;
        hintRoot.SetActive(false);

        // 恢复游戏
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // 标记“已看过”
        if (showScope == Scope.PerSession)
            seenThisSession.Add(prefsKey);
        else
        {
            PlayerPrefs.SetInt(prefsKey, 1);
            PlayerPrefs.Save();
        }

        Debug.Log("[TutorialHint] Hint closed by any key/click.");
    }
}