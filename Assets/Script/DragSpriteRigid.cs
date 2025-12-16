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
    public float stillTimeBeforeGetUp = 2f; // Time character must be still before getting up
    public float maxVelocityForStill = 0.5f; // Maximum velocity to consider character "still"
    public float groundOffsetFromBottom = 5f; // How far from bottom of screen to consider "on ground"

    [Header("Throw Settings")]
    public float throwSensitivity = 15f; // How responsive the throw is (higher = more responsive)
    public float maxThrowVelocity = 15f; // Maximum velocity when throwing

    private SpringJoint2D springJoint;
    private Camera mainCamera;
    private ParticleSystem collisionParticleSystem;
    private ParticleSystem explosionParticleSystem;
    private Vector2 previousMousePosition;

    // Animation/Ragdoll system
    private Animator animator;
    private bool isBeingDragged = false;
    private Coroutine resumeAnimationCoroutine;
    private Rigidbody2D rb;
    private RigidbodyConstraints2D originalConstraints;

    private void Start()
    {
        mainCamera = Camera.main;
        CreateCollisionParticleSystem();
        CreateExplosionParticleSystem();

        // Get Rigidbody2D component
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("No Rigidbody2D component found on " + gameObject.name);
        }
        else
        {
            // Store original constraints
            originalConstraints = rb.constraints;
        }

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
        // Auto-trigger floating animation when moving fast (e.g., from explosions)
        if (animator != null && rb != null && !isBeingDragged)
        {
            // If moving fast enough and not already floating, enter floating mode
            if (rb.velocity.magnitude > maxVelocityForStill && !animator.GetBool("isFloating"))
            {
                EnterRagdollMode();
                
                // Restart the exit floating animation coroutine
                if (resumeAnimationCoroutine != null)
                {
                    StopCoroutine(resumeAnimationCoroutine);
                }
                resumeAnimationCoroutine = StartCoroutine(ExitFloatingAnimation());
            }
        }
        
        // Check for right click to show menu
        if (Input.GetMouseButtonDown(1))
        {
            ShowMenu();
            return;
        }

        // Only check for drag start on mouse down, not every frame
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

        // Check if this object is the one being hit
        if (hit.rigidbody != rb)
        {
            return;
        }

        // Create spring joint for dragging
        if (!springJoint)
        {
            GameObject obj = new GameObject("Rigidbody2D dragger");
            Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
            this.springJoint = obj.AddComponent<SpringJoint2D>();
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

    void ShowMenu()
    {
        // Don't show points if one is already displaying
        if (MenuDisplay.IsDisplaying())
        {
            return;
        }
        
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
                MenuDisplay.ShowMenu(objectCenter, totalPoints);
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
        // Set isBeingDragged immediately to prevent issues
        isBeingDragged = true;
        
        if (springJoint == null || springJoint.connectedBody == null)
        {
            isBeingDragged = false;
            yield break;
        }

        float oldDrag = springJoint.connectedBody.drag;
        float oldAngularDrag = springJoint.connectedBody.angularDrag;
        
        springJoint.connectedBody.drag = drag;
        springJoint.connectedBody.angularDrag = angularDrag;

        // Enter ragdoll mode - enable floating animation
        EnterRagdollMode();

        while (Input.GetMouseButton(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            springJoint.transform.position = mousePos;
            
            // Ensure floating animation stays active during drag
            if (animator != null && !animator.GetBool("isFloating"))
            {
                animator.SetBool("isFloating", true);
                animator.SetBool("isDancing", false);
            }
            
            yield return null;
        }

        // Restore drag values
        if (springJoint.connectedBody)
        {
            springJoint.connectedBody.drag = oldDrag;
            springJoint.connectedBody.angularDrag = oldAngularDrag;
            springJoint.connectedBody = null;
        }

        isBeingDragged = false;
        
        // Resume animation after a delay
        if (resumeAnimationCoroutine != null)
        {
            StopCoroutine(resumeAnimationCoroutine);
        }
        resumeAnimationCoroutine = StartCoroutine(ExitFloatingAnimation());
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

            // Restart animation coroutine after collision (if not being dragged)
            if (!isBeingDragged)
            {
                if (resumeAnimationCoroutine != null)
                {
                    StopCoroutine(resumeAnimationCoroutine);
                }
                resumeAnimationCoroutine = StartCoroutine(ExitFloatingAnimation());
            }
        }
    }

    private void EnterRagdollMode()
    {
        if (animator != null)
        {
            // Ensure floating animation is set
            animator.SetBool("isDancing", false);
            animator.SetBool("isFloating", true);
             }
    }

    private void ExitRagdollMode()
    {
        if (animator != null)
        {
            animator.SetBool("isFloating", false);
            animator.SetBool("isDancing", true);
        }
        
        // Smoothly rotate character back to upright position
        if (rb != null)
        {
            rb.rotation = 0f;
        }
    }

    private bool IsStill()
    {
        if (rb == null) return false;
        
        // Check if velocity is below threshold (character is still)
        float velocityMagnitude = rb.velocity.magnitude;
        return velocityMagnitude <= maxVelocityForStill;
    }

    private bool IsOnGround()
    {
        if (rb == null || mainCamera == null) return false;
        
        // Calculate the bottom of the screen in world coordinates
        float screenBottom = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, 0)).y;
        float groundThreshold = screenBottom + groundOffsetFromBottom;
        
        // Check if character's Y position is at or below the ground threshold
        return transform.position.y <= groundThreshold;
    }

    private IEnumerator ExitFloatingAnimation()
    {
        if (animator == null || rb == null)
        {
            yield break;
        }

        // Wait until character stops moving AND is on the ground
        while (!IsStill() || !IsOnGround())
        {
            //Debug.Log($"Waiting... Velocity: {rb.velocity.magnitude}, Y pos: {transform.position.y}, On ground: {IsOnGround()}");
            yield return null;
        }

        Debug.Log("Character is still and on ground! Resuming dancing animation.");
        // Resume dancing animation when stopped and on ground
        ExitRagdollMode();
    }

}
