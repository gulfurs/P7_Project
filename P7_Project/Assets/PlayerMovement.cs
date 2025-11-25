using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Player")]
    public float moveSpeed = 4.0f;
    public float sprintSpeed = 6.0f;
    public float crouchSpeed = 2.0f;
    public float jumpHeight = 1.2f;
    public float gravity = -15.0f;

    [Header("Camera")]
    public Transform cameraTransform;
    public float cameraSensitivity = 1.0f;
    public float maxCameraAngle = 85f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.4f;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;
    public bool isGrounded;

    private float _fallTimeoutDelta;
    private float _jumpTimeoutDelta;
    private const float _terminalVelocity = 53.0f;

    [Space(10)]
    public float JumpTimeout = 0.1f;
    public float FallTimeout = 0.15f;

    private CharacterController controller;
    private InputHandler input;

    private Vector3 velocity;
    private float cameraPitch = 0f;
    bool canMove = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = GetComponent<InputHandler>();
    }

    void Start()
    {
        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;

        Vector3 checkPosition = transform.position + Vector3.down * (controller.height / 2f) + controller.center;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }

    private void Update()
    {
        GroundedCheck();
        JumpAndGravity();
        if (!canMove)
            return;
        Move();
    }

    private void LateUpdate()
    {
        //CameraRotation();
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = transform.position + Vector3.down * (controller.height / 2f) + controller.center;
        isGrounded = Physics.CheckSphere(spherePosition, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void JumpAndGravity()
    {
        if (isGrounded)
        {
            _fallTimeoutDelta = FallTimeout;

            if (velocity.y < 0.0f)
            {
                velocity.y = -2f;
            }

            if (input.jump && _jumpTimeoutDelta <= 0.0f)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                _jumpTimeoutDelta = JumpTimeout;
                input.jump = false;
            }
        }
        else
        {
            _fallTimeoutDelta -= Time.deltaTime;
        }

        if (_jumpTimeoutDelta >= 0.0f)
        {
            _jumpTimeoutDelta -= Time.deltaTime;
        }

        velocity.y = Mathf.Max(velocity.y + gravity * Time.deltaTime, -_terminalVelocity);

        controller.Move(velocity * Time.deltaTime);
    }


    private void Move()
    {
        float speed = input.sprint ? sprintSpeed : (input.crouch ? crouchSpeed : moveSpeed);

        Vector3 move = (transform.right * input.move.x + transform.forward * input.move.y).normalized;
        controller.Move(move * speed * Time.deltaTime);
    }

    /*private void CameraRotation()
    {
        if (input.look.sqrMagnitude >= 0.01f)
        {
            float mouseX = input.look.x * cameraSensitivity * Time.deltaTime;
            float mouseY = input.look.y * cameraSensitivity * Time.deltaTime;

            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -maxCameraAngle, maxCameraAngle);
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

            transform.Rotate(Vector3.up * mouseX);
        }
    }*/

    public void UnlockMove(bool value) => canMove = value;
}
