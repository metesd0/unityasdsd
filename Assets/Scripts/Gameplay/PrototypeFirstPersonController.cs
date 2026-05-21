using UnityEngine;
using UnityEngine.InputSystem;
using MobilOfl.UI;
using MobilOfl.Online;

namespace MobilOfl.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class PrototypeFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 6.5f;
        [SerializeField] private float groundAcceleration = 18f;
        [SerializeField] private float groundDeceleration = 24f;
        [SerializeField] private float airControl = 7f;
        [SerializeField] private float moveInputDeadZone = 0.08f;
        [SerializeField] private bool allowJump;
        [SerializeField] private bool allowCrouch;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float lookSensitivity = 2f;
        [SerializeField] private float lookSmoothing = 18f;
        [SerializeField] private float maxLookDelta = 42f;
        [SerializeField] private float maxLookAngle = 80f;
        [SerializeField] private Key crouchKey = Key.C;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchSpeed = 2.8f;
        [SerializeField] private float crouchTransitionSpeed = 10f;
        [SerializeField] private float sprintStaminaDrainPerSecond = 0.32f;
        [SerializeField] private float sprintStaminaRecoverPerSecond = 0.24f;
        [SerializeField] private float sprintRecoveryDelay = 1.1f;
        [SerializeField] private float headBobAmplitude = 0.035f;
        [SerializeField] private float sprintBobAmplitude = 0.055f;
        [SerializeField] private float crouchBobAmplitude = 0.02f;
        [SerializeField] private float headBobFrequency = 7.5f;
        [SerializeField] private MobileJoystick mobileMoveJoystick;
        [SerializeField] private MobileLookArea mobileLookArea;
        [SerializeField] private MobileButton mobileSprintButton;
        [SerializeField] private MobileButton mobileJumpButton;
        [SerializeField] private MobileButton mobileCrouchButton;

        private CharacterController _characterController;
        private float _currentBobAmplitude;
        private float _verticalVelocity;
        private float _pitch;
        private float _headBobTime;
        private float _lastSprintTime;
        private float _standingHeight;
        private Vector3 _standingCenter;
        private Vector3 _cameraBaseLocalPosition;
        private Vector2 _smoothedLookDelta;
        private Vector2 _pendingLookDelta;
        private Vector3 _horizontalVelocity;
        private float _sprintStamina = 1f;
        private bool _isSprinting;
        private bool _isCrouching;
        private Transform _pitchPivot;

        public float SprintStamina01 => _sprintStamina;
        public bool IsSprinting => _isSprinting;
        public bool IsCrouching => _isCrouching;
        public float LookSensitivity
        {
            get => lookSensitivity;
            set => lookSensitivity = Mathf.Clamp(value, 0.6f, 4.5f);
        }

        public void RestoreSprintStamina(float amount)
        {
            _sprintStamina = Mathf.Clamp01(_sprintStamina + amount);
        }

        public void ResetVerticalVelocity()
        {
            _verticalVelocity = 0f;
        }

        public void SetMobileControls(
            MobileJoystick moveJoystick,
            MobileLookArea lookArea,
            MobileButton sprintButton,
            MobileButton jumpButton,
            MobileButton crouchButton)
        {
            mobileMoveJoystick = moveJoystick;
            mobileLookArea = lookArea;
            mobileSprintButton = sprintButton;
            mobileJumpButton = jumpButton;
            mobileCrouchButton = crouchButton;
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _standingHeight = _characterController.height;
            _standingCenter = _characterController.center;
            _currentBobAmplitude = headBobAmplitude;

            ResolveActivePlayerCamera();
            if (cameraPivot != null)
            {
                _cameraBaseLocalPosition = cameraPivot.localPosition;
                EnsurePitchPivot();
            }
        }

        private void OnEnable()
        {
            if (!MainMenuHud.IsBlockingGameplay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            if (MainMenuHud.IsBlockingGameplay || (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved))
            {
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                return;
            }

            if (CaseNotebookHud.IsAnyNotebookOpen)
            {
                return;
            }

            UpdateStance();
            UpdateMovement();
            UpdateCameraLocalPose();
        }

        private void LateUpdate()
        {
            if (MainMenuHud.IsBlockingGameplay || (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved))
            {
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                return;
            }

            if (cameraPivot == null)
            {
                ResolveActivePlayerCamera();
            }

            if (cameraPivot == null)
            {
                return;
            }

            EnsurePitchPivot();
            UpdateLook();
        }

        private void UpdateLook()
        {
            if (CaseNotebookHud.IsAnyNotebookOpen)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _pendingLookDelta = Vector2.zero;
                _smoothedLookDelta = Vector2.zero;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            var lookInput = ReadLookInput();
            if (mobileLookArea != null)
            {
                lookInput += mobileLookArea.ConsumeLookDelta();
            }

            lookInput = Vector2.ClampMagnitude(lookInput, maxLookDelta);
            _pendingLookDelta = lookInput * (lookSensitivity * 0.05f);

            var smoothing = 1f - Mathf.Exp(-lookSmoothing * Time.unscaledDeltaTime);
            _smoothedLookDelta = Vector2.Lerp(_smoothedLookDelta, _pendingLookDelta, smoothing);

            var mouseX = _smoothedLookDelta.x;
            var mouseY = _smoothedLookDelta.y;

            transform.Rotate(Vector3.up * mouseX);

            _pitch -= mouseY;
            _pitch = Mathf.Clamp(_pitch, -maxLookAngle, maxLookAngle);
            EnsurePitchPivot();
            if (_pitchPivot != null)
            {
                _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
            else
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void UpdateMovement()
        {
            var planarInput = ReadMoveInput();
            if (mobileMoveJoystick != null)
            {
                planarInput += mobileMoveJoystick.Value;
            }

            planarInput = Vector2.ClampMagnitude(planarInput, 1f);
            if (planarInput.magnitude < moveInputDeadZone)
            {
                planarInput = Vector2.zero;
            }

            var moveInput = new Vector3(planarInput.x, 0f, planarInput.y);
            moveInput = Vector3.ClampMagnitude(moveInput, 1f);

            var isSprinting =
                (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) ||
                (mobileSprintButton != null && mobileSprintButton.IsPressed);
            if (_isCrouching)
            {
                isSprinting = false;
            }

            if (_sprintStamina <= 0.01f)
            {
                isSprinting = false;
            }

            var speed = _isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            var targetHorizontalVelocity = transform.TransformDirection(moveInput) * speed;
            var acceleration = _characterController.isGrounded
                ? (moveInput.sqrMagnitude > 0.001f ? groundAcceleration : groundDeceleration)
                : airControl;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity,
                targetHorizontalVelocity,
                acceleration * Time.deltaTime);

            var move = _horizontalVelocity;
            var jumpPressed =
                allowJump &&
                ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                (mobileJumpButton != null && mobileJumpButton.ConsumeWasPressedThisFrame()));

            if (_characterController.isGrounded)
            {
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }

                if (jumpPressed)
                {
                    _isCrouching = false;
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                }
            }

            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;

            _characterController.Move(move * Time.deltaTime);
            UpdateSprintStamina(moveInput, isSprinting);
            UpdateHeadBob(moveInput, isSprinting);
        }

        private void UpdateStance()
        {
            if (!allowCrouch)
            {
                _isCrouching = false;
                return;
            }

            var crouchPressed =
                (Keyboard.current != null && Keyboard.current[crouchKey].wasPressedThisFrame) ||
                (mobileCrouchButton != null && mobileCrouchButton.ConsumeWasPressedThisFrame());

            if (crouchPressed)
            {
                if (_isCrouching)
                {
                    if (CanStandUp())
                    {
                        _isCrouching = false;
                    }
                }
                else
                {
                    _isCrouching = true;
                }
            }

            var targetHeight = _isCrouching ? crouchHeight : _standingHeight;
            _characterController.height = Mathf.Lerp(
                _characterController.height,
                targetHeight,
                1f - Mathf.Exp(-crouchTransitionSpeed * Time.deltaTime));

            var center = _characterController.center;
            center.y = _characterController.height * 0.5f;
            _characterController.center = Vector3.Lerp(
                center,
                new Vector3(_standingCenter.x, _characterController.height * 0.5f, _standingCenter.z),
                1f - Mathf.Exp(-crouchTransitionSpeed * Time.deltaTime));
        }

        private void UpdateCameraLocalPose()
        {
            if (cameraPivot == null)
            {
                return;
            }

            EnsurePitchPivot();
            var crouchOffset = _isCrouching ? -0.34f : 0f;
            var bobOffset = Mathf.Sin(_headBobTime) * _currentBobAmplitude;
            var targetPosition = _cameraBaseLocalPosition + new Vector3(0f, crouchOffset + bobOffset, 0f);
            var moveTarget = _pitchPivot != null ? _pitchPivot : cameraPivot;
            moveTarget.localPosition = Vector3.Lerp(
                moveTarget.localPosition,
                targetPosition,
                1f - Mathf.Exp(-crouchTransitionSpeed * Time.deltaTime));
        }

        private void EnsurePitchPivot()
        {
            if (cameraPivot == null)
            {
                return;
            }

            if (_pitchPivot != null && cameraPivot.IsChildOf(_pitchPivot))
            {
                return;
            }

            var existing = transform.Find("CameraPitchPivot");
            if (existing == null)
            {
                var pivotObject = new GameObject("CameraPitchPivot");
                existing = pivotObject.transform;
                existing.SetParent(transform, false);
            }

            existing.localPosition = cameraPivot.localPosition;
            existing.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            existing.localScale = Vector3.one;

            cameraPivot.SetParent(existing, false);
            cameraPivot.localPosition = Vector3.zero;
            cameraPivot.localRotation = Quaternion.identity;
            _pitchPivot = existing;
            _cameraBaseLocalPosition = _pitchPivot.localPosition;
        }

        private void ResolveActivePlayerCamera()
        {
            var cameras = GetComponentsInChildren<Camera>(true);
            if (cameras == null || cameras.Length == 0)
            {
                return;
            }

            Camera selectedCamera = null;
            var selectedScore = float.NegativeInfinity;
            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null)
                {
                    continue;
                }

                var score = candidate.depth;
                if (candidate.enabled && candidate.gameObject.activeInHierarchy)
                {
                    score += 1000f;
                }

                if (candidate.CompareTag("MainCamera"))
                {
                    score += 100f;
                }

                if (cameraPivot == candidate.transform)
                {
                    score += 1f;
                }

                if (selectedCamera == null || score > selectedScore)
                {
                    selectedCamera = candidate;
                    selectedScore = score;
                }
            }

            if (selectedCamera == null)
            {
                return;
            }

            cameraPivot = selectedCamera.transform;
            for (var i = 0; i < cameras.Length; i++)
            {
                var candidate = cameras[i];
                if (candidate == null || candidate == selectedCamera)
                {
                    continue;
                }

                candidate.enabled = false;
                var listener = candidate.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }

        private void UpdateHeadBob(Vector3 moveInput, bool isSprinting)
        {
            if (moveInput.sqrMagnitude <= 0.001f || !_characterController.isGrounded)
            {
                _headBobTime = Mathf.Lerp(_headBobTime, 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));
                _currentBobAmplitude = Mathf.Lerp(_currentBobAmplitude, 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));
                return;
            }

            var frequency = headBobFrequency * (_isCrouching ? 0.72f : (isSprinting ? 1.22f : 1f));
            _headBobTime += Time.deltaTime * frequency;
            var targetAmplitude = _isCrouching ? crouchBobAmplitude : (isSprinting ? sprintBobAmplitude : headBobAmplitude);
            _currentBobAmplitude = Mathf.Lerp(_currentBobAmplitude, targetAmplitude, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        private void UpdateSprintStamina(Vector3 moveInput, bool isSprinting)
        {
            var activelyMoving = moveInput.sqrMagnitude > 0.01f && _characterController.isGrounded;
            _isSprinting = isSprinting && activelyMoving;

            if (_isSprinting)
            {
                _lastSprintTime = Time.time;
                _sprintStamina = Mathf.Max(0f, _sprintStamina - sprintStaminaDrainPerSecond * Time.deltaTime);
                return;
            }

            if (Time.time < _lastSprintTime + sprintRecoveryDelay)
            {
                return;
            }

            _sprintStamina = Mathf.Min(1f, _sprintStamina + sprintStaminaRecoverPerSecond * Time.deltaTime);
        }

        private bool CanStandUp()
        {
            var desiredHeight = _standingHeight;
            var radius = Mathf.Max(0.05f, _characterController.radius - 0.02f);
            var worldCenter = transform.position + _standingCenter;
            var bottom = worldCenter + Vector3.down * ((desiredHeight * 0.5f) - radius);
            var top = worldCenter + Vector3.up * ((desiredHeight * 0.5f) - radius);
            return !Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        }

        private static Vector2 ReadLookInput()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.delta.ReadValue();
            }

            if (Gamepad.current != null)
            {
                return Gamepad.current.rightStick.ReadValue() * 15f;
            }

            return Vector2.zero;
        }

        private static Vector2 ReadMoveInput()
        {
            var move = Vector2.zero;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed)
                {
                    move.x -= 1f;
                }

                if (Keyboard.current.dKey.isPressed)
                {
                    move.x += 1f;
                }

                if (Keyboard.current.sKey.isPressed)
                {
                    move.y -= 1f;
                }

                if (Keyboard.current.wKey.isPressed)
                {
                    move.y += 1f;
                }
            }

            if (Gamepad.current != null)
            {
                move += Gamepad.current.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(move, 1f);
        }
    }
}
