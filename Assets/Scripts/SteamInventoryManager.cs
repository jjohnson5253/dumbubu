using UnityEngine;
using System.Collections.Generic;
using Steamworks;

public class SteamInventoryManager : MonoBehaviour
{
    public static SteamInventoryManager Instance { get; private set; }
    
    private HashSet<int> ownedItemDefIds = new HashSet<int>();
    private Dictionary<int, uint> itemQuantities = new Dictionary<int, uint>();
    private Dictionary<int, List<SteamItemInstanceID_t>> itemInstances = new Dictionary<int, List<SteamItemInstanceID_t>>();
    private bool inventoryLoaded = false;
    
    private Callback<SteamInventoryResultReady_t> m_SteamInventoryResultReady;
    private SteamInventoryResult_t m_inventoryResult = SteamInventoryResult_t.Invalid;
    private SteamInventoryResult_t m_dropResult = SteamInventoryResult_t.Invalid;
    
    public System.Action OnInventoryLoaded;
    public System.Action<int> OnItemDropped; // Called when an item is dropped with the itemDefId
    
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
            
            // Trigger initial drop check on game load
            TriggerItemDrop(69420);
            
            // Check for drops every minute
            InvokeRepeating("CheckForPlaytimeDrop", 60f, 60f);
        }
        else
        {
            Debug.LogWarning("SteamManager not initialized. Cannot load inventory.");
        }
    }
    
    private void CheckForPlaytimeDrop()
    {
        TriggerItemDrop(69420);
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
            Debug.LogError($"Failed to get item count for result handle {pCallback.m_handle}. Result: {pCallback.m_result}");
            
            // Don't return immediately - clean up the result and continue
            SteamInventory.DestroyResult(pCallback.m_handle);
            
            // If this was the main inventory result, we still need to mark as loaded and notify
            if (pCallback.m_handle == m_inventoryResult)
            {
                m_inventoryResult = SteamInventoryResult_t.Invalid;
                inventoryLoaded = true;
                OnInventoryLoaded?.Invoke();
            }
            return;
        }
        
        Debug.Log($"Inventory result contains {itemCount} items");
        
        // Check if this is a drop result (from TriggerItemDrop)
        if (pCallback.m_handle == m_dropResult)
        {
            if (itemCount > 0)
            {
                // Get the dropped items
                SteamItemDetails_t[] items = new SteamItemDetails_t[itemCount];
                if (SteamInventory.GetResultItems(pCallback.m_handle, items, ref itemCount))
                {
                    foreach (var item in items)
                    {
                        int itemDefId = item.m_iDefinition.m_SteamItemDef;
                        Debug.Log($"Item dropped! ItemDefID={itemDefId}, Quantity={item.m_unQuantity}");
                        OnItemDropped?.Invoke(itemDefId);
                    }
                    
                    // Reload inventory to get updated state
                    LoadInventory();
                }
            }
            else
            {
                Debug.Log("TriggerItemDrop returned empty - player not eligible yet");
            }
            
            SteamInventory.DestroyResult(pCallback.m_handle);
            m_dropResult = SteamInventoryResult_t.Invalid;
            return;
        }
        
        // Regular inventory load
        if (itemCount == 0)
        {
            inventoryLoaded = true;
            OnInventoryLoaded?.Invoke();
            return;
        }
        
        // Get the actual items
        SteamItemDetails_t[] allItems = new SteamItemDetails_t[itemCount];
        if (SteamInventory.GetResultItems(pCallback.m_handle, allItems, ref itemCount))
        {
            ownedItemDefIds.Clear();
            itemQuantities.Clear();
            itemInstances.Clear();
            
            foreach (var item in allItems)
            {
                int itemDefId = item.m_iDefinition.m_SteamItemDef;
                uint quantity = item.m_unQuantity;
                SteamItemInstanceID_t instanceId = item.m_itemId;
                
                ownedItemDefIds.Add(itemDefId);
                
                // Add to existing quantity if we already have this itemDefId
                if (itemQuantities.ContainsKey(itemDefId))
                {
                    itemQuantities[itemDefId] += quantity;
                }
                else
                {
                    itemQuantities[itemDefId] = quantity;
                }
                
                // Store instance IDs for each itemDefId
                if (!itemInstances.ContainsKey(itemDefId))
                {
                    itemInstances[itemDefId] = new List<SteamItemInstanceID_t>();
                }
                itemInstances[itemDefId].Add(instanceId);
                
                Debug.Log($"Player owns item: ItemDefID={itemDefId}, ItemID={instanceId}, Quantity={quantity}, Total for DefID: {itemQuantities[itemDefId]}");
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
    
    /// <summary>
    /// Get the quantity of a specific item owned by the player
    /// </summary>
    /// <param name="itemDefId">The item definition ID to check</param>
    /// <returns>The quantity owned, or 0 if not owned</returns>
    public uint GetItemCount(int itemDefId)
    {
        if (!inventoryLoaded)
        {
            Debug.LogWarning("Inventory not loaded yet");
            return 0;
        }
        
        return itemQuantities.TryGetValue(itemDefId, out uint quantity) ? quantity : 0;
    }
    
    /// <summary>
    /// Get a Steam item instance ID for a specific item definition ID
    /// </summary>
    /// <param name="itemDefId">The item definition ID to get an instance for</param>
    /// <returns>A Steam item instance ID, or Invalid if none available</returns>
    public SteamItemInstanceID_t GetItemInstance(int itemDefId)
    {
        if (!inventoryLoaded)
        {
            Debug.LogWarning("Inventory not loaded yet");
            return SteamItemInstanceID_t.Invalid;
        }
        
        if (itemInstances.TryGetValue(itemDefId, out List<SteamItemInstanceID_t> instances) && instances.Count > 0)
        {
            return instances[0]; // Return the first available instance
        }
        
        return SteamItemInstanceID_t.Invalid;
    }
    
    /// <summary>
    /// Get all Steam item instance IDs for a specific item definition ID
    /// </summary>
    /// <param name="itemDefId">The item definition ID to get instances for</param>
    /// <returns>List of Steam item instance IDs</returns>
    public List<SteamItemInstanceID_t> GetItemInstances(int itemDefId)
    {
        if (!inventoryLoaded)
        {
            Debug.LogWarning("Inventory not loaded yet");
            return new List<SteamItemInstanceID_t>();
        }
        
        return itemInstances.TryGetValue(itemDefId, out List<SteamItemInstanceID_t> instances) ? 
               new List<SteamItemInstanceID_t>(instances) : new List<SteamItemInstanceID_t>();
    }
    
    /// <summary>
    /// Trigger a playtime item drop. Call this at significant game moments (end of level, match, etc.)
    /// </summary>
    /// <param name="playtimeGeneratorDefId">The itemdefid of the "playtimegenerator" type item</param>
    public void TriggerItemDrop(int playtimeGeneratorDefId)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Cannot trigger item drop - Steam not initialized");
            return;
        }
        
        Debug.Log($"Triggering item drop for playtime generator {playtimeGeneratorDefId}...");
        
        SteamItemDef_t itemDef = new SteamItemDef_t(playtimeGeneratorDefId);
        
        if (SteamInventory.TriggerItemDrop(out m_dropResult, itemDef))
        {
            Debug.Log("TriggerItemDrop called successfully - waiting for callback");
        }
        else
        {
            Debug.LogError("Failed to trigger item drop");
        }
    }
    
    private void OnDestroy()
    {
        if (m_inventoryResult != SteamInventoryResult_t.Invalid)
        {
            SteamInventory.DestroyResult(m_inventoryResult);
        }
        
        if (m_dropResult != SteamInventoryResult_t.Invalid)
        {
            SteamInventory.DestroyResult(m_dropResult);
        }
    }
}
