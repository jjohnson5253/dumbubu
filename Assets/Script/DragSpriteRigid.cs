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
        // Don't show points if one is already displaying
        if (FloatingPointsDisplay.IsDisplaying())
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
        // Set isBeingDragged immediately to prevent issues
        isBeingDragged = true;
        
        float oldDrag = this.springJoint.connectedBody.drag;
        float oldAngularDrag = this.springJoint.connectedBody.angularDrag;
        springJoint.connectedBody.drag = drag;
        springJoint.connectedBody.angularDrag = angularDrag;

        // Lock rotation during drag if enabled
        if (rb != null && lockRotationDuringDrag)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.rotation = 0f; // Ensure it starts upright
        }

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
            
            // Keep rotation locked during drag
            if (rb != null && lockRotationDuringDrag)
            {
                rb.rotation = 0f;
            }
            
            yield return null;
        }

        if (springJoint.connectedBody)
        {
            springJoint.connectedBody.drag = oldDrag;
            springJoint.connectedBody.angularDrag = oldAngularDrag;
            springJoint.connectedBody = null;
        }

        // Restore original constraints
        if (rb != null && lockRotationDuringDrag)
        {
            rb.constraints = originalConstraints;
        }

        isBeingDragged = false;
        
        // Start rotation correction when dropped
        if (rb != null)
        {
            StartRotationCorrection();
        }
        
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
                animator.SetTrigger("fall");
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
            animator.SetTrigger("fall");
            Debug.Log("Entered ragdoll mode - floating animation enabled");
            Debug.Log($"[ENTER] isDancing: {animator.GetBool("isDancing")} | isFloating: {animator.GetBool("isFloating")}");
        }
    }

    private void ExitRagdollMode()
    {
        if (animator != null)
        {
            isRagdoll = false;

          Debug.Log("get_up3");
            animator.SetBool("isFloating", false);
            animator.SetBool("isDancing", true);
            animator.SetTrigger("dance");

            Debug.Log($"[EXIT] isDancing: {animator.GetBool("isDancing")} | isFloating: {animator.GetBool("isFloating")}");
            Debug.Log("Exited ragdoll mode - animation resumed");
        }
    }

    private IEnumerator ResumeAnimationAfterDelay()
    {
        animator.SetTrigger("get_up");
        yield return new WaitForSeconds(timeToResumeAnimation);
        ExitRagdollMode();
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
