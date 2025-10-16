using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HighlightHalo : MonoBehaviour
{
    public float scale = 1.08f;
    public float alpha = 0.5f;
    public Color color = Color.white;
    public int sortingOffset = -1; // 放到本体下层

    SpriteRenderer _sr, _haloSr;
    GameObject _halo;
    Vector3 _orig;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _orig = transform.localScale;
    }

    void EnsureHalo()
    {
        if (_halo != null) return;
        _halo = new GameObject("Halo");
        _halo.transform.SetParent(transform, false);
        _haloSr = _halo.AddComponent<SpriteRenderer>();
        _haloSr.sprite = _sr.sprite;
        _haloSr.sortingLayerID = _sr.sortingLayerID;
        _haloSr.sortingOrder = _sr.sortingOrder + sortingOffset;
        var c = color; c.a = alpha;
        _haloSr.color = c;
        _halo.SetActive(false);
    }

    public void SetHighlight(bool on)
    {
        EnsureHalo();
        if (on)
        {
            _haloSr.sprite = _sr.sprite;            // 若运行时换图，保持一致
            _haloSr.sortingLayerID = _sr.sortingLayerID;
            _haloSr.sortingOrder = _sr.sortingOrder + sortingOffset;
            _halo.SetActive(true);
            transform.localScale = _orig * scale;
        }
        else
        {
            if (_halo) _halo.SetActive(false);
            transform.localScale = _orig;
        }
    }
}