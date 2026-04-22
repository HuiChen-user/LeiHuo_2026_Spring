using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WaveLightRevealer : MonoBehaviour, IRingInteractable
{
    [Header("设置")]
    [Tooltip("是否在被波命中后，恢复碰撞体的物理阻挡（移除 isTrigger）。如果是纯光源（希望玩家穿过），请保持默认 false。")]
    public bool restorePhysicalCollision = false;

    private Collider _collider;
    private Renderer[] _renderers;
    private Light[] _lights;
    private ParticleSystem[] _particleSystems;
    private bool _isRevealed = false;

    private void Start()
    {
        _collider = GetComponent<Collider>();
        
        // 强制设为 Trigger，确保初始状态不会产生物理阻挡，但依然存在碰撞体能被波检测到
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }

        // 获取自身及子物体下的所有渲染器、光源组件和粒子系统 (true代表包含那些当前未激活的节点)
        _renderers = GetComponentsInChildren<Renderer>(true);
        _lights = GetComponentsInChildren<Light>(true);
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        // 初始化时立即隐藏外观和光照
        SetVisibility(false);
    }

    public void OnRingHit(ExpandingRing ring)
    {
        if (_isRevealed) return;
        _isRevealed = true;

        // 接触波后进行显示
        SetVisibility(true);

        // 如果需要，恢复实体的物理阻挡
        if (restorePhysicalCollision && _collider != null)
        {
            _collider.isTrigger = false;
        }

        // Debug.Log($"【WaveLightRevealer】 {gameObject.name} 被波命中，光源已显现！");
    }

    private void SetVisibility(bool state)
    {
        foreach (var r in _renderers)
        {
            if (r != null) r.enabled = state;
        }

        foreach (var l in _lights)
        {
            if (l != null) l.enabled = state;
        }

        foreach (var ps in _particleSystems)
        {
            if (ps != null)
            {
                if (state)
                {
                    ps.Play(true);
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
