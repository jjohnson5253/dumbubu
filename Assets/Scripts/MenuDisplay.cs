using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MenuDisplay : MonoBehaviour
{
    public static MenuDisplay Instance { get; private set; }
    
    [Header("UI References")]
    public GameObject menuPanel; // The main menu container
    public TextMeshProUGUI pointsText; // "Total: X points"
    public Button grenadeToggleButton; // Grenade mode toggle
    public TextMeshProUGUI grenadeButtonText; // Text on grenade button
    public Button brownButton;
    public Button whiteButton;
    public Button blueButton;
    public Button pinkButton;
    public Button closeButton; // X button
    public Button quitButton;
    
    [Header("Settings")]
    public Vector3 worldPositionOffset = Vector3.up * 3f; // Offset from Dumbubu
    
    private static bool grenadeMode = false;
    private static int requiredPointsForGrenadeMode = 1;
    private GameObject dumbubu;
    private Camera mainCamera;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        mainCamera = Camera.main;
        dumbubu = GameObject.Find("Dumbubu");
        
        // Hide menu panel initially
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }
        
        // Setup button listeners
        SetupButtonListeners();
        
        // Initialize grenade mode state
        UpdateGrenadeButton();
    }
    
    private void Update()
    {
        // Position menu above Dumbubu when active
        if (menuPanel != null && menuPanel.activeSelf && dumbubu != null && mainCamera != null)
        {
            Vector3 worldPosition = dumbubu.transform.position + worldPositionOffset;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
            menuPanel.transform.position = screenPosition;
        }
        
        // Handle escape key to close menu
        if (Input.GetKeyDown(KeyCode.Escape) && IsDisplaying())
        {
            HideMenu();
            if (grenadeMode)
            {
                DisableGrenadeMode();
            }
        }
        
        // Handle clicking outside menu to close it
        if (Input.GetMouseButtonDown(0) && IsDisplaying())
        {
            // Check if click is outside the menu panel
            RectTransform menuRect = menuPanel.GetComponent<RectTransform>();
            if (menuRect != null && !RectTransformUtility.RectangleContainsScreenPoint(menuRect, Input.mousePosition))
            {
                HideMenu();
            }
        }
    }
    
    private void SetupButtonListeners()
    {
        // Grenade toggle button
        if (grenadeToggleButton != null)
        {
            grenadeToggleButton.onClick.AddListener(() => {
                if (CanUseGrenadeMode())
                {
                    ToggleGrenadeMode();
                }
            });
        }
        
        // Color buttons
        if (brownButton != null)
        {
            brownButton.onClick.AddListener(() => SwitchColor(DumbubuColor.Brown));
        }
        
        if (whiteButton != null)
        {
            whiteButton.onClick.AddListener(() => SwitchColor(DumbubuColor.White));
        }
        
        if (blueButton != null)
        {
            blueButton.onClick.AddListener(() => SwitchColor(DumbubuColor.Blue));
        }
        
        if (pinkButton != null)
        {
            pinkButton.onClick.AddListener(() => SwitchColor(DumbubuColor.Pink));
        }
        
        // Close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideMenu);
        }
        
        // Quit button
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(() => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
    
    private void SwitchColor(DumbubuColor color)
    {
        if (TextureSwitcher.Instance != null)
        {
            TextureSwitcher.Instance.SwitchColor(color);
        }
        HideMenu(); // Close menu after selection
    }
    
    public static void ShowMenu(Vector3 worldPosition, int points)
    {
        if (Instance == null) return;
        
        // Don't show if already displaying
        if (IsDisplaying()) return;
        
        // Update points display
        if (Instance.pointsText != null)
        {
            Instance.pointsText.text = $"Total: {points} points";
        }
        
        // Update button states
        Instance.UpdateButtonStates();
        
        // Show the menu panel
        if (Instance.menuPanel != null)
        {
            Instance.menuPanel.SetActive(true);
        }
    }
    
    public static void HideMenu()
    {
        if (Instance == null) return;
        
        if (Instance.menuPanel != null)
        {
            Instance.menuPanel.SetActive(false);
        }
        
        // Show grenade mode message if grenade mode is active
        if (grenadeMode && MessagesDisplay.Instance != null)
        {
            MessagesDisplay.Instance.ShowGrenadeMessage();
        }
    }
    
    public static bool IsDisplaying()
    {
        return Instance != null && Instance.menuPanel != null && Instance.menuPanel.activeSelf;
    }
    
    public static bool IsGrenadeModeEnabled()
    {
        return grenadeMode;
    }
    
    public static void ToggleGrenadeMode()
    {
        grenadeMode = !grenadeMode;
        Debug.Log($"Grenade mode: {(grenadeMode ? "ON" : "OFF")}");
        
        if (Instance != null)
        {
            Instance.UpdateGrenadeButton();
        }
    }
    
    public static void DisableGrenadeMode()
    {
        grenadeMode = false;
        Debug.Log("Grenade mode: OFF (disabled by escape key)");
        
        if (Instance != null)
        {
            Instance.UpdateGrenadeButton();
        }
    }
    
    private bool CanUseGrenadeMode()
    {
        return PointsManager.Instance != null && PointsManager.Instance.GetPoints() >= requiredPointsForGrenadeMode;
    }
    
    private void UpdateButtonStates()
    {
        // Update color button availability
        UpdateColorButton(brownButton, DumbubuColor.Brown);
        UpdateColorButton(whiteButton, DumbubuColor.White);
        UpdateColorButton(blueButton, DumbubuColor.Blue);
        UpdateColorButton(pinkButton, DumbubuColor.Pink);
        
        // Update grenade button
        UpdateGrenadeButton();
    }
    
    private void UpdateColorButton(Button button, DumbubuColor color)
    {
        if (button == null) return;
        
        bool isAvailable = TextureSwitcher.Instance != null && TextureSwitcher.Instance.IsColorAvailable(color);
        button.interactable = isAvailable;
    }
    
    private void UpdateGrenadeButton()
    {
        if (grenadeToggleButton == null) return;
        
        bool canUseGrenade = CanUseGrenadeMode();
        grenadeToggleButton.interactable = canUseGrenade;
        
        // Update button text
        if (grenadeButtonText != null)
        {
            grenadeButtonText.text = grenadeMode ? "Grenade Mode: ON" : "Grenade Mode: OFF";
        }
    }
}