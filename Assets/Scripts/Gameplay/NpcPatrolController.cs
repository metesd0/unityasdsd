using MobilOfl.Online;
using MobilOfl.UI;
using MobilOfl.Visuals;
using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class NpcPatrolController : MonoBehaviour
    {
        [SerializeField] private NpcInteractable npcInteractable;
        [SerializeField] private bool patrolEnabled = true;
        [SerializeField] private Vector3[] patrolOffsets =
        {
            Vector3.zero,
            new Vector3(0f, 0f, 1.8f),
            new Vector3(0f, 0f, -1.8f)
        };
        [SerializeField] private float moveSpeed = 1.25f;
        [SerializeField] private float acceleration = 3.6f;
        [SerializeField] private float deceleration = 5.2f;
        [SerializeField] private float arrivalSlowdownDistance = 0.7f;
        [SerializeField] private float turnSpeed = 5.4f;
        [SerializeField] private float waitDuration = 1.15f;
        [SerializeField] private float waitJitter = 0.45f;
        [SerializeField] private float arrivalDistance = 0.18f;
        [SerializeField] private float maxPatrolRadius = 2.6f;
        [SerializeField] private float stuckDistanceThreshold = 0.025f;
        [SerializeField] private float stuckTimeThreshold = 0.75f;
        [SerializeField] private float viewDistance = 6.8f;
        [SerializeField] private float viewAngle = 62f;
        [SerializeField] private float sightPressurePerSecond = 0.52f;
        [SerializeField] private float eyeHeight = 1.35f;
        [SerializeField] private float collisionRadius = 0.32f;
        [SerializeField] private float collisionHeight = 1.7f;
        [SerializeField] private float obstacleProbeDistance = 0.24f;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private LayerMask movementBlockMask = ~0;

        private PlayerStealthController _playerStealth;
        private bool _footFixEnsured;
        private Vector3 _anchorPosition;
        private int _currentPatrolIndex;
        private float _waitUntil;
        private float _currentMoveSpeed;
        private Vector3 _lastPosition;
        private float _stuckSince;

        private void Awake()
        {
            _anchorPosition = transform.position;
            _lastPosition = transform.position;
            ResolveReferences();
        }

        private void Update()
        {
            ResolveReferences();

            if (MainMenuHud.IsBlockingGameplay || CaseNotebookHud.IsAnyNotebookOpen)
            {
                return;
            }

            if (CaseSessionManager.Instance != null && CaseSessionManager.Instance.IsCaseResolved)
            {
                return;
            }

            if (NetworkCaseState.Instance != null &&
                NetworkCaseState.Instance.IsOnlineSessionActive &&
                !NetworkCaseState.Instance.IsGameplayPhase)
            {
                return;
            }

            UpdatePatrol();
            UpdateSightPressure();
        }

        private void UpdatePatrol()
        {
            if (npcInteractable != null && npcInteractable.IsInteractionFocused)
            {
                FaceFocusTarget();
                _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, 0f, deceleration * Time.deltaTime);
                _waitUntil = Time.time + GetWaitDuration();
                return;
            }

            if (!patrolEnabled || patrolOffsets == null || patrolOffsets.Length <= 1)
            {
                _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, 0f, deceleration * Time.deltaTime);
                _lastPosition = transform.position;
                return;
            }

            if (Time.time < _waitUntil)
            {
                _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, 0f, deceleration * Time.deltaTime);
                _lastPosition = transform.position;
                return;
            }

            if (IsOutsidePatrolBubble())
            {
                RecenterPatrolWithoutTeleport();
                return;
            }

            var targetPosition = GetBoundedPatrolTarget(_currentPatrolIndex);
            var toTarget = targetPosition - transform.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= arrivalDistance)
            {
                _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolOffsets.Length;
                _currentMoveSpeed = 0f;
                _waitUntil = Time.time + GetWaitDuration();
                return;
            }

            var moveDirection = toTarget.normalized;
            var desiredSpeed = moveSpeed * Mathf.Clamp01(toTarget.magnitude / Mathf.Max(arrivalDistance, arrivalSlowdownDistance));
            if (toTarget.magnitude > arrivalDistance * 2f)
            {
                desiredSpeed = Mathf.Max(desiredSpeed, moveSpeed * 0.35f);
            }

            _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, desiredSpeed, acceleration * Time.deltaTime);
            var moveStep = Mathf.Min(_currentMoveSpeed * Time.deltaTime, toTarget.magnitude);
            if (!CanMove(moveDirection, moveStep + obstacleProbeDistance))
            {
                HoldThenAdvancePatrol();
                return;
            }

            transform.position += moveDirection * moveStep;
            UpdateStuckState();

            if (toTarget.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }

        private Vector3 GetBoundedPatrolTarget(int patrolIndex)
        {
            var offset = patrolOffsets[Mathf.Clamp(patrolIndex, 0, patrolOffsets.Length - 1)];
            offset.y = 0f;
            var radius = Mathf.Max(arrivalDistance, maxPatrolRadius);
            if (offset.magnitude > radius)
            {
                offset = offset.normalized * radius;
            }

            return _anchorPosition + offset;
        }

        private bool IsOutsidePatrolBubble()
        {
            var flatOffset = transform.position - _anchorPosition;
            flatOffset.y = 0f;
            return flatOffset.magnitude > Mathf.Max(maxPatrolRadius + 0.45f, arrivalDistance * 2f);
        }

        private void RecenterPatrolWithoutTeleport()
        {
            _currentPatrolIndex = 0;
            _currentMoveSpeed = 0f;
            _waitUntil = Time.time + GetWaitDuration();
            _lastPosition = transform.position;
            _stuckSince = 0f;
        }

        private void HoldThenAdvancePatrol()
        {
            _currentPatrolIndex = (_currentPatrolIndex + 1) % patrolOffsets.Length;
            _currentMoveSpeed = 0f;
            _waitUntil = Time.time + GetWaitDuration();
            _lastPosition = transform.position;
            _stuckSince = 0f;
        }

        private void UpdateStuckState()
        {
            var flatDelta = transform.position - _lastPosition;
            flatDelta.y = 0f;
            if (_currentMoveSpeed <= 0.05f || flatDelta.magnitude >= stuckDistanceThreshold)
            {
                _lastPosition = transform.position;
                _stuckSince = 0f;
                return;
            }

            if (_stuckSince <= 0f)
            {
                _stuckSince = Time.time;
                return;
            }

            if (Time.time - _stuckSince >= stuckTimeThreshold)
            {
                HoldThenAdvancePatrol();
            }
        }

        private float GetWaitDuration()
        {
            var jitter = Mathf.Max(0f, waitJitter);
            return Mathf.Max(0f, waitDuration + Random.Range(-jitter, jitter));
        }

        private void UpdateSightPressure()
        {
            if (_playerStealth == null || !_playerStealth.isActiveAndEnabled)
            {
                return;
            }

            var playerPosition = _playerStealth.transform.position + Vector3.up * 1f;
            var eyePosition = transform.position + Vector3.up * eyeHeight;
            var toPlayer = playerPosition - eyePosition;
            var distance = toPlayer.magnitude;
            if (distance > viewDistance || distance <= 0.05f)
            {
                return;
            }

            var direction = toPlayer / distance;
            if (Vector3.Angle(transform.forward, direction) > viewAngle * 0.5f)
            {
                return;
            }

            if (Physics.Raycast(eyePosition, direction, out var hit, distance, occlusionMask, QueryTriggerInteraction.Ignore))
            {
                if (!(hit.transform.IsChildOf(transform) || hit.transform == transform) &&
                    !hit.transform.IsChildOf(_playerStealth.transform) &&
                    hit.transform != _playerStealth.transform)
                {
                    return;
                }
            }

            _playerStealth.RegisterNpcSightPressure(sightPressurePerSecond * Time.deltaTime, npcInteractable != null ? npcInteractable.NpcDisplayName : name);
        }

        private void ResolveReferences()
        {
            if (npcInteractable == null || !npcInteractable.isActiveAndEnabled)
            {
                npcInteractable = GetComponent<NpcInteractable>();
            }

            if (_playerStealth == null || !_playerStealth.isActiveAndEnabled)
            {
                _playerStealth = Object.FindAnyObjectByType<PlayerStealthController>();
            }

            EnsureFootAlignmentFix();
        }

        private bool CanMove(Vector3 direction, float distance)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                return true;
            }

            var radius = Mathf.Max(0.12f, collisionRadius);
            var height = Mathf.Max(radius * 2f, collisionHeight);
            var center = transform.position + Vector3.up * (height * 0.5f);
            var halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            var bottom = center - Vector3.up * halfSegment;
            var top = center + Vector3.up * halfSegment;

            if (!Physics.CapsuleCast(bottom, top, radius, direction.normalized, out var hit, Mathf.Max(0.01f, distance), movementBlockMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        private void FaceFocusTarget()
        {
            var target = npcInteractable != null ? npcInteractable.FocusedInteractor : null;
            if (target == null)
            {
                return;
            }

            var direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        private void EnsureFootAlignmentFix()
        {
            if (_footFixEnsured)
            {
                return;
            }

            var visualAnimator = GetComponentInChildren<Animator>(true);
            if (visualAnimator == null)
            {
                return;
            }

            var fix = visualAnimator.GetComponent<NpcFootAlignmentFix>();
            if (fix == null)
            {
                fix = visualAnimator.gameObject.AddComponent<NpcFootAlignmentFix>();
            }

            _footFixEnsured = true;
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0.01f, acceleration);
            deceleration = Mathf.Max(0.01f, deceleration);
            arrivalSlowdownDistance = Mathf.Max(0.01f, arrivalSlowdownDistance);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            waitDuration = Mathf.Max(0f, waitDuration);
            waitJitter = Mathf.Max(0f, waitJitter);
            arrivalDistance = Mathf.Max(0.01f, arrivalDistance);
            maxPatrolRadius = Mathf.Max(arrivalDistance, maxPatrolRadius);
            stuckDistanceThreshold = Mathf.Max(0.001f, stuckDistanceThreshold);
            stuckTimeThreshold = Mathf.Max(0.05f, stuckTimeThreshold);
        }
    }
}
