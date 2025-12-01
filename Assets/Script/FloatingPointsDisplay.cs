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
        
        // Add the click-to-dismiss component
        ClickToDismiss clickHandler = textObj.AddComponent<ClickToDismiss>();
        clickHandler.Initialize();
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
/// Handles click-to-dismiss functionality for floating text
/// </summary>
public class ClickToDismiss : MonoBehaviour
{
    private RectTransform rectTransform;
    
    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
    }
    
    private void Update()
    {
        // Check for mouse click
        if (Input.GetMouseButtonDown(0))
        {
            // Check if click is outside this UI element
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
            {
                // Click was outside, destroy this text
                Destroy(gameObject);
            }
        }
    }
}

