using UnityEngine;
using UnityEngine.InputSystem;

public class CameraOrbitController : MonoBehaviour
{
    [Header("Mode")]
    public bool isOrbitPivot = true;

    [Header("Target")]
    public Transform target;
    public Vector3 targetOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Orbit Angles")]
    public float currentYaw = 0f;
    [Range(-30f, 80f)]
    public float currentPitch = 15f;
    public float minPitch = -25f;
    public float maxPitch = 70f;

    [Header("Input Settings")]
    public float mouseSensitivity = 2.0f;
    public bool requireRightClick = false;
    public bool invertY = false;
    public bool lockCursor = false;

    [Header("Direct Camera Mode Settings")]
    public float distance = 4.8f;
    public float minDistance = 1.0f;
    public LayerMask collisionMask = ~0;
    public float positionDamping = 0.05f;

    private Vector3 currentVelocity;

    private void Start()
    {
        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        if (target == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) target = p.transform;
        }

        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
            transform.position = target.position + targetOffset;
            transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        }

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        HandleCursorLockToggle();
        ReadMouseInput();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) target = p.transform;
            else return;
        }

        if (isOrbitPivot)
        {
            UpdatePivotOrientation();
        }
        else
        {
            UpdateDirectCameraPosition();
        }
    }

    private void HandleCursorLockToggle()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && lockCursor && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ReadMouseInput()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool canRotate = !requireRightClick || mouse.rightButton.isPressed;
        if (canRotate)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude > 0.0001f)
            {
                currentYaw += delta.x * mouseSensitivity * 0.1f;
                float pitchDelta = (invertY ? delta.y : -delta.y) * mouseSensitivity * 0.1f;
                currentPitch = Mathf.Clamp(currentPitch + pitchDelta, minPitch, maxPitch);
            }
        }
    }

    private void UpdatePivotOrientation()
    {
        transform.position = target.position + targetOffset;
        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void UpdateDirectCameraPosition()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        Vector3 desiredDirection = rotation * Vector3.back;
        Vector3 desiredPosition = focusPoint + desiredDirection * distance;

        float currentDist = distance;
        if (Physics.SphereCast(focusPoint, 0.2f, desiredDirection, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            currentDist = Mathf.Clamp(hit.distance - 0.1f, minDistance, distance);
            desiredPosition = focusPoint + desiredDirection * currentDist;
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, positionDamping);
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position);
    }
}
