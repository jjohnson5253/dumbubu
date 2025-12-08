using UnityEngine;

public class GrenadeSpawner : MonoBehaviour
{
    [Header("Grenade Settings")]
    public GameObject grenadePrefab;
    
    [Header("Input Settings")]
    public KeyCode spawnKey = KeyCode.Mouse1; // Right mouse button
    
    private Camera camera;

    private void Start()
    {
        camera = Camera.main;
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

        // Get mouse position in world space
        Vector3 mouseWorldPos = camera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0; // Ensure it's at z=0 for 2D

        // Instantiate the grenade at mouse position
        Instantiate(grenadePrefab, mouseWorldPos, Quaternion.identity);
    }
}
