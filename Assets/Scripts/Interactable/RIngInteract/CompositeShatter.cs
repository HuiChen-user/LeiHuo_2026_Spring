using UnityEngine;

public class CompositeShatter : MonoBehaviour, IRingInteractable
{
    [Header("组合破碎设置")]
    [Tooltip("整个组合物体的根节点（爸爸）")]
    public GameObject rootObject;

    [Tooltip("包含盾牌碎片的整体破碎预制体")]
    public GameObject compositeFracturedPrefab;

    [Tooltip("爆炸力度")]
    public float explosionForce = 600f;
    public float explosionRadius = 5f;

    public void OnRingHit(ExpandingRing ring)
    {
        // 1. 生成整个物体的碎片替身
        if (compositeFracturedPrefab != null)
        {
            // 注意：我们在根节点的位置生成碎片，因为碎片是代表整个物体的
            GameObject debris = Instantiate(compositeFracturedPrefab, rootObject.transform.position, rootObject.transform.rotation);
            
            // 2. 炸开所有碎片
            Rigidbody[] rbs = debris.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rbs)
            {
                // 以接触点（脆弱部分）为中心炸开，效果更真实
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);
            }

            Destroy(debris, 5f); // 5秒后清理垃圾
        }

        // 3. 销毁整个组合物体（爸爸），连同旁边的盾牌一起带走
        Destroy(rootObject);
        
        Debug.Log("击中弱点，整体破碎！");
    }
}
