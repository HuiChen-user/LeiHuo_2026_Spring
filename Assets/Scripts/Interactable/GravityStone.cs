using UnityEngine;

public class GravityStone : MonoBehaviour
{
    [Header("颜色设置")]
    [Tooltip("普通状态下的颜色")]
    public Color normalColor = Color.gray;
    [Tooltip("被瞄准时的颜色")]
    public Color highlightedColor = Color.cyan;

    private Renderer _renderer;
    private Material _materialInstance; // 使用材质实例，防止修改到原始材质球

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            // 获取材质实例，这样修改颜色不会影响到项目里的其他物体
            _materialInstance = _renderer.material;
            _materialInstance.color = normalColor;
        }
    }

    // 提供给外部调用的方法，用于切换高亮状态
    public void SetHighlight(bool isHighlighted)
    {
        if (_materialInstance != null)
        {
            _materialInstance.color = isHighlighted ? highlightedColor : normalColor;
        }
    }
}
