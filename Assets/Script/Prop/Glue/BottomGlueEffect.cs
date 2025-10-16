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
        if (!_rb) Debug.LogWarning("[BottomGlueEffect] 未找到 Rigidbody2D。");
    }

    void OnCollisionEnter2D(Collision2D col) { TryStick(col); }
    void OnCollisionStay2D(Collision2D col) { TryStick(col); }

    void TryStick(Collision2D col)
    {
        if (_stuck || !_rb) return;

        // 只粘在“允许层”
        if (((1 << col.collider.gameObject.layer) & _stickable) == 0) return;

        // 只接受“底部接触”：接触法线朝上（从对方指向我）
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
                fj.connectedBody = otherRb; // null = 连到世界（例如无刚体的底座）

                _stuck = true;

                // 视觉/音频反馈
                if (_stickSfx) AudioSource.PlayClipAtPoint(_stickSfx, c.point, _sfxVolume);
                if (_splashPrefab) Instantiate(_splashPrefab, c.point, Quaternion.identity);

                // 粘住后即可移除此脚本（保留关节）
                Destroy(this);
                return;
            }
        }
    }
}