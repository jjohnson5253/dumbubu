using UnityEngine;
using System.Collections.Generic;

public enum LabubuColor
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
        public LabubuColor color;
        public Texture2D texture;
    }
    
    [Header("Color Textures")]
    public List<ColorTexture> colorTextures = new List<ColorTexture>();
    
    [Header("Material")]
    public Material targetMaterial;
    
    private Dictionary<LabubuColor, Texture2D> textureLookup;
    
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
        
        // Build lookup dictionary
        textureLookup = new Dictionary<LabubuColor, Texture2D>();
        foreach (var colorTexture in colorTextures)
        {
            if (colorTexture.texture != null)
            {
                textureLookup[colorTexture.color] = colorTexture.texture;
                Debug.Log($"{colorTexture.color} texture assigned: {colorTexture.texture.name}");
            }
        }
        
        // Load textures from Resources if not assigned
        if (!textureLookup.ContainsKey(LabubuColor.Brown))
        {
            Texture2D brownTexture = Resources.Load<Texture2D>("Textures/brown_texture");
            if (brownTexture != null)
            {
                textureLookup[LabubuColor.Brown] = brownTexture;
                Debug.Log($"Loaded Brown texture from Resources: {brownTexture.name}");
            }
        }
        
        if (!textureLookup.ContainsKey(LabubuColor.White))
        {
            Texture2D whiteTexture = Resources.Load<Texture2D>("Textures/white_texture");
            if (whiteTexture != null)
            {
                textureLookup[LabubuColor.White] = whiteTexture;
                Debug.Log($"Loaded White texture from Resources: {whiteTexture.name}");
            }
        }
        
        // Find the material if not assigned
        if (targetMaterial == null)
        {
            Debug.Log("Searching for Labubu GameObject...");
            // Try to find the Labubu model and get its material
            GameObject labubu = GameObject.Find("Labubu");
            if (labubu != null)
            {
                Debug.Log("Found Labubu GameObject");
                SkinnedMeshRenderer renderer = labubu.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null && renderer.materials.Length > 0)
                {
                    targetMaterial = renderer.materials[0];
                    Debug.Log($"Found mat_0 material on Labubu: {targetMaterial.name}");
                }
                else
                {
                    Debug.LogWarning("SkinnedMeshRenderer not found or has no materials");
                }
            }
            else
            {
                Debug.LogWarning("Labubu GameObject not found in scene");
            }
        }
        else
        {
            Debug.Log($"Target material already assigned: {targetMaterial.name}");
        }
    }
    
    public void SwitchColor(LabubuColor color)
    {
        Debug.Log($"SwitchColor({color}) called");
        
        if (targetMaterial == null)
        {
            Debug.LogWarning("Cannot switch color - targetMaterial is null");
            return;
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
    
    // Legacy methods for backwards compatibility
    public void SwitchToBrown()
    {
        SwitchColor(LabubuColor.Brown);
    }
    
    public void SwitchToWhite()
    {
        SwitchColor(LabubuColor.White);
    }
}
