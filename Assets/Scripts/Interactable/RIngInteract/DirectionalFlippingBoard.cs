using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class DirectionalFlippingBoard : MonoBehaviour, IRingInteractable
{
    [Header("Configuration")]
    [Tooltip("The specific edge point around which the board will flip. Highly recommended for flat boards!")]
    public Transform pivotPoint;

    [Tooltip("The local direction the wave should hit from to trigger the flip (e.g., Forward = 0,0,1).")]
    public Vector3 hitDirection = Vector3.forward;

    [Tooltip("The rotational force (Torque) applied to flip the board.")]
    public float flipTorque = 150f;

    [Tooltip("Max angle deviation allowed for the push to trigger the flip.")]
    [Range(0f, 90f)]
    public float hitToleranceAngle = 45f;

    [Header("Physics Settings")]
    [Tooltip("Mass of the board.")]
    public float mass = 10f;
    public float drag = 0.5f;
    public float angularDrag = 0.5f;

    private Rigidbody _rb;
    private bool _hasFlipped = false;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        SetupPhysics();
        SetupHingeJoint();
    }

    private void SetupPhysics()
    {
        _rb.mass = mass;
        _rb.drag = drag;
        _rb.angularDrag = angularDrag;
        
        _rb.isKinematic = false; 
        _rb.useGravity = true;
        
        // 使用约束彻底冻结它来代替 isKinematic 防止穿模撕裂
        _rb.constraints = RigidbodyConstraints.FreezeAll; 
    }

    private void SetupHingeJoint()
    {
        // 1. 确定旋转点 (强推手动指定，否则尝试自动推算远端边缘)
        Vector3 pivotPos = pivotPoint != null ? pivotPoint.position : GetCalculatedEdgePivot();
        
        // 2. 添加铰链
        HingeJoint hinge = gameObject.AddComponent<HingeJoint>();
        hinge.anchor = transform.InverseTransformPoint(pivotPos);

        // 3. 确定旋转轴 (垂直于受击方向和向上方向的叉乘)
        Vector3 rotationAxis = Vector3.Cross(Vector3.up, hitDirection).normalized;
        hinge.axis = rotationAxis;

        // 4. 配置角度限制
        JointLimits limits = new JointLimits();
        limits.min = -5f;  // 允许轻微反弹
        limits.max = 170f; // 允许木板彻底翻过去 170 度平躺
        hinge.limits = limits;
        hinge.useLimits = true;
    }

    private Vector3 GetCalculatedEdgePivot()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Vector3 worldCenter = col.bounds.center;
            Vector3 worldHitDir = transform.TransformDirection(hitDirection).normalized;
            
            // 粗略计算：底部 + 顺着受力方向的边缘
            float maxExtent = Mathf.Max(col.bounds.extents.x, col.bounds.extents.z);
            return worldCenter - new Vector3(0, col.bounds.extents.y, 0) + worldHitDir * maxExtent;
        }
        return transform.position;
    }

    public void OnRingHit(ExpandingRing ring)
    {
        //Debug.Log($"【DirectionalFlippingBoard】 收到波浪打击！ 物体：{gameObject.name}");
        if (_hasFlipped) 
        {
            Debug.Log(" -> 已经被翻转过了，忽略。");
            return;
        }

        // 1. pushDir 是波浪推向物体的方向
        Vector3 pushDir = (transform.position - ring.transform.position).normalized;
        pushDir.y = 0; pushDir.Normalize();

        // 2. myHitDirWorld 是物体【本身朝向】设定的受击方向
        Vector3 myHitDirWorld = transform.TransformDirection(hitDirection).normalized;
        myHitDirWorld.y = 0; myHitDirWorld.Normalize();

        // 核心修复：木板感受到的“推力方向”必须和它“设定的受打方向”【同向】（即夹角接近0），
        // 才能被顺着推翻过去。
        float angle = Vector3.Angle(pushDir, myHitDirWorld);
        
        if (angle <= hitToleranceAngle)
        {
            TriggerFlip();
        }
    }

    private void TriggerFlip()
    {
        _hasFlipped = true;

        // 解除物理锁定
        _rb.constraints = RigidbodyConstraints.None;
        _rb.WakeUp();

        // 核心改动 1：直接施加纯粹的旋转力矩 (Torque)，无视扁平对象的力臂(极短)问题
        Vector3 worldRotationAxis = transform.TransformDirection(Vector3.Cross(Vector3.up, hitDirection).normalized);
        _rb.AddTorque(worldRotationAxis * flipTorque, ForceMode.Impulse);

        // 核心改动 2：辅助提升力。由于木板完全躺平，重心初始阶段很难越过死区
        // 我们给物体中心施加一个向上的脉冲力，帮助它“扬起来”
        _rb.AddForce(Vector3.up * (mass * 4f), ForceMode.Impulse);
        
        Debug.Log("11");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Vector3 pPos = transform.position;
        if (pivotPoint != null)
        {
            pPos = pivotPoint.position;
        }
        else
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                pPos = GetCalculatedEdgePivot();
            }
        }
        
        // 黄色球体表示旋转轴 (Pivot)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pPos, 0.1f);

        // 青色表示受击方向 (即波浪应该从哪个方向推过来)
        Vector3 dir = transform.TransformDirection(hitDirection).normalized;
        if (dir != Vector3.zero)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pPos, pPos - dir * 2f); // 反向画线，表示力的来源方向
            
            // 红色表示铰链旋转轴
            Vector3 axis = transform.TransformDirection(Vector3.Cross(Vector3.up, hitDirection).normalized);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pPos - axis, pPos + axis);
        }
    }
#endif
}
