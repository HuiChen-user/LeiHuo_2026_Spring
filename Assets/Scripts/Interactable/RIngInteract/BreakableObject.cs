using UnityEngine;

public class BreakableObject : MonoBehaviour, IRingInteractable
{
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

    public void OnRingHit(ExpandingRing ring)
    {
        // 1. 生成碎片替身 (位置和旋转要和本体一模一样)
        if (fracturedPrefab != null)
        {
            GameObject debris = Instantiate(fracturedPrefab, transform.position, transform.rotation);
            
            // 2. 找到碎片里所有的刚体 (每个小碎片都应该有个Rigidbody)
            Rigidbody[] rbs = debris.GetComponentsInChildren<Rigidbody>();
            
            foreach (Rigidbody rb in rbs)
            {
                // 3. 施加爆炸力
                // transform.position: 以物体自身中心为爆炸原点，向四周炸开
                // explosionRadius: 爆炸波及范围
                // upwardModifier: 把碎片“掀起来”的效果
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, upwardModifier);
                
                // (可选) 如果你想让碎片按照圆环冲击波的方向飞，可以用下面这行代替上面那行：
                // Vector3 direction = (transform.position - ringCenter).normalized;
                // rb.AddForce(direction * explosionForce, ForceMode.Impulse);
            }

            // 4. 设定碎片在几秒后自动销毁 (为了性能，不要留一地垃圾)
            Destroy(debris, debrisLifetime);
        }
        else
        {
            Debug.LogWarning("你忘了拖入碎片预制体！物体直接消失了。");
        }

        // 5. 销毁完整的本体
        Destroy(gameObject);
		Debug.Log("物体被圆环击中，破碎了！");
    }
}
