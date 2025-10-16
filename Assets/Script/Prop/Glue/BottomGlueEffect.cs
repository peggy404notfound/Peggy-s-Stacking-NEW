using UnityEngine;

[DisallowMultipleComponent]
public class BottomGlueEffect : MonoBehaviour
{
    LayerMask _stickable;
    float _dotThreshold = 0.5f;
    bool _stuck = false;
    Rigidbody2D _rb;

    AudioClip _stickSfx;
    float _sfxVolume = 0.9f;
    GameObject _splashPrefab;

    public void Init(LayerMask stickable, float bottomDotThreshold,
                     AudioClip stickSfx, float sfxVolume, GameObject splashPrefab)
    {
        _stickable = stickable;
        _dotThreshold = Mathf.Clamp01(bottomDotThreshold);
        _stickSfx = stickSfx;
        _sfxVolume = sfxVolume;
        _splashPrefab = splashPrefab;
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (!_rb) Debug.LogWarning("[BottomGlueEffect] δ�ҵ� Rigidbody2D��");
    }

    void OnCollisionEnter2D(Collision2D col) { TryStick(col); }
    void OnCollisionStay2D(Collision2D col) { TryStick(col); }

    void TryStick(Collision2D col)
    {
        if (_stuck || !_rb) return;

        // ֻճ�ڡ�����㡱
        if (((1 << col.collider.gameObject.layer) & _stickable) == 0) return;

        // ֻ���ܡ��ײ��Ӵ������Ӵ����߳��ϣ��ӶԷ�ָ���ң�
        foreach (var c in col.contacts)
        {
            if (Vector2.Dot(c.normal, Vector2.up) >= _dotThreshold)
            {
                var fj = gameObject.AddComponent<FixedJoint2D>();
                fj.autoConfigureConnectedAnchor = true;
                fj.enableCollision = true;
                fj.breakForce = Mathf.Infinity;
                fj.breakTorque = Mathf.Infinity;

                var otherRb = col.rigidbody ?? col.collider.GetComponentInParent<Rigidbody2D>();
                fj.connectedBody = otherRb; // null = �������磨�����޸���ĵ�����

                _stuck = true;

                // �Ӿ�/��Ƶ����
                if (_stickSfx) AudioSource.PlayClipAtPoint(_stickSfx, c.point, _sfxVolume);
                if (_splashPrefab) Instantiate(_splashPrefab, c.point, Quaternion.identity);

                // ճס�󼴿��Ƴ��˽ű��������ؽڣ�
                Destroy(this);
                return;
            }
        }
    }
}