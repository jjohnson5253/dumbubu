using UnityEngine;
using System.Collections.Generic;
using Steamworks;

public class SteamInventoryManager : MonoBehaviour
{
    public static SteamInventoryManager Instance { get; private set; }
    
    private HashSet<int> ownedItemDefIds = new HashSet<int>();
    private bool inventoryLoaded = false;
    
    private Callback<SteamInventoryResultReady_t> m_SteamInventoryResultReady;
    private SteamInventoryResult_t m_inventoryResult = SteamInventoryResult_t.Invalid;
    
    public System.Action OnInventoryLoaded;
    
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
        if (SteamManager.Initialized)
        {
            m_SteamInventoryResultReady = Callback<SteamInventoryResultReady_t>.Create(OnSteamInventoryResultReady);
            LoadInventory();
        }
        else
        {
            Debug.LogWarning("SteamManager not initialized. Cannot load inventory.");
        }
    }
    
    public void LoadInventory()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Cannot load inventory - Steam not initialized");
            return;
        }
        
        Debug.Log("Loading Steam inventory...");
        
        // Get all items in the user's inventory
        if (SteamInventory.GetAllItems(out m_inventoryResult))
        {
            Debug.Log("GetAllItems request sent successfully");
        }
        else
        {
            Debug.LogError("Failed to request inventory items");
        }
    }
    
    private void OnSteamInventoryResultReady(SteamInventoryResultReady_t pCallback)
    {
        if (pCallback.m_result != EResult.k_EResultOK)
        {
            Debug.LogError($"Steam Inventory Result failed: {pCallback.m_result}");
            return;
        }
        
        if (pCallback.m_handle == SteamInventoryResult_t.Invalid)
        {
            Debug.LogError("Invalid inventory result handle");
            return;
        }
        
        Debug.Log("Steam Inventory Result Ready!");
        
        // Get the number of items
        uint itemCount = 0;
        if (!SteamInventory.GetResultItems(pCallback.m_handle, null, ref itemCount))
        {
            Debug.LogError("Failed to get item count");
            return;
        }
        
        Debug.Log($"Inventory contains {itemCount} items");
        
        if (itemCount == 0)
        {
            inventoryLoaded = true;
            OnInventoryLoaded?.Invoke();
            return;
        }
        
        // Get the actual items
        SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
        if (SteamInventory.GetResultItems(pCallback.m_handle, items, ref itemCount))
        {
            ownedItemDefIds.Clear();
            
            foreach (var item in items)
            {
                int itemDefId = item.m_iDefinition.m_SteamItemDef;
                ownedItemDefIds.Add(itemDefId);
                Debug.Log($"Player owns item: ItemDefID={itemDefId}, Quantity={item.m_unQuantity}");
            }
            
            inventoryLoaded = true;
            OnInventoryLoaded?.Invoke();
        }
        else
        {
            Debug.LogError("Failed to get result items");
        }
        
        // Clean up the result
        SteamInventory.DestroyResult(pCallback.m_handle);
    }
    
    /// <summary>
    /// Check if the player owns a specific item
    /// </summary>
    public bool HasItem(int itemDefId)
    {
        if (!inventoryLoaded)
        {
            Debug.LogWarning("Inventory not loaded yet");
            return false;
        }
        
        return ownedItemDefIds.Contains(itemDefId);
    }
    
    /// <summary>
    /// Check if inventory has been loaded
    /// </summary>
    public bool IsInventoryLoaded()
    {
        return inventoryLoaded;
    }
    
    /// <summary>
    /// Get all owned item definition IDs
    /// </summary>
    public HashSet<int> GetOwnedItems()
    {
        return new HashSet<int>(ownedItemDefIds);
    }
    
    private void OnDestroy()
    {
        if (m_inventoryResult != SteamInventoryResult_t.Invalid)
        {
            SteamInventory.DestroyResult(m_inventoryResult);
        }
    }
}
