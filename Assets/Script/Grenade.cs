using System.Collections;
using UnityEngine;

public class Grenade : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionForce = 10f;
    public float explosionRadius = 2f;
    public float fuseTime = 2f;

    [Header("Physics Settings")]
    public float gravityScale = 1f;
    public float drag = 0.5f;
    public float angularDrag = 0.5f;

    private Camera mainCamera;
    private ParticleSystem explosionParticleSystem;
    private Rigidbody2D rb;
    private float timeSpawned;

    private void Awake()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        
        // Set up rigidbody physics
        if (rb != null)
        {
            rb.gravityScale = gravityScale;
            rb.drag = drag;
            rb.angularDrag = angularDrag;
        }
        
        CreateExplosionParticleSystem();
        timeSpawned = Time.time;
    }

    private void Start()
    {
        // Start countdown to explosion
        StartCoroutine(ExplodeAfterDelay());
    }

    private void CreateExplosionParticleSystem()
    {
        GameObject particleObj = new GameObject("ExplosionParticles");
        particleObj.transform.SetParent(transform);
        particleObj.transform.localPosition = Vector3.zero;
        explosionParticleSystem = particleObj.AddComponent<ParticleSystem>();

        var main = explosionParticleSystem.main;
        main.startLifetime = 0.7f;
        main.startSpeed = 5f;
        main.startSize = 0.3f;
        main.startColor = new Color(1f, 0.5f, 0f); // Orange color
        main.maxParticles = 30;

        var emission = explosionParticleSystem.emission;
        emission.enabled = false;

        var shape = explosionParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Add some size over lifetime for explosion effect
        var sizeOverLifetime = explosionParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

        explosionParticleSystem.Stop();
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(fuseTime);
        Explode();
    }

    private void Explode()
    {
        Vector2 explosionCenter = transform.position;

        // Spawn explosion particles at grenade position
        explosionParticleSystem.transform.position = explosionCenter;
        explosionParticleSystem.Emit(25);

        // Find all rigidbodies within explosion radius
        Collider2D[] colliders = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius);

        foreach (Collider2D col in colliders)
        {
            Rigidbody2D targetRb = col.GetComponent<Rigidbody2D>();
            
            if (targetRb != null && !targetRb.isKinematic && targetRb != rb)
            {
                // Calculate direction from explosion center to object
                Vector2 objectCenter = targetRb.position;
                Vector2 explosionDirection = (objectCenter - explosionCenter).normalized;

                // Calculate distance-based force falloff
                float distance = Vector2.Distance(explosionCenter, objectCenter);
                float forceFalloff = 1f - (distance / explosionRadius);
                forceFalloff = Mathf.Clamp01(forceFalloff);

                // Apply explosion force
                targetRb.AddForce(explosionDirection * explosionForce * forceFalloff, ForceMode2D.Impulse);

                // Add some random rotation for effect
                targetRb.AddTorque(UnityEngine.Random.Range(-5f, 5f), ForceMode2D.Impulse);
            }
        }

        // Detach particle system so it doesn't get destroyed with the grenade
        if (explosionParticleSystem != null)
        {
            explosionParticleSystem.transform.SetParent(null);
            Destroy(explosionParticleSystem.gameObject, 1f);
        }

        // Destroy the grenade immediately now that particles are detached
        Destroy(gameObject);
    }

    // Visualize explosion radius in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
