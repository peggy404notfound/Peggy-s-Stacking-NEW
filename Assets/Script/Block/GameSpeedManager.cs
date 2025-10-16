using UnityEngine;

/// <summary>
/// ȫ���ƶ��ٶȹ���������ʱ���������ٶȣ�����ÿ30��+0.8����ֱ�����ޡ�
/// ���ڳ�����Ψһ��һ�������ϣ�DontDestroyOnLoad��ѡ���� 
/// �κ��»�ľ�� StartHover() ʱ�������ȡ��ǰ�ٶȡ�
/// </summary>
public class GameSpeedManager : MonoBehaviour
{
    public static GameSpeedManager Instance { get; private set; }

    [Header("��ʼ�����ٶ�")]
    public float baseMoveSpeed = 3f;

    [Header("ÿ��ʱ�����������ٶ�")]
    public float speedPerInterval = 0.8f;

    [Header("ʱ�������룩")]
    [Min(0.01f)] public float intervalSeconds = 30f;

    [Header("����ٶ�����")]
    public float maxMoveSpeed = 9f;

    [Header("�Ƿ�糡������")]
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
    /// ���ء��˿̡�Ӧʹ�õ�ȫ���ƶ��ٶȡ�
    /// ���򣺻����ٶ� + (floor(����ʱ/���) * ÿ������)���Ҳ��������ޡ�
    /// </summary>
    public float GetCurrentMoveSpeed()
    {
        float elapsed = Time.timeSinceLevelLoad;
        int intervals = Mathf.Max(0, Mathf.FloorToInt(elapsed / Mathf.Max(0.01f, intervalSeconds)));
        float target = baseMoveSpeed + intervals * speedPerInterval;
        return Mathf.Min(target, maxMoveSpeed);
    }
}