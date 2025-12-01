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
        
        // Create the floating text container
        GameObject textObj = new GameObject("FloatingPoints");
        textObj.transform.SetParent(canvas.transform);
        
        // Add grey background box
        Image background = textObj.AddComponent<Image>();
        background.color = new Color(0.3f, 0.3f, 0.3f, 0.9f); // Grey with slight transparency
        
        // Create child object for the text
        GameObject textChild = new GameObject("PointsText");
        textChild.transform.SetParent(textObj.transform);
        
        // Add regular UI Text component (more compatible)
        Text uiText = textChild.AddComponent<Text>();
        uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        uiText.fontSize = 32;
        uiText.color = Color.yellow;
        uiText.alignment = TextAnchor.MiddleCenter;
        uiText.fontStyle = FontStyle.Bold;
        
        // Add outline for better visibility
        Outline outline = textChild.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        // Set child text to fill parent
        RectTransform textRect = textChild.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Set text content
        uiText.text = $"Total: {points} pts";
        
        Debug.Log($"Showing floating points: {points}");
        
        // Position and size the container box
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300, 80);
        
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

