using System;
using UnityEngine;
using System.Collections;

public class DragSpriteRigid : MonoBehaviour
{
    public float dampingRatio = 5.0f;
    public float frequency = 2.5f;
    public float drag = 10.0f;
    public float angularDrag = 5.0f;
    public float minCollisionVelocity = 0.5f;

    [Header("Explosion Settings")]
    public float explosionForce = 10f;
    public float explosionRadius = 0.5f;
    public float doubleClickTime = 0.3f;

    [Header("Animation Settings")]
    public float timeToResumeAnimation = 0.5f; // Time after collision before resuming animation

    private SpringJoint2D springJoint;
    private Camera mainCamera;
    private ParticleSystem collisionParticleSystem;
    private ParticleSystem explosionParticleSystem;
    private float lastClickTime = 0f;
    
    // Animation/Ragdoll system
    private Animator animator;
    private bool isRagdoll = false;
    private bool isBeingDragged = false;
    private Coroutine resumeAnimationCoroutine;

    private void Start()
    {
        mainCamera = Camera.main;
        CreateCollisionParticleSystem();
        CreateExplosionParticleSystem();
        
        // Find the Animator component
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("No Animator component found on " + gameObject.name + ". Ragdoll mode won't affect animations.");
        }
        else
        {
            Debug.Log("Animator found! Animation/Ragdoll mode ready.");
        }
    }

    private void CreateCollisionParticleSystem()
    {
        GameObject particleObj = new GameObject("CollisionParticles");
        particleObj.transform.SetParent(transform);
        collisionParticleSystem = particleObj.AddComponent<ParticleSystem>();

        var main = collisionParticleSystem.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 3f;
        main.startSize = 0.2f;
        main.startColor = Color.yellow;
        main.maxParticles = 20;

        var emission = collisionParticleSystem.emission;
        emission.enabled = false;

        var shape = collisionParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        collisionParticleSystem.Stop();
    }

    private void CreateExplosionParticleSystem()
    {
        GameObject particleObj = new GameObject("ExplosionParticles");
        particleObj.transform.SetParent(transform);
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

    void Update()
    {
        // Check for right click explosion
        if (Input.GetMouseButtonDown(1))
        {
            CheckForExplosion();
            return;
        }

        // Check for double click explosion
        if (Input.GetMouseButtonDown(0))
        {
            if (Time.time - lastClickTime < doubleClickTime)
            {
                CheckForExplosion();
                lastClickTime = 0f;
                return;
            }
            lastClickTime = Time.time;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        RaycastHit2D hit = Physics2D.Raycast(
                mainCamera.ScreenToWorldPoint(Input.mousePosition),
                Vector2.zero);

        if (hit.collider == null || !hit.rigidbody || hit.rigidbody.isKinematic)
        {
            return;
        }

        if (!springJoint)
        {
            GameObject obj = new GameObject("Rigidbody2D dragger");
            Rigidbody2D body = obj.AddComponent<Rigidbody2D>() as Rigidbody2D;
            this.springJoint = obj.AddComponent<SpringJoint2D>() as SpringJoint2D;
            body.isKinematic = true;
        }

        springJoint.transform.position = hit.point;
        springJoint.anchor = Vector2.zero;
        springJoint.connectedAnchor = hit.transform.InverseTransformPoint(hit.point);
        springJoint.dampingRatio = this.dampingRatio;
        springJoint.frequency = this.frequency;
        springJoint.enableCollision = false;
        springJoint.connectedBody = hit.rigidbody;
        springJoint.distance = 0.2f;
        springJoint.autoConfigureDistance = false;

        StartCoroutine(DragObject());
    }

    void CheckForExplosion()
    {
        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

        if (hit.collider != null && hit.rigidbody && !hit.rigidbody.isKinematic)
        {
            // Get object center for positioning the floating text
            Vector2 objectCenter = hit.rigidbody.position;
            
            // Show floating points display above the sprite
            if (PointsManager.Instance != null)
            {
                int totalPoints = PointsManager.Instance.GetPoints();
                Debug.Log($"Right-clicked on sprite. Showing points: {totalPoints}");
                FloatingPointsDisplay.ShowPoints(objectCenter, totalPoints);
            }
            else
            {
                Debug.LogWarning("PointsManager.Instance is null!");
            }
        }
        else
        {
            Debug.Log("Right-click didn't hit a valid sprite");
        }
    }

    IEnumerator DragObject()
    {
        float oldDrag = this.springJoint.connectedBody.drag;
        float oldAngularDrag = this.springJoint.connectedBody.angularDrag;
        springJoint.connectedBody.drag = drag;
        springJoint.connectedBody.angularDrag = angularDrag;

        // Enter ragdoll mode - disable animation
        EnterRagdollMode();

        while (Input.GetMouseButton(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            springJoint.transform.position = mousePos;
            yield return null;
        }

        if (springJoint.connectedBody)
        {
            springJoint.connectedBody.drag = oldDrag;
            springJoint.connectedBody.angularDrag = oldAngularDrag;
            springJoint.connectedBody = null;
        }
        
        isBeingDragged = false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.relativeVelocity.magnitude > minCollisionVelocity)
        {
            collisionParticleSystem.transform.position = collision.contacts[0].point;
            collisionParticleSystem.Emit(10);
            
            // Add points for collision and save to Steam Cloud
            if (PointsManager.Instance != null)
            {
                PointsManager.Instance.AddPoints();
            }
            
            // Resume animation after collision
            if (isRagdoll && !isBeingDragged)
            {
                // Cancel any existing resume animation coroutine
                if (resumeAnimationCoroutine != null)
                {
                    StopCoroutine(resumeAnimationCoroutine);
                }
                // Start a new one
                resumeAnimationCoroutine = StartCoroutine(ResumeAnimationAfterDelay());
            }
        }
    }
    
    private void EnterRagdollMode()
    {
        if (animator != null && !isRagdoll)
        {
            isRagdoll = true;
            isBeingDragged = true;
            animator.enabled = false;
            Debug.Log("Entered ragdoll mode - animation disabled");
        }
    }
    
    private void ExitRagdollMode()
    {
        if (animator != null && isRagdoll)
        {
            isRagdoll = false;
            animator.enabled = true;
            Debug.Log("Exited ragdoll mode - animation resumed");
        }
    }
    
    private IEnumerator ResumeAnimationAfterDelay()
    {
        yield return new WaitForSeconds(timeToResumeAnimation);
        ExitRagdollMode();
    }
}