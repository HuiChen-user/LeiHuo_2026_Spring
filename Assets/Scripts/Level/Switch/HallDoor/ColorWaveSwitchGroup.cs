using UnityEngine;

public class ColorWaveSwitchGroup : MonoBehaviour
{
    [Header("绑定设置")]
    [Tooltip("需要全部开启的开关数组")]
    public ColorWaveSwitch[] switches;

    [Tooltip("当所有开关都开启后，需要显示的物体（处于激活状态）")]
    public GameObject targetObjectToActivate;

    private bool hasActivated = false;

    private void Start()
    {
        // 确保初始状态下目标物体是隐藏的（符合说明：此前isActive为False）
        if (targetObjectToActivate != null)
        {
            targetObjectToActivate.SetActive(false);
        }

        // 为所有的开关自动绑定检查事件
        foreach (var sw in switches)
        {
            if (sw != null)
            {
                // 当任何一个开关被激活时，都会触发检查
                if (sw.onSwitchActivated == null)
                {
                    sw.onSwitchActivated = new UnityEngine.Events.UnityEvent();
                }
                sw.onSwitchActivated.AddListener(CheckAllSwitches);
            }
        }
    }

    /// <summary>
    /// 检查所有受管理的开关是否都已经开启
    /// </summary>
    public void CheckAllSwitches()
    {
        // 如果之前已经激活过了，不再重复判断
        if (hasActivated) return;

        foreach (var sw in switches)
        {
            // 只要有任何一个开关没有处于 isOpened 状态，则直接返回
            if (!sw.isOpened)
            {
                return;
            }
        }

        // 走到这一步说明所有的开关都已经处于开启状态且是首次触发
        hasActivated = true;

        if (targetObjectToActivate != null)
        {
            targetObjectToActivate.SetActive(true);
            Debug.Log($"[{gameObject.name}] 所有关联的波纹开关均已开启，已在场景中显示目标物体: {targetObjectToActivate.name}");
        }
    }
}
