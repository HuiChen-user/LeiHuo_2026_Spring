using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(LineRenderer))]
public class ExpandingRing : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("圆环扩大的速度")]
    public float expansionSpeed = 5f;
    
    [Tooltip("最大半径")]
    public float maxRadius = 10f;
    
    [Tooltip("圆环的精细度（点越多越圆）")]
    public int segments = 50;

    [Tooltip("圆环在Y轴(高度)上的可触发厚度(单面)，例如填2代表发波平面的上下各2米内有效")]
    public float yAxisLimit = 2f;

    [Tooltip("物体标签Layer")]
    public LayerMask targetLayer;
    
    [Header("Visuals")]
    [Tooltip("圆环初始颜色")]
    public Color normalColor = Color.white;
    [Tooltip("扩波后的圆环颜色")]
    public Color amplifiedColor = Color.yellow;

    private LineRenderer lineRenderer;
    private float currentRadius = 0f;
    private bool isAmplified = false;

    // 关键点：防止一个包含多个组件的组合物体被重复多次触发不同部位
    private HashSet<GameObject> hitRoots = new HashSet<GameObject>();
    
    // 标记圆环是否已经消散
    private bool isDissipated = false;
    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = segments + 1; // +1 是为了闭合圆环
        lineRenderer.useWorldSpace = false; // 使用本地坐标，跟随物体移动
        lineRenderer.loop = true;
        
        // 初始化颜色
        UpdateRingColor(normalColor);
    }
    
    private void UpdateRingColor(Color c)
    {
        if (lineRenderer == null) lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer.material == null) return;
        lineRenderer.startColor = c;
        lineRenderer.endColor = c;
        lineRenderer.material.color = c;
    }

    /// <summary>
    /// 初始化圆环的属性（用于其他物体生成新波时的定制）
    /// </summary>
    public void InitializeRing(float newSpeed, float newRadius, Color newColor)
    {
        expansionSpeed = newSpeed;
        maxRadius = newRadius;
        UpdateRingColor(newColor);
    }

    void Update()
    {
        // 如果圆环已经没了，就不跑任何逻辑了
        if (isDissipated) return;
        
        // 1. 增加半径
        currentRadius += expansionSpeed * Time.deltaTime;

        // 2. 检测并触发交互（核心逻辑）
        DetectAndInteract();
        
        
        // 3. 绘制圆环
        DrawCircle();
        
        // 4. 销毁判定
        if (currentRadius >= maxRadius)
        {
            Destroy(gameObject);
        }
    }

    void DetectAndInteract()
    {
        // 获取当前半径内的所有碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, currentRadius, targetLayer);

        var sortedHits = hits
            .Where(h => h != null)
            // 修复1：改用 ClosestPointOnBounds 替代 ClosestPoint，完美兼容 MeshCollider 从而避免报错中断
            .OrderBy(h => Vector3.Distance(transform.position, h.ClosestPointOnBounds(transform.position)))
            .ToArray();
        
        foreach (var hit in sortedHits)
        {
            if (isDissipated) break;
            
            // 关键：因为在前一个物体的交互（比如破碎逻辑里直接 Destroy(物体)）之后
            // 后面的碰撞体可能在这同一帧已经跟着父物体一起被销毁了！
            // 尝试访问被销毁的受害者会导致引擎底层的激烈报错（甚至影响 Editor UI绘制）
            if (hit == null || hit.gameObject == null) continue;

            // 新增：高度过滤 (重构为支持任意朝向发波)
            // 取出碰撞体表面距离波纹中心点最近的点
            Vector3 closestPoint = hit.ClosestPointOnBounds(transform.position);
            
            // 获知当前这股波浪的平面法线（因为我们允许WaveResonator改变它的transform.rotation）
            Vector3 waveNormal = transform.up;
            
            // 计算从波中心到该最近点的向量
            Vector3 vectorToPoint = closestPoint - transform.position;
            
            // 计算这个向量在波传播平面法线上的投影距离绝对值（即：物体真实距离当前波平面的“厚度”差）
            float thicknessDiff = Mathf.Abs(Vector3.Dot(vectorToPoint, waveNormal));
            
            // 如果超出了我们设定的容许厚度（即：物体太高或者太低），则无视此物体
            if (thicknessDiff > yAxisLimit) 
            {
                continue;
            }

            GameObject target = hit.gameObject;

            // 1. 获取这个部位所属的真正整体根节点
            GameObject rootIdentity = target;
            
            // 是否触发全组联动的标志
            bool triggerAll = false;
            IRingInteractable delayedPrimaryInteractable = null;
            
            // 为了摆脱必须依赖刚体(Rigidbody)的限制，我们直接向上查找组合配置组件（如 WaveDrivenMover）。
            // 只要找到了组合组件，我们就使用它的 "目标整体(targetWhole/targetRigidbody)" 作为真正的根节点。
            // 如果没填，为了确保同父级下的兄弟节点能被一起找到，我们会退而求其次试着拿它的父节点作为根。
            CompositeLevitation levitation = target.GetComponentInParent<CompositeLevitation>();
            if (levitation != null)
            {
                rootIdentity = levitation.targetWhole != null ? levitation.targetWhole.gameObject : (levitation.transform.parent != null ? levitation.transform.parent.gameObject : levitation.gameObject);
                if (levitation.allowSimultaneous)
                {
                    if (levitation.delaySimultaneous) delayedPrimaryInteractable = levitation;
                    else triggerAll = true;
                }
            }

            CompositePush pushComp = target.GetComponentInParent<CompositePush>();
            if (pushComp != null)
            {
                rootIdentity = pushComp.targetRigidbody != null ? pushComp.targetRigidbody.gameObject : (pushComp.transform.parent != null ? pushComp.transform.parent.gameObject : pushComp.gameObject);
                if (pushComp.allowSimultaneous)
                {
                    triggerAll = true;
                    delayedPrimaryInteractable = null;
                }
            }

            WaveDrivenMover waveMover = target.GetComponentInParent<WaveDrivenMover>();
            if (waveMover != null)
            {
                rootIdentity = waveMover.targetWhole != null ? waveMover.targetWhole.gameObject : (waveMover.transform.parent != null ? waveMover.transform.parent.gameObject : waveMover.gameObject);
                if (waveMover.allowSimultaneous)
                {
                    if (waveMover.delaySimultaneous)
                    {
                        delayedPrimaryInteractable = waveMover;
                        triggerAll = false;
                    }
                    else
                    {
                        triggerAll = true;
                        delayedPrimaryInteractable = null;
                    }
                }
            }
            
            // 如果上述三种特定的组合脚本都没有找到，此时才去回退查找刚体作为根节点的常规操作
            if (rootIdentity == target)
            {
                Rigidbody rb = target.GetComponentInParent<Rigidbody>();
                if (rb != null)
                {
                    rootIdentity = rb.gameObject; 
                }
            }

            // 2. 如果这个组合（根节点）已经和波发生过互动，
            // 无论是独立触发了一次还是全组触发了一次，都直接拉黑后续波对于该组合的其他部位碰撞
            if (hitRoots.Contains(rootIdentity)) continue;

            if (triggerAll)
            {
                // 全组触发模式：找出组合体下所有的交互组件
                IRingInteractable[] allInteractables = rootIdentity.GetComponentsInChildren<IRingInteractable>();
                
                if (allInteractables.Length > 0)
                {
                    // 标记这个整体已经完成了一次合法互动，拉黑它后续的所有零件检测保证只触发一次
                    hitRoots.Add(rootIdentity);

                    foreach (var interactable in allInteractables)
                    {
                        interactable.OnRingHit(this);

                        // 如果某个组件（如护盾）把波吸收消散了，就停止后续互动
                        if (this == null || isDissipated) return;
                    }
                }
            }
            else if (delayedPrimaryInteractable != null)
            {
                // 延迟联动的模式：只触发主导组件，主导组件稍后会触发其他组件
                hitRoots.Add(rootIdentity);
                delayedPrimaryInteractable.OnRingHit(this);

                // 如果某个组件把波吸收消散了，就停止后续互动
                if (this == null || isDissipated) return;
            }
            else
            {
                // 3. 独立触发模式：仅获取受击部位自身及直系父辈逻辑
                IRingInteractable[] interactables = target.GetComponentsInParent<IRingInteractable>();
                
                if (interactables.Length > 0)
                {
                    // 标记这个整体已经完成了一次合法互动
                    hitRoots.Add(rootIdentity);

                    // 4. 只执行“首当其冲”的最先接触部位的第一个逻辑
                    interactables[0].OnRingHit(this);

                    // 如果圆环已经被护盾/障碍物吸收消散，停止后续波群扩展
                    if (this == null || isDissipated) return; 
                }
            }
        }
    }
    
    public void Dissipate()
    {
        if (isDissipated) return;

        isDissipated = true; // 标记死亡
        
        // 这里以后可以加“消散特效”，比如播放一个噗呲的声音或粒子
        Debug.Log("圆环撞到了阻碍物，消散了！");
        
        Destroy(gameObject);
    }
    void DrawCircle()
    {
        if (isDissipated) return;
        
        float angleStep = 360f / segments;
        
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            
            // 计算 x 和 z (在水平面上画圆)
            float x = Mathf.Cos(angle) * currentRadius;
            float z = Mathf.Sin(angle) * currentRadius;

            lineRenderer.SetPosition(i, new Vector3(x, 0, z));
        }
    }

    /// <summary>
    /// 被扩波器放大
    /// </summary>
    public void Amplify(float speedMultiplier, float maxRadiusMultiplier)
    {
        // 允许被不同的扩波器多次叠加（已有互斥机制保证不会被同一扩波器疯狂连触发）
        isAmplified = true;

        expansionSpeed *= speedMultiplier;
        maxRadius *= maxRadiusMultiplier;
        
        // 视觉上表现出速度和范围被增强（换色）
        UpdateRingColor(amplifiedColor);
        
        Debug.Log($"圆环经过扩波器增强叠加！当前速度: {expansionSpeed}, 最大范围激增至: {maxRadius}");
    }

    private void OnDrawGizmos()
    {
        // 可视化：在编辑器的Scene视图中画出这个波的最大传播范围
        Gizmos.color = isAmplified ? new Color(1f, 0.9f, 0f, 0.5f) : new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, maxRadius);
    }
}
