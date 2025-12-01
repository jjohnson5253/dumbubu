using UnityEngine;
using System.Collections.Generic;

public enum DumbubuColor
{
    Brown,
    White
}

public class TextureSwitcher : MonoBehaviour
{
    public static TextureSwitcher Instance { get; private set; }
    
    [System.Serializable]
    public class ColorTexture
    {
        public DumbubuColor color;
        public Texture2D texture;
        public int requiredItemDefId; // 0 = no requirement, otherwise requires Steam inventory item
    }
    
    [Header("Color Textures")]
    public List<ColorTexture> colorTextures = new List<ColorTexture>();
    
    [Header("Material")]
    public Material targetMaterial;
    
    private Dictionary<DumbubuColor, Texture2D> textureLookup;
    private Dictionary<DumbubuColor, int> itemRequirements;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        Debug.Log("TextureSwitcher Start() called");
        
        // Build lookup dictionaries
        textureLookup = new Dictionary<DumbubuColor, Texture2D>();
        itemRequirements = new Dictionary<DumbubuColor, int>();
        
        foreach (var colorTexture in colorTextures)
        {
            if (colorTexture.texture != null)
            {
                textureLookup[colorTexture.color] = colorTexture.texture;
                itemRequirements[colorTexture.color] = colorTexture.requiredItemDefId;
                Debug.Log($"{colorTexture.color} texture assigned: {colorTexture.texture.name}, requires item: {colorTexture.requiredItemDefId}");
            }
        }
        
        // Load textures from Resources if not assigned
        if (!textureLookup.ContainsKey(DumbubuColor.Brown))
        {
            Texture2D brownTexture = Resources.Load<Texture2D>("Textures/brown_texture");
            if (brownTexture != null)
            {
                textureLookup[DumbubuColor.Brown] = brownTexture;
                Debug.Log($"Loaded Brown texture from Resources: {brownTexture.name}");
            }
        }
        
        if (!textureLookup.ContainsKey(DumbubuColor.White))
        {
            Texture2D whiteTexture = Resources.Load<Texture2D>("Textures/white_texture");
            if (whiteTexture != null)
            {
                textureLookup[DumbubuColor.White] = whiteTexture;
                Debug.Log($"Loaded White texture from Resources: {whiteTexture.name}");
            }
        }
        
        // Find the material if not assigned
        if (targetMaterial == null)
        {
            Debug.Log("Searching for Dumbubu GameObject...");
            // Try to find the Dumbubu model and get its material
            GameObject dumbubu = GameObject.Find("Dumbubu");
            if (dumbubu != null)
            {
                Debug.Log("Found Dumbubu GameObject");
                SkinnedMeshRenderer renderer = dumbubu.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null && renderer.materials.Length > 0)
                {
                    targetMaterial = renderer.materials[0];
                    Debug.Log($"Found mat_0 material on Dumbubu: {targetMaterial.name}");
                }
                else
                {
                    Debug.LogWarning("SkinnedMeshRenderer not found or has no materials");
                }
            }
            else
            {
                Debug.LogWarning("Dumbubu GameObject not found in scene");
            }
        }
        else
        {
            Debug.Log($"Target material already assigned: {targetMaterial.name}");
        }
    }
    
    public void SwitchColor(DumbubuColor color)
    {
        Debug.Log($"SwitchColor({color}) called");
        
        if (targetMaterial == null)
        {
            Debug.LogWarning("Cannot switch color - targetMaterial is null");
            return;
        }
        
        // Check if this color requires a Steam inventory item
        if (itemRequirements.TryGetValue(color, out int requiredItemId) && requiredItemId > 0)
        {
            if (SteamInventoryManager.Instance == null || !SteamInventoryManager.Instance.IsInventoryLoaded())
            {
                Debug.LogWarning($"Cannot switch to {color} - Steam inventory not loaded");
                return;
            }
            
            if (!SteamInventoryManager.Instance.HasItem(requiredItemId))
            {
                Debug.LogWarning($"Cannot switch to {color} - player doesn't own item {requiredItemId}");
                return;
            }
        }
        
        if (textureLookup.TryGetValue(color, out Texture2D texture))
        {
            targetMaterial.mainTexture = texture;
            Debug.Log($"Switched to {color} texture. Current texture: {targetMaterial.mainTexture.name}");
        }
        else
        {
            Debug.LogWarning($"Cannot switch to {color} - texture not found in lookup");
        }
    }
    
    /// <summary>
    /// Check if a color is available based on Steam inventory
    /// </summary>
    public bool IsColorAvailable(DumbubuColor color)
    {
        // Check if texture exists
        if (!textureLookup.ContainsKey(color))
        {
            return false;
        }
        
        // Check if item is required
        if (itemRequirements.TryGetValue(color, out int requiredItemId) && requiredItemId > 0)
        {
            if (SteamInventoryManager.Instance == null || !SteamInventoryManager.Instance.IsInventoryLoaded())
            {
                return false;
            }
            
            return SteamInventoryManager.Instance.HasItem(requiredItemId);
        }
        
        // No requirement, always available
        return true;
    }
    
    // Legacy methods for backwards compatibility
    public void SwitchToBrown()
    {
        SwitchColor(DumbubuColor.Brown);
    }
    
    public void SwitchToWhite()
    {
        SwitchColor(DumbubuColor.White);
    }
}
