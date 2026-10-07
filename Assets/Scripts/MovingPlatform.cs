using UnityEngine;
using UnityEngine.InputSystem;

public class MovingPlatform : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        HandleAttach(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        HandleDetach(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleAttach(collision.gameObject);
    }

    private void OnCollisionExit(Collision collision)
    {
        HandleDetach(collision.gameObject);
    }

    private void HandleAttach(GameObject target)
    {
        if (target.CompareTag("Player") || target.GetComponent<PlayerController>() != null)
        {
            target.transform.SetParent(transform);
        }
    }

    private void HandleDetach(GameObject target)
    {
        if (target.CompareTag("Player") || target.GetComponent<PlayerController>() != null)
        {
            if (target.transform.parent == transform)
            {
                target.transform.SetParent(null);
            }
        }
    }
}
