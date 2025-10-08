using UnityEngine;

public class PropLifetimeByTurn : MonoBehaviour
{
    [Tooltip("还能存活多少个回合；每次 PropGeneration.NotifyNewRound 调用会减1")]
    public int lifeTurns = 1;

    [Tooltip("可选：销毁前的淡出时长")]
    public float fadeOutSeconds = 0.3f;

    public void OnRoundPassed()
    {
        lifeTurns--;
        if (lifeTurns <= 0 && gameObject.activeInHierarchy)
            StartCoroutine(FadeAndDestroy());
    }

    private System.Collections.IEnumerator FadeAndDestroy()
    {
        // 如果有 SpriteRenderer，就做个简单淡出；没有就直接销毁
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || fadeOutSeconds <= 0f)
        {
            Destroy(gameObject);
            yield break;
        }

        float t = 0f;
        Color c = sr.color;
        while (t < fadeOutSeconds)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / fadeOutSeconds);
            sr.color = c;
            yield return null;
        }
        Destroy(gameObject);
    }
}