using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class GravityDropObject : MonoBehaviour, IRingInteractable
{
    public enum DropTriggerType
    {
        WaveOnly,
        CollisionOnly,
        Both
    }

    [Header("配置")]
    [Tooltip("掉落的触发方式。\nWaveOnly: 仅波浪打击才会掉落\nCollisionOnly: 仅角色进入触发区域才会掉落\nBoth: 两种方式均可")]
    public DropTriggerType triggerType = DropTriggerType.Both;

    [Tooltip("可选项：将场景中任意带有碰撞体（需设为 IsTrigger）的空物体拖入此处。运行时它会自动作为本物体的触发器！如果留空，则使用物体自身的触发器。")]
    public Collider externalTriggerZone;

    [Tooltip("角色碰撞检测所认定的 Tag (默认 Player)")]
    public string playerTag = "Player";

    private Rigidbody _rb;
    private bool _hasDropped = false;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        
        // 初始状态：静止悬空
        _rb.useGravity = false;
        _rb.isKinematic = true; 

        // 运行时动态注入触发器监听脚本
        // 这样只需配置一个脚本，无需向用户暴露多余的脚本文件
        if (externalTriggerZone != null && (triggerType == DropTriggerType.CollisionOnly || triggerType == DropTriggerType.Both))
        {
            var listener = externalTriggerZone.gameObject.AddComponent<GravityDropInternalListener>();
            listener.Init(this, playerTag);
        }
    }

    // 处理来自自身、子物体、或者被动态注入的外部触发器的碰撞事件
    public void HandleCollisionTrigger(string otherTag)
    {
        if (_hasDropped) return;

        if ((triggerType == DropTriggerType.CollisionOnly || triggerType == DropTriggerType.Both) &&
            otherTag == playerTag)
        {
            TriggerDrop();
        }
    }

    // 接收自身或子物体的触发 (当没有指定 externalTriggerZone 时作为后备方案)
    private void OnTriggerEnter(Collider other)
    {
        // 如果我们已经指定了专门的外部触发器，自身不再响应针对角色的碰撞检测，避免逻辑冲突
        if (externalTriggerZone != null) return;

        HandleCollisionTrigger(other.tag);
    }

    // 由波浪打击触发
    public void OnRingHit(ExpandingRing ring)
    {
        if (_hasDropped) return;

        if (triggerType == DropTriggerType.WaveOnly || triggerType == DropTriggerType.Both)
        {
            TriggerDrop();
        }
    }

    public void TriggerDrop()
    {
        if (_hasDropped) return;
        
        _hasDropped = true;

        // 解除运动学限制，并启用重力，自然下落
        _rb.isKinematic = false;
        _rb.useGravity = true;

        // Debug.Log($"【GravityDropObject】 {gameObject.name} 已触发掉落！");
    }
}

/// <summary>
/// 内部辅助类：运行时自动附加到用户在 inspector 指派的外部碰撞体上（空物体）。
/// 用于将碰撞事件透明地转发给 GravityDropObject，实现零额外脚本配置体验。
/// </summary>
public class GravityDropInternalListener : MonoBehaviour
{
    private GravityDropObject owner;
    private string targetTag;

    public void Init(GravityDropObject ownerObj, string tag)
    {
        owner = ownerObj;
        targetTag = tag;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner != null && other.CompareTag(targetTag))
        {
            owner.HandleCollisionTrigger(other.tag);
        }
    }
}
