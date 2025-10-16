using UnityEngine;

/// <summary>
/// 全局移动速度管理器：按时间间隔提升速度（例如每30秒+0.8），直到上限。
/// 放在场景中唯一的一个对象上（DontDestroyOnLoad可选）。 
/// 任何新积木在 StartHover() 时从这里获取当前速度。
/// </summary>
public class GameSpeedManager : MonoBehaviour
{
    public static GameSpeedManager Instance { get; private set; }

    [Header("初始基础速度")]
    public float baseMoveSpeed = 3f;

    [Header("每个时间间隔提升的速度")]
    public float speedPerInterval = 0.8f;

    [Header("时间间隔（秒）")]
    [Min(0.01f)] public float intervalSeconds = 30f;

    [Header("最大速度上限")]
    public float maxMoveSpeed = 9f;

    [Header("是否跨场景保留")]
    public bool dontDestroyOnLoad = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 返回“此刻”应使用的全局移动速度。
    /// 规则：基础速度 + (floor(已用时/间隔) * 每次提升)，且不超过上限。
    /// </summary>
    public float GetCurrentMoveSpeed()
    {
        float elapsed = Time.timeSinceLevelLoad;
        int intervals = Mathf.Max(0, Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, intervalSeconds)));
        float target = baseMoveSpeed + intervals * speedPerInterval;
        return Mathf.Min(target, maxMoveSpeed);
    }
}