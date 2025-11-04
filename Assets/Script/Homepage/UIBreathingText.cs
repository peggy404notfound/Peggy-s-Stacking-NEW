using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class UIBreathingText : MonoBehaviour
{
    [Header("缩放参数")]
    public float scaleMin = 0.95f;   // 最小缩放
    public float scaleMax = 1.05f;   // 最大缩放
    public float speed = 2f;         // 呼吸速度（次数/秒）

    private RectTransform rectTransform;
    private float t;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        t = Random.Range(0f, Mathf.PI * 2f); // 随机初始相位，避免多个一起闪时同步
    }

    void Update()
    {
        // 用 sin 波规律变化
        t += Time.unscaledDeltaTime * speed;
        float scale = Mathf.Lerp(scaleMin, scaleMax, (Mathf.Sin(t) + 1f) / 2f);
        rectTransform.localScale = Vector3.one * scale;
    }
}