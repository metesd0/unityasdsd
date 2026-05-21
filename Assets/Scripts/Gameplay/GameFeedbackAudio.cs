using System;
using MobilOfl.Case;
using UnityEngine;

namespace MobilOfl.Gameplay
{
    public class GameFeedbackAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _source;
        private CaseSessionManager _session;
        private AudioClip _collectClip;
        private AudioClip _toolClip;
        private AudioClip _conversationClip;
        private AudioClip _noteClip;
        private AudioClip _inferenceClip;
        private AudioClip _successClip;
        private AudioClip _warningClip;
        private AudioClip _scanClip;
        private float _nextHeartbeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (UnityEngine.Object.FindAnyObjectByType<GameFeedbackAudio>() != null)
            {
                return;
            }

            var gameObject = new GameObject("GameFeedbackAudio");
            DontDestroyOnLoad(gameObject);
            gameObject.AddComponent<GameFeedbackAudio>();
        }

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0.42f;

            _collectClip = LoadFreesoundClip("item_pickup") ?? LoadFreesoundClip("paper_rustle") ?? CreateTone("Collect", 740f, 980f, 0.12f, 0.22f);
            _toolClip = LoadFreesoundClip("item_pickup") ?? CreateTone("Tool", 520f, 820f, 0.16f, 0.24f);
            _conversationClip = LoadFreesoundClip("ui_click") ?? CreateTone("Conversation", 360f, 430f, 0.1f, 0.18f);
            _noteClip = LoadFreesoundClip("paper_rustle") ?? CreateTone("Note", 620f, 0f, 0.08f, 0.18f);
            _inferenceClip = LoadFreesoundClip("suspense_sting") ?? CreateTone("Inference", 440f, 1320f, 0.2f, 0.22f);
            _successClip = LoadFreesoundClip("ui_click") ?? CreateTone("Success", 520f, 1040f, 0.32f, 0.25f);
            _warningClip = LoadFreesoundClip("door_locked") ?? LoadFreesoundClip("suspense_sting") ?? CreateTone("Warning", 180f, 120f, 0.22f, 0.26f);
            _scanClip = LoadFreesoundClip("scan_pulse") ?? CreateTone("Scan", 260f, 680f, 0.28f, 0.14f);
            if (AudioListener.volume <= 0.001f)
            {
                AudioListener.volume = 0.75f;
            }

            Play(_noteClip, 0.28f);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_session == CaseSessionManager.Instance)
            {
                PlayLowTensionPulse();
                return;
            }

            Unsubscribe();
            _session = CaseSessionManager.Instance;
            if (_session == null)
            {
                return;
            }

            _session.EvidenceCollected += HandleEvidenceCollected;
            _session.ToolUnlocked += HandleToolUnlocked;
            _session.NpcConversationRegistered += HandleNpcConversationRegistered;
            _session.TeamNoteAdded += HandleTeamNoteAdded;
            _session.CaseResolved += HandleCaseResolved;
            _session.InferenceUnlocked += HandleInferenceUnlocked;
            _session.SessionMessagePublished += HandleSessionMessagePublished;
        }

        private void PlayLowTensionPulse()
        {
            if (Time.time < _nextHeartbeat || DynamicAtmosphereController.Instance == null)
            {
                return;
            }

            var tension = DynamicAtmosphereController.Instance.TensionBlend;
            if (tension < 0.35f)
            {
                return;
            }

            _nextHeartbeat = Time.time + Mathf.Lerp(2.6f, 1.25f, tension);
            Play(_warningClip, Mathf.Lerp(0.08f, 0.22f, tension));
        }

        private void Unsubscribe()
        {
            if (_session == null)
            {
                return;
            }

            _session.EvidenceCollected -= HandleEvidenceCollected;
            _session.ToolUnlocked -= HandleToolUnlocked;
            _session.NpcConversationRegistered -= HandleNpcConversationRegistered;
            _session.TeamNoteAdded -= HandleTeamNoteAdded;
            _session.CaseResolved -= HandleCaseResolved;
            _session.InferenceUnlocked -= HandleInferenceUnlocked;
            _session.SessionMessagePublished -= HandleSessionMessagePublished;
            _session = null;
        }

        private void HandleEvidenceCollected(EvidenceData _)
        {
            Play(_collectClip, 0.78f);
        }

        private void HandleToolUnlocked(string _, string __)
        {
            Play(_toolClip, 0.82f);
        }

        private void HandleNpcConversationRegistered(string _, string __, string ___, bool revealedLead)
        {
            Play(revealedLead ? _collectClip : _conversationClip, revealedLead ? 0.65f : 0.5f);
        }

        private void HandleTeamNoteAdded(string _, string __)
        {
            Play(_noteClip, 0.55f);
        }

        private void HandleInferenceUnlocked(string _, string __)
        {
            Play(_inferenceClip, 0.72f);
        }

        private void HandleCaseResolved(bool success, string _)
        {
            Play(success ? _successClip : _warningClip, success ? 0.88f : 0.72f);
        }

        private void HandleSessionMessagePublished(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && message.StartsWith("Tarama:", StringComparison.OrdinalIgnoreCase))
            {
                Play(_scanClip, 0.45f);
            }
        }

        private void Play(AudioClip clip, float volumeScale)
        {
            if (_source == null || clip == null)
            {
                return;
            }

            _source.PlayOneShot(clip, volumeScale);
        }

        private static AudioClip CreateTone(string name, float frequencyA, float frequencyB, float duration, float amplitude)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / SampleRate;
                var envelope = Mathf.Sin(Mathf.PI * i / Mathf.Max(1, sampleCount - 1));
                var value = Mathf.Sin(2f * Mathf.PI * frequencyA * t);
                if (frequencyB > 0f)
                {
                    value = (value + Mathf.Sin(2f * Mathf.PI * frequencyB * t)) * 0.5f;
                }

                samples[i] = value * envelope * amplitude;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip LoadFreesoundClip(string prefix)
        {
            var clips = Resources.LoadAll<AudioClip>("Audio/Freesound");
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip != null && clip.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
