using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FloatingPointsDisplay : MonoBehaviour
{
    private static GameObject canvasObject;
    private static Canvas canvas;
    private static GameObject currentPointsDisplay; // Track the current display
    
    /// <summary>
    /// Check if a points display is currently showing
    /// </summary>
    public static bool IsDisplaying()
    {
        return currentPointsDisplay != null;
    }
    
    /// <summary>
    /// Clear the current display reference (called when display is destroyed)
    /// </summary>
    public static void ClearCurrentDisplay()
    {
        currentPointsDisplay = null;
    }
    
    /// <summary>
    /// Show floating points display at a world position
    /// </summary>
    public static void ShowPoints(Vector3 worldPosition, int points)
    {
        // Don't create a new display if one already exists
        if (currentPointsDisplay != null)
        {
            return;
        }
        
        // Create canvas if it doesn't exist
        if (canvas == null)
        {
            CreateCanvas();
        }
        
        // Create the floating text container
        GameObject textObj = new GameObject("FloatingPoints");
        textObj.transform.SetParent(canvas.transform);
        currentPointsDisplay = textObj; // Store reference
        
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
        
        // Set child text to fill parent (top portion only, leaving space for buttons)
        RectTransform textRect = textChild.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0.4f);
        textRect.anchorMax = new Vector2(1, 1);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        // Set text content
        uiText.text = $"Total: {points} pts";
        
        Debug.Log($"Showing floating points: {points}");
        
        // Position and size the container box
        RectTransform rectTransform = textObj.GetComponent<RectTransform>();
        
        // Set anchors to bottom-left for consistent positioning
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        
        // Set size
        rectTransform.sizeDelta = new Vector2(300, 190); // Increased height for buttons
        
        // Convert world position to screen position and set anchored position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
        rectTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y + 170); // Offset above the sprite
        
        // Reset scale to ensure it's not affected by parent scaling
        rectTransform.localScale = Vector3.one;
        
        // Create buttons container
        CreateColorButtons(textObj);
        
        // Create close button in top-right corner
        CreateCloseButton(textObj);
        
        // Create quit button at the bottom
        CreateQuitButton(textObj);
        
        // Add the click-to-dismiss component
        ClickToDismiss clickHandler = textObj.AddComponent<ClickToDismiss>();
        clickHandler.Initialize();
    }
    
    private static void CreateColorButtons(GameObject parent)
    {
        // Create buttons container
        GameObject buttonsContainer = new GameObject("ButtonsContainer");
        buttonsContainer.transform.SetParent(parent.transform);
        
        RectTransform buttonsRect = buttonsContainer.AddComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0, 0);
        buttonsRect.anchorMax = new Vector2(1, 0);
        buttonsRect.pivot = new Vector2(0.5f, 0);
        buttonsRect.anchoredPosition = new Vector2(0, 60);
        buttonsRect.sizeDelta = new Vector2(-20, 40);
        
        // Create Brown button
        CreateButton(buttonsContainer, "Brown", new Vector2(-80, 0), DumbubuColor.Brown, () => {
            if (TextureSwitcher.Instance != null)
                TextureSwitcher.Instance.SwitchColor(DumbubuColor.Brown);
        });
        
        // Create White button
        CreateButton(buttonsContainer, "White", new Vector2(80, 0), DumbubuColor.White, () => {
            if (TextureSwitcher.Instance != null)
                TextureSwitcher.Instance.SwitchColor(DumbubuColor.White);
        });

        // Create Blue button
        CreateButton(buttonsContainer, "Blue", new Vector2(-80, -50), DumbubuColor.Blue, () => {
            if (TextureSwitcher.Instance != null)
                TextureSwitcher.Instance.SwitchColor(DumbubuColor.Blue);
        });

        // Create Pink button
        CreateButton(buttonsContainer, "Pink", new Vector2(80, -50), DumbubuColor.Pink, () => {
            if (TextureSwitcher.Instance != null)
                TextureSwitcher.Instance.SwitchColor(DumbubuColor.Pink);
        });
    }
    
    private static void CreateCloseButton(GameObject parent)
    {
        GameObject closeButtonObj = new GameObject("CloseButton");
        closeButtonObj.transform.SetParent(parent.transform);
        
        RectTransform closeButtonRect = closeButtonObj.AddComponent<RectTransform>();
        closeButtonRect.anchorMin = new Vector2(1f, 1f); // Top-right corner
        closeButtonRect.anchorMax = new Vector2(1f, 1f);
        closeButtonRect.pivot = new Vector2(1f, 1f);
        closeButtonRect.anchoredPosition = new Vector2(-5, -5); // 5 pixel margin from edges
        closeButtonRect.sizeDelta = new Vector2(25, 25); // Small square button
        
        // Add button background
        Image buttonBg = closeButtonObj.AddComponent<Image>();
        buttonBg.color = new Color(0.8f, 0.2f, 0.2f, 0.9f); // Red background
        
        // Add Button component
        Button button = closeButtonObj.AddComponent<Button>();
        button.targetGraphic = buttonBg;
        
        // Set button colors for hover effects
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
        colors.highlightedColor = new Color(1f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        button.colors = colors;
        
        // Add click listener to close the menu
        button.onClick.AddListener(() => {
            if (currentPointsDisplay != null)
            {
                Destroy(currentPointsDisplay);
            }
        });
        
        // Create "X" text
        GameObject textObj = new GameObject("XText");
        textObj.transform.SetParent(closeButtonObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = "×"; // Using multiplication symbol for a cleaner X
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 18;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontStyle = FontStyle.Bold;
    }
    
    private static void CreateQuitButton(GameObject parent)
    {
        GameObject quitButtonObj = new GameObject("QuitButton");
        quitButtonObj.transform.SetParent(parent.transform);
        
        RectTransform quitButtonRect = quitButtonObj.AddComponent<RectTransform>();
        quitButtonRect.anchorMin = new Vector2(0.5f, 0f); // Bottom center
        quitButtonRect.anchorMax = new Vector2(0.5f, 0f);
        quitButtonRect.pivot = new Vector2(0.5f, 0f);
        quitButtonRect.anchoredPosition = new Vector2(0, 10); // 10 pixel margin from bottom
        quitButtonRect.sizeDelta = new Vector2(100, 30); // Rectangular button
        
        // Add button background
        Image buttonBg = quitButtonObj.AddComponent<Image>();
        buttonBg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f); // Dark red background
        
        // Add Button component
        Button button = quitButtonObj.AddComponent<Button>();
        button.targetGraphic = buttonBg;
        
        // Set button colors for hover effects
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        colors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.4f, 0.1f, 0.1f, 1f);
        button.colors = colors;
        
        // Add click listener to quit the game
        button.onClick.AddListener(() => {
            Debug.Log("Quit button clicked - closing application");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
        
        // Create "Quit" text
        GameObject textObj = new GameObject("QuitText");
        textObj.transform.SetParent(quitButtonObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = "Quit";
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 16;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontStyle = FontStyle.Bold;
    }
    
    private static void CreateButton(GameObject parent, string label, Vector2 position, DumbubuColor color, System.Action onClick)
    {
        GameObject buttonObj = new GameObject(label + "Button");
        buttonObj.transform.SetParent(parent.transform);
        
        RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(120, 35);
        
        // Add button background
        Image buttonBg = buttonObj.AddComponent<Image>();
        buttonBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        
        // Add Button component
        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonBg;
        
        // Set button colors
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        button.colors = colors;
        
        // Add click listener
        button.onClick.AddListener(() => onClick());
        
        // Create button text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        
        Text buttonText = textObj.AddComponent<Text>();
        buttonText.text = label;
        buttonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        buttonText.fontSize = 18;
        
        // Check if color is available and grey out if locked
        bool isAvailable = TextureSwitcher.Instance != null && TextureSwitcher.Instance.IsColorAvailable(color);
        buttonText.color = isAvailable ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.7f);
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.fontStyle = FontStyle.Bold;
        
        // Disable button if not available
        button.interactable = isAvailable;
    }
    
    private static void CreateCanvas()
    {
        canvasObject = new GameObject("FloatingPointsCanvas");
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // Make sure it's on top
        
        // Don't use CanvasScaler to avoid scaling issues
        
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
    private GraphicRaycaster raycaster;
    
    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Add GraphicRaycaster to canvas if it doesn't exist
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
    
    private void Update()
    {
        // Check for mouse click
        if (Input.GetMouseButtonDown(0))
        {
            // Check if we clicked on a button first
            if (IsPointerOverButton())
            {
                return; // Don't dismiss if clicking a button
            }
            
            // Check if click is outside the main container
            if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition))
            {
                // Click was outside, destroy this text
                Destroy(gameObject);
            }
        }
    }
    
    private bool IsPointerOverButton()
    {
        // Check if mouse is over any button (including close button)
        Button[] buttons = GetComponentsInChildren<Button>();
        foreach (Button button in buttons)
        {
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            if (RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition))
            {
                return true;
            }
        }
        return false;
    }
    
    private void OnDestroy()
    {
        // Clear the reference when destroyed
        FloatingPointsDisplay.ClearCurrentDisplay();
    }
}

