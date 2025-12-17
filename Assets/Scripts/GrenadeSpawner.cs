using UnityEngine;

public class GrenadeSpawner : MonoBehaviour
{
    [Header("Grenade Settings")]
    public GameObject grenadePrefab;
    
    [Header("Input Settings")]
    public KeyCode spawnKey = KeyCode.Mouse1; // Right mouse button
    
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        // Check for configured spawn key and if grenade mode is enabled
        if (Input.GetKeyDown(spawnKey) && MenuDisplay.IsGrenadeModeEnabled())
        {
            SpawnGrenade();
        }
    }

    private void SpawnGrenade()
    {
        if (grenadePrefab == null)
        {
            Debug.LogWarning("Grenade prefab not assigned to GrenadeSpawner!");
            return;
        }

        // Don't spawn if clicking on the menu display
        if (MenuDisplay.IsDisplaying())
        {
            return;
        }

        // Get mouse position in world space
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = -2; // Place in front of Dumbubu (lower Z = closer to camera)

        // don't spawn grenade if clicking rigid body (likely character). 
        // This lets dragging of character still happen.
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);
        if (hit.collider != null && hit.rigidbody != null)
        {
            
            return;
        }

        // Instantiate the grenade at mouse position
        Instantiate(grenadePrefab, mouseWorldPos, Quaternion.identity);
    }
}
