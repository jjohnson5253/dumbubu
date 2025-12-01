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
        // Load textures if not assigned
        if (brownTexture == null)
        {
            brownTexture = Resources.Load<Texture2D>("Art/Textures/brown_texture");
        }
        if (whiteTexture == null)
        {
            whiteTexture = Resources.Load<Texture2D>("Art/Textures/white_texture");
        }
        
        // Find the material if not assigned
        if (targetMaterial == null)
        {
            // Try to find the Labubu model and get its material
            GameObject labubu = GameObject.Find("Labubu");
            if (labubu != null)
            {
                SkinnedMeshRenderer renderer = labubu.GetComponentInChildren<SkinnedMeshRenderer>();
                if (renderer != null && renderer.materials.Length > 0)
                {
                    targetMaterial = renderer.materials[0];
                    Debug.Log("Found mat_0 material on Labubu");
                }
            }
        }
    }
    
    public void SwitchToBrown()
    {
        if (targetMaterial != null && brownTexture != null)
        {
            targetMaterial.mainTexture = brownTexture;
            Debug.Log("Switched to brown texture");
        }
        else
        {
            Debug.LogWarning("Cannot switch to brown - material or texture missing");
        }
    }
    
    public void SwitchToWhite()
    {
        if (targetMaterial != null && whiteTexture != null)
        {
            targetMaterial.mainTexture = whiteTexture;
            Debug.Log("Switched to white texture");
        }
        else
        {
            Debug.LogWarning("Cannot switch to white - material or texture missing");
        }
    }
}
