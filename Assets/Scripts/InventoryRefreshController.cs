using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public static class InventoryRefreshController
{
    /// <summary>
    /// Start the inventory refresh process with cooldown and animation
    /// </summary>
    /// <param name="monoBehaviour">The MonoBehaviour to run the coroutine on</param>
    /// <param name="refreshButton">The refresh button to animate and disable</param>
    /// <param name="debugPrefix">Prefix for debug messages to identify which panel is refreshing</param>
    /// <param name="onRefreshStart">Optional action to execute before starting the refresh (e.g., setting flags)</param>
    public static void RefreshInventory(MonoBehaviour monoBehaviour, Button refreshButton, string debugPrefix = "", System.Action onRefreshStart = null)
    {
        Debug.Log($"{debugPrefix}Refresh button pressed - starting 10 second cooldown");
        
        if (SteamInventoryManager.Instance != null)
        {
            monoBehaviour.StartCoroutine(RefreshInventoryCoroutine(refreshButton, debugPrefix, onRefreshStart));
        }
        else
        {
            Debug.LogWarning($"{debugPrefix}SteamInventoryManager.Instance is null - cannot refresh inventory");
        }
    }
    
    private static System.Collections.IEnumerator RefreshInventoryCoroutine(Button refreshButton, string debugPrefix, System.Action onRefreshStart = null)
    {
        // Disable refresh button
        if (refreshButton != null)
        {
            refreshButton.interactable = false;
        }
        
        // Spin for 10 seconds
        Debug.Log($"{debugPrefix}Waiting 10 seconds before refreshing inventory...");
        float elapsedTime = 0f;
        float spinDuration = 10f;
        float spinSpeed = -360f; // degrees per second (negative for counterclockwise)
        
        while (elapsedTime < spinDuration)
        {
            // Rotate the refresh button
            if (refreshButton != null)
            {
                refreshButton.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
            }
            
            elapsedTime += Time.deltaTime;
            yield return null; // Wait for next frame
        }
        
        // Reset button rotation
        if (refreshButton != null)
        {
            refreshButton.transform.rotation = Quaternion.identity;
        }
        
        // Execute optional pre-refresh action (e.g., setting reloadRequest flag)
        onRefreshStart?.Invoke();
        
        // Perform the actual refresh
        Debug.Log($"{debugPrefix}10 seconds elapsed - reloading Steam inventory");
        SteamInventoryManager.Instance.LoadInventory();
        
        // Re-enable refresh button
        if (refreshButton != null)
        {
            refreshButton.interactable = true;
        }
    }
}