using UnityEngine;
using System.Collections.Generic;

public class SyncSwitchManager : MonoBehaviour
{
    [Header("同步机关设置")]
    [Tooltip("当所有开关都被激活后，需要执行隐藏(打开)的目标门")]
    public GameObject targetDoor;

    [Tooltip("从第一个开关被触发开始算起的限时（秒）")]
    public float timeLimit = 5.0f;

    [Header("状态监控(勿手动修改)")]
    public List<SyncSwitchNode> allNodes = new List<SyncSwitchNode>();
    public bool isTimerRunning = false;
    public float currentTimer = 0f;
    public bool isSolved = false; // 是否已经成功解开，公开给 Node 读取

    private void Update()
    {
        // 如果已经解开，或者计时没开始，都不需要管
        if (isSolved || !isTimerRunning) return;

        currentTimer += Time.deltaTime;

        // 如果超时了还没全部打开
        if (currentTimer >= timeLimit)
        {
            ResetAllNodes();
        }
    }

    /// <summary>
    /// 被各个节点(SyncSwitchNode)通知时调用
    /// </summary>
    public void NotifySwitchHit()
    {
        if (isSolved) return; // 已经解开的机关不用再处理

        // 如果是第一个被按下的开关，启动计时器
        if (!isTimerRunning)
        {
            isTimerRunning = true;
            currentTimer = 0f;
            Debug.Log($"[SyncSwitch] 机关组 {gameObject.name} 倒计时开始！限时 {timeLimit} 秒。");
        }

        CheckAllActivated();
    }

    /// <summary>
    /// 检查是否所有的子开关都亮了
    /// </summary>
    private void CheckAllActivated()
    {
        foreach (var node in allNodes)
        {
            // 只要有一个没激活，就还没成功，继续等
            if (!node.isActivated) return;
        }

        // 全亮了！
        Success();
    }

    private void Success()
    {
        isSolved = true;
        isTimerRunning = false;
        
        Debug.Log($"[SyncSwitch] 机关组 {gameObject.name} 解谜成功！门已开启！");

        if (targetDoor != null)
        {
            targetDoor.SetActive(false); // 让门消失开路
        }
    }

    private void ResetAllNodes()
    {
        Debug.Log($"[SyncSwitch] 机关组 {gameObject.name} 倒计时 {timeLimit} 秒已到，重置所有开关！");
        
        isTimerRunning = false;
        currentTimer = 0f;

        // 让所有属于该管理器的子开关熄灭
        foreach (var node in allNodes)
        {
            node.ResetSwitch();
        }
    }

    /// <summary>
    /// 可视化展示
    /// </summary>
    private void OnDrawGizmos()
    {
        // 1. 画线连接所有的节点，方便在场景里看到哪些开关属于这个管理器
        if (allNodes != null && allNodes.Count > 0)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f); // 浅蓝色连线
            foreach (var node in allNodes)
            {
                if (node != null)
                {
                    Gizmos.DrawLine(transform.position, node.transform.position);
                }
            }
        }

#if UNITY_EDITOR
        // 2. 如果正在运行且处于倒计时，显示剩余时间
        if (Application.isPlaying && isTimerRunning && !isSolved)
        {
            float timeLeft = Mathf.Max(0, timeLimit - currentTimer);
            string timeStr = $"剩秒: {timeLeft:F1}s";
            
            // 在管理器头顶显示时间的飘字
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, timeStr);
            
            // 为了更明显，也把时间飘字印在所有已被激活的节点头上
            foreach (var node in allNodes)
            {
                if (node != null && node.isActivated)
                {
                    GUIStyle style = new GUIStyle();
                    style.normal.textColor = Color.red;
                    style.fontSize = 14;
                    UnityEditor.Handles.Label(node.transform.position + Vector3.up * 1.5f, timeStr, style);
                }
            }
        }
#endif
    }
}
