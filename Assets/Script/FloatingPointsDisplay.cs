using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FloatingPointsDisplay : MonoBehaviour
{
    private static GameObject canvasObject;
    private static Canvas canvas;
    
    /// <summary>
    /// Show floating points display at a world position
    /// </summary>
    public static void ShowPoints(Vector3 worldPosition, int points)
    {
        // Create canvas if it doesn't exist
        if (canvas == null)
        {
            CreateCanvas();
        }
        
        // Create the floating text
        GameObject textObj = new GameObject("FloatingPoints");
        textObj.transform.SetParent(canvas.transform);
        
        // Add regular UI Text component (more compatible)
        Text uiText = textObj.AddComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.fontSize = 32;
        uiText.color = Color.yellow;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.fontStyle = FontStyle.Bold;
        
        // Add outline for better visibility
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        // Set text content
        uiText.text = $"Total: {points} pts";
        
        Debug.Log($"Showing floating points: {points}");
        
        // Position the text
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300, 60);
        
        // Convert world position to screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        rectTransform.position = screenPos + new Vector3(0, 80, 0); // Offset above the sprite
        
        // Add the animation component
        FloatingTextAnimation animation = textObj.AddComponent<FloatingTextAnimation>();
        animation.Initialize(2.0f, 60f);
    }
    
    private static void CreateCanvas()
    {
        canvasObject = new GameObject("FloatingPointsCanvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Make sure it's on top
        
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        DontDestroyOnLoad(canvasObject);
        
        Debug.Log("FloatingPointsCanvas created!");
    }
}

/// <summary>
/// Handles the animation of floating text
/// </summary>
public class FloatingTextAnimation : MonoBehaviour
{
    private float duration;
    private float floatDistance;
    private float startTime;
    private Vector3 startPosition;
    private CanvasGroup canvasGroup;
    
    public void Initialize(float duration, float floatDistance)
    {
        this.duration = duration;
        this.floatDistance = floatDistance;
        
        startTime = Time.time;
        startPosition = transform.position;
        
        // Add CanvasGroup for fading
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        StartCoroutine(AnimateText());
    }
    
    private IEnumerator AnimateText()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime = Time.time - startTime;
            float progress = elapsedTime / duration;
            
            // Move up
            Vector3 newPosition = startPosition + Vector3.up * floatDistance * progress;
            transform.position = newPosition;
            
            // Fade out (start fading after 50% of duration)
            if (progress > 0.5f)
            {
                float fadeProgress = (progress - 0.5f) / 0.5f;
                canvasGroup.alpha = 1f - fadeProgress;
            }
            
            // Scale effect (slight grow then shrink)
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
            transform.localScale = Vector3.one * scale;
            
            yield return null;
        }
        
        // Destroy after animation
        Destroy(gameObject);
    }
}

