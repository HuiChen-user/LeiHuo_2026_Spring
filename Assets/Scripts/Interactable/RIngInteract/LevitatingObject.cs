using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class LevitatingObject : MonoBehaviour, IRingInteractable
{
    [Header("悬浮设置")]
    [Tooltip("上升的高度（米）")]
    public float riseHeight = 5f;
    
    [Tooltip("上升过程需要多少秒（时间越短速度越快）")]
    public float riseDuration = 1.0f;
    
    [Tooltip("在最高点停留多少秒")]
    public float hoverDuration = 2.0f;

    [Header("状态")]
    [SerializeField] private bool isInteracting = false; // 防止重复触发

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // 实现接口：被圆环击中
    public void OnRingHit(ExpandingRing ring)
    {
        // 如果正在上升或悬浮，就忽略这次撞击
        if (isInteracting) return;

        StartCoroutine(LevitateRoutine());
    }

    // 核心流程：上升 -> 悬浮 -> 掉落
    IEnumerator LevitateRoutine()
    {
        isInteracting = true;
        
        // 记录此时此刻的位置作为起飞点（因为物体可能之前被推稍微移动了一点）
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.up * riseHeight;

        // ------------------------------------------------
        // 第一阶段：上升 (由代码完全控制)
        // ------------------------------------------------
        rb.isKinematic = true; // 暂时关掉物理，让它变成“幽灵”，不受重力影响，也不会被其他东西撞飞
        
        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / riseDuration; // 0 到 1 的进度
            
            // 使用 SmoothStep 让起步和停止稍微平滑一点，不像机器那样生硬
            float smoothProgress = Mathf.SmoothStep(0, 1, progress);

            // 移动物体
            transform.position = Vector3.Lerp(startPos, targetPos, smoothProgress);
            
            yield return null; // 等待下一帧
        }
        
        // 确保精准到达最高点
        transform.position = targetPos;

        // ------------------------------------------------
        // 第二阶段：悬浮 (停留)
        // ------------------------------------------------
        yield return new WaitForSeconds(hoverDuration);

        // ------------------------------------------------
        // 第三阶段：掉落 (把控制权交还给重力)
        // ------------------------------------------------
        rb.isKinematic = false; // 开启物理，重力生效
        rb.WakeUp(); // 唤醒刚体，确保它立刻开始下落
        
        // 稍微给一个向下的初速度，手感更好（可选）
         rb.velocity = Vector3.down * 2f; 

        // 流程结束，允许再次被触发
        // (如果你希望它落地稳住后才能再次触发，可以在这里加个简单的落地检测逻辑，
        //  或者简单地延时几秒设为 false)
        yield return new WaitForSeconds(1f); 
        isInteracting = false;
    }

    // ------------------------------------------------
    // 可视化功能 (满足你的编辑器预览需求)
    // ------------------------------------------------
    void OnDrawGizmosSelected()
    {
        Vector3 basePos = transform.position;
        Vector3 targetPos = basePos + Vector3.up * riseHeight;

        // 1. 画出最高点的虚影（黄色线框）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetPos, transform.localScale);

        // 2. 画出上升路径（虚线）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(basePos, targetPos);

        // 3. 画出文字标签 (可选，只在Scene视图显示文字)
        // 这里的文字能帮你直观看到上升时间和速度关系
        #if UNITY_EDITOR
        string info = $"Height: {riseHeight}m\nRise Time: {riseDuration}s\nHover: {hoverDuration}s";
        UnityEditor.Handles.Label(targetPos + Vector3.up * 1.5f, info);
        #endif
    }
}