using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class WaveAmplifier : MonoBehaviour, IRingInteractable
{
    [Header("Amplifier Settings")]
    [Tooltip("波速倍率（大于1代表加速）")]
    public float speedMultiplier = 1.5f;
    
    [Tooltip("波最大范围倍率（大于1代表扩圈）")]
    public float maxRadiusMultiplier = 1.5f;
    
    [Header("Visuals (Gizmo Only)")]
    [Tooltip("编辑器中可视化指示器的颜色")]
    public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.5f);

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public void OnRingHit(ExpandingRing ring)
    {
        // 调用波的增幅方法
        ring.Amplify(speedMultiplier, maxRadiusMultiplier);
        
        // 此处可添加自身特效，例如播放声音或粒子表现
        Debug.Log(gameObject.name + " 增幅了传播经过的波！");
    }

    private void OnDrawGizmos()
    {
        // 简单在扩波器自身周围画一个高亮框/球，说明它是能改变波属性的对象
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
        
        // 画一个向外的箭头表示"扩大"概念
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);
        Gizmos.DrawLine(transform.position + transform.forward * 2f, transform.position + transform.forward * 1.5f + transform.right * 0.5f);
        Gizmos.DrawLine(transform.position + transform.forward * 2f, transform.position + transform.forward * 1.5f - transform.right * 0.5f);
    }
}
