using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 4.2f;
    [SerializeField] private float sprintSpeed = 8.5f;
    [SerializeField] private float rotationSpeed = 14.0f;
    [SerializeField] private float acceleration = 25.0f;
    [SerializeField] private float jumpForce = 7.5f;
    [SerializeField] private float fallMultiplier = 2.0f;
    [SerializeField] private float groundCheckRadius = 0.28f;
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0, 0.15f, 0);
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool cameraRelative = true;
    [SerializeField] private Animator animator;
    [SerializeField] private float attackDuration = 0.8f;

    [Header("Crop Collection & Throwing")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float pickupRange = 2.6f;
    [SerializeField] private float throwForce = 6.5f;
    [SerializeField] private float throwUpwardForce = 2.5f;
    [SerializeField] private float grabAnimationDuration = 0.65f;
    [SerializeField] private LayerMask cropLayers = ~0;

    public float WalkSpeed { get => walkSpeed; set => walkSpeed = value; }
    public float SprintSpeed { get => sprintSpeed; set => sprintSpeed = value; }
    public float JumpForce { get => jumpForce; set => jumpForce = value; }
    public bool CameraRelative { get => cameraRelative; set => cameraRelative = value; }
    public Animator CharacterAnimator { get => animator; set => animator = value; }
    public Transform CharacterCameraTransform { get => cameraTransform; set => cameraTransform = value; }
    public Transform HoldPoint { get => holdPoint; set => holdPoint = value; }
    public float PickupRange { get => pickupRange; set => pickupRange = value; }
    public float ThrowForce { get => throwForce; set => throwForce = value; }
    public float ThrowUpwardForce { get => throwUpwardForce; set => throwUpwardForce = value; }
    public CropItem HeldCrop => heldCrop;

    private Rigidbody rb;
    private CapsuleCollider col;

    private Vector2 moveInput;
    private bool isSprintPressed;
    private bool jumpRequested;
    private bool attackRequested;
    private bool grabOrInteractRequested;
    private bool throwRequested;

    private float attackTimer;
    private float grabTimer;
    private float cropActionCooldown;
    private float jumpBufferTimer;
    private float coyoteTimer;
    private float jumpCooldownTimer;
    private const float JumpBufferDuration = 0.15f;
    private const float CoyoteDuration = 0.15f;
    private bool isGrounded;
    private bool wasGroundedLastFrame;

    private CropItem heldCrop;

    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int RunningHash = Animator.StringToHash("Running");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LandHash = Animator.StringToHash("Land");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int GrabHash = Animator.StringToHash("Grab");
    private static readonly int MotionScaleHash = Animator.StringToHash("MotionScale");

    public bool IsGrounded => isGrounded;
    public Vector2 CurrentInput => moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

        DeduplicateVisualMeshes();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        EnsureHoldPoint();
    }

    private void DeduplicateVisualMeshes()
    {
        Animator[] childAnimators = GetComponentsInChildren<Animator>(true);
        if (childAnimators == null || childAnimators.Length <= 1) return;

        Animator primary = animator != null ? animator : childAnimators[childAnimators.Length - 1];
        animator = primary;

        foreach (var anim in childAnimators)
        {
            if (anim != null && anim != primary && anim.gameObject != gameObject)
            {
                Destroy(anim.gameObject);
            }
        }
    }

    private void EnsureHoldPoint()
    {
        if (holdPoint != null)
        {
            holdPoint.localPosition = new Vector3(0f, 1.0f, 0.5f);
            holdPoint.localRotation = Quaternion.identity;
            return;
        }

        Transform existing = transform.Find("HoldPoint");
        if (existing != null)
        {
            holdPoint = existing;
            holdPoint.localPosition = new Vector3(0f, 1.0f, 0.5f);
            holdPoint.localRotation = Quaternion.identity;
            return;
        }

        GameObject holdGO = new GameObject("HoldPoint");
        holdGO.transform.SetParent(transform, false);
        holdGO.transform.localPosition = new Vector3(0f, 1.0f, 0.5f);
        holdGO.transform.localRotation = Quaternion.identity;
        holdPoint = holdGO.transform;
    }

    private void Update()
    {
        ReadInputSystem();

        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;
        if (coyoteTimer > 0f) coyoteTimer -= Time.deltaTime;
        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= Time.deltaTime;
        if (attackTimer > 0f) attackTimer -= Time.deltaTime;
        if (grabTimer > 0f) grabTimer -= Time.deltaTime;
        if (cropActionCooldown > 0f) cropActionCooldown -= Time.deltaTime;

        CheckGround();

        if (isGrounded)
        {
            coyoteTimer = CoyoteDuration;
        }

        HandleCropAndCombatActions();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        ApplyMovementPhysics();
        ApplyJumpPhysics();
        ApplyCustomGravity();
    }

    private void ReadInputSystem()
    {
        Vector2 input = Vector2.zero;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (heldCrop != null)
                {
                    throwRequested = true;
                }
                else
                {
                    attackRequested = true;
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && heldCrop != null)
            {
                throwRequested = true;
            }
        }

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;

            isSprintPressed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
                jumpBufferTimer = JumpBufferDuration;
            }

            if (keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame || keyboard.gKey.wasPressedThisFrame)
            {
                if (heldCrop != null)
                {
                    throwRequested = true;
                }
                else
                {
                    grabOrInteractRequested = true;
                }
            }

            if (keyboard.qKey.wasPressedThisFrame && heldCrop != null)
            {
                throwRequested = true;
            }
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.05f)
            {
                input = stick;
            }

            if (gamepad.leftStickButton.isPressed)
            {
                isSprintPressed = true;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                jumpRequested = true;
            }

            if (gamepad.buttonWest.wasPressedThisFrame || gamepad.rightTrigger.wasPressedThisFrame)
            {
                if (heldCrop != null)
                {
                    throwRequested = true;
                }
                else
                {
                    grabOrInteractRequested = true;
                }
            }
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        moveInput = input;
    }

    private void HandleCropAndCombatActions()
    {
        if (heldCrop == null && (grabOrInteractRequested || attackRequested) && cropActionCooldown <= 0f)
        {
            CropItem nearestCrop = FindNearestCrop();
            if (nearestCrop != null)
            {
                grabOrInteractRequested = false;
                attackRequested = false;
                PickupCrop(nearestCrop);
                return;
            }

            if (grabOrInteractRequested)
            {
                grabOrInteractRequested = false;
                TriggerGrabAnimation();
                return;
            }
        }

        if (heldCrop != null && (throwRequested || grabOrInteractRequested || attackRequested) && cropActionCooldown <= 0f)
        {
            throwRequested = false;
            grabOrInteractRequested = false;
            attackRequested = false;
            ThrowHeldCrop();
            return;
        }

        throwRequested = false;
        grabOrInteractRequested = false;

        if (attackRequested)
        {
            attackRequested = false;
            if (isGrounded && attackTimer <= 0f)
            {
                attackTimer = attackDuration;
                if (animator != null)
                {
                    animator.SetTrigger(AttackHash);
                }
            }
        }
    }

    public CropItem FindNearestCrop()
    {
        Vector3 searchCenter = transform.position + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(searchCenter, pickupRange, cropLayers, QueryTriggerInteraction.Collide);

        CropItem bestCrop = null;
        float bestScore = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            CropItem crop = hit.GetComponentInParent<CropItem>();
            if (crop == null) crop = hit.GetComponent<CropItem>();
            if (crop == null || crop.IsHeld || crop.IsDelivered) continue;

            float dist = Vector3.Distance(transform.position, crop.transform.position);
            if (dist <= pickupRange && dist < bestScore)
            {
                bestScore = dist;
                bestCrop = crop;
            }
        }

        if (bestCrop != null) return bestCrop;

        CropItem[] allCrops = FindObjectsByType<CropItem>(FindObjectsSortMode.None);
        foreach (var crop in allCrops)
        {
            if (crop == null || crop.IsHeld || crop.IsDelivered) continue;
            float dist = Vector3.Distance(transform.position, crop.transform.position);
            if (dist <= pickupRange && dist < bestScore)
            {
                bestScore = dist;
                bestCrop = crop;
            }
        }

        return bestCrop;
    }

    public void PickupCrop(CropItem crop)
    {
        if (crop == null || crop.IsHeld || crop.IsDelivered) return;

        EnsureHoldPoint();
        heldCrop = crop;
        cropActionCooldown = 0.2f;

        Vector3 toCrop = crop.transform.position - transform.position;
        toCrop.y = 0f;
        if (toCrop.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(toCrop.normalized, Vector3.up);
        }

        crop.PickUp(holdPoint, col);
        TriggerGrabAnimation();
    }

    public void ThrowHeldCrop()
    {
        if (heldCrop == null) return;

        CropItem cropToThrow = heldCrop;
        heldCrop = null;
        cropActionCooldown = 0.2f;

        EnsureHoldPoint();
        Vector3 startPos = holdPoint != null ? holdPoint.position : (transform.position + Vector3.up * 1.05f + transform.forward * 0.55f);
        cropToThrow.transform.position = startPos;

        Vector3 throwDir = transform.forward;
        float horizontalImpulse = throwForce;
        float verticalImpulse = throwUpwardForce;

        Rigidbody cropRb = cropToThrow.CropRigidbody != null ? cropToThrow.CropRigidbody : cropToThrow.GetComponent<Rigidbody>();
        float mass = cropRb != null ? Mathf.Max(0.1f, cropRb.mass) : 1.0f;

        CropItemBox targetBox = FindTargetItemBox(startPos, out Vector3 boxTargetPoint);
        if (targetBox != null)
        {
            Vector3 delta = boxTargetPoint - startPos;
            Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
            float distXZ = deltaXZ.magnitude;
            if (distXZ > 0.25f)
            {
                throwDir = deltaXZ.normalized;
                transform.rotation = Quaternion.LookRotation(throwDir, Vector3.up);

                float flightTime = Mathf.Clamp(distXZ / 5.5f, 0.35f, 0.95f);
                float gravity = Mathf.Abs(Physics.gravity.y);
                float vx = distXZ / flightTime;
                float vy = (delta.y + 0.5f * gravity * flightTime * flightTime) / flightTime;

                horizontalImpulse = vx * mass;
                verticalImpulse = vy * mass;
            }
        }
        else
        {
            horizontalImpulse = throwForce * mass;
            verticalImpulse = throwUpwardForce * mass;
        }

        cropToThrow.Throw(throwDir, horizontalImpulse, verticalImpulse, col);
    }

    private CropItemBox FindTargetItemBox(Vector3 origin, out Vector3 targetPoint)
    {
        targetPoint = Vector3.zero;
        CropItemBox[] boxes = FindObjectsByType<CropItemBox>(FindObjectsSortMode.None);
        CropItemBox bestBox = null;
        float bestAngle = 55f;

        foreach (var box in boxes)
        {
            if (box == null) continue;
            Vector3 candidatePoint = box.transform.position + new Vector3(0f, 0.6f, 0f);
            Vector3 toBoxXZ = new Vector3(candidatePoint.x - origin.x, 0f, candidatePoint.z - origin.z);
            float dist = toBoxXZ.magnitude;
            if (dist < 0.4f || dist > 8.5f) continue;

            float angle = Vector3.Angle(transform.forward, toBoxXZ.normalized);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                bestBox = box;
                targetPoint = candidatePoint;
            }
        }

        return bestBox;
    }

    private void TriggerGrabAnimation()
    {
        grabTimer = grabAnimationDuration;
        if (animator != null)
        {
            animator.SetTrigger(GrabHash);
        }
    }

    private void CheckGround()
    {
        if (jumpCooldownTimer > 0f)
        {
            isGrounded = false;
            return;
        }

        Vector3 checkPos = transform.position + groundCheckOffset;
        Collider[] hits = Physics.OverlapSphere(checkPos, groundCheckRadius, groundLayers, QueryTriggerInteraction.Ignore);

        bool grounded = false;
        foreach (var hit in hits)
        {
            if (hit.gameObject != gameObject && !hit.transform.IsChildOf(transform))
            {
                grounded = true;
                break;
            }
        }

        isGrounded = grounded;

        if (isGrounded && !wasGroundedLastFrame)
        {
            if (animator != null)
            {
                animator.SetTrigger(LandHash);
            }
        }

        wasGroundedLastFrame = isGrounded;
    }

    private void ApplyMovementPhysics()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }

        Vector3 moveDirection = (forward * moveInput.y + right * moveInput.x);
        float currentSpeed = isSprintPressed ? sprintSpeed : walkSpeed;
        if (attackTimer > 0f)
        {
            currentSpeed *= 0.2f;
        }
        Vector3 targetVelocity = moveDirection * currentSpeed;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        Vector3 newHorizontal = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(newHorizontal.x, currentVelocity.y, newHorizontal.z);

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void ApplyJumpPhysics()
    {
        bool canJump = (jumpRequested || jumpBufferTimer > 0f) && (isGrounded || coyoteTimer > 0f) && jumpCooldownTimer <= 0f;
        if (canJump)
        {
            jumpRequested = false;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpCooldownTimer = 0.15f;
            isGrounded = false;

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;

            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

            if (animator != null)
            {
                animator.SetTrigger(JumpHash);
            }
        }
        else if (jumpRequested && jumpBufferTimer <= 0f)
        {
            jumpRequested = false;
        }
    }

    private void ApplyCustomGravity()
    {
        if (!isGrounded && rb.linearVelocity.y < 0f)
        {
            rb.AddForce(Vector3.down * (Physics.gravity.magnitude * (fallMultiplier - 1f)), ForceMode.Acceleration);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool isRunning = isMoving && isSprintPressed;

        animator.SetBool(WalkingHash, isMoving);
        animator.SetBool(RunningHash, isRunning);

        float motionScale = isRunning ? 1.6f : (isMoving ? 1.0f : 0.0f);
        animator.SetFloat(MotionScaleHash, motionScale);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, pickupRange);
    }
}
