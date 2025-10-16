using System.Collections;
using UnityEngine;

/// <summary>
/// ����֡ͼƬ��һ�����׷綯������������
/// - �Զ��� warn/main/tail ����ʱ���ڣ������ѭ����֡������
/// - ����ݷ������ҷ�ת����ת
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class WindVisual2Frame : MonoBehaviour
{
    [Header("��֡��ͼ")]
    public Sprite frameA;
    public Sprite frameB;

    [Header("��������")]
    public float fps = 10f;             // ��֡�ٶ�
    public bool rotateByDirection = false; // false=ˮƽ+���ҷ�ת��true=��������ת

    [Header("���뵭��")]
    public float fadeIn = 0.15f;
    public float fadeOut = 0.2f;

    [Header("��΢λ�ƣ�Ӫ�������У���Ϊ0��")]
    public float driftSpeed = 2.5f;     // ���������ƶ��ٶȣ���λ/�룩
    public float driftAmplitudeY = 0.15f;

    SpriteRenderer _sr;
    Coroutine _co;
    Vector3 _startPos;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _sr.color = new Color(1, 1, 1, 0); // ��ʼ͸��
    }

    public void Play(Vector2 center, Vector2 dir, float warnTime, float mainTime, float tailTime)
    {
        if (_co != null) StopCoroutine(_co);
        transform.position = center;
        _startPos = transform.position;

        // ����
        if (rotateByDirection)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, ang);
            _sr.flipX = false;
        }
        else
        {
            transform.rotation = Quaternion.identity;
            _sr.flipX = (dir.x < 0f); // �ҡ���ʱˮƽ��ת
        }

        _co = StartCoroutine(Co_Run(warnTime, mainTime, tailTime));
    }

    IEnumerator Co_Run(float warnTime, float mainTime, float tailTime)
    {
        // ���루�� warn ��ִ�У�
        float t = 0f;
        while (t < fadeIn + Mathf.Max(0f, warnTime - fadeIn))
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeIn));
            SetAlpha(a);
            DoDrift();
            yield return null;
        }

        // ��ѭ����mainTime ���� fps ������֡��
        float timeMain = 0f;
        float frameTimer = 0f;
        bool toggle = false;
        while (timeMain < mainTime)
        {
            timeMain += Time.deltaTime;
            frameTimer += Time.deltaTime;

            if (frameTimer >= 1f / Mathf.Max(1f, fps))
            {
                frameTimer = 0f;
                toggle = !toggle;
                _sr.sprite = toggle ? frameA : frameB;
            }

            DoDrift();
            yield return null;
        }

        // ������tail �Σ�
        float t2 = 0f;
        Color c = _sr.color;
        while (t2 < Mathf.Max(fadeOut, tailTime))
        {
            t2 += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t2 / Mathf.Max(0.0001f, fadeOut));
            SetAlpha(a);
            DoDrift();
            yield return null;
        }

        gameObject.SetActive(false);
    }

    void DoDrift()
    {
        if (driftSpeed == 0f && driftAmplitudeY == 0f) return;
        var p = _startPos;
        p.x += Time.time * driftSpeed;
        p.y += Mathf.Sin(Time.time * 4f) * driftAmplitudeY;
        transform.position = p;
    }

    void SetAlpha(float a)
    {
        var c = _sr.color; c.a = a; _sr.color = c;
    }
}