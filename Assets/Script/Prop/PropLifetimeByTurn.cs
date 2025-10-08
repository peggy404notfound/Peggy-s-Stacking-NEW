using UnityEngine;

public class PropLifetimeByTurn : MonoBehaviour
{
    [Tooltip("���ܴ����ٸ��غϣ�ÿ�� PropGeneration.NotifyNewRound ���û��1")]
    public int lifeTurns = 1;

    [Tooltip("��ѡ������ǰ�ĵ���ʱ��")]
    public float fadeOutSeconds = 0.3f;

    public void OnRoundPassed()
    {
        lifeTurns--;
        if (lifeTurns <= 0 && gameObject.activeInHierarchy)
            StartCoroutine(FadeAndDestroy());
    }

    private System.Collections.IEnumerator FadeAndDestroy()
    {
        // ����� SpriteRenderer���������򵥵�����û�о�ֱ������
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