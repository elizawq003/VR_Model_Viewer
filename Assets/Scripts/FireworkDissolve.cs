using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;

public class FireworkDissolve : MonoBehaviour
{
    
    public GameObject fireworkPrefab;

    [Header("Floor Detection")]
    public string floorTag = "Floor";
    public float impactThreshold = 2f;
    public float throwWindow = 3f;

    [Header("Timing")]
    public float reassembleDelay = 4f;
    public float reassembleSpeed = 2f;

    private XRGrabInteractable grabInteractable;
    private Vector3 originalScale;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool isHeld = false;
    private bool isDissolving = false;
    private bool wasThrown = false;
    private float timeSinceRelease = 0f;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        originalScale = transform.localScale;
        homePosition = transform.position;
        homeRotation = transform.rotation;

        if (grabInteractable == null)
        {
            Debug.LogError("FireworkDissolve: No XR Grab Interactable found!");
            return;
        }
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void Update()
    {
        if (wasThrown)
        {
            timeSinceRelease += Time.deltaTime;
            if (timeSinceRelease > throwWindow)
            {
                wasThrown = false;
                timeSinceRelease = 0f;
            }
        }
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
        wasThrown = false;
        timeSinceRelease = 0f;
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        wasThrown = true;
        timeSinceRelease = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isHeld && !isDissolving && wasThrown && collision.gameObject.CompareTag(floorTag))
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if (impactForce >= impactThreshold)
            {
                wasThrown = false;
                Debug.Log("Impact force: " + impactForce + " — FIREWORK!");
                StartCoroutine(FireworkAndReassemble());
            }
        }
    }

    IEnumerator FireworkAndReassemble()
    {
        isDissolving = true;

        Vector3 impactPosition = transform.position;

        // Hide and freeze original
        SetOriginalVisible(false);

        if (grabInteractable != null)
            grabInteractable.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Move hidden original out of the way
        transform.position = new Vector3(0, -100, 0);

        // Spawn the firework effect at impact position
        GameObject firework = null;
        if (fireworkPrefab != null)
        {
            firework = Instantiate(fireworkPrefab, impactPosition, Quaternion.identity);

            // Make sure it plays
            ParticleSystem ps = firework.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                // Force stop and replay in case Play On Awake was on or off
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }

            // Also play any child particle systems
            ParticleSystem[] childPS = firework.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem child in childPS)
            {
                child.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                child.Play();
            }
        }
        else
        {
            Debug.LogWarning("FireworkDissolve: No firework prefab assigned!");
        }

        // Wait for firework to finish
        yield return new WaitForSeconds(reassembleDelay);

        // Clean up firework
        if (firework != null)
            Destroy(firework);

        // Reassemble: scale up from nothing at home position
        transform.position = homePosition;
        transform.rotation = homeRotation;
        transform.localScale = Vector3.zero;
        SetOriginalVisible(true);

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Scale up smoothly
        float elapsed = 0f;
        float scaleDuration = 1f / reassembleSpeed;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / scaleDuration);
            transform.localScale = originalScale * t;
            yield return null;
        }

        transform.localScale = originalScale;

        // Re-enable everything
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (grabInteractable != null)
            grabInteractable.enabled = true;

        isDissolving = false;
        Debug.Log("Firework object reassembled!");
    }

    void SetOriginalVisible(bool visible)
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer r in renderers)
        {
            r.enabled = visible;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            c.enabled = visible;
        }
    }

    void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}