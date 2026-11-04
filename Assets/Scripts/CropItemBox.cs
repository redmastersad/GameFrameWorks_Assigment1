using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CropItemBox : MonoBehaviour
{
    [SerializeField] private int deliveredCropsCount;

    public int DeliveredCropsCount => deliveredCropsCount;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void EnsureTriggerCollider()
    {
        BoxCollider[] colliders = GetComponents<BoxCollider>();
        bool hasTrigger = false;
        foreach (var col in colliders)
        {
            if (col != null && col.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.55f, 0f);
            trigger.size = new Vector3(1.6f, 0.95f, 1.6f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null)
        {
            TryDeliverCrop(other.gameObject);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other != null)
        {
            TryDeliverCrop(other.gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision != null && collision.gameObject != null)
        {
            TryDeliverCrop(collision.gameObject);
        }
    }

    private void TryDeliverCrop(GameObject candidate)
    {
        if (candidate == null) return;

        CropItem crop = candidate.GetComponentInParent<CropItem>();
        if (crop == null)
        {
            crop = candidate.GetComponent<CropItem>();
        }

        if (crop != null && !crop.IsHeld && !crop.IsDelivered)
        {
            deliveredCropsCount++;
            Debug.Log($"[CropItemBox] Crop '{crop.name}' delivered to Item Box! Total delivered: {deliveredCropsCount}");
            crop.DeliverToBox();
        }
    }
}
