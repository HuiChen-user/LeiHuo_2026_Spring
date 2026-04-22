using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class ColorWaveSwitch : MonoBehaviour, IRingInteractable
{
    [Header("状态设定")]
    [Tooltip("开关当前是否处于已开启状态")]
    public bool isOpened = false;

    [Header("外观及颜色设置")]
    [Tooltip("需要改变颜色的渲染器，如果不指定会自动获取自身的")]
    public Renderer targetRenderer;
    [Tooltip("未开启时的颜色")]
    public Color closedColor = Color.red;
    [Tooltip("已开启时的颜色")]
    public Color openedColor = Color.green;

    [Header("触发联动")]
    [Tooltip("当开关被波碰撞后触发的事件，可在这里指定任何需要改变状态的物体")]
    public UnityEvent onSwitchActivated;

    private void Awake()
    {
        // 如果没有预先指定渲染器，尝试从物体及其子物体中获取
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
            
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }
        }

        UpdateColor();
    }

    // 实现IRingInteractable接口：当角色发出的波击中此物体时被调用
    public void OnRingHit(ExpandingRing ring)
    {
        // 如果它还没有被开启
        if (!isOpened)
        {
            // 标记为开启状态
            isOpened = true;
            
            // 更新外观颜色为已开启颜色
            UpdateColor();
            
            // 触发事件以执行其他指定物体的状态改变（如打开门等）
            onSwitchActivated?.Invoke();
            
            Debug.Log($"[{gameObject.name}] 开关接收到波的碰撞，已开启！颜色发生改变，并已触发关联事件。");
        }
    }

    private void UpdateColor()
    {
        if (targetRenderer != null)
        {
            // 修改材质颜色以提供开关状态的视觉反馈
            targetRenderer.material.color = isOpened ? openedColor : closedColor;
        }
    }
}
