using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 targetOffset = new Vector3(0, 1.4f, 0);
    public float distance = 4.8f;
    [Range(-20f, 75f)]
    public float pitch = 15f;
    public float yaw = 0f;
    public float mouseSensitivity = 1.5f;
    public bool requireRightClick = false;
    public float positionDamping = 0.05f;
    public bool enableCollisionAvoidance = true;
    public LayerMask collisionLayers = ~0;
    public float minDistance = 1.0f;

    private Vector3 currentVelocity;

    private void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            bool canRotate = !requireRightClick || mouse.rightButton.isPressed;
            if (canRotate)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * mouseSensitivity * 0.1f;
                pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity * 0.1f, -15f, 65f);
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
            else return;
        }

        Vector3 targetPosition = target.position + targetOffset;
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 backDirection = orbitRotation * Vector3.back;
        Vector3 desiredPosition = targetPosition + backDirection * distance;

        if (enableCollisionAvoidance && Application.isPlaying)
        {
            if (Physics.SphereCast(targetPosition, 0.2f, backDirection, out RaycastHit hit, distance, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                float targetDist = Mathf.Clamp(hit.distance - 0.1f, minDistance, distance);
                desiredPosition = targetPosition + backDirection * targetDist;
            }
        }

        if (Application.isPlaying)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionDamping);
            transform.rotation = Quaternion.LookRotation(targetPosition - transform.position);
        }
        else
        {
            transform.position = desiredPosition;
            transform.LookAt(targetPosition);
        }
    }
}
