using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class DynamicAtmosphereController : MonoBehaviour
    {
        [SerializeField] private float ambientIntensityNormal = 0.45f;
        [SerializeField] private float ambientIntensityTense = 0.25f;
        [SerializeField] private Color ambientColorNormal = new Color(0.42f, 0.48f, 0.58f, 1f);
        [SerializeField] private Color ambientColorTense = new Color(0.28f, 0.22f, 0.32f, 1f);
        [SerializeField] private Color ambientColorHighAlert = new Color(0.38f, 0.18f, 0.16f, 1f);
        [SerializeField] private float fogDensityNormal = 0.008f;
        [SerializeField] private float fogDensityTense = 0.022f;
        [SerializeField] private Color fogColorNormal = new Color(0.12f, 0.14f, 0.18f, 1f);
        [SerializeField] private Color fogColorTense = new Color(0.08f, 0.06f, 0.1f, 1f);
        [SerializeField] private float transitionSpeed = 1.2f;
        [SerializeField] private float heartbeatInterval = 0.85f;
        [SerializeField] private float heartbeatVolume = 0.22f;

        private PlayerStealthController _stealthController;
        private AudioSource _ambienceSource;
        private AudioSource _heartbeatSource;
        private float _tensionBlend;
        private float _nextHeartbeat;
        private bool _heartbeatActive;
        private static DynamicAtmosphereController _instance;

        public static DynamicAtmosphereController Instance => _instance;
        public float TensionBlend => _tensionBlend;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            SetupAudio();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            ResolveReferences();
            UpdateTensionBlend();
            UpdateLighting();
            UpdateFog();
            UpdateHeartbeat();
            UpdateAmbience();
        }

        private void UpdateTensionBlend()
        {
            var targetTension = 0f;
            if (_stealthController != null)
            {
                targetTension = _stealthController.AlertLevel01;
            }

            _tensionBlend = Mathf.MoveTowards(_tensionBlend, targetTension, transitionSpeed * Time.deltaTime);
        }

        private void UpdateLighting()
        {
            var targetColor = _tensionBlend > 0.75f
                ? Color.Lerp(ambientColorTense, ambientColorHighAlert, (_tensionBlend - 0.75f) / 0.25f)
                : Color.Lerp(ambientColorNormal, ambientColorTense, _tensionBlend);

            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetColor, Time.deltaTime * 3f);
            RenderSettings.ambientIntensity = Mathf.Lerp(ambientIntensityNormal, ambientIntensityTense, _tensionBlend);
        }

        private void UpdateFog()
        {
            RenderSettings.fogDensity = Mathf.Lerp(fogDensityNormal, fogDensityTense, _tensionBlend);
            RenderSettings.fogColor = Color.Lerp(fogColorNormal, fogColorTense, _tensionBlend);
        }

        private void UpdateHeartbeat()
        {
            var shouldBeat = _tensionBlend > 0.6f;
            _heartbeatActive = shouldBeat;

            if (!shouldBeat || _heartbeatSource == null)
            {
                return;
            }

            if (Time.time < _nextHeartbeat)
            {
                return;
            }

            _nextHeartbeat = Time.time + Mathf.Lerp(heartbeatInterval, heartbeatInterval * 0.5f, (_tensionBlend - 0.6f) / 0.4f);
            PlayProceduralHeartbeat();
        }

        private void UpdateAmbience()
        {
            if (_ambienceSource == null)
            {
                return;
            }

            if (!_ambienceSource.isPlaying)
            {
                if (_ambienceSource.clip == null)
                {
                    _ambienceSource.clip = LoadFreesoundClip("ambience_school_hall");
                }

                if (_ambienceSource.clip == null)
                {
                    GenerateAmbienceClip();
                }

                _ambienceSource.Play();
            }

            _ambienceSource.volume = Mathf.Lerp(0.04f, 0.12f, _tensionBlend);
            _ambienceSource.pitch = Mathf.Lerp(0.9f, 1.15f, _tensionBlend);
        }

        private void PlayProceduralHeartbeat()
        {
            var sampleRate = AudioSettings.outputSampleRate;
            var duration = 0.25f;
            var samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create("heartbeat", samples, 1, sampleRate, false);
            var data = new float[samples];

            var beat1 = Mathf.RoundToInt(samples * 0.08f);
            var beat2 = Mathf.RoundToInt(samples * 0.22f);

            for (var i = 0; i < samples; i++)
            {
                var t = (float)i / samples;

                // Two thuds
                var dist1 = Mathf.Abs(i - beat1) / (float)samples;
                var dist2 = Mathf.Abs(i - beat2) / (float)samples;
                var env1 = Mathf.Exp(-dist1 * 80f);
                var env2 = Mathf.Exp(-dist2 * 100f) * 0.7f;

                var freq1 = Mathf.Sin(t * Mathf.PI * 42f);
                var freq2 = Mathf.Sin(t * Mathf.PI * 56f);

                data[i] = (freq1 * env1 + freq2 * env2) * 0.65f;
            }

            clip.SetData(data, 0);
            var vol = heartbeatVolume * Mathf.Lerp(0.4f, 1f, (_tensionBlend - 0.6f) / 0.4f);
            _heartbeatSource.PlayOneShot(clip, Mathf.Clamp01(vol));
        }

        private void GenerateAmbienceClip()
        {
            var sampleRate = AudioSettings.outputSampleRate;
            var duration = 8f;
            var samples = Mathf.RoundToInt(sampleRate * duration);
            var clip = AudioClip.Create("ambience", samples, 1, sampleRate, false);
            var data = new float[samples];

            for (var i = 0; i < samples; i++)
            {
                var t = (float)i / samples;
                var noise = (Random.Range(-1f, 1f) * 0.3f);
                var hum = Mathf.Sin(t * Mathf.PI * 100f) * 0.15f; // Electrical hum
                var drone = Mathf.Sin(t * Mathf.PI * 32f) * 0.12f; // Low drone
                var wind = Mathf.PerlinNoise(t * 2f, 0.5f) * 0.2f - 0.1f;
                data[i] = (noise + hum + drone + wind) * 0.25f;
            }

            clip.SetData(data, 0);
            _ambienceSource.clip = clip;
            _ambienceSource.loop = true;
        }

        private void SetupAudio()
        {
            // Ambience
            _ambienceSource = gameObject.AddComponent<AudioSource>();
            _ambienceSource.spatialBlend = 0f;
            _ambienceSource.playOnAwake = false;
            _ambienceSource.loop = true;
            _ambienceSource.volume = 0.06f;
            _ambienceSource.priority = 200;

            // Heartbeat
            _heartbeatSource = gameObject.AddComponent<AudioSource>();
            _heartbeatSource.spatialBlend = 0f;
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.loop = false;
            _heartbeatSource.volume = heartbeatVolume;
            _heartbeatSource.priority = 64;
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

        private void ResolveReferences()
        {
            if (_stealthController == null || !_stealthController.isActiveAndEnabled)
            {
                _stealthController = Object.FindAnyObjectByType<PlayerStealthController>();
            }
        }
    }
}
