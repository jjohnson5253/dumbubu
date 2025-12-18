using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Steamworks;
using System.Collections.Generic;

public class BlindBoxDisplay : MonoBehaviour
{
    public static BlindBoxDisplay Instance { get; private set; }
    
    [Header("UI References")]
    public GameObject blindBoxPanel; // The blind box UI container
    public Button backButton; // Back to menu button
    public TextMeshProUGUI boxesText; // "Boxes: X" display
    public Image rewardImage; // Shows reward item image
    public TextMeshProUGUI rewardText; // Shows reward item name
    public Button openButton; // Open blind box button
    
    [Header("Reward Images")]
    public Sprite whiteInventorySprite;
    public Sprite blueInventorySprite; 
    public Sprite pinkInventorySprite;
    
    [Header("Settings")]
    public Vector3 worldPositionOffset = Vector3.up * 3f; // Offset from Dumbubu (same as MenuDisplay)
    
    private int blindBoxItemDefId = 69421; // Blind box item
    private int generatorItemDefId = 69422; // Generator for exchange
    private GameObject dumbubu;
    private Camera mainCamera;
    private int boxesCount = 0; // Track current box count
    
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
        
        // Hide blind box panel initially
        if (blindBoxPanel != null)
        {
            blindBoxPanel.SetActive(false);
        }
        
        // Setup button listeners
        SetupButtonListeners();
        
        // Setup Steam inventory manager callbacks
        if (SteamInventoryManager.Instance != null)
        {
            SteamInventoryManager.Instance.OnExchangeCompleted += OnExchangeCompleted;
            SteamInventoryManager.Instance.OnItemDropped += OnItemDropped;
        }
        
