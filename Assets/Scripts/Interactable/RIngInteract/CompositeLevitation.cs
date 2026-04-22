using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class CompositeLevitation : MonoBehaviour, IRingInteractable
{
    [Header("组合悬浮设置")]
    [Tooltip("需要受到控制并悬浮的整体对象（必须挂载Rigidbody）")]
    public Rigidbody targetWhole;

    [Tooltip("上升的高度（米）")]
    public float riseHeight = 5f;
    
    [Tooltip("上升过程需要多少秒（时间越短速度越快）")]
    public float riseDuration = 1.0f;
    
    [Tooltip("在最高点停留多少秒")]
    public float hoverDuration = 2.0f;

    [Tooltip("勾选后允许与其他组件同时触发。不勾选则维持同一部件的互斥性（先碰到的生效）。")]
    public bool allowSimultaneous = false;

    [Tooltip("延迟同时触发：需要勾选上方选项。若勾选，同组的其他功能将在物体上升至指定高度（最高点）后才触发。")]
    public bool delaySimultaneous = false;

    [Header("状态")]
    [SerializeField] private bool isInteracting = false; // 防止重复触发

    // 实现接口：被圆环击中
    public void OnRingHit(ExpandingRing ring)
    {
        if (targetWhole == null)
        {
            Debug.LogWarning(gameObject.name + " 的 CompositeLevitation 未分配 targetWhole！");
            return;
        }

        // 如果正在上升或悬浮，就忽略这次撞击
        if (isInteracting) return;

        StartCoroutine(LevitateRoutine());
        Debug.Log(gameObject.name + " 触发了电梯逻辑，正在带动整体上升！");
    }

    // 核心流程：带动整个组合体上升 -> 悬浮 -> 掉落
    IEnumerator LevitateRoutine()
    {
        isInteracting = true;
        
        // 我们需要移动的目标Transform是整个组合体的根节点
        Transform wholeTransform = targetWhole.transform;

        // 记录整体的起飞点
        Vector3 startPos = wholeTransform.position;
        Vector3 targetPos = startPos + Vector3.up * riseHeight;

        // ------------------------------------------------
        // 第一阶段：上升 (由代码完全控制)
        // ------------------------------------------------
        targetWhole.isKinematic = true; // 暂时关掉物理，不受重力影响
        
        float timer = 0f;
        while (timer < riseDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / riseDuration; // 0 到 1 的进度
            
            // 使用 SmoothStep 让起步和停止稍微平滑一点
            float smoothProgress = Mathf.SmoothStep(0, 1, progress);

            // 移动整体物体
            wholeTransform.position = Vector3.Lerp(startPos, targetPos, smoothProgress);
            
            yield return null; // 等待下一帧
        }
        
        // 确保精准到达最高点
        wholeTransform.position = targetPos;

        // 如果开启了延迟同时触发，则在到达最高点后触发同组其他组件
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

        // ------------------------------------------------
        // 第二阶段：悬浮 (停留)
        // ------------------------------------------------
        yield return new WaitForSeconds(hoverDuration);

        // ------------------------------------------------
        // 第三阶段：掉落 (把控制权交还给重力)
        // ------------------------------------------------
        targetWhole.isKinematic = false; // 开启物理，重力生效
        targetWhole.WakeUp(); // 唤醒刚体，确保它立刻开始下落
        
        // 稍微给一个向下的初速度，手感更好（可选）
        targetWhole.velocity = Vector3.down * 4f; 

        // 流程结束，允许再次被触发
        yield return new WaitForSeconds(1f); 
        isInteracting = false;
    }

    // ------------------------------------------------
    // 可视化功能 (在编辑器的Scene视图预览悬浮轨迹)
    // ------------------------------------------------
    void OnDrawGizmosSelected()
    {
        if (targetWhole == null) return;

        Vector3 basePos = targetWhole.transform.position;
        Vector3 targetPos = basePos + Vector3.up * riseHeight;

        // 1. 画出最高点的虚影（黄色线框）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetPos, targetWhole.transform.localScale);

        // 2. 画出上升路径（虚线）
        Gizmos.color = Color.green;
        Gizmos.DrawLine(basePos, targetPos);

        #if UNITY_EDITOR
        string info = $"Overall Height: {riseHeight}m\nOverall Rise Time: {riseDuration}s\nOverall Hover: {hoverDuration}s";
        UnityEditor.Handles.Label(targetPos + Vector3.up * 1.5f, info);
        #endif
    }
}
