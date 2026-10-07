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

    public float WalkSpeed { get => walkSpeed; set => walkSpeed = value; }
    public float SprintSpeed { get => sprintSpeed; set => sprintSpeed = value; }
    public float JumpForce { get => jumpForce; set => jumpForce = value; }
    public bool CameraRelative { get => cameraRelative; set => cameraRelative = value; }
    public Animator CharacterAnimator { get => animator; set => animator = value; }
    public Transform CharacterCameraTransform { get => cameraTransform; set => cameraTransform = value; }

    private Rigidbody rb;
    private CapsuleCollider col;

    private Vector2 moveInput;
    private bool isSprintPressed;
    private bool jumpRequested;
    private float jumpBufferTimer;
    private float coyoteTimer;
    private float jumpCooldownTimer;
    private const float JumpBufferDuration = 0.15f;
    private const float CoyoteDuration = 0.15f;
    private bool isGrounded;
    private bool wasGroundedLastFrame;

    private static readonly int WalkingHash = Animator.StringToHash("Walking");
    private static readonly int RunningHash = Animator.StringToHash("Running");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LandHash = Animator.StringToHash("Land");
    private static readonly int MotionScaleHash = Animator.StringToHash("MotionScale");

    public bool IsGrounded => isGrounded;
    public Vector2 CurrentInput => moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();

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
    }

    private void Update()
    {
        ReadInputSystem();

        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;
        if (coyoteTimer > 0f) coyoteTimer -= Time.deltaTime;
        if (jumpCooldownTimer > 0f) jumpCooldownTimer -= Time.deltaTime;

        CheckGround();

        if (isGrounded)
        {
            coyoteTimer = CoyoteDuration;
        }

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
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.05f)
            {
                input = stick;
            }

            if (gamepad.rightTrigger.isPressed || gamepad.leftStickButton.isPressed)
            {
                isSprintPressed = true;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        moveInput = input;
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
    }
}