        // Clear reward display initially
        ClearRewardDisplay();
    }
    
    private void Update()
    {
        // Position blind box panel above Dumbubu when active (same as MenuDisplay)
        if (blindBoxPanel != null && blindBoxPanel.activeSelf && dumbubu != null && mainCamera != null)
        {
            Vector3 worldPosition = dumbubu.transform.position + worldPositionOffset;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
            blindBoxPanel.transform.position = screenPosition;
        }
    }
    
    private void SetupButtonListeners()
    {
        // Back button
        if (backButton != null)
        {
            backButton.onClick.AddListener(() => {
                HideBlindBoxPanel();
                MenuDisplay.ShowMenuPanel();
            });
        }
        
        // Open button
        if (openButton != null)
        {
            openButton.onClick.AddListener(OpenBlindBox);
        }
    }
    
    public static void ShowBlindBoxPanel()
    {
        if (Instance == null) return;
        
        // Hide menu panel
        MenuDisplay.HideMenuPanel();
        
        // Show blind box panel
        if (Instance.blindBoxPanel != null)
        {
            Instance.blindBoxPanel.SetActive(true);
        }
        
        // Update boxes count
        Instance.UpdateBoxesCount();
        
        // Clear previous reward
        Instance.ClearRewardDisplay();
    }
    
    public static void HideBlindBoxPanel()
    {
        if (Instance == null) return;
        
        if (Instance.blindBoxPanel != null)
        {
            Instance.blindBoxPanel.SetActive(false);
        }
    }
    
    public static bool IsDisplaying()
    {
        return Instance != null && Instance.blindBoxPanel != null && Instance.blindBoxPanel.activeSelf;
    }
    
    private void UpdateBoxesCount()
    {
        Debug.Log("UpdateBoxesCount called");
        
        if (!SteamManager.Initialized)
        {
            Debug.Log("Steam not initialized");
            SetBoxesText(0);
            return;
        }
        
        Debug.Log($"SteamInventoryManager.Instance: {SteamInventoryManager.Instance}");
        Debug.Log($"Inventory loaded: {SteamInventoryManager.Instance?.IsInventoryLoaded()}");
        
        // Get count of blind box items from Steam inventory
        if (SteamInventoryManager.Instance != null && SteamInventoryManager.Instance.IsInventoryLoaded())
        {
            Debug.Log($"Looking for blind box item: {blindBoxItemDefId}");
            
            int boxCount = GetBlindBoxCount();
            Debug.Log($"Blind box count: {boxCount}");
            SetBoxesText(boxCount);
            
            // Enable/disable open button based on box count
            if (openButton != null)
            {
                openButton.interactable = boxCount > 0;
            }
        }
        else
        {
            Debug.Log("SteamInventoryManager not available or inventory not loaded");
            SetBoxesText(0);
            if (openButton != null)
            {
                openButton.interactable = false;
            }
        }
    }
    
    private int GetBlindBoxCount()
    {
        if (SteamInventoryManager.Instance == null)
        {
            Debug.Log("SteamInventoryManager.Instance is null");
            return 0;
        }
        
        uint boxCount = SteamInventoryManager.Instance.GetItemCount(blindBoxItemDefId);
        Debug.Log($"Player has {boxCount} blind box items (ID: {blindBoxItemDefId})");
        
        return (int)boxCount;
    }
    
    private void SetBoxesText(int count)
    {
        boxesCount = count;
        if (boxesText != null)
        {
            boxesText.text = $"Boxes: {count}";
        }
    }
    
    private void OpenBlindBox()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam not initialized");
            return;
        }
        
        Debug.Log("Opening blind box...");
        
        // Disable open button during exchange
        if (openButton != null)
        {
            openButton.interactable = false;
        }
        
        // Call the real Steam exchange
        PerformSteamExchange();
    }
    
    private void PerformSteamExchange()
    {
        Debug.Log("Performing real Steam exchange...");
        
        // Get a blind box instance ID to consume
        SteamItemInstanceID_t blindBoxInstanceId = SteamInventoryManager.Instance.GetItemInstance(blindBoxItemDefId);
        
        if (blindBoxInstanceId == SteamItemInstanceID_t.Invalid)
        {
            Debug.LogError("No blind box instances available for exchange");
            // Re-enable button on failure
            if (openButton != null)
            {
                openButton.interactable = true;
            }
            return;
        }
        
        Debug.Log($"Using blind box instance ID: {blindBoxInstanceId} for exchange");
        
        // Use SteamInventoryManager to perform the exchange
        bool exchangeStarted = SteamInventoryManager.Instance.PerformExchange(generatorItemDefId, blindBoxInstanceId);
        
        if (!exchangeStarted)
        {
            Debug.LogError("Failed to start Steam exchange");
            // Re-enable button on failure
            if (openButton != null)
            {
                openButton.interactable = true;
            }
        }
    }
    
    private void SimulateExchange()
    {
        // Simulate getting a random reward (for testing)
        int[] possibleRewards = { 1, 2, 3 }; // White, Blue, Pink
        int[] weights = { 9000, 999, 1 }; // Match bundle weights from steam-inventory.json
        
        int totalWeight = 0;
        foreach (int weight in weights)
        {
            totalWeight += weight;
        }
        
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;
        int selectedReward = 1; // Default to white
        
        for (int i = 0; i < possibleRewards.Length; i++)
        {
            currentWeight += weights[i];
            if (randomValue < currentWeight)
            {
                selectedReward = possibleRewards[i];
                break;
            }
        }
        
        ShowReward(selectedReward);
        UpdateBoxesCount(); // Update count after opening
    }
    
    private void OnExchangeCompleted(int[] itemDefIds, bool success)
    {
        if (success && itemDefIds != null && itemDefIds.Length > 0)
        {
            Debug.Log("Steam exchange completed successfully!");
            
            // Filter out the blind box item ID (69421) and find the actual reward
            int rewardItemDefId = 0;
            foreach (int itemDefId in itemDefIds)
            {
                Debug.Log($"Exchange result contains ItemDefID: {itemDefId}");
                
                // Skip the blind box item ID - we want the actual reward
                if (itemDefId != blindBoxItemDefId)
                {
                    rewardItemDefId = itemDefId;
                    Debug.Log($"Found reward item: {rewardItemDefId}");
                    break;
                }
            }
            
            if (rewardItemDefId != 0)
            {
                ShowReward(rewardItemDefId);
                // Manually decrement box count since we consumed 1 box
                DecrementBoxCount();
            }
            else
            {
                Debug.LogWarning("No reward item found in exchange results");
                // Re-enable button if no reward found
                if (openButton != null)
                {
                    openButton.interactable = true;
                }
            }
        }
        else
        {
            Debug.LogError("Exchange failed or returned no items");
            // Re-enable open button on failure
            if (openButton != null)
            {
                openButton.interactable = true;
            }
        }
    }
    
    private void ShowReward(int itemDefId)
    {
        switch (itemDefId)
        {
            case 1: // White
                if (rewardImage != null) rewardImage.sprite = whiteInventorySprite;
                if (rewardText != null) rewardText.text = "White";
                break;
            case 2: // Blue  
                if (rewardImage != null) rewardImage.sprite = blueInventorySprite;
                if (rewardText != null) rewardText.text = "Blue";
                break;
            case 3: // Pink
                if (rewardImage != null) rewardImage.sprite = pinkInventorySprite;
                if (rewardText != null) rewardText.text = "Pink";
                break;
            default:
                Debug.LogWarning($"Unknown reward item: {itemDefId}");
                break;
        }
        
        // Show reward elements
        if (rewardImage != null) rewardImage.gameObject.SetActive(true);
        if (rewardText != null) rewardText.gameObject.SetActive(true);
        
        Debug.Log($"Reward shown: ItemDefId {itemDefId}");
    }
    
    private void ClearRewardDisplay()
    {
        if (rewardImage != null) 
        {
            rewardImage.sprite = null;
            rewardImage.gameObject.SetActive(false);
        }
        if (rewardText != null) 
        {
            rewardText.text = "";
            rewardText.gameObject.SetActive(false);
        }
    }
    
    private void DecrementBoxCount()
    {
        // Decrement the tracked count
        if (boxesCount > 0)
        {
            boxesCount--;
            
            // Update display
            if (boxesText != null)
            {
                boxesText.text = $"Boxes: {boxesCount}";
            }
            
            // Update open button state
            if (openButton != null)
            {
                openButton.interactable = boxesCount > 0;
            }
        }
    }
    
    private void OnItemDropped(int itemDefId)
    {
        // Check if the dropped item is a blind box
        if (itemDefId == blindBoxItemDefId)
        {
            Debug.Log($"Blind box dropped! Incrementing box count from {boxesCount} to {boxesCount + 1}");
            
            // Increment our tracked count
            boxesCount++;
            
            // Update display if panel is active
            if (blindBoxPanel != null && blindBoxPanel.activeSelf)
            {
                if (boxesText != null)
                {
                    boxesText.text = $"Boxes: {boxesCount}";
                }
                
                // Update open button state
                if (openButton != null)
                {
                    openButton.interactable = boxesCount > 0;
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        if (SteamInventoryManager.Instance != null)
        {
            SteamInventoryManager.Instance.OnExchangeCompleted -= OnExchangeCompleted;
            SteamInventoryManager.Instance.OnItemDropped -= OnItemDropped;
        }
    }
}