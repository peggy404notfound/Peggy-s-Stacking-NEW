using System.Collections;
using UnityEngine;

public class PreGameController : MonoBehaviour
{
    [Header("引用")]
    public Animator animator;              // 控制 3-2-1-Go 动画的 UI Image
    public CountdownTimer mainTimer;       // 主计时器（autoStart 关闭）
    public TurnManager turnManager;        // 控制积木生成的脚本

    [Header("动画设置")]
    public string animationStateName = "PreGame";  // Animator 状态名
    public AnimationClip preGameClip;              // 倒计时动画 clip
    public float animationLength = 3f;             // 若没 clip，手动填

    [Header("音效")]
    public AudioSource sfxSource;
    public AudioClip sfxBeep;   // “3、2、1”
    public AudioClip sfxGo;     // “GO!”

    void Awake()
    {
        // Animator 使用 UnscaledTime 播放（不受暂停影响）
        if (animator) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (preGameClip) animationLength = preGameClip.length;

        // 一开始先禁用积木生成和倒计时
        if (turnManager) turnManager.enabled = false;
        if (mainTimer) mainTimer.Pause();
    }

    void Start()
    {
        // 自动播放倒计时动画
        StartCoroutine(PlayCountdownAndStartGame());
    }

    IEnumerator PlayCountdownAndStartGame()
    {
        // 播放动画
        if (animator) animator.Play(animationStateName, 0, 0f);

        // 等动画播完（实际时间）
        yield return new WaitForSecondsRealtime(animationLength);

        // 启用积木生成 + 启动主倒计时
        if (turnManager) turnManager.enabled = true;
        if (mainTimer) mainTimer.StartTimer();
    }

    // === Animation Event 调用 ===
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