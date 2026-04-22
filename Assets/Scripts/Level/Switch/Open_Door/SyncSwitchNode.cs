using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Renderer))] // 需要有一个 Renderer 用来变颜色
public class SyncSwitchNode : MonoBehaviour, IRingInteractable
{
    [Header("所属机关组")]
    [Tooltip("对应的管理器对象。必须分配，否则该节点不起作用。")]
    public SyncSwitchManager manager;

    [Header("颜色反馈设置")]
    [Tooltip("未点亮时的默认颜色")]
    public Color idleColor = Color.gray;
    
    [Tooltip("刚点亮但仍处于倒计时(未解开)时的闪烁颜色A")]
    public Color blinkColorA = Color.yellow;
    
    [Tooltip("刚点亮但仍处于倒计时(未解开)时的闪烁颜色B")]
    public Color blinkColorB = new Color(1f, 0.5f, 0f); // 橙红色
    
    [Tooltip("闪烁呼吸的速度")]
    public float blinkSpeed = 5f;

    [Tooltip("所有开关都成功点亮，机关解开后的锁定常亮颜色")]
    public Color successColor = Color.green;

    [Header("状态监控")]
    public bool isActivated = false;

    private Renderer meshRenderer;
    private Material matInstance; // 防止直接修改共享材质导致所有同款物体变色

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        // 获取材质实例，确保自己变色不影响别人
        if (meshRenderer != null)
        {
            matInstance = meshRenderer.material;
            SetColor(idleColor); // 初始化颜色
        }
    }

    private void Start()
    {
        // 游戏开始自动向分配的管理器报到
        if (manager != null)
        {
            if (!manager.allNodes.Contains(this))
            {
                manager.allNodes.Add(this);
            }
        }
        else
        {
            Debug.LogWarning($"[SyncSwitch] 子开关 {gameObject.name} 没有分配 Manager！");
        }
    }

    private void Update()
    {
        if (meshRenderer == null || matInstance == null) return;

        // 如果已被激活
        if (isActivated)
        {
            // 如果管理器宣告了“大局已定(成功)”
            if (manager != null && manager.isSolved)
            {
                // 变成成功常亮色！
                SetColor(successColor);
            }
            else
            {
                // 还在倒计时中！在两个颜色之间像呼吸灯一样 PingPong 闪烁
                float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);
                Color currentColor = Color.Lerp(blinkColorA, blinkColorB, t);
                SetColor(currentColor);
            }
        }
        else
        {
            // 没激活就保持沉寂色
            SetColor(idleColor);
        }
    }

    private void SetColor(Color color)
    {
        if (matInstance != null)
        {
            matInstance.color = color;
            // 如果你使用了 URP 或者 Emission，这里可以开启关键词修改发射颜色
            // matInstance.SetColor("_EmissionColor", color);
        }
    }

    /// <summary>
    /// 被主角发出的波击中时触发
    /// </summary>
    public void OnRingHit(ExpandingRing ring)
    {
        // 如果没有分配管理器，或者自己已经激活了，不要理会
        if (manager == null || isActivated) return;

        // 激活自己
        isActivated = true;
        Debug.Log($"[SyncSwitch] 子开关 {gameObject.name} 被波击中点亮！进入闪烁状态！");
        
        // 汇报给上级
        manager.NotifySwitchHit();
    }

    /// <summary>
    /// 倒计时失败，被主管强制重置
    /// </summary>
    public void ResetSwitch()
    {
        isActivated = false; // 关闭状态
        // 颜色在 Update 中会自动被改回 idleColor
    }

    /// <summary>
    /// 可视化指示（在不运行游戏时方便查看位置和判定框）
    /// </summary>
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        Vector3 displayPos = col != null ? col.bounds.center : transform.position;

        if (isActivated)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); 
            Gizmos.DrawCube(displayPos, transform.localScale * 1.05f);
        }
        else
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireCube(displayPos, transform.localScale * 1.05f);
        }
    }
}
