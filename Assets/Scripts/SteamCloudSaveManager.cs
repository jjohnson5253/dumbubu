using UnityEngine;
using System;
using Steamworks;

public class SteamCloudSaveManager : MonoBehaviour
{
    public static SteamCloudSaveManager Instance { get; private set; }
    
    [Header("Steam Cloud Settings")]
    public string saveFileName = "pet_save.dat";
    
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
    
    /// <summary>
    /// Save data to Steam Cloud
    /// </summary>
    public bool SaveToSteamCloud(string data)
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam not initialized. Cannot save to cloud.");
            return false;
        }
        
        try
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
            bool success = SteamRemoteStorage.FileWrite(saveFileName, bytes, bytes.Length);
            
            if (success)
            {
                Debug.Log($"Successfully saved to Steam Cloud: {saveFileName}");
                return true;
            }
            else
            {
                Debug.LogError("Failed to write to Steam Cloud");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving to Steam Cloud: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Load data from Steam Cloud
    /// </summary>
    public string LoadFromSteamCloud()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam not initialized. Cannot load from cloud.");
            return null;
        }
        
        try
        {
            if (!SteamRemoteStorage.FileExists(saveFileName))
            {
                Debug.Log("No save file found in Steam Cloud");
                return null;
            }
            
            int fileSize = SteamRemoteStorage.GetFileSize(saveFileName);
            if (fileSize <= 0)
            {
                Debug.LogWarning("Save file is empty");
                return null;
            }
            
            byte[] buffer = new byte[fileSize];
            int bytesRead = SteamRemoteStorage.FileRead(saveFileName, buffer, fileSize);
            
            if (bytesRead == fileSize)
            {
                string data = System.Text.Encoding.UTF8.GetString(buffer);
                Debug.Log($"Successfully loaded from Steam Cloud: {saveFileName}");
                return data;
            }
            else
            {
                Debug.LogError("Failed to read complete file from Steam Cloud");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading from Steam Cloud: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Check if Steam Cloud is available
    /// </summary>
    public bool IsSteamCloudAvailable()
    {
        return SteamManager.Initialized && 
               SteamRemoteStorage.IsCloudEnabledForAccount() && 
               SteamRemoteStorage.IsCloudEnabledForApp();
    }
    
    /// <summary>
    /// Get Steam Cloud info for debugging
    /// </summary>
    public void LogSteamCloudInfo()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogWarning("Steam not initialized.");
            return;
        }
        
        Debug.Log($"Steam Cloud enabled for account: {SteamRemoteStorage.IsCloudEnabledForAccount()}");
        Debug.Log($"Steam Cloud enabled for app: {SteamRemoteStorage.IsCloudEnabledForApp()}");
        Debug.Log($"Steam Cloud quota: {SteamRemoteStorage.GetQuota(out ulong total, out ulong available)} - Total: {total} Available: {available}");
        
        int fileCount = SteamRemoteStorage.GetFileCount();
        Debug.Log($"Files in Steam Cloud: {fileCount}");
        
        for (int i = 0; i < fileCount; i++)
        {
            string filename = SteamRemoteStorage.GetFileNameAndSize(i, out int size);
            Debug.Log($"  {i}: {filename} ({size} bytes)");
        }
    }
}