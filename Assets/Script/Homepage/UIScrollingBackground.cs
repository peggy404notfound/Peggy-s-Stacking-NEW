using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class UIScrollingBackground : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.02f, 0f); // X 轴向右滚，改成(0,0.02)则向上
    private RawImage _ri;

    void Awake() { _ri = GetComponent<RawImage>(); }

    void Update()
    {
        // 用 unscaledDeltaTime，哪怕 Time.timeScale=0（比如菜单或暂停）也继续动
        var r = _ri.uvRect;
        r.position += speed * Time.unscaledDeltaTime;
        _ri.uvRect = r; // 超出会自动按 Repeat 循环
    }
}