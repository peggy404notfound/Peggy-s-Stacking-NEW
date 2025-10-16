using System.Collections;
using UnityEngine;

public class WindGustSystem : MonoBehaviour
{
    public static WindGustSystem Instance { get; private set; }

    [Header("预制 & 锚点")]
    public WindZone2D windZonePrefab;
    public Transform leftAnchor;
    public Transform rightAnchor;

    [Header("风区尺寸与时序")]
    public Vector2 zoneSize = new Vector2(16f, 6.5f);
    public float warnTime = 0.5f;
    public float mainTime = 1.1f;
    public float coolTail = 0.3f;

    [Header("影响对象与强度")]
    public LayerMask targetLayers;
    public bool onlyAffectWhenMoving = true;
    public float baseStrength = 35f;
    public AnimationCurve strengthOverTime = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve horizontalFalloff = AnimationCurve.Linear(0, 1, 1, 0);

    // 刮风动画
    public Animator animatorWind;


    WindZone2D _runningZone;
    WindVisual2Frame _runningFx;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void PlayGustForOpponent(int userPlayerId)
    {
        StartCoroutine(Co_Play(userPlayerId));
    }

    IEnumerator Co_Play(int userPlayerId)
    {
        TurnManager.Instance?.LockTurnInput();
        if (warnTime > 0f) yield return new WaitForSeconds(warnTime);

        Vector2 dir = (userPlayerId == 1) ? Vector2.right : Vector2.left;

        // 计算风带中心（X取两锚中点微偏向风源，Y取spawn附近）
        float cx = (leftAnchor && rightAnchor)
            ? 0.5f * (leftAnchor.position.x + rightAnchor.position.x) - 0.5f * dir.x
            : 0f;
        float cy = 0f;
        if (TurnManager.Instance && TurnManager.Instance.spawnPoint)
            cy = TurnManager.Instance.spawnPoint.position.y - 2.0f;
        Vector2 center = new Vector2(cx, cy);

        // 物理区：生成或复用
        if (_runningZone == null)
            _runningZone = Instantiate(windZonePrefab);
        _runningZone.gameObject.SetActive(true);
        _runningZone.targetLayers = targetLayers;
        _runningZone.onlyAffectWhenMoving = onlyAffectWhenMoving;
        _runningZone.baseStrength = baseStrength;
        _runningZone.strengthOverTime = strengthOverTime;
        _runningZone.horizontalFalloff = horizontalFalloff;
        _runningZone.Play(center, zoneSize, dir, mainTime);

        // ★ 视觉：生成或复用 + 播放两帧动画（可放多个实例做层叠）
        if (animatorWind)
        {
            var go = Instantiate(animatorWind);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr) sr.flipX = (dir.x < 0f); // 右→左时水平翻转
            Destroy(go, mainTime + coolTail + 0.1f); // 风停后销毁
        }

        // 等待主风+收尾
        float wait = Mathf.Max(0f, mainTime) + Mathf.Max(0f, coolTail);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (_runningZone) _runningZone.gameObject.SetActive(false);
        if (_runningFx) _runningFx.gameObject.SetActive(false);

        TurnManager.Instance?.UnlockTurnInput();
    }
}