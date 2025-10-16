using UnityEngine;
using System.Collections.Generic;

public class TutorialHintOneShot : MonoBehaviour
{
    [Header("UI ����")]
    public GameObject hintRoot;

    [Header("Ψһ����ͬһ����ʾ����˼���")]
    public string prefsKey = "tut_key";

    public enum Scope { PerSession, PerDevice }
    [Header("����Ƶ��")]
    public Scope showScope = Scope.PerSession;  // Ĭ�ϣ�ÿ��������Ϸ��һ��

    [Header("�Ƿ��ڳ�����ʼ���Զ���")]
    public bool showOnSceneStart = false;

    // ���� �Ự�ڡ��ѿ������ļ����ϣ������˳�����գ�����
    private static readonly HashSet<string> seenThisSession = new HashSet<string>();

    bool _showing = false;

    void Start()
    {
        if (showOnSceneStart) TriggerIfNeeded();
    }

    void Update()
    {
        if (!_showing) return;

        // ���������������/�����ر�
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            CloseHint();
        }
    }

    public void TriggerIfNeeded()
    {
        if (string.IsNullOrEmpty(prefsKey)) return;

        // �ж��Ƿ��ѿ���
        bool seen =
            (showScope == Scope.PerSession && seenThisSession.Contains(prefsKey)) ||
            (showScope == Scope.PerDevice && PlayerPrefs.GetInt(prefsKey, 0) == 1);

        if (seen) return;

        if (!hintRoot)
        {
            Debug.LogWarning("[TutorialHintOneShot] hintRoot δ���ã���������");
            return;
        }

        // ��ʾ���ö�
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

        // ��ͣ��Ϸ
        Time.timeScale = 0f;
        AudioListener.pause = true;

        _showing = true;
    }

    void CloseHint()
    {
        if (!_showing) return;

        _showing = false;
        hintRoot.SetActive(false);

        // �ָ���Ϸ
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // ��ǡ��ѿ�����
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