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

    private SpringJoint2D springJoint;
    private Camera camera;
    private ParticleSystem collisionParticleSystem;
    private ParticleSystem explosionParticleSystem;
    private float lastClickTime = 0f;

    private void Start()
    {
        camera = Camera.main;
        CreateCollisionParticleSystem();
        CreateExplosionParticleSystem();
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
                camera.ScreenToWorldPoint(Input.mousePosition),
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
        Vector2 mouseWorldPos = camera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

        if (hit.collider != null && hit.rigidbody && !hit.rigidbody.isKinematic)
        {
            // Calculate direction from click point to object center
            Vector2 objectCenter = hit.rigidbody.position;
            Vector2 clickPoint = hit.point;
            Vector2 explosionDirection = (objectCenter - clickPoint).normalized;

            // Apply explosion force
            hit.rigidbody.AddForce(explosionDirection * explosionForce, ForceMode2D.Impulse);

            // Add some random rotation for effect
            hit.rigidbody.AddTorque(UnityEngine.Random.Range(-5f, 5f), ForceMode2D.Impulse);

            // Spawn explosion particles at click point
            explosionParticleSystem.transform.position = clickPoint;
            explosionParticleSystem.Emit(25);
        }
    }

    IEnumerator DragObject()
    {
        float oldDrag = this.springJoint.connectedBody.drag;
        float oldAngularDrag = this.springJoint.connectedBody.angularDrag;
        springJoint.connectedBody.drag = drag;
        springJoint.connectedBody.angularDrag = angularDrag;

        while (Input.GetMouseButton(0))
        {
            Vector3 mousePos = camera.ScreenToWorldPoint(Input.mousePosition);
            springJoint.transform.position = mousePos;
            yield return null;
        }

        if (springJoint.connectedBody)
        {
            springJoint.connectedBody.drag = oldDrag;
            springJoint.connectedBody.angularDrag = oldAngularDrag;
            springJoint.connectedBody = null;
        }
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
        }
    }
}