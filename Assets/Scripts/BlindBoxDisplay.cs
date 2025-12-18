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
    public Sprite blindBoxSprite; // The blind box image to show initially
    public Sprite whiteInventorySprite;
    public Sprite blueInventorySprite; 
    public Sprite pinkInventorySprite;
    
    [Header("Settings")]
    public Vector3 worldPositionOffset = Vector3.up * 3f; // Offset from Dumbubu (same as MenuDisplay)
    
    private int blindBoxItemDefId = 69421; // Blind box item
    private int generatorItemDefId = 69422; // Generator for exchange
    private SteamInventoryResult_t exchangeResult = SteamInventoryResult_t.Invalid;
    private Callback<SteamInventoryResultReady_t> steamInventoryResultReady;
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
        
        // Hide blind box panel initially
        if (blindBoxPanel != null)
        {
            blindBoxPanel.SetActive(false);
        }
        
        // Setup button listeners
        SetupButtonListeners();
        
        // Setup Steam callbacks
        if (SteamManager.Initialized)
        {
            steamInventoryResultReady = Callback<SteamInventoryResultReady_t>.Create(OnSteamInventoryResultReady);
        }
        
        // Subscribe to inventory loaded events
        if (SteamInventoryManager.Instance != null)
        {
            SteamInventoryManager.Instance.OnInventoryLoaded += OnInventoryUpdated;
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
        
        // Show blind box image initially
        Instance.ShowBlindBoxImage();
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
        
        // Recipe: generator 69422 creates random rewards from consumed blind box 69421
        SteamItemDef_t[] recipe = new SteamItemDef_t[] { new SteamItemDef_t(generatorItemDefId) };
        uint[] recipeQuantities = new uint[] { 1 };
        SteamItemInstanceID_t[] materials = new SteamItemInstanceID_t[] { blindBoxInstanceId };
        uint[] materialQuantities = new uint[] { 1 };
        
        // Perform the exchange
        if (SteamInventory.ExchangeItems(out exchangeResult, recipe, recipeQuantities, 1, materials, materialQuantities, 1))
        {
            Debug.Log("Steam exchange initiated successfully - waiting for callback");
        }
        else
        {
            Debug.LogError("Failed to initiate Steam exchange");
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
    
    private void OnSteamInventoryResultReady(SteamInventoryResultReady_t callback)
    {
        if (callback.m_handle == exchangeResult)
        {
            if (callback.m_result == EResult.k_EResultOK)
            {
                Debug.Log("Steam exchange completed successfully!");
                
                // Get the exchange result
                uint itemCount = 0;
                if (SteamInventory.GetResultItems(callback.m_handle, null, ref itemCount))
                {
                    if (itemCount > 0)
                    {
                        SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
                        if (SteamInventory.GetResultItems(callback.m_handle, items, ref itemCount))
                        {
                            foreach (var item in items)
                            {
                                int itemDefId = item.m_iDefinition.m_SteamItemDef;
                                uint quantity = item.m_unQuantity;
                                Debug.Log($"Exchange reward: ItemDefID={itemDefId}, Quantity={quantity}");
                                ShowReward(itemDefId);
                            }
                        }
                    }
                }
                
                // Reload inventory to update counts after exchange
                if (SteamInventoryManager.Instance != null)
                {
                    SteamInventoryManager.Instance.LoadInventory();
                }
            }
            else
            {
                Debug.LogError($"Exchange failed: {callback.m_result}");
                // Re-enable open button on failure
                if (openButton != null)
                {
                    openButton.interactable = true;
                }
            }
            
            // Clean up
            SteamInventory.DestroyResult(callback.m_handle);
            exchangeResult = SteamInventoryResult_t.Invalid;
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
        
        // Update boxes count
        UpdateBoxesCount();
    }
    
    private void ShowBlindBoxImage()
    {
        if (rewardImage != null && blindBoxSprite != null)
        {
            rewardImage.sprite = blindBoxSprite;
            rewardImage.gameObject.SetActive(true);
        }
        if (rewardText != null)
        {
            rewardText.text = "Drops every 10 minutes";
            rewardText.gameObject.SetActive(true);
        }
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
    
    private void OnInventoryUpdated()
    {
        Debug.Log("BlindBoxDisplay: Inventory updated callback received");
        UpdateBoxesCount();
        
        // Re-enable the open button if we have blind boxes
        int blindBoxCount = (int)SteamInventoryManager.Instance.GetItemCount(100);
        if (blindBoxCount > 0 && openButton != null)
        {
            openButton.interactable = true;
        }
    }
    
    private void OnDestroy()
    {
        if (exchangeResult != SteamInventoryResult_t.Invalid)
        {
            SteamInventory.DestroyResult(exchangeResult);
        }
        
        if (steamInventoryResultReady != null)
        {
            steamInventoryResultReady.Dispose();
        }
        
        // Unsubscribe from inventory events
        if (SteamInventoryManager.Instance != null)
        {
            SteamInventoryManager.Instance.OnInventoryLoaded -= OnInventoryUpdated;
        }
    }
}