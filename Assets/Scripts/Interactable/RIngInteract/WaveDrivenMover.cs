using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class WaveDrivenMover : MonoBehaviour, IRingInteractable
{
    [Header("组合目标")]
    [Tooltip("选填：需要被整体移动的父物体。如果不填，默认移动当前自身。")]
    public Transform targetWhole;

    [Tooltip("勾选后允许与其他组件同时触发。不勾选则维持同一部件的互斥性（先碰到的生效）。")]
    public bool allowSimultaneous = false;

    [Tooltip("延迟同时触发：需要勾选上方选项。若勾选，同组的其他功能（如开门、变色）将在此物体到达目的地后再触发。")]
    public bool delaySimultaneous = false;

    [Header("位移设置")]
    [Tooltip("位移的方向和距离（局部坐标系下，基于整体对象的方向计算）")]
    public Vector3 localMoveOffset = new Vector3(0, 0, 5f);
    
    [Tooltip("位移速度（单位/秒）")]
    public float moveSpeed = 5f;

    [Header("可视化与平滑")]
    [Tooltip("是否开启往返(Ping-Pong)模式？如果勾选，再次触发时将返回原点。")]
    public bool isPingPong = false;
    
    [Tooltip("是否在游戏开始前允许波触发复位？(如果开启了往返模式，此选项将被忽略)")]
    public bool isOneShot = true;
    
    [Header("防循环冷却")]
    [Tooltip("到达目的地后，是否在一小段时间内自动取消勾选 Ping-Pong（防止被共鸣器等无限弹回）？")]
    public bool enablePingPongCooldown = true;
    
    [Tooltip("取消勾选 Ping-Pong 的时长（秒）")]
    public float pingPongCooldownDuration = 1.5f;

    [Tooltip("到达终点目标颜色的可视化线框")]
    public Color gizmoTargetColor = Color.green;
    [Tooltip("表示速度的路径指示线颜色")]
    public Color gizmoPathColor = Color.cyan;

    private bool _hasTriggered = false;
    
    // 固定的世界坐标 A点和B点
    private Vector3 _initialStartPos;
    private Vector3 _initialTargetPos;
    
    // 状态记录：当前是否正前往/已位于终点？
    private bool _isHeadingToTarget = false;
    // 防止高频鬼畜：记录当前是否正在移动中
    private bool _isMoving = false;

    private Transform GetActiveTarget()
    {
        return targetWhole != null ? targetWhole : transform;
    }

    private void Start()
    {
        Transform target = GetActiveTarget();
        _initialStartPos = target.position;
        _initialTargetPos = target.TransformPoint(localMoveOffset);
    }

    public void OnRingHit(ExpandingRing ring)
    {
        // 若不是往返模式且已触发过，并且是 OneShot，截断。
        if (!isPingPong && _hasTriggered && isOneShot) return;

        // 若物体正在移动，为了防止鬼畜卡死，我们通常忽略飞行中途被波击中（或者直接掉头，这里采用更稳定的忽略中途）
        if (_isMoving) return;

        _hasTriggered = true;
        Transform target = GetActiveTarget();

        // 确定这次真正要去的终点
        Vector3 currentDestination;
        
        if (isPingPong)
        {
            // 翻转状态：如果之前去了终点，现在就回起点；反之亦然。
            _isHeadingToTarget = !_isHeadingToTarget;
            currentDestination = _isHeadingToTarget ? _initialTargetPos : _initialStartPos;
        }
        else
        {
            // 单向模式：永远前往目标点
            currentDestination = _initialTargetPos;
            
            // 安全机制：如果已经到达该目标点，则直接返回，防止重复触发延迟联动的死循环
            if (Vector3.Distance(target.position, currentDestination) < 0.01f)
            {
                return;
            }
        }

        StopAllCoroutines();
        StartCoroutine(MoveToTargetCoroutine(target, currentDestination));

        Debug.Log($"{gameObject.name} (目标: {target.name}) 受到波的冲击，开始平滑位移前往 {(isPingPong && !_isHeadingToTarget ? "起点" : "终点")}！");
    }

    private IEnumerator MoveToTargetCoroutine(Transform target, Vector3 destination)
    {
        _isMoving = true;
        
        while (Vector3.Distance(target.position, destination) > 0.01f)
        {
            target.position = Vector3.MoveTowards(target.position, destination, moveSpeed * Time.deltaTime);
            yield return null;
        }
        
        target.position = destination; // 保证精准对齐
        _isMoving = false;

        // --- 防死循环：到达目的地后短时间内取消 IsPingPong 勾选 ---
        if (isPingPong && enablePingPongCooldown)
        {
            StartCoroutine(CooldownRoutine());
        }

        // 如果开启了延迟同时触发，则在到达目的地后触发同组其他组件
        if (allowSimultaneous && delaySimultaneous)
        {
            GameObject rootIdentity = targetWhole != null ? targetWhole.gameObject : (transform.parent != null ? transform.parent.gameObject : gameObject);
            IRingInteractable[] allInteractables = rootIdentity.GetComponentsInChildren<IRingInteractable>();
            foreach (var interactable in allInteractables)
            {
                if ((MonoBehaviour)interactable != this)
                {
                    interactable.OnRingHit(null);
                }
            }
        }
    }

    private IEnumerator CooldownRoutine()
    {
        isPingPong = false; // 暂时取消勾选
        yield return new WaitForSeconds(pingPongCooldownDuration);
        isPingPong = true;  // 恢复勾选
    }

    private void OnDrawGizmosSelected()
    {
        Transform target = targetWhole != null ? targetWhole : transform;
        
        // 1. 确定两端坐标
        Vector3 startP = Application.isPlaying ? _initialStartPos : target.position;
        Vector3 endP = Application.isPlaying ? _initialTargetPos : target.TransformPoint(localMoveOffset);

        // 2. 绘制目标的模型轮廓线框，让作者“能知道位置在哪儿”
        Gizmos.color = gizmoTargetColor;
        
        // 我们只在常驻的 B点(终点) 画线框作为预览，A点（起点）就是物体自身
        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
        
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Gizmos.DrawWireMesh(meshFilter.sharedMesh, endP, target.rotation, target.localScale);
        }
        else
        {
            Collider col = target.GetComponent<Collider>();
            if (col == null) col = GetComponent<Collider>();
            
            if (col != null)
            {
                Gizmos.DrawWireCube(endP, col.bounds.size);
            }
            else
            {
                Gizmos.DrawWireSphere(endP, 0.5f);
            }
        }

        // 3. 绘制路径长连线
        Gizmos.color = gizmoPathColor;
        Gizmos.DrawLine(startP, endP);

        // 4. 绘制路径上的“速度可视化”带方向箭头
        DrawSpeedMarkers(startP, endP);
    }

    private void DrawSpeedMarkers(Vector3 start, Vector3 end)
    {
        float distance = Vector3.Distance(start, end);
        if (distance <= 0.01f) return;

        Vector3 direction = (end - start).normalized;
        
        float timeInterval = 0.5f; 
        float distanceInterval = moveSpeed * timeInterval;
        
        if (distanceInterval <= 0.1f) distanceInterval = 0.1f; 

        int numMarkers = Mathf.FloorToInt(distance / distanceInterval);
        
        for (int i = 1; i <= numMarkers; i++)
        {
            Vector3 markerPos = start + direction * (i * distanceInterval);
            DrawArrowGizmo(markerPos, direction);
            
            // 如果开启了 Ping-Pong，我们在同一个位置画个反向箭头表示路径可逆
            if (isPingPong)
            {
                DrawArrowGizmo(markerPos, -direction);
            }
        }
    }

    private void DrawArrowGizmo(Vector3 pos, Vector3 direction)
    {
        float arrowHeadLength = 0.4f;
        float arrowHeadAngle = 20.0f;

        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);

        Gizmos.DrawRay(pos, right * arrowHeadLength);
        Gizmos.DrawRay(pos, left * arrowHeadLength);
        Gizmos.DrawRay(pos, direction * (arrowHeadLength * 0.5f)); 
    }
}
