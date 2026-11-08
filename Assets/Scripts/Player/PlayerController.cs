using UnityEngine;
using UnityEngine.InputSystem;

public enum CameraMode
{
    FirstPerson,
    ThirdPersonBack,
    ThirdPersonFront,
}

/// Điều khiển nhân vật kiểu Minecraft (Input System mới).
/// WASD đi, Space nhảy, Ctrl chạy nhanh, Shift lén / hạ độ cao khi bay, F bật/tắt bay,
/// F5 hoặc V đổi góc nhìn, Esc thả chuột.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] World world;
    [SerializeField] Transform cameraTransform;

    [Header("Look")]
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] float eyeHeight = 1.62f;

    [Header("Camera góc thứ ba")]
    [SerializeField] float thirdPersonDistance = 4f;
    [SerializeField] float cameraCollisionRadius = 0.2f;

    [Header("Movement (block/giây, giống Minecraft)")]
    [SerializeField] float walkSpeed = 4.317f;
    [SerializeField] float sprintSpeed = 5.612f;
    [SerializeField] float sneakSpeed = 1.31f;
    [SerializeField] float flySpeed = 10.92f;
    [SerializeField] float gravity = 32f;
    [SerializeField] float jumpHeight = 1.25f;
    [SerializeField] float terminalVelocity = 78.4f;
    [Tooltip("Chế độ sáng tạo: cho phép bấm F để bay. Sinh tồn thì tắt.")]
    [SerializeField] bool creativeFlying = false;

    CharacterController cc;
    PlayerModel model;
    PlayerStats stats;
    float yaw, pitch, verticalVelocity;
    float cameraDistance;
    bool spawned, flying, lockRequested, sprinting;
    bool wasGrounded = true;
    float fallStartY;
    Vector3 knockback;
    Vector3 spawnPoint;
    CameraMode cameraMode = CameraMode.FirstPerson;
    readonly RaycastHit[] camHits = new RaycastHit[16];

    public bool IsSpawned => spawned;
    public bool IsFlying => flying;
    public bool CanFly => creativeFlying;
    public bool IsSprinting => sprinting;
    public bool IsSneaking { get; private set; }
    public CameraMode Mode => cameraMode;
    public Vector3 EyePosition => transform.position + Vector3.up * (IsSneaking && !flying ? eyeHeight - 0.25f : eyeHeight);
    public Vector3 LookDirection => Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
    public float HorizontalSpeed => cc.enabled ? new Vector2(cc.velocity.x, cc.velocity.z).magnitude : 0f;
    public bool IsGroundedMoving => cc.enabled && cc.isGrounded && !flying && HorizontalSpeed > 0.1f;
    public PlayerModel Model => model;
    public Vector3 SpawnPoint => spawnPoint;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.enabled = false;               // đợi chunk dưới chân load xong mới bật
        // Scene cũ có thể thiếu component -> tự thêm (thứ tự quan trọng vì chúng tìm nhau trong Awake)
        Ensure<MobileControls>();
        Ensure<PlayerInventory>();
        Ensure<PlayerStats>();
        Ensure<BlockInteraction>();
        Ensure<GameHUD>();
        Ensure<InventoryUI>();
        stats = GetComponent<PlayerStats>();
        model = GetComponentInChildren<PlayerModel>();
        if (model == null)
        {
            var modelGO = new GameObject("Model");
            modelGO.transform.SetParent(transform, false);
            model = modelGO.AddComponent<PlayerModel>();
        }
        yaw = transform.eulerAngles.y;
        cameraDistance = thirdPersonDistance;
        ApplyCameraMode();
    }

    void Ensure<T>() where T : Component
    {
        if (GetComponent<T>() == null) gameObject.AddComponent<T>();
    }

    void Update()
    {
        if (!spawned)
        {
            TrySpawn();
            return;
        }

        bool inputEnabled = !UIState.IsOpen && !PlayerStats.IsDead;
        if (inputEnabled)
        {
            if (!GameInput.MobileMode) HandleCursor();
            HandleCameraModeInput();
            Look();
        }
        Move(inputEnabled && GameInput.PointerCaptured);
    }

    void LateUpdate()
    {
        if (lockRequested)
        {
            lockRequested = false;
            if (!UIState.IsOpen && !PlayerStats.IsDead) SetCursorLocked(true);
        }

        UpdateCamera();

        if (model != null)
        {
            bool grounded = cc.enabled && (cc.isGrounded || flying);
            model.Animate(HorizontalSpeed, pitch, grounded, IsSneaking && !flying, Time.deltaTime);
        }
    }

    void TrySpawn()
    {
        if (world == null || !world.IsChunkReadyAt(transform.position)) return;

        int x = Mathf.FloorToInt(transform.position.x);
        int z = Mathf.FloorToInt(transform.position.z);
        int top = world.GetTopSolidY(x, z);
        spawnPoint = new Vector3(x + 0.5f, top + 1.05f, z + 0.5f);
        Teleport(spawnPoint);
        spawned = true;
        SetCursorLocked(true);
    }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        verticalVelocity = 0f;
        knockback = Vector3.zero;
        fallStartY = pos.y;
        cc.enabled = true;
    }

    public void Respawn()
    {
        Vector3 p = spawnPoint;
        if (world != null && world.IsChunkReadyAt(p))
        {
            int top = world.GetTopSolidY(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            p.y = top + 1.05f;
        }
        Teleport(p);
        flying = false;
    }

    public void AddKnockback(Vector3 v)
    {
        knockback.x += v.x;
        knockback.z += v.z;
        if (v.y > 0f) verticalVelocity = Mathf.Max(verticalVelocity, v.y);
    }

    // ---------------------------------------------------------------- input

    void HandleCursor()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !UIState.JustClosed) SetCursorLocked(false);
        // Khoá lại ở LateUpdate để cú click này không bị tính là phá block
        if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
            lockRequested = true;
    }

    public static void SetCursorLocked(bool locked)
    {
        if (GameInput.MobileMode) locked = false;   // điện thoại không có con trỏ chuột
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleCameraModeInput()
    {
        if (GameInput.CameraToggle)
        {
            cameraMode = (CameraMode)(((int)cameraMode + 1) % 3);
            cameraDistance = 0.5f;   // bắt đầu gần rồi kéo ra cho mượt
            ApplyCameraMode();
        }
    }

    void ApplyCameraMode()
    {
        if (model == null) return;
        model.Build();   // phòng khi Awake của PlayerModel chưa chạy
        model.SetFirstPerson(cameraMode == CameraMode.FirstPerson);
    }

    void Look()
    {
        if (!GameInput.PointerCaptured) return;

        Vector2 delta = GameInput.LookDegrees(mouseSensitivity);
        yaw += delta.x;
        pitch = Mathf.Clamp(pitch - delta.y, -89.9f, 89.9f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---------------------------------------------------------------- camera

    void UpdateCamera()
    {
        if (cameraTransform == null) return;

        Vector3 eye = EyePosition;
        Quaternion look = Quaternion.Euler(pitch, yaw, 0f);

        if (cameraMode == CameraMode.FirstPerson)
        {
            cameraTransform.SetPositionAndRotation(eye, look);
            return;
        }

        Vector3 forward = look * Vector3.forward;
        Vector3 dir = cameraMode == CameraMode.ThirdPersonBack ? -forward : forward;

        float allowed = ClipCameraDistance(eye, dir, thirdPersonDistance);
        // Bị chắn thì kéo vào ngay, thoáng thì kéo ra từ từ
        cameraDistance = allowed < cameraDistance
            ? allowed
            : Mathf.MoveTowards(cameraDistance, allowed, Time.deltaTime * 12f);

        Vector3 pos = eye + dir * cameraDistance;
        Quaternion rot = cameraMode == CameraMode.ThirdPersonBack ? look : Quaternion.LookRotation(-dir, Vector3.up);
        cameraTransform.SetPositionAndRotation(pos, rot);
    }

    float ClipCameraDistance(Vector3 origin, Vector3 dir, float maxDist)
    {
        int count = Physics.SphereCastNonAlloc(origin, cameraCollisionRadius, dir, camHits, maxDist,
                                               ~0, QueryTriggerInteraction.Ignore);
        float best = maxDist;
        for (int i = 0; i < count; i++)
        {
            var hit = camHits[i];
            if (hit.collider == cc) continue;          // bỏ qua collider của chính người chơi
            if (hit.distance <= 0f) continue;          // collider đã chồng lên điểm bắt đầu
            best = Mathf.Min(best, hit.distance);
        }
        return Mathf.Max(best - 0.05f, 0.3f);
    }

    // ---------------------------------------------------------------- movement

    void Move(bool hasInput)
    {
        Vector2 input = Vector2.zero;
        bool jump = false, sprintKey = false, sneak = false;

        if (hasInput)
        {
            input = GameInput.Move;
            jump = GameInput.Jump;
            sneak = GameInput.Sneak;
            sprintKey = GameInput.Sprint;

            if (!creativeFlying) flying = false;
            if (creativeFlying && GameInput.FlyToggle)
            {
                flying = !flying;
                verticalVelocity = 0f;
            }
        }

        IsSneaking = sneak;
        bool canSprint = stats == null || stats.CanSprint || flying;
        sprinting = sprintKey && input.y > 0 && !sneak && canSprint;

        Vector3 wish = transform.right * input.x + transform.forward * input.y;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        float dt = Time.deltaTime;
        Vector3 velocity;

        if (flying)
        {
            float speed = flySpeed * (sprinting ? 2f : 1f);
            float vertical = (jump ? 1f : 0f) - (sneak ? 1f : 0f);
            velocity = wish * speed + Vector3.up * vertical * flySpeed * 0.75f;
            fallStartY = transform.position.y;
        }
        else
        {
            float speed = sneak ? sneakSpeed : (sprinting ? sprintSpeed : walkSpeed);

            if (cc.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (jump && knockback.sqrMagnitude < 1f)
                {
                    verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                    if (stats != null) stats.AddExhaustion(sprinting ? 0.2f : 0.05f);
                }
            }

            verticalVelocity = Mathf.Max(verticalVelocity - gravity * dt, -terminalVelocity);
            velocity = wish * speed + Vector3.up * verticalVelocity;
        }

        // Knockback giảm dần
        velocity += knockback;
        knockback = Vector3.Lerp(knockback, Vector3.zero, dt * (cc.isGrounded ? 8f : 2f));

        // Lén trên mép block: không rơi xuống
        Vector3 move = velocity * dt;
        if (sneak && !flying && cc.isGrounded) move = PreventEdgeFall(move);

        CollisionFlags flags = cc.Move(move);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

        // Sát thương khi ngã
        bool grounded = cc.isGrounded;
        if (!flying)
        {
            if (!grounded && wasGrounded) fallStartY = transform.position.y;
            if (!grounded) fallStartY = Mathf.Max(fallStartY, transform.position.y);
            if (grounded && !wasGrounded && stats != null) stats.OnLanded(fallStartY - transform.position.y);
            if (grounded) fallStartY = transform.position.y;
        }
        wasGrounded = grounded;

        if (transform.position.y < -30f)
        {
            if (stats != null) stats.Damage(1000f, transform.position, null, false, "rơi khỏi thế giới");
            else Respawn();
        }
    }

    Vector3 PreventEdgeFall(Vector3 move)
    {
        if (world == null) return move;
        Vector3 p = transform.position;
        Vector3 mx = new Vector3(move.x, 0f, 0f), mz = new Vector3(0f, 0f, move.z);
        if (!HasGroundBelow(p + mx)) move.x = 0f;
        if (!HasGroundBelow(p + mz)) move.z = 0f;
        return move;
    }

    bool HasGroundBelow(Vector3 p)
    {
        const float r = 0.3f;
        int y = Mathf.FloorToInt(p.y - 0.1f);
        for (int i = -1; i <= 1; i += 2)
        for (int j = -1; j <= 1; j += 2)
            if (world.IsSolidAt(Mathf.FloorToInt(p.x + i * r), y, Mathf.FloorToInt(p.z + j * r))) return true;
        return false;
    }
}
