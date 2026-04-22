using UnityEngine;

// 挂载在物体A（模型本体）上
public class PlayerTriggerBreakableObject : MonoBehaviour
{
    [Header("触发器绑定")]
    [Tooltip("把你在场景里创建好的触发空物体（物体B，需带有碰撞体组件）拖入此槽位中")]
    public Collider externalTrigger;

    [Tooltip("触发破碎的层级 (推荐勾选 Player 层，以优化性能。设为Everything则全检测)")]
    public LayerMask triggerLayers = ~0;
    
    [Tooltip("触发破碎的指定Tag (例如填写 Player，为空则不判断Tag)")]
    public string triggerTag = "Player";

    [Header("破碎设置")]
    [Tooltip("拖入做好的碎片预制体(Fractured Prefab)")]
    public GameObject fracturedPrefab;

    [Tooltip("爆炸力度 (建议 500-1000)")]
    public float explosionForce = 500f;

    [Tooltip("爆炸半径 (影响力的范围)")]
    public float explosionRadius = 5f;
    
    [Tooltip("向上抛洒的力 (让碎片稍微往上飞一点，效果更真实)")]
    public float upwardModifier = 2.0f;

    [Tooltip("碎片在几秒后自动消失")]
    public float debrisLifetime = 5f;

    private bool _isBroken = false;

    private void Start()
    {
        if (externalTrigger != null)
        {
            // 确保拖入的碰撞体是触发器
            if (!externalTrigger.isTrigger)
            {
                Debug.Log("检测到拖入的外部碰撞体未勾选 'Is Trigger'，已代码自动协助勾选。");
                externalTrigger.isTrigger = true;
            }

            // 运行游戏时，动态给外部触发器(物体B)塞入一个隐藏脚本，帮我们把触发事件转发回来
            var forwarder = externalTrigger.gameObject.AddComponent<BreakableTriggerForwarder>();
            forwarder.Initialize(this);
        }
        else
        {
            Debug.LogWarning(gameObject.name + ": 尚未给 externalTrigger 赋值，请把触发器物体B拖入该插槽中！");
        }
    }

    // 由辅助脚本从物体B传回来的触发事件
    public void OnForwardedTriggerEnter(Collider other)
    {
        if (_isBroken) return;

        // 验证层级 LayerMask 
        if (((1 << other.gameObject.layer) & triggerLayers) != 0)
        {
            // 验证 Tag
            if (string.IsNullOrEmpty(triggerTag) || other.CompareTag(triggerTag))
            {
                TriggerBreak();
            }
        }
    }

    private void TriggerBreak()
    {
        _isBroken = true;

        // 1. 生成碎片替身
        if (fracturedPrefab != null)
        {
            GameObject debris = Instantiate(fracturedPrefab, transform.position, transform.rotation);
            
            // 2. 找到碎片里所有的刚体 
            Rigidbody[] rbs = debris.GetComponentsInChildren<Rigidbody>();
            
            foreach (Rigidbody rb in rbs)
            {
                // 3. 施加爆炸力
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardModifier);
            }

            // 4. 设定碎片在几秒后自动销毁
            Destroy(debris, debrisLifetime);
        }
        else
        {
            Debug.LogWarning("未指定碎片预制体 (fracturedPrefab)，物体直接消失。");
        }

        // 5. 销毁本体 A
        Destroy(gameObject);
        
        Debug.Log("角色碰触到物体B触发器，物体A已破碎！");
    }

    private void OnDrawGizmos()
    {
        // 场景可视化：如果指定了外部触发器，画一条绿线连接它们，更方便直观看到“谁和谁绑定了”
        if (externalTrigger != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, externalTrigger.transform.position);
            
            // 为了更明显，还在触发器中心画一个小球
            Gizmos.DrawWireSphere(externalTrigger.bounds.center, 0.2f);
        }
    }
}

// --- 以下是一个内部辅助组件，不需要你手动挂载 ---
// 游戏运行时它会自动挂载到触发器B上，以此向主机A传递碰到角色的信息
public class BreakableTriggerForwarder : MonoBehaviour
{
    private PlayerTriggerBreakableObject _hostObject;

    public void Initialize(PlayerTriggerBreakableObject host)
    {
        _hostObject = host;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hostObject != null)
        {
            _hostObject.OnForwardedTriggerEnter(other);
        }
    }
}
