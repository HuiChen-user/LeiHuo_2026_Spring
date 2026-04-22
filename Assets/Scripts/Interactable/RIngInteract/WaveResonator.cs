using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaveResonator : MonoBehaviour, IRingInteractable
{
    [Header("Resonator Settings")]
    [Tooltip("生成的波的预制体")]
    public GameObject ringPrefab;
    [Tooltip("新波的传播速度")]
    public float resonatedSpeed = 5f;
    [Tooltip("新波的最大传播范围")]
    public float resonatedMaxRadius = 10f;
    [Tooltip("新波的颜色（通过颜色深浅等可视化速度）")]
    public Color resonatedColor = Color.green;

    [Header("Direction Settings")]
    [Tooltip("是否使用自定义发波朝向（否则默认贴近地面）")]
    public bool useCustomDirection = false;
    [Tooltip("自定义的波传播法线方向")]
    public Vector3 customNormal = Vector3.up;

    [Header("Ground Placement")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 2.0f;

    [Header("Cooldown Settings")]
    [Tooltip("共鸣触发的冷却时间（秒）")]
    public float cooldownTime = 1.0f;

    private bool hasResonated = false; // 防止同一波或者短时间内密集波疯狂触发

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    public void OnRingHit(ExpandingRing ring)
    {
        if (hasResonated) return;
        hasResonated = true; 
        
        SpawnResonatedRing();
        Debug.Log(gameObject.name + " 共鸣了！生成了新的波。");

        // 一段时间后重置标记，允许下一次共鸣
        if (cooldownTime > 0)
        {
            Invoke(nameof(ResetResonance), cooldownTime);
        }
        else
        {
            ResetResonance();
        }
    }

    private void ResetResonance()
    {
        hasResonated = false;
    }

    private void SpawnResonatedRing()
    {
        if (ringPrefab == null) return;

        Quaternion spawnRotation = Quaternion.identity;
        Vector3 spawnPosition = transform.position;

        if (useCustomDirection)
        {
            // 使用用户自定义的法线方向
            spawnRotation = Quaternion.FromToRotation(Vector3.up, customNormal.normalized);
            // 自定义朝向时，可以直接在中心生成，或者你可以根据需求微调位置
            spawnPosition = transform.position; 
        }
        else
        {
            // 向下检测地面，确保贴合地形发射
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out hit, groundCheckDistance, groundLayer))
            {
                spawnRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                spawnPosition = hit.point + hit.normal * 0.1f;
            }
        }

        GameObject newRingObj = Instantiate(ringPrefab, spawnPosition, spawnRotation);
        ExpandingRing newRing = newRingObj.GetComponent<ExpandingRing>();

        if (newRing != null)
        {
            // 初始化新生波的速度、范围和颜色，实现要求中的可视化效果
            newRing.InitializeRing(resonatedSpeed, resonatedMaxRadius, resonatedColor);
        }
    }

    private void OnDrawGizmos()
    {
        // 在Scene窗口可视化共鸣器即将产生的波的范围与速度（颜色暗示）
        // 半透明绘制最大范围
        Gizmos.color = new Color(resonatedColor.r, resonatedColor.g, resonatedColor.b, 0.4f);
        Gizmos.DrawWireSphere(transform.position, resonatedMaxRadius);
        
        // 画一个中心方块，颜色也相同，暗示共鸣源
        Gizmos.DrawCube(transform.position, Vector3.one * 0.8f);

        // --- 画出波传播平面的法线指示（朝向） ---
        Vector3 normalDirection = Vector3.up;
        Vector3 startPoint = transform.position;

        if (useCustomDirection)
        {
            normalDirection = customNormal.normalized;
        }
        else
        {
            // 尝试模拟原本的地面检测朝向
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer))
            {
                normalDirection = hit.normal;
                startPoint = hit.point;
            }
        }

        // 用一条醒目的红线表示法线（根据你的最大范围决定线长，或者固定2米）
        float lineLength = Mathf.Clamp(resonatedMaxRadius * 0.5f, 1f, 5f);
        Vector3 endPoint = startPoint + normalDirection * lineLength;

        // 画直线
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPoint, endPoint);
        
        // 在线段末端画个小球充当“箭头”顶端
        Gizmos.DrawSphere(endPoint, 0.2f);
    }
}
