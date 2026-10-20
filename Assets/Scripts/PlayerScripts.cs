using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerScript : MonoBehaviour
{
    public InputAction LeftAction;
    public InputAction MoveAction;

    [Header("Actions")]
    public InputAction JumpAction;
    public InputAction DashAction;
    public InputAction SprintAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float sprintMultiplier = 1.4f;
    [SerializeField] private float groundAcceleration = 70f;
    [SerializeField] private float airAcceleration = 35f;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 13f;
    [SerializeField, Min(1)] private int maxJumps = 2;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField, Range(0f, 1f)] private float jumpReleaseMultiplier = 0.5f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.16f;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private Vector2 moveInput;
    private int facingDirection = 1;
    private int jumpsUsed;
    private int dashesAvailable = 1;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float dashTimer;
    private float originalGravityScale;
    private bool isGrounded;
    private bool isDashing;
    private bool dashQueued;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        body.freezeRotation = true;
        originalGravityScale = body.gravityScale;
    }

    private void OnEnable()
    {
        EnableAction(LeftAction);
        EnableAction(MoveAction);
        EnableAction(JumpAction);
        EnableAction(DashAction);
        EnableAction(SprintAction);
    }

    private void OnDisable()
    {
        DisableAction(LeftAction);
        DisableAction(MoveAction);
        DisableAction(JumpAction);
        DisableAction(DashAction);
        DisableAction(SprintAction);

        if (body != null)
        {
            body.gravityScale = originalGravityScale;
        }
    }

    private void Update()
    {
        moveInput = ReadMoveInput();
        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            facingDirection = moveInput.x > 0f ? 1 : -1;
        }

        if (JumpPressed())
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (JumpReleased() && body.linearVelocity.y > 0f)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpReleaseMultiplier);
        }

        if (DashPressed())
        {
            dashQueued = true;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            jumpsUsed = 0;
            dashesAvailable = 1;
        }
        else
        {
            coyoteTimer -= Time.fixedDeltaTime;
        }

        if (dashQueued && dashesAvailable > 0 && !isDashing)
        {
            StartDash();
        }
        dashQueued = false;

        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                body.gravityScale = originalGravityScale;
                body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
            }
            else
            {
                return;
            }
        }

        ApplyHorizontalMovement();
        TryJump();
    }

    private void UpdateGroundedState()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayers);
        isGrounded = bodyCollider.Cast(Vector2.down, filter, groundHits, groundCheckDistance) > 0;
    }

    private void ApplyHorizontalMovement()
    {
        float speed = moveSpeed * (SprintHeld() ? sprintMultiplier : 1f);
        float targetSpeed = moveInput.x * speed;
        float acceleration = isGrounded ? groundAcceleration : airAcceleration;
        float velocityX = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(velocityX, body.linearVelocity.y);
    }

    private void TryJump()
    {
        if (jumpBufferTimer <= 0f)
        {
            return;
        }

        bool canUseCoyoteJump = coyoteTimer > 0f && jumpsUsed == 0;
        if (!canUseCoyoteJump && jumpsUsed >= maxJumps)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
        jumpsUsed++;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
    }

    private void StartDash()
    {
        Vector2 direction = moveInput.sqrMagnitude > 0.01f
            ? moveInput.normalized
            : new Vector2(facingDirection, 0f);

        isDashing = true;
        dashTimer = dashDuration;
        dashesAvailable--;
        body.gravityScale = 0f;
        body.linearVelocity = direction * dashSpeed;
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = MoveAction != null && MoveAction.enabled ? MoveAction.ReadValue<Vector2>() : Vector2.zero;
        if (input.sqrMagnitude > 0.01f || Keyboard.current == null)
        {
            return input;
        }

        Keyboard keyboard = Keyboard.current;
        float horizontal = (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1f : 0f)
            - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1f : 0f);
        float vertical = (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1f : 0f)
            - (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1f : 0f);
        return new Vector2(horizontal, vertical);
    }

    private bool JumpPressed()
    {
        return (JumpAction != null && JumpAction.enabled && JumpAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
    }

    private bool JumpReleased()
    {
        return (JumpAction != null && JumpAction.enabled && JumpAction.WasReleasedThisFrame())
            || (Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame);
    }

    private bool DashPressed()
    {
        return (DashAction != null && DashAction.enabled && DashAction.WasPressedThisFrame())
            || (Keyboard.current != null && Keyboard.current.leftCtrlKey.wasPressedThisFrame);
    }

    private bool SprintHeld()
    {
        return (SprintAction != null && SprintAction.enabled && SprintAction.IsPressed())
            || (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed);
    }

    private static void EnableAction(InputAction action)
    {
        if (action != null && !action.enabled)
        {
            action.Enable();
        }
    }

    private static void DisableAction(InputAction action)
    {
        if (action != null && action.enabled)
        {
            action.Disable();
        }
    }
}
