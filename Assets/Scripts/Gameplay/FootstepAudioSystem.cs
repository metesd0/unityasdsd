using UnityEngine;

namespace MobilOfl.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class FootstepAudioSystem : MonoBehaviour
    {
        [System.Serializable]
        public class SurfaceProfile
        {
            public string SurfaceName;
            public PhysicsMaterial PhysicsMaterial;
            public float PitchMin = 0.88f;
            public float PitchMax = 1.12f;
            public float VolumeMultiplier = 1f;
        }

        [SerializeField] private float walkStepInterval = 0.52f;
        [SerializeField] private float sprintStepInterval = 0.34f;
        [SerializeField] private float crouchStepInterval = 0.72f;
        [SerializeField] private float baseVolume = 0.35f;
        [SerializeField] private float sprintVolumeBoost = 0.18f;
        [SerializeField] private float crouchVolumeReduction = 0.2f;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private SurfaceProfile[] surfaceProfiles;

        private CharacterController _characterController;
        private PrototypeFirstPersonController _movementController;
        private AudioSource _audioSource;
        private float _stepTimer;
        private float _lastStepDistance;
        private Vector3 _lastPosition;
        private string _currentSurface = "default";

        public string CurrentSurface => _currentSurface;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _movementController = GetComponent<PrototypeFirstPersonController>();
            EnsureAudioSource();
            LoadFootstepClipsIfNeeded();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (_movementController == null)
            {
                _movementController = GetComponent<PrototypeFirstPersonController>();
            }

            if (!_characterController.isGrounded)
            {
                _stepTimer = 0f;
                return;
            }

            var displacement = transform.position - _lastPosition;
            var planarSpeed = new Vector2(displacement.x, displacement.z).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            _lastPosition = transform.position;

            if (planarSpeed < 0.5f)
            {
                _stepTimer = 0f;
                return;
            }

            DetectSurface();

            var isSprinting = _movementController != null && _movementController.IsSprinting;
            var isCrouching = _movementController != null && _movementController.IsCrouching;
            var interval = isCrouching ? crouchStepInterval : (isSprinting ? sprintStepInterval : walkStepInterval);

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= interval)
            {
                _stepTimer -= interval;
                PlayFootstep(isSprinting, isCrouching);
            }
        }

        private void PlayFootstep(bool isSprinting, bool isCrouching)
        {
            EnsureAudioSource();
            if (_audioSource == null)
            {
                return;
            }

            var volume = baseVolume;
            if (isSprinting)
            {
                volume += sprintVolumeBoost;
            }
            else if (isCrouching)
            {
                volume -= crouchVolumeReduction;
            }

            var profile = GetCurrentSurfaceProfile();
            if (profile != null)
            {
                volume *= profile.VolumeMultiplier;
                _audioSource.pitch = Random.Range(profile.PitchMin, profile.PitchMax);
            }
            else
            {
                _audioSource.pitch = Random.Range(0.88f, 1.12f);
            }

            _audioSource.volume = Mathf.Clamp01(volume);

            if (footstepClips != null && footstepClips.Length > 0)
            {
                var clip = footstepClips[Random.Range(0, footstepClips.Length)];
                if (clip != null)
                {
                    _audioSource.PlayOneShot(clip, _audioSource.volume);
                    return;
                }
            }

            PlayProceduralStep(isSprinting);
        }

        private void LoadFootstepClipsIfNeeded()
        {
            if (footstepClips != null && footstepClips.Length > 0)
            {
                return;
            }

            var allClips = Resources.LoadAll<AudioClip>("Audio/Freesound");
            var matches = new System.Collections.Generic.List<AudioClip>();
            for (var i = 0; i < allClips.Length; i++)
            {
                var clip = allClips[i];
                if (clip != null && clip.name.StartsWith("footstep_", System.StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(clip);
                }
            }

            footstepClips = matches.ToArray();
        }

        private void PlayProceduralStep(bool isSprinting)
        {
            // Generate a very short procedural footstep sound
            var sampleRate = AudioSettings.outputSampleRate;
            var duration = isSprinting ? 0.06f : 0.04f;
            var samples = Mathf.RoundToInt(sampleRate * duration);
            if (samples < 64)
            {
                samples = 64;
            }

            var clip = AudioClip.Create("footstep", samples, 1, sampleRate, false);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = (float)i / samples;
                var envelope = (1f - t) * (1f - t);
                var noise = Random.Range(-1f, 1f);
                var lowFreq = Mathf.Sin(t * Mathf.PI * Random.Range(60f, 120f));
                data[i] = (noise * 0.6f + lowFreq * 0.4f) * envelope * 0.7f;
            }

            clip.SetData(data, 0);
            _audioSource.PlayOneShot(clip, _audioSource.volume);
        }

        private void DetectSurface()
        {
            if (!Physics.Raycast(transform.position, Vector3.down, out var hit, _characterController.height * 0.5f + 0.3f, ~0, QueryTriggerInteraction.Ignore))
            {
                _currentSurface = "default";
                return;
            }

            var collider = hit.collider;
            if (collider == null)
            {
                _currentSurface = "default";
                return;
            }

            // Check by physics material
            if (collider.sharedMaterial != null)
            {
                var matName = collider.sharedMaterial.name.ToLowerInvariant();
                if (matName.Contains("wood") || matName.Contains("ahsap") || matName.Contains("parke"))
                {
                    _currentSurface = "wood";
                    return;
                }

                if (matName.Contains("tile") || matName.Contains("fayans") || matName.Contains("ceramic"))
                {
                    _currentSurface = "tile";
                    return;
                }

                if (matName.Contains("concrete") || matName.Contains("beton"))
                {
                    _currentSurface = "concrete";
                    return;
                }

                if (matName.Contains("metal"))
                {
                    _currentSurface = "metal";
                    return;
                }
            }

            // Check by object name or tag
            var objName = collider.gameObject.name.ToLowerInvariant();
            if (objName.Contains("wood") || objName.Contains("parke") || objName.Contains("floor"))
            {
                _currentSurface = "wood";
            }
            else if (objName.Contains("tile") || objName.Contains("bathroom") || objName.Contains("tuvalet"))
            {
                _currentSurface = "tile";
            }
            else if (objName.Contains("grass") || objName.Contains("avlu") || objName.Contains("yard"))
            {
                _currentSurface = "grass";
            }
            else
            {
                _currentSurface = "default";
            }
        }

        private SurfaceProfile GetCurrentSurfaceProfile()
        {
            if (surfaceProfiles == null)
            {
                return null;
            }

            for (var i = 0; i < surfaceProfiles.Length; i++)
            {
                if (surfaceProfiles[i] != null &&
                    !string.IsNullOrWhiteSpace(surfaceProfiles[i].SurfaceName) &&
                    surfaceProfiles[i].SurfaceName.Equals(_currentSurface, System.StringComparison.OrdinalIgnoreCase))
                {
                    return surfaceProfiles[i];
                }
            }

            return null;
        }

        private void EnsureAudioSource()
        {
            if (_audioSource != null)
            {
                return;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.spatialBlend = 0.4f;
            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.priority = 128;
        }
    }
}
