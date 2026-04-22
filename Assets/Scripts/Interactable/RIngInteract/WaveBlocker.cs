using UnityEngine;

public class WaveBlocker : MonoBehaviour, IRingInteractable
{
    [Header("阻碍设置")]
    [Tooltip("当阻挡成功时播放的特效")]
    public GameObject blockEffect;

    public void OnRingHit(ExpandingRing ring)
    {
        // 1. 播放阻挡特效（如果有）
        // 我们在圆环消失的位置（或者碰撞点）播放特效
        if (blockEffect != null)
        {
            // 在圆环边缘（大概位置）播放特效
            Vector3 contactPoint = (ring.transform.position + transform.position) / 2;
            Instantiate(blockEffect, contactPoint, Quaternion.identity);
        }

        // 2. 关键的一步：命令圆环自我毁灭
        ring.Dissipate();
    }
}
