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
    public Scope showScope = Scope.PerSession;

    [Header("�Ƿ��ڳ�����ʼ���Զ���")]
    public bool showOnSceneStart = false;

    // ��������ѡ�ĵ���ʱ���ã�û�Ͼ��Զ��ң�
    [Header("��ѡ��Ҫ��ͣ/�ָ��ĵ���ʱ")]
    public CountdownTimer countdown;

    private static readonly HashSet<string> seenThisSession = new HashSet<string>();
    private static int s_activeHints = 0; // �����������������������һ���ر�ʱ�ٻָ�
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

        if (!hintRoot) { Debug.LogWarning("[TutorialHintOneShot] hintRoot δ���ã���������"); return; }

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

        // �Ƴ���Time.timeScale = 0f;  AudioListener.pause = true;
        // ��Ϊ���߼���ͣ + ��ͣ����ʱ
        s_activeHints++;
        GamePause.Pause();               // �������߼���ͣ��־������������ϵͳ�ο���
        countdown?.Pause();              // ֻ��ͣ����ʱ��BGM ����Ӱ�죩

        _showing = true;
    }

    void CloseHint()
    {
        if (!_showing) return;
        _showing = false;
        hintRoot.SetActive(false);

        // �Ƴ���Time.timeScale = 1f;  AudioListener.pause = false;
        // ֻ�����һ����ʾ�ر�ʱ���Żָ����߼���ͣ��
        s_activeHints = Mathf.Max(0, s_activeHints - 1);
        if (s_activeHints == 0) GamePause.Resume();

        // �ָ�����ʱ
        countdown?.Resume();

        if (showScope == Scope.PerSession) seenThisSession.Add(prefsKey);
        else { PlayerPrefs.SetInt(prefsKey, 1); PlayerPrefs.Save(); }

        Debug.Log("[TutorialHint] Hint closed by any key/click.");
    }
}