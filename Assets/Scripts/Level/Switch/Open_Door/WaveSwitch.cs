using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaveSwitch : MonoBehaviour, IRingInteractable
{
    [Header("开关设置")]
    [Tooltip("需要控制的目标门（接触波后该物体会消失）")]
    public GameObject targetDoor;

    [Tooltip("是否只能触发一次")]
    public bool triggerOnce = true;

    // 防止被重复触发
    private bool isActivated = false;

    public void OnRingHit(ExpandingRing ring)
    {
        // 如果设置为只能触发一次并且已经激活了，就不再处理
        if (triggerOnce && isActivated)
        {
            return;
        }

        if (targetDoor != null)
        {
            // 让门消失（相当于打开）
            targetDoor.SetActive(false);
            
            isActivated = true;
            Debug.Log($"[WaveSwitch] 开关 {gameObject.name} 被波触发，已将门 {targetDoor.name} 隐藏！");
        }
        else
        {
            Debug.LogWarning($"[WaveSwitch] 开关 {gameObject.name} 被波触发，但是没有分配挂载目标门 (targetDoor)！", this);
        }
    }
}
