using System.Collections;
using UnityEngine;

public class PreGameController : MonoBehaviour
{
    [Header("����")]
    public Animator animator;              // ���� 3-2-1-Go ������ UI Image
    public CountdownTimer mainTimer;       // ����ʱ����autoStart �رգ�
    public TurnManager turnManager;        // ���ƻ�ľ���ɵĽű�

    [Header("��������")]
    public string animationStateName = "PreGame";  // Animator ״̬��
    public AnimationClip preGameClip;              // ����ʱ���� clip
    public float animationLength = 3f;             // ��û clip���ֶ���

    [Header("��Ч")]
    public AudioSource sfxSource;
    public AudioClip sfxBeep;   // ��3��2��1��
    public AudioClip sfxGo;     // ��GO!��

    void Awake()
    {
        // Animator ʹ�� UnscaledTime ���ţ�������ͣӰ�죩
        if (animator) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (preGameClip) animationLength = preGameClip.length;

        // һ��ʼ�Ƚ��û�ľ���ɺ͵���ʱ
        if (turnManager) turnManager.enabled = false;
        if (mainTimer) mainTimer.Pause();
    }

    void Start()
    {
        // �Զ����ŵ���ʱ����
        StartCoroutine(PlayCountdownAndStartGame());
    }

    IEnumerator PlayCountdownAndStartGame()
    {
        // ���Ŷ���
        if (animator) animator.Play(animationStateName, 0, 0f);

        // �ȶ������꣨ʵ��ʱ�䣩
        yield return new WaitForSecondsRealtime(animationLength);

        // ���û�ľ���� + ����������ʱ
        if (turnManager) turnManager.enabled = true;
        if (mainTimer) mainTimer.StartTimer();
    }

    // === Animation Event ���� ===
    public void PlayBeep()
    {
        if (sfxSource && sfxBeep)
            sfxSource.PlayOneShot(sfxBeep);
    }

    public void PlayGo()
    {
        if (sfxSource && sfxGo)
            sfxSource.PlayOneShot(sfxGo);
    }
}