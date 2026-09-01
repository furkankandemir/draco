using System.Collections;
using UnityEngine;

namespace Hovl
{
    [RequireComponent(typeof(CharacterController))]
    public class HS_MovementInput : MonoBehaviour
    {
        [Header("Movement")]
        public float velocity = 9f;

        [Space]
        public float InputX;
        public float InputZ;
        public Vector3 desiredMoveDirection;
        public bool blockRotationPlayer;
        public float desiredRotationSpeed = 0.1f;
        public Animator anim;
        public float Speed;
        public float allowPlayerRotation = 0.1f;
        public Camera cam;
        public CharacterController controller;
        public bool isGrounded;

        [Space]
        [Header("Jump / Fly")]
        public bool enableJump = true;
        public bool fly = false;
        public KeyCode jumpKey = KeyCode.Space;
        public float jumpHeight = 1.5f;
        public float flySpeed = 6f;

        [Tooltip("Recommended value: -15 to -30")]
        public float gravity = -20f;

        [Tooltip("Small negative value keeps CharacterController grounded.")]
        public float groundedGravity = -2f;

        [Space]
        [Header("Animation Smoothing")]
        [Range(0, 1f)]
        public float HorizontalAnimSmoothTime = 0.2f;

        [Range(0, 1f)]
        public float VerticalAnimTime = 0.2f;

        [Range(0, 1f)]
        public float StartAnimTime = 0.3f;

        [Range(0, 1f)]
        public float StopAnimTime = 0.15f;

        [Space]
        public bool canMove = true;

        private float verticalVel;
        private Vector3 moveVector;

        private void Awake()
        {
            if (!anim)
                anim = GetComponent<Animator>();

            if (!cam)
                cam = Camera.main;

            if (!controller)
                controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!controller)
                return;

            isGrounded = controller.isGrounded;

            if (canMove)
            {
                InputMagnitude();
                JumpInput();
            }

            ApplyGravityOrFly();
        }

        private void JumpInput()
        {
            if (!enableJump)
                return;

            if (fly)
            {
                if (Input.GetKey(jumpKey))
                {
                    verticalVel = flySpeed;
                }

                return;
            }

            if (isGrounded && Input.GetKeyDown(jumpKey))
            {
                verticalVel = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        private void ApplyGravityOrFly()
        {
            if (fly)
            {
                if (!Input.GetKey(jumpKey))
                {
                    if (isGrounded && verticalVel < 0f)
                    {
                        verticalVel = groundedGravity;
                    }

                    verticalVel += gravity * Time.deltaTime;
                }

                moveVector = new Vector3(0f, verticalVel, 0f);
                controller.Move(moveVector * Time.deltaTime);
                return;
            }

            if (isGrounded && verticalVel < 0f)
            {
                verticalVel = groundedGravity;
            }

            verticalVel += gravity * Time.deltaTime;

            moveVector = new Vector3(0f, verticalVel, 0f);
            controller.Move(moveVector * Time.deltaTime);
        }

        private void PlayerMoveAndRotation()
        {
            InputX = Input.GetAxis("Horizontal");
            InputZ = Input.GetAxis("Vertical");

            if (!cam)
                cam = Camera.main;

            if (!cam || !controller)
                return;

            var forward = cam.transform.forward;
            var right = cam.transform.right;

            forward.y = 0f;
            right.y = 0f;

            forward.Normalize();
            right.Normalize();

            desiredMoveDirection = forward * InputZ + right * InputX;
            desiredMoveDirection.Normalize();

            if (blockRotationPlayer == false)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(forward),
                    desiredRotationSpeed
                );

                if (InputZ < -0.5f)
                {
                    controller.Move(desiredMoveDirection * Time.deltaTime * (velocity / 1.5f));
                }
                else
                {
                    controller.Move(desiredMoveDirection * Time.deltaTime * velocity);
                }
            }
        }

        private void InputMagnitude()
        {
            if (!anim)
                return;

            InputX = Input.GetAxis("Horizontal");
            InputZ = Input.GetAxis("Vertical");

            anim.SetFloat("InputZ", InputZ, VerticalAnimTime, Time.deltaTime * 2f);
            anim.SetFloat("InputX", InputX, HorizontalAnimSmoothTime, Time.deltaTime * 2f);

            Speed = new Vector2(InputX, InputZ).sqrMagnitude;

            if (Speed > allowPlayerRotation)
            {
                anim.SetFloat("InputMagnitude", Speed, StartAnimTime, Time.deltaTime);
                PlayerMoveAndRotation();
            }
            else if (Speed < allowPlayerRotation)
            {
                anim.SetFloat("InputMagnitude", Speed, StopAnimTime, Time.deltaTime);
            }
        }

        public void SetAnimZero()
        {
            if (!anim)
                return;

            anim.SetFloat("InputMagnitude", 0);
            anim.SetFloat("InputZ", 0);
            anim.SetFloat("InputX", 0);
        }

        public void BlockMovement()
        {
            canMove = false;
            SetAnimZero();
        }

        public void AllowMovement()
        {
            canMove = true;
        }

        public IEnumerator RotateToTarget(float rotatingTime, Vector3 targetPoint)
        {
            float delay = rotatingTime;

            var lookPos = targetPoint - transform.position;
            lookPos.y = 0;

            if (lookPos.sqrMagnitude < 0.001f)
                yield break;

            var rotation = Quaternion.LookRotation(lookPos);

            while (true)
            {
                if (Speed == 0)
                {
                    transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * 20);
                }

                delay -= Time.deltaTime;

                if (delay <= 0 || Quaternion.Angle(transform.rotation, rotation) < 0.1f)
                {
                    yield break;
                }

                yield return null;
            }
        }
    }
}