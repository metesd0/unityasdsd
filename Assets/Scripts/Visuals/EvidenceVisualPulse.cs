using UnityEngine;

namespace MobilOfl.Visuals
{
    public class EvidenceVisualPulse : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color baseColor = new Color(0.2f, 0.9f, 1f);
        [SerializeField] private Color emissionColor = new Color(0.15f, 0.85f, 1f);
        [SerializeField] private float pulseSpeed = 2.6f;
        [SerializeField] private float pulseAmount;
        [SerializeField] private float rotationSpeed;
        [SerializeField] private float bobHeight;
        [SerializeField] private float bobSpeed = 1.8f;
        [SerializeField] private float proximityRange = 6f;
        [SerializeField] private float proximityGlowBoost = 1.8f;
        [SerializeField] private Light glowLight;
        [SerializeField] private bool ensureGlowLight = true;
        [SerializeField] private float glowLightHeight = 0.35f;

        private Vector3 _baseScale;
        private Vector3 _basePosition;
        private Material _runtimeMaterial;
        private float _proximityFactor;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            if (glowLight == null && ensureGlowLight)
            {
                var glowObject = new GameObject("EvidenceGlow");
                glowObject.transform.SetParent(transform, false);
                glowObject.transform.localPosition = Vector3.up * glowLightHeight;
                glowLight = glowObject.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.color = emissionColor;
                glowLight.range = 2.6f;
                glowLight.intensity = 0.65f;
                glowLight.shadows = LightShadows.None;
            }

            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;

            if (targetRenderer != null)
            {
                _runtimeMaterial = targetRenderer.material;
                _runtimeMaterial.color = baseColor;

                if (_runtimeMaterial.HasProperty("_EmissionColor"))
                {
                    _runtimeMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        private void Update()
        {
            var pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            if (pulseAmount > 0f)
            {
                transform.localScale = _baseScale * (1f + pulse * pulseAmount);
            }

            if (Mathf.Abs(rotationSpeed) > 0.001f)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            }

            if (bobHeight > 0f)
            {
                var bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.localPosition = _basePosition + Vector3.up * bob;
            }

            // Proximity glow
            UpdateProximityFactor();
            var proximityBoost = Mathf.Lerp(1f, proximityGlowBoost, _proximityFactor);
            var glow = Mathf.Lerp(0.45f, 1.25f, pulse) * proximityBoost;

            if (_runtimeMaterial != null && _runtimeMaterial.HasProperty("_EmissionColor"))
            {
                _runtimeMaterial.SetColor("_EmissionColor", emissionColor * glow);
            }

            if (glowLight != null)
            {
                glowLight.intensity = glow;
                glowLight.range = Mathf.Lerp(2f, 4f, _proximityFactor);
            }
        }

        private void UpdateProximityFactor()
        {
            var player = Camera.main;
            if (player == null)
            {
                _proximityFactor = 0f;
                return;
            }

            var distance = Vector3.Distance(transform.position, player.transform.position);
            _proximityFactor = Mathf.Clamp01(1f - (distance / proximityRange));
        }
    }
}
