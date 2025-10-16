using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CameraShake2D : MonoBehaviour
{
    public static CameraShake2D I;  // 单例方便全局调用

    [Tooltip("要抖动的Transform（拖 Main Camera、BackgroundCamera 进来）")]
    public List<Transform> targets = new List<Transform>();

    [Tooltip("抖动频率（Perlin 噪声采样速度）")]
    public float frequency = 30f;

    Vector3[] _origPos;

    void Awake()
    {
        I = this;
        CacheOriginals();
    }

    void OnValidate()
    {
        if (Application.isPlaying) CacheOriginals();
    }

    void CacheOriginals()
    {
        if (targets == null) targets = new List<Transform>();
        _origPos = new Vector3[targets.Count];
        for (int i = 0; i < targets.Count; i++)
            _origPos[i] = targets[i] ? targets[i].localPosition : Vector3.zero;
    }

    public void Shake(float amplitude = 0.08f, float duration = 0.12f)
    {
        if (!isActiveAndEnabled || targets.Count == 0) return;
        StopAllCoroutines();
        StartCoroutine(DoShake(amplitude, duration));
    }

    IEnumerator DoShake(float amp, float dur)
    {
        float t = 0f;
        float seedX = Random.value * 100f;
        float seedY = Random.value * 100f;

        // 记录初始位置（防止运行时改过 targets）
        for (int i = 0; i < targets.Count; i++)
            if (targets[i]) _origPos[i] = targets[i].localPosition;

        while (t < dur)
        {
            float n = t * frequency;
            float offsetX = (Mathf.PerlinNoise(seedX, n) * 2f - 1f) * amp;
            float offsetY = (Mathf.PerlinNoise(seedY, n) * 2f - 1f) * amp;

            for (int i = 0; i < targets.Count; i++)
                if (targets[i])
                    targets[i].localPosition = _origPos[i] + new Vector3(offsetX, offsetY, 0f);

            t += Time.unscaledDeltaTime; // 不受 TimeScale 影响
            yield return null;
        }

        // 复位
        for (int i = 0; i < targets.Count; i++)
            if (targets[i]) targets[i].localPosition = _origPos[i];
    }
}