using UnityEngine;
using System.Collections.Generic;
using StarterAssets;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider))]
public class IntermittentForceZone : MonoBehaviour
{
    [System.Serializable]
    public struct ForceWave
    {
        [Tooltip("Time interval before this force is applied (or between repetitions).")]
        public float interval;
        [Tooltip("Force magnitude for this wave.")]
        public float force;
        [Tooltip("How many times to repeat this specific wave configuration.")]
        public int repeatCount;
    }

    [Header("Force Settings")]
    [Tooltip("The direction of the force (in world space).")]
    public Vector3 forceDirection = Vector3.forward;

    [Tooltip("List of force waves to cycle through.")]
    public List<ForceWave> wavePattern = new List<ForceWave>();

    [Tooltip("Starting delay before the first force application.")]
    public float startDelay = 0.5f;

    [Header("Warning Settings")]
    [Tooltip("Time before force application to trigger the warning event (seconds).")]
    public float warningTime = 0.5f;

    [Tooltip("Event triggered when the warning time is reached before a force wave.")]
    public UnityEngine.Events.UnityEvent OnWarning;
    
    [Tooltip("Event triggered exactly when the force is applied (for resetting visuals).")]
    public UnityEngine.Events.UnityEvent OnForceApplied;

    [Tooltip("Optional particle system to play when warning starts.")]
    public ParticleSystem warningEffect;


    [Header("Visualization")]
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.3f);
    public float arrowLength = 2.0f;
    public bool showDebugInfo = true;

    // Runtime state
    private float _timer;
    private int _currentWaveIndex = 0;
    private int _currentRepeatCount = 0;
    private bool _isPlayerInside;
    private ThirdPersonController _targetController;
    private bool _hasTriggeredWarning = false;
    private bool _isRunning = false;

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        
        // Add default pattern if empty for easy testing
        if (wavePattern.Count == 0)
        {
            wavePattern.Add(new ForceWave { interval = 2f, force = 10f, repeatCount = 3 });
            wavePattern.Add(new ForceWave { interval = 5f, force = 30f, repeatCount = 1 });
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _targetController = other.GetComponent<ThirdPersonController>();
            if (_targetController != null)
            {
                _isPlayerInside = true;
                
                // Only start the cycle on the very first entry
                if (!_isRunning)
                {
                    _isRunning = true;
                    _timer = startDelay; 
                    _currentWaveIndex = 0;
                    _currentRepeatCount = 0;
                    _hasTriggeredWarning = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = false;
            _targetController = null;
        }
    }

    private void Update()
    {
        // 只要启动了，就一直跑下去（无论玩家是否在内）
        if (!_isRunning || wavePattern.Count == 0) return;

        _timer -= Time.deltaTime;

        // Trigger warning if within the warning time window
        if (_timer <= warningTime && !_hasTriggeredWarning)
        {
            TriggerWarning();
        }

        if (_timer <= 0f)
        {
            ExecuteWave();
        }
    }

    private void TriggerWarning()
    {
        _hasTriggeredWarning = true;
        
        // Trigger generic particle, if assigned
        if (warningEffect != null)
        {
            warningEffect.Play();
        }
        
        // 由于区域变成了持久运行的，红屏闪烁和事件可能我们希望只在玩家在里面的时候才生效，或者对本物体的警告（颜色）始终生效
        // 屏幕由于是全局的，最好只有当玩家确实在区域内时才闪烁
        if (_isPlayerInside && PlayerWarningUI.Instance != null)
        {
            PlayerWarningUI.Instance.FlashWarning();
        }
        
        // 无论玩家在不在里面都可以抛出事件（例如让平台自己变红）
        OnWarning?.Invoke();
    }

    private void ExecuteWave()
    {
        ForceWave currentWave = wavePattern[_currentWaveIndex];

        // Reset warning state for the next wave
        _hasTriggeredWarning = false;
        
        // Trigger applied event (useful to reset colors/visuals changed by warning)
        OnForceApplied?.Invoke();

        if (_isPlayerInside)
        {
            // Apply Force
            ApplyForce(currentWave.force);
        }

        // Update repetition logic
        _currentRepeatCount++;

        // Check if we need to switch to the next wave configuration
        if (_currentRepeatCount >= currentWave.repeatCount)
        {
            _currentRepeatCount = 0;
            _currentWaveIndex = (_currentWaveIndex + 1) % wavePattern.Count;
        }

        // Set timer for the NEXT interval
        // Note: The structure implies 'interval' is the time to wait FOR this wave.
        // User asked: "every 2 seconds apply force".
        // So we reset timer to the interval of the (now current) wave type.
        // If we just switched indexes, we use the NEW wave's interval.
        // If we are repeating the same wave, we use the SAME wave's interval.
        
        _timer = wavePattern[_currentWaveIndex].interval;
    }

    private void ApplyForce(float magnitude)
    {
        if (_targetController != null)
        {
            Vector3 worldDir = transform.TransformDirection(forceDirection).normalized;
            _targetController.AddImpact(worldDir, magnitude);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        // Draw Zone
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(box.center, box.size);

        // Draw Arrow
        Vector3 center = transform.TransformPoint(box.center);
        Vector3 direction = transform.TransformDirection(forceDirection).normalized;
        Vector3 endPoint = center + direction * arrowLength;

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center, endPoint);
        
        // Arrowhead
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 150, 0) * new Vector3(0, 0, 1);
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -150, 0) * new Vector3(0, 0, 1);
        Gizmos.DrawRay(endPoint, right * 0.5f);
        Gizmos.DrawRay(endPoint, left * 0.5f);

#if UNITY_EDITOR
        if (showDebugInfo)
        {
            string info = $"Force Zone\nStatus: {(_isRunning ? "Running" : "Idle")}\nPlayer: {(_isPlayerInside ? "Inside" : "Outside")}\n";
            if (_isRunning && wavePattern.Count > 0)
            {
                info += $"Wave: {_currentWaveIndex + 1}/{wavePattern.Count}\n";
                info += $"Repeat: {_currentRepeatCount + 1}/{wavePattern[_currentWaveIndex].repeatCount}\n";
                info += $"Next Hit: {_timer:F1}s";
            }
            else
            {
                info += $"Pattern Count: {wavePattern.Count}";
            }
            Handles.Label(center + Vector3.up * 1.0f, info);
        }
#endif
    }
}
