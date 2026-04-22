using UnityEngine;

public class PlayerRingSkill : MonoBehaviour
{
    public GameObject ringPrefab; // 拖入上面做好的 Prefab
    
    [Header("预览设置")]
    public float previewMaxRadius = 10f; // 仅用于Gizmos显示

    public LayerMask groundLayer; // 记得设置这个！选 Default 或 Terrain
    public float groundCheckDistance = 2.0f; // 向下检测多远
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)|| Input.GetKeyDown(KeyCode.Q))
        {
            SpawnAlignedRing();
        }
    }

    // 实现“编辑器可视化调整”的核心代码
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        // 画出最大半径的线框球，方便你在编辑器里看范围
        Gizmos.DrawWireSphere(transform.position, previewMaxRadius);
    }
    
    void SpawnAlignedRing()
    {
        // 默认旋转是水平的（万一在空中，就发水平波）
        Quaternion spawnRotation = Quaternion.identity;
        Vector3 spawnPosition = transform.position;

        // 1. 向下发射射线检测地面
        RaycastHit hit;
        // 从角色中心向下射出射线
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance, groundLayer))
        {
            // 2. 核心魔法：计算旋转
            // Quaternion.FromToRotation(当前方向, 目标方向)
            // 把“正上方”对齐到“地面的法线方向”
            spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            // 3. 微调位置：把生成点放在脚底下的地面上，并稍微抬高一点点(0.1米)防止穿模
            spawnPosition = hit.point + hit.normal * 0.1f;
        }

        // 4. 生成圆环
        Instantiate(ringPrefab, spawnPosition, spawnRotation);
    }
}
