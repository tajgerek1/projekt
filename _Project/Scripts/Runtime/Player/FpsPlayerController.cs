using UnityEngine;

namespace NocnaStraz
{
    /// <summary>
    /// Prosty kontroler FPS (CharacterController): ruch WASD, sprint, skok, grawitacja.
    /// Yaw (obrót poziomy) wykonywany jest na obiekcie gracza.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FpsPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 5.5f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -18f;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float pitchMin = -80f;
        [SerializeField] private float pitchMax = 80f;

        [Header("Refs")]
        [SerializeField] private Transform cameraPivot;

        private CharacterController _cc;
        private float _verticalVelocity;
        private float _pitch;
        private bool _cursorLocked = true;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();

            if (cameraPivot == null)
            {
                var cam = GetComponentInChildren<Camera>();
                cameraPivot = cam != null ? cam.transform : null;
            }

            LockCursor(true);
        }

        private void Update()
        {
            // Toggle cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(!_cursorLocked);
            }

            if (_cursorLocked)
            {
                HandleLook();
            }

            HandleMove();
        }

        private void HandleLook()
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // yaw on player
            transform.Rotate(Vector3.up, mx, Space.World);

            // pitch on camera pivot
            if (cameraPivot != null)
            {
                _pitch -= my;
                _pitch = Mathf.Clamp(_pitch, pitchMin, pitchMax);
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void HandleMove()
        {
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            var input = new Vector3(x, 0f, z);
            input = Vector3.ClampMagnitude(input, 1f);

            float speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
            Vector3 move = transform.TransformDirection(input) * speed;

            // grounded
            if (_cc.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f; // stick to ground

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _cc.Move(move * Time.deltaTime);
        }

        private void LockCursor(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
