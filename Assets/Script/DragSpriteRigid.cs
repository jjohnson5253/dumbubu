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
    public float rotationCorrectionSpeed = 10f; // Speed at which rotation corrects when dropped
    public bool lockRotationDuringDrag = true; // Lock rotation while dragging
    public bool continuousRotationCorrection = true; // Continuously correct rotation
    public float stillTimeBeforeGetUp = 2f; // Time character must be still before getting up
    public float maxVelocityForStill = 0.5f; // Maximum velocity to consider character "still"

    [Header("Throw Settings")]
    public float throwSensitivity = 15f; // How responsive the throw is (higher = more responsive)
    public float maxThrowVelocity = 15f; // Maximum velocity when throwing

    private Camera mainCamera;
    private ParticleSystem collisionParticleSystem;
    private ParticleSystem explosionParticleSystem;
    private float lastClickTime = 0f;
    private Vector2 previousMousePosition;

    // Animation/Ragdoll system
    private Animator animator;
    private bool isRagdoll = false;
    private bool isBeingDragged = false;
    private Coroutine resumeAnimationCoroutine;
    private Coroutine rotationCorrectionCoroutine;
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

    private void FixedUpdate()
    {
        // Continuously correct rotation when not being dragged
        if (rb != null && continuousRotationCorrection && !isBeingDragged)
        {
            // Only correct if rotation is significantly off
            float currentRotation = rb.rotation;
            // Normalize rotation to -180 to 180 range
            while (currentRotation > 180f) currentRotation -= 360f;
            while (currentRotation < -180f) currentRotation += 360f;

            // If rotation is off by more than 2 degrees, correct it
            if (Mathf.Abs(currentRotation) > 2f)
            {
                // Use the shorter rotation path
                float rotationDiff = -currentRotation;
                if (rotationDiff > 180f) rotationDiff -= 360f;
                if (rotationDiff < -180f) rotationDiff += 360f;

                // Apply rotation correction smoothly
                float correction = rotationDiff * rotationCorrectionSpeed * Time.fixedDeltaTime;
                float newRotation = currentRotation + correction;
                rb.MoveRotation(newRotation);
            }
            else if (Mathf.Abs(currentRotation) > 0.1f)
            {
                // Snap to 0 if very close
                rb.rotation = 0f;
            }
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
        
        if (rb == null)
        {
            isBeingDragged = false;
            yield break;
        }

        float oldDrag = rb.drag;
        float oldAngularDrag = rb.angularDrag;
        bool wasKinematic = rb.isKinematic;
        float oldGravityScale = rb.gravityScale;
        
        // Set drag to 0 during dragging for free movement
        rb.drag = 0f;
        rb.angularDrag = angularDrag;
        // Temporarily disable gravity for smoother dragging
        rb.gravityScale = 0f;

        // Lock rotation during drag if enabled
        if (lockRotationDuringDrag)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.rotation = 0f; // Ensure it starts upright
        }
        else
        {
            // Ensure no position constraints during drag
            rb.constraints = RigidbodyConstraints2D.None;
        }

        // Enter ragdoll mode - enable floating animation
        EnterRagdollMode();

        // Initialize previous mouse position and offset
        Vector2 initialMousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 offset = (Vector2)transform.position - initialMousePos;
        previousMousePosition = initialMousePos;

        while (Input.GetMouseButton(0))
        {
            Vector2 currentMousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 targetPosition = currentMousePos + offset;
            
            // Calculate velocity based on mouse movement for throw effect
            Vector2 mouseDelta = currentMousePos - previousMousePosition;
            Vector2 throwVelocity = mouseDelta * throwSensitivity;
            
            // Also calculate follow velocity to keep object near mouse
            Vector2 direction = targetPosition - (Vector2)transform.position;
            Vector2 followVelocity = direction * throwSensitivity * 2f; // More responsive following
            
            // Combine both velocities, prioritizing follow
            Vector2 targetVelocity = followVelocity + throwVelocity * 0.5f;
            
            // Clamp velocity to max throw velocity
            if (targetVelocity.magnitude > maxThrowVelocity)
            {
                targetVelocity = targetVelocity.normalized * maxThrowVelocity;
            }
            
            // Apply velocity directly to rigidbody
            rb.velocity = targetVelocity;
            
            // Update previous mouse position for throw calculation
            previousMousePosition = currentMousePos;
            
            // Ensure floating animation stays active during drag
            if (animator != null && !animator.GetBool("isFloating"))
            {
                animator.SetBool("isFloating", true);
                animator.SetBool("isDancing", false);
            }
            
            // Keep rotation locked during drag
            if (lockRotationDuringDrag)
            {
                rb.rotation = 0f;
            }
            
            yield return null;
        }

        // Restore drag values and gravity
        rb.drag = oldDrag;
        rb.angularDrag = oldAngularDrag;
        rb.gravityScale = oldGravityScale;

        // Restore original constraints, but ensure X and Y position are not frozen
        if (lockRotationDuringDrag)
        {
            // Only restore rotation constraint, keep position free
            RigidbodyConstraints2D newConstraints = originalConstraints;
            // Remove any position constraints (X or Y freeze)
            newConstraints &= ~RigidbodyConstraints2D.FreezePositionX;
            newConstraints &= ~RigidbodyConstraints2D.FreezePositionY;
            // Keep rotation constraint from original
            if ((originalConstraints & RigidbodyConstraints2D.FreezeRotation) != 0)
            {
                newConstraints |= RigidbodyConstraints2D.FreezeRotation;
            }
            rb.constraints = newConstraints;
        }
        else
        {
            // If not locking rotation during drag, just restore original constraints
            rb.constraints = originalConstraints;
        }

        isBeingDragged = false;
        
        // Start rotation correction when dropped
        //StartRotationCorrection();
        
        // Resume animation after a delay
        if (resumeAnimationCoroutine != null)
        {
            StopCoroutine(resumeAnimationCoroutine);
        }
        resumeAnimationCoroutine = StartCoroutine(ResumeAnimationAfterDelay());
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

            // Start rotation correction after collision
            if (rb != null && !isBeingDragged)
            {
                StartRotationCorrection();
            }

            // Resume animation after collision
            if (!isBeingDragged)
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
        if (animator != null)
        {
            isRagdoll = true;
            // Ensure floating animation is set
            animator.SetBool("isDancing", false);
            animator.SetBool("isFloating", true);
             }
    }

    private void ExitRagdollMode()
    {
        if (animator != null)
        {
            isRagdoll = false;

         
            animator.SetBool("isFloating", false);
            animator.SetBool("isDancing", true);
   
        }
    }

    private bool IsStill()
    {
        if (rb == null) return false;
        
        // Check if velocity is below threshold (character is still)
        float velocityMagnitude = rb.velocity.magnitude;
        return velocityMagnitude <= maxVelocityForStill;
    }

    private IEnumerator ResumeAnimationAfterDelay()
    {
        if (animator == null || rb == null)
        {
            yield break;
        }

        float stillTime = 0f;
        float maxWaitTime = 10f; // Maximum time to wait (prevent infinite waiting)
        float totalWaitTime = 0f;

        // Wait until character has been still for the required time
        while (stillTime < stillTimeBeforeGetUp && totalWaitTime < maxWaitTime)
        {
            if (IsStill())
            {
                stillTime += Time.deltaTime;
            }
            else
            {
                // Reset still time if character starts moving
                stillTime = 0f;
            }
            
            totalWaitTime += Time.deltaTime;
            yield return null;
        }

        // Trigger get_up if character has been still long enough
        if (stillTime >= stillTimeBeforeGetUp)
        {
       
            yield return new WaitForSeconds(timeToResumeAnimation);
            ExitRagdollMode();
        }
        else
        {
   
            ExitRagdollMode();
        }
    }

    private void StartRotationCorrection()
    {
        // Stop any existing rotation correction
        if (rotationCorrectionCoroutine != null)
        {
            StopCoroutine(rotationCorrectionCoroutine);
        }
        rotationCorrectionCoroutine = StartCoroutine(CorrectRotation());
    }

    private IEnumerator CorrectRotation()
    {
        if (rb == null) yield break;

        // Target rotation is 0 (upright)
        float targetRotation = 0f;
        float currentRotation = rb.rotation;

        // Normalize rotation to -180 to 180 range
        while (currentRotation > 180f) currentRotation -= 360f;
        while (currentRotation < -180f) currentRotation += 360f;

        // If rotation is close to 0, we're done
        if (Mathf.Abs(currentRotation) < 1f)
        {
            rb.rotation = 0f;
            yield break;
        }

        // Smoothly rotate to target
        while (Mathf.Abs(currentRotation - targetRotation) > 0.5f)
        {
            currentRotation = rb.rotation;
            // Normalize rotation to -180 to 180 range
            while (currentRotation > 180f) currentRotation -= 360f;
            while (currentRotation < -180f) currentRotation += 360f;

            // Use the shorter rotation path
            float rotationDiff = targetRotation - currentRotation;
            if (rotationDiff > 180f) rotationDiff -= 360f;
            if (rotationDiff < -180f) rotationDiff += 360f;

            // Apply rotation correction
            float newRotation = currentRotation + rotationDiff * rotationCorrectionSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(newRotation);

            yield return new WaitForFixedUpdate();
        }

        // Snap to final rotation
        rb.rotation = 0f;
        rotationCorrectionCoroutine = null;
    }
}
