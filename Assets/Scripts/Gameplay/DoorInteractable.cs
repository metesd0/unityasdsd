using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class DoorInteractable : InteractableBase
    {
        [SerializeField] private Transform doorTransform;
        [SerializeField] private string requiredToolId;
        [SerializeField] private string lockedMessage = "Bu kapi icin uygun erisim gerekiyor.";
        [SerializeField] private float openAngle = 88f;
        [SerializeField] private float turnSpeed = 8f;
        [SerializeField] private bool openAwayFromInteractor = true;
        [SerializeField] private bool startsOpen;

        private Quaternion _closedRotation;
        private Quaternion _openRotation;
        private bool _isOpen;
        private Transform _rotationTransform;
        private Vector3 _doorCenterLocalToPivot;
        private AudioSource _audioSource;
        private AudioClip _openClip;
        private AudioClip _lockedClip;

        private void Awake()
        {
            if (doorTransform == null)
            {
                doorTransform = transform;
            }

            _rotationTransform = ResolveRotationTransform();
            _closedRotation = _rotationTransform.localRotation;
            _openRotation = _closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            _doorCenterLocalToPivot = CalculateDoorCenterLocalToPivot();
            _isOpen = startsOpen;
            _rotationTransform.localRotation = _isOpen ? _openRotation : _closedRotation;
            EnsureAudio();
            RefreshPrompt();
        }

        private void Update()
        {
            if (_rotationTransform == null)
            {
                return;
            }

            var target = _isOpen ? _openRotation : _closedRotation;
            _rotationTransform.localRotation = Quaternion.Slerp(
                _rotationTransform.localRotation,
                target,
                1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        public override bool TryInteract(GameObject interactor)
        {
            if (!HasAccess())
            {
                if (CaseSessionManager.Instance != null)
                {
                    CaseSessionManager.Instance.PublishMessage(string.IsNullOrWhiteSpace(lockedMessage)
                        ? "Bu kapi kilitli."
                        : lockedMessage);
                }

                PlayDoorClip(_lockedClip, 0.82f);
                return false;
            }

            if (!_isOpen && openAwayFromInteractor)
            {
                _openRotation = CalculateOpenRotation(interactor);
            }

            _isOpen = !_isOpen;
            PlayDoorClip(_openClip, 0.72f);
            RefreshPrompt();
            return true;
        }

        public override bool CanShowInteractionPrompt(GameObject interactor)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            RefreshPrompt();

            if (interactor == null)
            {
                return true;
            }

            var interactorPosition = interactor.transform.position + Vector3.up * 1.05f;
            var colliders = GetComponentsInChildren<Collider>(false);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                var closestPoint = collider.ClosestPoint(interactorPosition);
                if (Vector3.Distance(interactorPosition, closestPoint) <= 1.8f)
                {
                    return true;
                }
            }

            return Vector3.Distance(interactor.transform.position, transform.position) <= 2.2f;
        }

        public void ConfigureAccess(string toolId, string message, bool openAtStart)
        {
            requiredToolId = string.IsNullOrWhiteSpace(toolId) ? string.Empty : toolId.Trim();
            if (!string.IsNullOrWhiteSpace(message))
            {
                lockedMessage = message.Trim();
            }

            startsOpen = openAtStart;
            _isOpen = openAtStart;
        }

        private bool HasAccess()
        {
            return string.IsNullOrWhiteSpace(requiredToolId) ||
                   CaseSessionManager.Instance == null ||
                   CaseSessionManager.Instance.HasTool(requiredToolId);
        }

        private void RefreshPrompt()
        {
            if (!HasAccess())
            {
                ConfigurePrompt("Kilitli kapi");
                return;
            }

            ConfigurePrompt(_isOpen ? "Kapiyi kapat" : "Kapiyi ac");
        }

        private Quaternion CalculateOpenRotation(GameObject interactor)
        {
            if (_rotationTransform == null || interactor == null)
            {
                return _closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            }

            var positiveRotation = _closedRotation * Quaternion.Euler(0f, Mathf.Abs(openAngle), 0f);
            var negativeRotation = _closedRotation * Quaternion.Euler(0f, -Mathf.Abs(openAngle), 0f);
            var positiveCenter = GetDoorCenterForRotation(positiveRotation);
            var negativeCenter = GetDoorCenterForRotation(negativeRotation);
            var interactorPosition = interactor.transform.position;
            var positiveDistance = (positiveCenter - interactorPosition).sqrMagnitude;
            var negativeDistance = (negativeCenter - interactorPosition).sqrMagnitude;

            return positiveDistance >= negativeDistance ? positiveRotation : negativeRotation;
        }

        private Transform ResolveRotationTransform()
        {
            if (doorTransform != null && doorTransform.parent == transform && transform.name.StartsWith("DoorHinge", System.StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }

            return doorTransform != null ? doorTransform : transform;
        }

        private Vector3 CalculateDoorCenterLocalToPivot()
        {
            if (doorTransform == null || _rotationTransform == null)
            {
                return Vector3.zero;
            }

            var renderers = doorTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return Vector3.forward * 0.5f;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return _rotationTransform.InverseTransformPoint(bounds.center);
        }

        private void EnsureAudio()
        {
            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
                if (_audioSource == null)
                {
                    _audioSource = gameObject.AddComponent<AudioSource>();
                }

                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 1f;
                _audioSource.minDistance = 1.4f;
                _audioSource.maxDistance = 8f;
                _audioSource.rolloffMode = AudioRolloffMode.Linear;
                _audioSource.volume = 0.75f;
            }

            _openClip ??= LoadFreesoundClip("door_open");
            _lockedClip ??= LoadFreesoundClip("door_locked");
        }

        private void PlayDoorClip(AudioClip clip, float volumeScale)
        {
            EnsureAudio();
            if (_audioSource == null || clip == null)
            {
                return;
            }

            _audioSource.pitch = Random.Range(0.94f, 1.06f);
            _audioSource.PlayOneShot(clip, volumeScale);
        }

        private static AudioClip LoadFreesoundClip(string prefix)
        {
            var clips = Resources.LoadAll<AudioClip>("Audio/Freesound");
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip != null && clip.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }

        private Vector3 GetDoorCenterForRotation(Quaternion localRotation)
        {
            if (_rotationTransform == null)
            {
                return transform.position;
            }

            var parent = _rotationTransform.parent;
            if (parent == null)
            {
                return _rotationTransform.position + localRotation * _doorCenterLocalToPivot;
            }

            return parent.TransformPoint(_rotationTransform.localPosition + localRotation * _doorCenterLocalToPivot);
        }
    }
}
