using UnityEngine;

[RequireComponent(typeof(Collider))]
public class CompositePush : MonoBehaviour, IRingInteractable
{
    [Header("组合推动设置")]
    [Tooltip("需要被推动的整体对象（必须挂载Rigidbody）")]
    public Rigidbody targetRigidbody;
    
    [Tooltip("推动力度")]
    public float pushForce = 10f;

    [Tooltip("是否允许被波命中时多部件同时触发（非互斥）")]
    public bool allowSimultaneous = false;

    [Header("互斥与冷却设置")]
    [Tooltip("是否启用同组互斥冷却。勾选后，波只会触发最先被命中的物体，规定时间内同组（共用targetRigidbody）的其他物体不会被触发。")]
    public bool enableExclusiveCooldown = false;

    [Tooltip("互斥触发的绝对冷却时间（秒）")]
    public float exclusiveCooldownTime = 2.0f;

    public void OnRingHit(ExpandingRing ring)
    {
        if (targetRigidbody == null)
        {
            Debug.LogWarning(gameObject.name + " 的 CompositePush 未分配 targetRigidbody！");
            return;
        }

        // 互斥冷却检查
        if (enableExclusiveCooldown)
        {
            CompositePushCooldown cooldownTracker = targetRigidbody.GetComponent<CompositePushCooldown>();
            if (cooldownTracker == null)
            {
                cooldownTracker = targetRigidbody.gameObject.AddComponent<CompositePushCooldown>();
                // 不在 Inspector 中显示该临时组件
                cooldownTracker.hideFlags = HideFlags.HideInInspector;
            }

            if (Time.time < cooldownTracker.lastTriggerTime + exclusiveCooldownTime)
            {
                // 还在规定时间内，绝对不会触发
                Debug.Log($"{gameObject.name} 处于组合冷却时间内，拒绝触发。");
                return;
            }

            // 更新触发时间
            cooldownTracker.lastTriggerTime = Time.time;
        }

        Collider col = GetComponent<Collider>();

        Vector3 ringCenter = ring.transform.position;
        
        // 1. 找到受力表面离圆心最近的点（这才是真正的“受力点”）
        Vector3 closestPoint = col.ClosestPoint(ringCenter);

        // 2. 拍扁高度（只取水平面）
        Vector3 flatHitPoint = new Vector3(closestPoint.x, 0, closestPoint.z);
        Vector3 flatRingPos = new Vector3(ringCenter.x, 0, ringCenter.z);

        // 3. 计算从“圆心”指向“受力点”的方向
        Vector3 direction = (flatHitPoint - flatRingPos).normalized;

        // 保护机制：如果圆心就在物体内部，direction可能会变成0，这时退回到用中心点
        if (direction == Vector3.zero)
        {
            Vector3 flatBoxCenter = new Vector3(transform.position.x, 0, transform.position.z);
            direction = (flatBoxCenter - flatRingPos).normalized;
        }

        // 获取整体的 Rigidbody 并施加力
        // 注意：推动的是 targetRigidbody 这个整体，而不是当前子物体
        targetRigidbody.AddForce(direction * pushForce, ForceMode.Impulse);
        
        // (可选) 为了调试，画出受力方向看看
        Debug.DrawLine(flatRingPos, flatHitPoint, Color.red, 2f);
        Debug.Log(gameObject.name + " 受到波的冲击，推动了整个组合物体！");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!enableExclusiveCooldown || targetRigidbody == null) return;

        bool inCooldown = false;
        float remainingTime = 0f;

        if (Application.isPlaying)
        {
            CompositePushCooldown tracker = targetRigidbody.GetComponent<CompositePushCooldown>();
            if (tracker != null)
            {
                remainingTime = (tracker.lastTriggerTime + exclusiveCooldownTime) - Time.time;
                if (remainingTime > 0) inCooldown = true;
            }
        }

        // 可视化：若处于冷却中，绘制红色球体警告；否则绘制绿色，表示准备就绪
        Gizmos.color = inCooldown ? new Color(1f, 0f, 0f, 0.5f) : new Color(0f, 1f, 0f, 0.3f);
        Vector3 pos = transform.position + Vector3.up * 1f; // 在物体上方一点显示
        Gizmos.DrawSphere(pos, 0.3f);

        // 如果在运行状态且处于冷却中，可以通过 GUI Label 渲染剩余时间
        if (inCooldown)
        {
            UnityEditor.Handles.Label(pos + Vector3.up * 0.5f, $"冷却中: {remainingTime:F1}s");
        }
    }
#endif
}

/// <summary>
/// 内部用于追踪共享 Rigidbody 的冷却时间的小组件
/// </summary>
public class CompositePushCooldown : MonoBehaviour
{
    public float lastTriggerTime = -1000f; // 初始赋负值以确保第一次必定触发
}
