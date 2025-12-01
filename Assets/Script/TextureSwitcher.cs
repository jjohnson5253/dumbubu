using UnityEngine;

public class TextureSwitcher : MonoBehaviour
{
    public static TextureSwitcher Instance { get; private set; }
    
    [Header("Textures")]
    public Texture2D brownTexture;
    public Texture2D whiteTexture;
    
    [Header("Material")]
    public Material targetMaterial;
    
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
        
        // Check if textures are assigned
        Debug.Log($"Brown texture assigned: {brownTexture != null}");
        if (brownTexture != null) Debug.Log($"Brown texture name: {brownTexture.name}");
        
        Debug.Log($"White texture assigned: {whiteTexture != null}");
        if (whiteTexture != null) Debug.Log($"White texture name: {whiteTexture.name}");
        
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
    
    public void SwitchToBrown()
    {
        Debug.Log("SwitchToBrown() called");
        Debug.Log($"targetMaterial: {targetMaterial != null}, brownTexture: {brownTexture != null}");
        
        if (targetMaterial != null && brownTexture != null)
        {
            targetMaterial.mainTexture = brownTexture;
            Debug.Log($"Switched to brown texture. Current texture: {targetMaterial.mainTexture.name}");
        }
        else
        {
            Debug.LogWarning($"Cannot switch to brown - targetMaterial: {targetMaterial}, brownTexture: {brownTexture}");
        }
    }
    
    public void SwitchToWhite()
    {
        Debug.Log("SwitchToWhite() called");
        Debug.Log($"targetMaterial: {targetMaterial != null}, whiteTexture: {whiteTexture != null}");
        
        if (targetMaterial != null && whiteTexture != null)
        {
            targetMaterial.mainTexture = whiteTexture;
            Debug.Log($"Switched to white texture. Current texture: {targetMaterial.mainTexture.name}");
        }
        else
        {
            Debug.LogWarning($"Cannot switch to white - targetMaterial: {targetMaterial}, whiteTexture: {whiteTexture}");
        }
    }
}
