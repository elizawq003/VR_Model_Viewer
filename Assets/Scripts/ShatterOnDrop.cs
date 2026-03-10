using UnityEngine;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;

public class ShatterOnDrop : MonoBehaviour
{
   
    public GameObject fracturedPrefab;

    [Header("Settings")]
    //public float shatterHeight = 0.5f;     // How close to ground (y=0) triggers shatter
    public float debrisRotation = 2f;      // How much pieces rotate randomly

    [Header("Floating Debris Settings")]
    public float expandAmount = 0.15f;     // How far pieces spread from center (gap size)
    public float floatTargetHeight = 1.5f;       // How high the whole thing floats up
    public float bobSpeed = 1.5f;          // How fast pieces bob up and down
    public float bobAmount = 0.02f;        // How much each piece bobs
    public float rotateSpeed = 15f;        // Slow rotation of entire debris cloud

    [Header("Timing")]
    public float expandDuration = 1f;      // How long the expand animation takes
    public float reassembleTime = 3f;      // Seconds before reassembly
    public float reassembleSpeed = 2f;     // How fast pieces lerp back

    [Header("Floor Detection")]
    public string floorTag = "Floor";      // Tag to identify the floor
    public float impactThreshold = 2f;// Minimum impact force to trigger shatter
    public float throwWindow = 3f;         // Seconds after release that shatter can happen

    private XRGrabInteractable grabInteractable;
    private Vector3 originalScale;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool isHeld = false;
    private bool isShattered = false;
    private bool wasThrown = false;        // Only true after player releases
    private float timeSinceRelease = 0f;

    void Start()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        originalScale = transform.localScale;
        homePosition = transform.position;
        homeRotation = transform.rotation;

