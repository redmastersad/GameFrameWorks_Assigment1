using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CropItem : MonoBehaviour
{
    [SerializeField] private string cropType = "Vegetable";
    [SerializeField] private Vector3 heldLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 heldLocalEulerAngles = Vector3.zero;

    private Rigidbody rb;
    private Collider[] cropColliders;
    private bool isHeld;
    private bool isDelivered;
    private Coroutine ignorePlayerCollisionRoutine;

    public string CropType { get => cropType; set => cropType = value; }
    public bool IsHeld => isHeld;
    public bool IsDelivered => isDelivered;
    public Rigidbody CropRigidbody => rb;

    private void Awake()
    {
        CenterMeshAndCollider();
        InitializePhysicsComponents();
    }

    public void CenterMeshAndCollider()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters == null || meshFilters.Length == 0) return;

        bool hasBounds = false;
        Bounds combinedMeshBounds = default;
        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (!hasBounds)
            {
                combinedMeshBounds = mf.sharedMesh.bounds;
                hasBounds = true;
            }
            else
            {
                combinedMeshBounds.Encapsulate(mf.sharedMesh.bounds);
            }
        }

        if (!hasBounds) return;

        foreach (var mf in meshFilters)
        {
            if (mf != null && mf.transform != transform)
            {
                mf.transform.localPosition = -combinedMeshBounds.center;
            }
        }

        float maxMeshDim = Mathf.Max(combinedMeshBounds.size.x, combinedMeshBounds.size.y, combinedMeshBounds.size.z);
        if (maxMeshDim > 0.0001f)
        {
            float targetWorldSize = 0.36f;
            float uniformScale = targetWorldSize / maxMeshDim;
            transform.localScale = new Vector3(uniformScale, uniformScale, uniformScale);

            SphereCollider sphereCol = GetComponent<SphereCollider>();
            if (sphereCol == null)
            {
                sphereCol = gameObject.AddComponent<SphereCollider>();
            }
            sphereCol.isTrigger = false;
            sphereCol.center = Vector3.zero;
            sphereCol.radius = maxMeshDim * 0.45f;
        }
    }

    public void InitializePhysicsComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        cropColliders = GetComponentsInChildren<Collider>(true);
        bool hasSolidCollider = false;
        if (cropColliders != null)
        {
            foreach (var col in cropColliders)
            {
                if (col != null && !col.isTrigger)
                {
                    hasSolidCollider = true;
                    break;
                }
            }
        }

        if (!hasSolidCollider)
        {
            SphereCollider fallbackCol = gameObject.AddComponent<SphereCollider>();
            fallbackCol.radius = 0.2f;
            fallbackCol.center = Vector3.zero;
            cropColliders = GetComponentsInChildren<Collider>(true);
        }
    }

    public void PickUp(Transform holdPoint, Collider playerCollider = null)
    {
        if (isDelivered) return;

        CenterMeshAndCollider();
        InitializePhysicsComponents();
        isHeld = true;

        if (ignorePlayerCollisionRoutine != null)
        {
            StopCoroutine(ignorePlayerCollisionRoutine);
            ignorePlayerCollisionRoutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (cropColliders != null)
        {
            foreach (var col in cropColliders)
            {
                if (col == null) continue;
                if (playerCollider != null)
                {
                    Physics.IgnoreCollision(col, playerCollider, true);
                }
                col.enabled = false;
            }
        }

        if (holdPoint != null)
        {
            transform.SetParent(holdPoint, true);
            transform.localPosition = heldLocalPosition;
            transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);
        }
    }

    public void Throw(Vector3 throwDirection, float throwForce, float upwardImpulse, Collider playerCollider = null)
    {
        if (isDelivered) return;

        InitializePhysicsComponents();
        isHeld = false;

        transform.SetParent(null, true);

        if (cropColliders != null)
        {
            foreach (var col in cropColliders)
            {
                if (col == null) continue;
                col.enabled = true;
                if (playerCollider != null)
                {
                    Physics.IgnoreCollision(col, playerCollider, true);
                }
            }
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 dir = throwDirection.sqrMagnitude > 0.001f ? throwDirection.normalized : Vector3.forward;
            Vector3 impulse = dir * throwForce + Vector3.up * upwardImpulse;
            rb.AddForce(impulse, ForceMode.Impulse);
            rb.AddTorque(new Vector3(2.2f, 1.1f, -1.5f), ForceMode.Impulse);
        }

        if (playerCollider != null && gameObject.activeInHierarchy)
        {
            ignorePlayerCollisionRoutine = StartCoroutine(RestorePlayerCollisionAfterDelay(playerCollider, 0.35f));
        }
    }

    public void DeliverToBox()
    {
        if (isDelivered) return;
        isDelivered = true;
        isHeld = false;
        Destroy(gameObject);
    }

    private IEnumerator RestorePlayerCollisionAfterDelay(Collider playerCollider, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerCollider != null && cropColliders != null && !isHeld)
        {
            foreach (var col in cropColliders)
            {
                if (col != null)
                {
                    Physics.IgnoreCollision(col, playerCollider, false);
                }
            }
        }
        ignorePlayerCollisionRoutine = null;
    }
}
