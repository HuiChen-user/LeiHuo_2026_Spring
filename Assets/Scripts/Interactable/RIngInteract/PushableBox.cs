using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PushableBox : MonoBehaviour, IRingInteractable // 注意这里继承了接口
{
    public float pushForce = 10f;

    // 实现接口强制要求的方法
    public void OnRingHit(ExpandingRing ring)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        Collider col = GetComponent<Collider>(); // 获取自身的碰撞器

        Vector3 ringCenter = ring.transform.position;
        
        // 新增检测：检测波的中心（通常是发出波的角色）是否正站在此物体上方
        // 方法：从波的中心点向上挪一点点，然后向下打射线。如果能打中自己，说明角色踩在头上。
        RaycastHit[] hits = Physics.RaycastAll(ringCenter + Vector3.up * 0.5f, Vector3.down, 1.5f);
        foreach (var hit in hits)
        {
            if (hit.collider == col)
            {
                // 波的源头正在箱子正上方，忽略这次推动（防止自己脚下的东西被推走掉下去）
                return;
            }
        }

        // 1. 找到箱子表面离圆心最近的点（这才是真正的“受力点”）
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

        rb.AddForce(direction * pushForce, ForceMode.Impulse);
        
        // (可选) 为了调试，画出受力方向看看
        Debug.DrawLine(flatRingPos, flatHitPoint, Color.red, 2f);
    }
}
