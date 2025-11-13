using UnityEngine;
using System;

[Serializable]
public class SaveData
{
    public int points = 0;
    public DateTime lastSaved = DateTime.Now;
}

public class PointsManager : MonoBehaviour
{
    public static PointsManager Instance { get; private set; }
    
    [Header("Points Settings")]
    public int pointsPerCollision = 1;
    public int currentPoints = 0;
    
    [Header("Display Settings")]
    public bool showPointsInConsole = true;
    
    private SaveData saveData = new SaveData();
    private const float AUTO_SAVE_INTERVAL = 5f; // Save every 5 seconds
    private float lastSaveTime = 0f;
    
    public event Action<int> OnPointsChanged;
    
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
            return;
        }
    }
    
    private void Start()
    {
        // Wait a frame for SteamManager to initialize, then load save data
        Invoke(nameof(LoadGameData), 0.1f);
    }
    
    private void Update()
    {
        // Auto-save periodically
        if (Time.time - lastSaveTime > AUTO_SAVE_INTERVAL)
        {
            SaveGameData();
            lastSaveTime = Time.time;
        }
    }
    
    /// <summary>
    /// Add points from collision
    /// </summary>
    public void AddPoints()
    {
        AddPoints(pointsPerCollision);
    }
    
    /// <summary>
    /// Add specific amount of points
    /// </summary>
    public void AddPoints(int amount)
    {
        currentPoints += amount;
        saveData.points = currentPoints;
        
        if (showPointsInConsole)
        {
            Debug.Log($"Points added! Current total: {currentPoints} (+{amount})");
        }
        
        OnPointsChanged?.Invoke(currentPoints);
        
        // Save immediately when points are added
        SaveGameData();
    }
    
    /// <summary>
    /// Get current points
    /// </summary>
    public int GetPoints()
    {
        return currentPoints;
    }
    
    /// <summary>
    /// Reset points to zero
    /// </summary>
    public void ResetPoints()
    {
        currentPoints = 0;
        saveData.points = 0;
        
        if (showPointsInConsole)
        {
            Debug.Log("Points reset to 0");
        }
        
        OnPointsChanged?.Invoke(currentPoints);
        SaveGameData();
    }
    
    /// <summary>
    /// Save game data to Steam Cloud
    /// </summary>
    private void SaveGameData()
    {
        if (SteamCloudSaveManager.Instance == null) return;
        
        try
        {
            saveData.points = currentPoints;
            saveData.lastSaved = DateTime.Now;
            
            string jsonData = JsonUtility.ToJson(saveData, true);
            bool success = SteamCloudSaveManager.Instance.SaveToSteamCloud(jsonData);
            
            if (success && showPointsInConsole)
            {
                Debug.Log($"Game saved! Points: {currentPoints}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving game data: {e.Message}");
        }
    }
    
    /// <summary>
    /// Load game data from Steam Cloud
    /// </summary>
    private void LoadGameData()
    {
        if (SteamCloudSaveManager.Instance == null) 
        {
            Debug.LogWarning("SteamCloudSaveManager not found. Starting with 0 points.");
            return;
        }
        
        try
        {
            string jsonData = SteamCloudSaveManager.Instance.LoadFromSteamCloud();
            
            if (!string.IsNullOrEmpty(jsonData))
            {
                saveData = JsonUtility.FromJson<SaveData>(jsonData);
                currentPoints = saveData.points;
                
                if (showPointsInConsole)
                {
                    Debug.Log($"Game loaded! Points: {currentPoints} (Last saved: {saveData.lastSaved})");
                }
                
                OnPointsChanged?.Invoke(currentPoints);
            }
            else
            {
                // No save data found, start fresh
                if (showPointsInConsole)
                {
                    Debug.Log("No save data found. Starting with 0 points.");
                }
                currentPoints = 0;
                OnPointsChanged?.Invoke(currentPoints);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading game data: {e.Message}");
            currentPoints = 0;
            OnPointsChanged?.Invoke(currentPoints);
        }
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        // Save when application is paused (like alt-tab)
        if (pauseStatus)
        {
            SaveGameData();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        // Save when application loses focus
        if (!hasFocus)
        {
            SaveGameData();
        }
    }
    
    /// <summary>
    /// Manual save for testing
    /// </summary>
    [ContextMenu("Force Save")]
    public void ForceSave()
    {
        SaveGameData();
        Debug.Log("Force save triggered!");
    }
    
    /// <summary>
    /// Manual load for testing
    /// </summary>
    [ContextMenu("Force Load")]
    public void ForceLoad()
    {
        LoadGameData();
        Debug.Log("Force load triggered!");
    }
    
    /// <summary>
    /// Show Steam Cloud debug info
    /// </summary>
    [ContextMenu("Show Steam Cloud Info")]
    public void ShowSteamCloudInfo()
    {
        if (SteamCloudSaveManager.Instance != null)
        {
            SteamCloudSaveManager.Instance.LogSteamCloudInfo();
        }
    }
}