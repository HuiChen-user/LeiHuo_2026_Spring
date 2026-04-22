using UnityEngine;

/// <summary>
/// 简单的范围敌人清除脚本。
/// 挂载在玩家角色（或跟随玩家的物体）上，按下指定按键即可让范围内的敌人消失。
/// 遵循KISS原则：无多余依赖，即插即用。
/// </summary>
public class AreaEnemyDestroyer : MonoBehaviour
{
    [Header("范围与目标设置")]
    [Tooltip("检测敌人的半径范围")]
    public float destroyRadius = 10f;
    
    [Tooltip("敌人的专属标签，用于防止误删场景其他物体")]
    public string enemyTag = "Enemy";

    [Tooltip("物理层级过滤（可选），使用 LayerMask 可提高查询性能，默认检测全部层")]
    public LayerMask enemyLayer = ~0;

    [Header("按键触发")]
    [Tooltip("发动清除的快捷键")]
    public KeyCode triggerKey = KeyCode.T;
    
    [Header("调试")]
    [Tooltip("是否在场景视图中画出检测范围辅助线")]
    public bool showDebugArea = true;

    private void Update()
    {
        if (Input.GetKeyDown(triggerKey))
        {
            ClearEnemiesInRange();
        }
    }

    private void ClearEnemiesInRange()
    {
        // 核心思路：通过球形射线检测获取范围内的碰撞体
        Collider[] hits = Physics.OverlapSphere(transform.position, destroyRadius, enemyLayer);
        int clearedCount = 0;

        foreach (Collider col in hits)
        {
            // 如果物体存在且标签一致，判定为敌人
            if (col != null && col.CompareTag(enemyTag))
            {
                // 事实为本：直接销毁物体达到“消失”的最简判定
                Destroy(col.gameObject);
                clearedCount++;
            }
        }

        Debug.Log($"玩家按下了 {triggerKey} 键，在 {destroyRadius} 范围内清除了 {clearedCount} 个敌人！");
    }

    private void OnDrawGizmosSelected()
    {
        // 渐进式开发辅助：方便在编辑器直观看到半径大小以供调节
        if (showDebugArea)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, destroyRadius);
        }
    }
}
