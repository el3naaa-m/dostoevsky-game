using UnityEngine;
using UnityEngine.InputSystem;

namespace NightChase
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform eyes;
        [SerializeField, Min(0)] float walkSpeed = 3.5f;
        [SerializeField, Min(0)] float runSpeed = 6f;
        [SerializeField, Min(.1f)] float acceleration = 18f;
        [SerializeField, Min(.1f)] float deceleration = 24f;
        [SerializeField, Min(.01f)] float jumpHeight = .85f;
        [SerializeField, Min(.1f)] float gravity = 19f;
        [SerializeField, Min(.001f)] float mouseSensitivity = .12f;
        [SerializeField, Min(.001f)] float lookSmoothTime = .035f;
        [SerializeField, Range(1, 89)] float pitchLimit = 85f;

        CharacterController controller;
        Vector3 planarVelocity;
        float verticalVelocity;
        float yaw, pitch, targetYaw, targetPitch, yawVelocity, pitchVelocity;
        bool captureRequested;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            yaw = targetYaw = transform.eulerAngles.y;
            pitch = targetPitch = Mathf.DeltaAngle(0, eyes.localEulerAngles.x);
        }

        void Start() => SetCapture(Application.isFocused);

        void OnDisable() => SetCapture(false);

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SetCapture(false);
        }

        void SetCapture(bool capture)
        {
            captureRequested = capture;
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
            targetYaw = yaw;
            targetPitch = pitch;
            yawVelocity = pitchVelocity = 0;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCapture(false);
            else if (Application.isFocused && !captureRequested && mouse != null
                     && mouse.leftButton.wasPressedThisFrame)
                SetCapture(true);

            bool captured = captureRequested && Cursor.lockState == CursorLockMode.Locked;
            if (captureRequested && !captured) SetCapture(false);
            float dt = Time.deltaTime;
            if (dt <= 0) return;

            if (captured && mouse != null) Look(mouse.delta.ReadValue(), dt);
            Move(captured ? keyboard : null, dt);
        }

        void Look(Vector2 delta, float dt)
        {
            targetYaw += delta.x * mouseSensitivity;
            targetPitch = Mathf.Clamp(targetPitch - delta.y * mouseSensitivity, -pitchLimit, pitchLimit);
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVelocity, lookSmoothTime, Mathf.Infinity, dt);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, lookSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            eyes.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void Move(Keyboard keyboard, float dt)
        {
            var input = Vector2.zero;
            bool running = false;
            bool jump = false;
            if (keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                input = Vector2.ClampMagnitude(input, 1);
                running = keyboard.leftShiftKey.isPressed;
                jump = keyboard.spaceKey.wasPressedThisFrame;
            }

            var desired = (transform.right * input.x + transform.forward * input.y)
                          * (running ? runSpeed : walkSpeed);
            float rate = desired.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, rate * dt);
            if (controller.isGrounded && verticalVelocity <= 0)
                verticalVelocity = jump ? Mathf.Sqrt(2 * gravity * jumpHeight) : -2f;
            verticalVelocity -= gravity * dt;

            var collisions = controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
        }
    }
}
