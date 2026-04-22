using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerWarningUI : MonoBehaviour
{
    public static PlayerWarningUI Instance { get; private set; }

    [Header("UI Component")]
    [Tooltip("The Image component used for the red flash. Usually spans the full screen.")]
    public Image warningImage;

    [Header("Flash Settings")]
    [Tooltip("The maximum alpha value during the flash (0 to 1).")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.5f;
    
    [Tooltip("Total duration of the flash effect in seconds.")]
    public float flashDuration = 0.5f;

    [Tooltip("Color of the flash. Usually Red.")]
    public Color flashColor = Color.red;

    private Coroutine _flashCoroutine;

    private void Awake()
    {
        // Setup Singleton
        if (Instance == null)
        {
            Instance = this;
            // DontDestroyOnLoad(gameObject); // Uncomment if you want this HUD to persist across scenes
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (warningImage == null)
        {
            warningImage = GetComponent<Image>();
        }

        if (warningImage != null)
        {
            // Ensure the image starts clear
            Color startColor = flashColor;
            startColor.a = 0f;
            warningImage.color = startColor;
        }
    }

    /// <summary>
    /// Triggers the screen flash earning effect.
    /// Can be called globally via PlayerWarningUI.Instance.FlashWarning()
    /// </summary>
    public void FlashWarning()
    {
        if (warningImage == null) return;

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        
        _flashCoroutine = StartCoroutine(DoFlash());
    }

    private IEnumerator DoFlash()
    {
        float halfDuration = flashDuration / 2f;
        float elapsed = 0f;
        
        // Ensure base color is set
        Color currentColor = flashColor;

        // Fade In
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, maxAlpha, elapsed / halfDuration);
            currentColor.a = alpha;
            warningImage.color = currentColor;
            yield return null;
        }

        elapsed = 0f;

        // Fade Out
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, elapsed / halfDuration);
            currentColor.a = alpha;
            warningImage.color = currentColor;
            yield return null;
        }
        
        // Ensure it is completely transparent at the end
        currentColor.a = 0f;
        warningImage.color = currentColor;
        
        _flashCoroutine = null;
    }
}
