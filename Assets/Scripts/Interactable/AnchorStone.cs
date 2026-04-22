using UnityEngine;
using StarterAssets;

[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(LineRenderer))]
public class AnchorStone : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The radius of the anchor zone.")]
    public float radius = 3.0f;
    [Tooltip("Key to hold for anchoring.")]
    public KeyCode interactKey = KeyCode.F;
    [Tooltip("Color of the ring when active.")]
    public Color activeColor = Color.cyan;
    [Tooltip("Color of the ring when inactive.")]
    public Color inactiveColor = Color.gray;

    [Header("Fixed Position")]
    [Tooltip("The specific fixed position to anchor the player to.")]
    public Transform targetFixedPosition;
    [Tooltip("Visual radius for the fixed position area in the Scene view.")]
    public float fixedPositionVisualRadius = 0.5f;
    [Tooltip("Color of the fixed position area in the Scene view.")]
    public Color fixedPositionAreaColor = new Color(1f, 1f, 0f, 0.4f);

    [Header("Visuals")]
    public int ringSegments = 50;

    private SphereCollider _collider;
    private LineRenderer _lineRenderer;
    private ThirdPersonController _playerInside;
    private bool _isAnchoring = false;

    private Transform GetValidTarget()
    {
        // 优先检查已分配的目标是否合法（必须是自己的子物体）
        if (targetFixedPosition != null && targetFixedPosition.IsChildOf(transform))
        {
            return targetFixedPosition;
        }

        // 如果不合法或者为空，尝试在自己的子物体里找个同名的
        if (targetFixedPosition != null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t != transform && t.name == targetFixedPosition.name)
                {
                    targetFixedPosition = t;
                    return t;
                }
            }
        }

        // 终极兜底：如果有子节点，绝对霸道地拿第一个做锚点
        if (transform.childCount > 0)
        {
            targetFixedPosition = transform.GetChild(0);
            return targetFixedPosition;
        }

        // 连子物体都没有时，绝不移动
        return null;
    }

    private void Awake()
    {
        // 在启动时先校验一次，断绝任何挂载在外的危险引用
        GetValidTarget();

        _collider = GetComponent<SphereCollider>();
        _lineRenderer = GetComponent<LineRenderer>();

        // Setup Collider
        _collider.isTrigger = true;
        _collider.radius = radius;

        // Setup LineRenderer
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = ringSegments + 1;
        _lineRenderer.startWidth = 0.1f;
        _lineRenderer.endWidth = 0.1f;
        
        UpdateRingVisuals(false);
        DrawRing();
    }

    private void Update()
    {
        if (_playerInside == null) return;

        // 【防抢夺防虚留检查】：如果玩家由于某种原因(比如位移技能或其他触发异常)远离了本石头，主动切断连接
        if (Vector3.Distance(transform.position, _playerInside.transform.position) > radius * 2.0f)
        {
            if (_isAnchoring) StopAnchoring();
            _playerInside = null;
            return;
        }

        // Check Input
        if (Input.GetKey(interactKey))
        {
            if (!_isAnchoring)
            {
                StartAnchoring();
            }
            else
            {
                Transform validTarget = GetValidTarget();
                if (validTarget != null)
                {
                    // 持有F键期间持续保持在固定位置
                    CharacterController cc = _playerInside.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    _playerInside.transform.position = validTarget.position;
                    if (cc != null) cc.enabled = true;
                }
            }
        }
        else
        {
            if (_isAnchoring)
            {
                StopAnchoring();
            }
        }
    }

    private void StartAnchoring()
    {
        // 如果玩家已被其他石头锚定（非本石头掌控中），放弃抢夺，防止多石抢人造成的穿梭闪烁
        if (_playerInside.IsAnchored && !_isAnchoring) return;

        _isAnchoring = true;
        _playerInside.IsAnchored = true;
        
        Transform validTarget = GetValidTarget();
        if (validTarget != null)
        {
            // 在修改Transform.position前需要暂时关闭CharacterController
            CharacterController cc = _playerInside.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            
            _playerInside.transform.position = validTarget.position;
            
            if (cc != null) cc.enabled = true;
        }

        UpdateRingVisuals(true);
        Debug.Log("Player Anchored!");
    }

    private void StopAnchoring()
    {
        _isAnchoring = false;
        if (_playerInside != null)
        {
            _playerInside.IsAnchored = false;
        }
        UpdateRingVisuals(false);
        Debug.Log("Player Released!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInside = other.GetComponent<ThirdPersonController>();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // If player leaves while holding F, force release
            if (_isAnchoring)
            {
                StopAnchoring();
            }
            _playerInside = null;
        }
    }

    private void UpdateRingVisuals(bool isActive)
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.startColor = isActive ? activeColor : inactiveColor;
            _lineRenderer.endColor = isActive ? activeColor : inactiveColor;
        }
    }

    private void DrawRing()
    {
        float angleStep = 360f / ringSegments;
        for (int i = 0; i <= ringSegments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            _lineRenderer.SetPosition(i, new Vector3(x, 0.1f, z)); // Slightly above ground
        }
    }

    private void OnValidate()
    {
        // Update collider radius in editor when changing radius
        if (_collider == null) _collider = GetComponent<SphereCollider>();
        if (_collider != null) _collider.radius = radius;

        // 在编辑器里实时纠偏，防止用户肉眼看到Gizmo球跑到其他石头底下！
        GetValidTarget();
    }

    private void OnDrawGizmos()
    {
        // 可视化目标固定位置的大致区域
        Transform target = GetValidTarget();
        if (target != null)
        {
            Gizmos.color = fixedPositionAreaColor;
            Gizmos.DrawSphere(target.position, fixedPositionVisualRadius);
        }
    }
}