        if (grabInteractable == null)
        {
            Debug.LogError("ShatterOnDrop: No XR Grab Interactable found on this object!");
            return;
        }
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
    }

    void Update()
    {
        // After releasing, count down the throw window
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
        /*
        if (transform.position.y < shatterHeight)
        {
            StartCoroutine(ShatterAndReassemble());
        }
        */
        isHeld = false;
        wasThrown = true;
        timeSinceRelease = 0f;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Only shatter if:
        // 1. We hit the floor
        // 2. We are NOT being held
        // 3. We are NOT already shattered
        // 4. The impact is hard enough
        // 5. The player recently threw it (not just falling off pedestal)

        if (!isHeld && !isShattered && wasThrown && collision.gameObject.CompareTag(floorTag))
        {
            float impactForce = collision.relativeVelocity.magnitude;
            if(impactForce >= impactThreshold)
            {
                wasThrown = false;
               // Vector3 contactPoint = collision.contacts[0].point;
                Debug.Log("Impact force: " + impactForce + " — SHATTER!");
                StartCoroutine(ShatterAndReassemble());
            }
            
        }
    }

    IEnumerator ShatterAndReassemble()
    {
        isShattered = true;

        if (fracturedPrefab == null)
        {
            Debug.LogError("ShatterOnDrop: No fractured prefab assigned!");
            isShattered = false;
            yield break;
        }

        // Save position and rotation before shattering
        /*
        Vector3 shatterPosition = transform.position;
        Quaternion shatterRotation = transform.rotation;
        */
        Vector3 impactPosition = transform.position;
        Quaternion impactRotation = transform.rotation;

        // Hide the original object
        SetOriginalVisible(false);

        // Disable grabbing while shattered
        if (grabInteractable != null)
            grabInteractable.enabled = false;

        // Disable rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Move hidden original out of the way
        transform.position = new Vector3(0, -100, 0);


        // Spawn debris at impact location
        GameObject debris = Instantiate(fracturedPrefab, impactPosition, impactRotation);
        debris.transform.localScale = originalScale;

        int childCount = debris.transform.childCount;
        Vector3[] originalLocalPositions = new Vector3[childCount];
        Vector3[] expandedLocalPositions = new Vector3[childCount];
        Quaternion[] originalLocalRotations = new Quaternion[childCount];
        Quaternion[] expandedLocalRotations = new Quaternion[childCount];
        float[] bobOffsets = new float[childCount];

        // Calculate center of all pieces
        Vector3 centerLocal = Vector3.zero;
        for (int i = 0; i < childCount; i++)
        {
            centerLocal += debris.transform.GetChild(i).localPosition;
        }
        centerLocal /= childCount;

        // Store original positions and calculate expanded positions
        for (int i = 0; i < childCount; i++)
        {
            Transform piece = debris.transform.GetChild(i);
            originalLocalPositions[i] = piece.localPosition;
            originalLocalRotations[i] = piece.localRotation;

            // Each piece moves outward from center, creating gaps
            Vector3 directionFromCenter = (piece.localPosition - centerLocal);
            if (directionFromCenter.magnitude < 0.001f)
            {
                directionFromCenter = Random.insideUnitSphere;
            }
            directionFromCenter = directionFromCenter.normalized;

            expandedLocalPositions[i] = piece.localPosition + directionFromCenter * expandAmount;

            // Small random rotation for each piece
            expandedLocalRotations[i] = piece.localRotation * Quaternion.Euler(
                Random.Range(-debrisRotation, debrisRotation),
                Random.Range(-debrisRotation, debrisRotation),
                Random.Range(-debrisRotation, debrisRotation)
            );

            // Random bob phase so pieces don't all bob in sync
            bobOffsets[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        // --- Phase 1: Expand outward + float up ---
        float elapsed = 0f;
        Vector3 floatStartPos = debris.transform.position;
        float safeY = Mathf.Max(floatTargetHeight, floatStartPos.y + 0.5f);
        Vector3 floatEndPos = new Vector3(floatStartPos.x, safeY, floatStartPos.z);

        
        while (elapsed < expandDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / expandDuration);

            // Float the whole debris object up
            debris.transform.position = Vector3.Lerp(floatStartPos, floatEndPos, t);

            // Expand each piece outward
            for (int i = 0; i < childCount; i++)
            {
                Transform piece = debris.transform.GetChild(i);
                piece.localPosition = Vector3.Lerp(originalLocalPositions[i], expandedLocalPositions[i], t);
                piece.localRotation = Quaternion.Lerp(originalLocalRotations[i], expandedLocalRotations[i], t);
            }

            yield return null;
        }

        // --- Phase 2: Float and bob for reassembleTime seconds ---
        float floatElapsed = 0f;

        while (floatElapsed < reassembleTime)
        {
            floatElapsed += Time.deltaTime;

            // Slowly rotate the entire debris cloud
            debris.transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

            // Bob each piece individually
            for (int i = 0; i < childCount; i++)
            {
                Transform piece = debris.transform.GetChild(i);
                float bob = Mathf.Sin((floatElapsed * bobSpeed) + bobOffsets[i]) * bobAmount;
                piece.localPosition = expandedLocalPositions[i] + Vector3.up * bob;
            }

            yield return null;
        }

        // --- Phase 3: Reassemble ---
        Vector3[] currentWorldPositions = new Vector3[childCount];
        Quaternion[] currentWorldRotations = new Quaternion[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform piece = debris.transform.GetChild(i);
            currentWorldPositions[i] = piece.position;
            currentWorldRotations[i] = piece.rotation;
        }

        
        debris.transform.position = homePosition;
        debris.transform.rotation = homeRotation;

        // Convert current world positions to new local positions
        Vector3[] reassembleStartLocal = new Vector3[childCount];
        Quaternion[] reassembleStartRotLocal = new Quaternion[childCount];

        for (int i = 0; i < childCount; i++)
        {
            Transform piece = debris.transform.GetChild(i);
            piece.position = currentWorldPositions[i];
            piece.rotation = currentWorldRotations[i];
            reassembleStartLocal[i] = piece.localPosition;
            reassembleStartRotLocal[i] = piece.localRotation;
        }

        elapsed = 0f;
        float reassembleDuration = 1f / reassembleSpeed;

        while (elapsed < reassembleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / reassembleDuration);

            for (int i = 0; i < childCount; i++)
            {
                Transform piece = debris.transform.GetChild(i);
                piece.localPosition = Vector3.Lerp(reassembleStartLocal[i], originalLocalPositions[i], t);
                piece.localRotation = Quaternion.Lerp(reassembleStartRotLocal[i], originalLocalRotations[i], t);
            }

            yield return null;
        }

        // Destroy the debris
        Destroy(debris);

        // Restore at home position (pedestal)
        transform.position = homePosition;
        transform.rotation = homeRotation;
        SetOriginalVisible(true);

        // Re-enable physics
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Re-enable grabbing
        if (grabInteractable != null)
            grabInteractable.enabled = true;

        isShattered = false;
        Debug.Log("Object reassembled on pedestal!");
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