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
    
    private int blindBoxItemDefId = 69421; // Blind box item
    private int generatorItemDefId = 69422; // Generator for exchange
    private SteamInventoryResult_t exchangeResult = SteamInventoryResult_t.Invalid;
    private Callback<SteamInventoryResultReady_t> steamInventoryResultReady;
    
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
        
        // Clear reward display initially
        ClearRewardDisplay();
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
        if (!SteamManager.Initialized)
        {
            SetBoxesText(0);
            return;
        }
        
        // Get count of blind box items from Steam inventory
        if (SteamInventoryManager.Instance != null && SteamInventoryManager.Instance.IsInventoryLoaded())
        {
            int boxCount = GetBlindBoxCount();
            SetBoxesText(boxCount);
            
            // Enable/disable open button based on box count
            if (openButton != null)
            {
                openButton.interactable = boxCount > 0;
            }
        }
        else
        {
            SetBoxesText(0);
            if (openButton != null)
            {
                openButton.interactable = false;
            }
        }
    }
    
    private int GetBlindBoxCount()
    {
        // This would need to be implemented in SteamInventoryManager
        // For now, return 0 as placeholder
        // TODO: Add method to SteamInventoryManager to get item count by ID
        return 0;
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
        
        // Setup exchange: consume 1 blind box (69421) to get generator result (69422)
        SteamItemDef_t[] recipe = new SteamItemDef_t[] { new SteamItemDef_t(generatorItemDefId) };
        SteamItemInstanceID_t[] materials = new SteamItemInstanceID_t[] { }; // Need actual instance ID
        uint[] materialQuantities = new uint[] { 1 };
        
        // Note: This is simplified - you'd need to get the actual instance ID of the blind box item
        // from the inventory to use in ExchangeItems
        Debug.LogWarning("ExchangeItems implementation needs actual item instance IDs from inventory");
        
        // For now, just simulate the exchange
        SimulateExchange();
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
                                Debug.Log($"Received item: {itemDefId}");
                                ShowReward(itemDefId);
                            }
                        }
                    }
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
    
    private void OnDestroy()
    {
        if (exchangeResult != SteamInventoryResult_t.Invalid)
        {
            SteamInventory.DestroyResult(exchangeResult);
        }
    }
}