using System.Collections.Generic;
using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiWorldMarkerHud : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private float scanBonusDistance = 14f;
        [SerializeField] private int maxVisibleMarkers = 3;
        [SerializeField] private float normalViewCenterRadius = 0.18f;
        [SerializeField] private float targetRefreshInterval = 0.25f;

        private RectTransform _root;
        private readonly List<MarkerTarget> _targets = new List<MarkerTarget>(32);
        private readonly List<MarkerView> _markerViews = new List<MarkerView>(8);
        private bool _built;
        private float _nextRefreshAt;

        private struct MarkerTarget
        {
            public Vector3 WorldPosition;
            public Transform SourceTransform;
            public Vector3 WorldOffset;
            public string Label;
            public Color Color;
            public float VerticalOffset;
        }

        private sealed class MarkerView
        {
            public RectTransform Root;
            public Image Background;
            public Image Accent;
            public Text Label;
        }

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            DisableLegacy();
            ResolveCamera();
            RefreshTargets();
        }

        private void Update()
        {
            BuildIfNeeded();
            DisableLegacy();
            ResolveCamera();
            if (_root != null)
            {
                _root.gameObject.SetActive(!MainMenuHud.IsBlockingGameplay && !CaseNotebookHud.IsAnyNotebookOpen);
            }

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                RefreshTargets();
                _nextRefreshAt = Time.unscaledTime + Mathf.Max(0.08f, targetRefreshInterval);
            }

            UpdateMarkerViews();
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            var canvasTransform = transform.Find("UguiWorldMarkerCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                canvasTransform = RuntimeUiFactory.CreateUiRoot("UguiWorldMarkerCanvas", transform);
            }

            var canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 73;

            var scaler = canvasTransform.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasTransform.gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RuntimeUiFactory.Stretch(canvasTransform);
            RuntimeUiFactory.ClearChildren(canvasTransform);
            _root = canvasTransform;
            EnsureMarkerPool();
            _built = true;
        }

        private void RefreshTargets()
        {
            if (_root == null || targetCamera == null)
            {
                return;
            }

            _targets.Clear();
            DrawEvidenceMarkers();
            DrawToolMarkers();
            DrawNpcMarkers();
            DrawPlayerMarkers();
            DrawSharedPingMarker();
        }

        private void UpdateMarkerViews()
        {
            EnsureMarkerPool();
            if (_root == null || targetCamera == null)
            {
                HideMarkerViews(0);
                return;
            }

            var visibleCount = 0;
            for (var i = 0; i < _targets.Count && visibleCount < Mathf.Max(0, maxVisibleMarkers); i++)
            {
                if (!TryProjectMarker(_targets[i], visibleCount, out var anchor, out var anchoredPosition, out var size, out var markerText, out var backgroundColor, out var accentColor))
                {
                    continue;
                }

                var view = _markerViews[visibleCount];
                view.Root.gameObject.SetActive(true);
                view.Root.anchorMin = anchor;
                view.Root.anchorMax = anchor;
                view.Root.pivot = new Vector2(0.5f, 0.5f);
                view.Root.anchoredPosition = anchoredPosition;
                view.Root.sizeDelta = size;
                view.Background.color = backgroundColor;
                if (view.Accent != null)
                {
                    view.Accent.color = accentColor;
                }

                view.Label.text = markerText;
                visibleCount++;
            }

            HideMarkerViews(visibleCount);
        }

        private void DrawEvidenceMarkers()
        {
            var evidenceList = Object.FindObjectsByType<EvidenceInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < evidenceList.Length; i++)
            {
                var evidence = evidenceList[i];
                if (evidence == null || !evidence.IsMarkerVisible)
                {
                    continue;
                }

                AddMarkerTarget(evidence.transform, Vector3.up * 1.1f, evidence.MarkerLabel, evidence.MarkerColor, 0f);
            }

            var searchSpotList = Object.FindObjectsByType<SearchSpotInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < searchSpotList.Length; i++)
            {
                var searchSpot = searchSpotList[i];
                if (searchSpot == null || !searchSpot.IsMarkerVisible)
                {
                    continue;
                }

                AddMarkerTarget(searchSpot.transform, Vector3.up * 1.1f, searchSpot.MarkerLabel, searchSpot.MarkerColor, 0f);
            }
        }

        private void DrawNpcMarkers()
        {
            var npcList = Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < npcList.Length; i++)
            {
                var npc = npcList[i];
                if (npc == null || !npc.IsMarkerVisible)
                {
                    continue;
                }

                AddMarkerTarget(npc.transform, Vector3.up * 2.1f, npc.MarkerLabel, npc.MarkerColor, 10f);
            }
        }

        private void DrawToolMarkers()
        {
            var toolPickups = Object.FindObjectsByType<ToolPickupInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < toolPickups.Length; i++)
            {
                var toolPickup = toolPickups[i];
                if (toolPickup == null || !toolPickup.IsMarkerVisible)
                {
                    continue;
                }

                AddMarkerTarget(toolPickup.transform, Vector3.up * 1.05f, toolPickup.MarkerLabel, toolPickup.MarkerColor, 0f);
            }
        }

        private void DrawPlayerMarkers()
        {
            var players = Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsInactive.Exclude);
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null || !player.ShouldShowWorldLabel)
                {
                    continue;
                }

                AddMarkerTarget(player.MarkerWorldPosition, player.DisplayName, new Color(0.58f, 0.86f, 1f, 1f), 10f);
            }
        }

        private void DrawSharedPingMarker()
        {
            var networkCaseState = NetworkCaseState.Instance;
            if (networkCaseState == null || !networkCaseState.HasActiveSharedPing)
            {
                return;
            }

            AddMarkerTarget(networkCaseState.SharedPingPosition + Vector3.up * 0.8f, "PING: " + networkCaseState.SharedPingLabel, new Color(1f, 0.84f, 0.3f, 1f), 0f);
        }

        private void AddMarkerTarget(Vector3 worldPosition, string label, Color color, float verticalOffset)
        {
            if (_targets.Count >= 32)
            {
                return;
            }

            _targets.Add(new MarkerTarget
            {
                WorldPosition = worldPosition,
                Label = label,
                Color = color,
                VerticalOffset = verticalOffset
            });
        }

        private void AddMarkerTarget(Transform sourceTransform, Vector3 worldOffset, string label, Color color, float verticalOffset)
        {
            if (_targets.Count >= 32 || sourceTransform == null)
            {
                return;
            }

            _targets.Add(new MarkerTarget
            {
                WorldPosition = sourceTransform.position + worldOffset,
                SourceTransform = sourceTransform,
                WorldOffset = worldOffset,
                Label = label,
                Color = color,
                VerticalOffset = verticalOffset
            });
        }

        private bool TryProjectMarker(MarkerTarget marker, int slotIndex, out Vector2 anchor, out Vector2 anchoredPosition, out Vector2 size, out string markerText, out Color backgroundColor, out Color accentColor)
        {
            anchor = Vector2.zero;
            anchoredPosition = Vector2.zero;
            size = Vector2.zero;
            markerText = string.Empty;
            backgroundColor = Color.clear;
            accentColor = Color.clear;

            var worldPosition = marker.SourceTransform != null
                ? marker.SourceTransform.position + marker.WorldOffset
                : marker.WorldPosition;
            var cameraPosition = targetCamera.transform.position;
            var scanActive = InvestigationScanner.IsScanActive;
            var effectiveMaxDistance = scanActive ? maxDistance + scanBonusDistance : maxDistance;
            var distance = Vector3.Distance(cameraPosition, worldPosition);
            if (distance > effectiveMaxDistance)
            {
                return false;
            }

            var viewport = targetCamera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            {
                return false;
            }

            if (!scanActive)
            {
                var centerDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
                if (centerDistance > Mathf.Max(0.02f, normalViewCenterRadius))
                {
                    return false;
                }
            }

            anchor = new Vector2(Mathf.Clamp(viewport.x, 0.1f, 0.9f), Mathf.Clamp(viewport.y, 0.14f, 0.88f));
            var scanBoost = scanActive ? 1f : 0f;
            markerText = BuildMarkerText(marker.Label, distance);
            var markerWidth = Mathf.Clamp(markerText.Length * 8.4f + 24f, 148f, 310f);
            var slotOffset = ((slotIndex % 3) - 1) * 16f;
            anchoredPosition = new Vector2(0f, marker.VerticalOffset + slotOffset);
            size = Vector2.Lerp(new Vector2(markerWidth, 28f), new Vector2(markerWidth + 16f, 35f), scanBoost);
            backgroundColor = Color.Lerp(new Color(0.04f, 0.07f, 0.1f, 0.82f), new Color(0.04f, 0.16f, 0.18f, 0.9f), scanBoost);
            accentColor = Color.Lerp(marker.Color, new Color(0.28f, 0.95f, 0.85f, 1f), scanBoost * 0.5f);
            return true;
        }

        private void EnsureMarkerPool()
        {
            if (_root == null)
            {
                return;
            }

            var poolSize = Mathf.Max(0, maxVisibleMarkers);
            while (_markerViews.Count < poolSize)
            {
                _markerViews.Add(CreateMarkerView(_markerViews.Count));
            }

            for (var i = poolSize; i < _markerViews.Count; i++)
            {
                _markerViews[i].Root.gameObject.SetActive(false);
            }
        }

        private MarkerView CreateMarkerView(int index)
        {
            var card = RuntimeUiFactory.CreateCard(
                "Marker" + index,
                _root,
                new Color(0.04f, 0.07f, 0.1f, 0.82f),
                ModernGuiTheme.AccentWarmColor);
            RuntimeUiFactory.AddVerticalLayout(card, 0f, new RectOffset(10, 10, 8, 7));
            var text = RuntimeUiFactory.CreateText("Label", card, string.Empty, 14, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = 14;

            var accent = card.Find("Accent");
            return new MarkerView
            {
                Root = card,
                Background = card.GetComponent<Image>(),
                Accent = accent != null ? accent.GetComponent<Image>() : null,
                Label = text
            };
        }

        private void HideMarkerViews(int startIndex)
        {
            for (var i = Mathf.Max(0, startIndex); i < _markerViews.Count; i++)
            {
                _markerViews[i].Root.gameObject.SetActive(false);
            }
        }

        private static string BuildMarkerText(string label, float distance)
        {
            var safeLabel = string.IsNullOrWhiteSpace(label) ? "Hedef" : label.Trim();
            if (safeLabel.Length > 24)
            {
                safeLabel = safeLabel.Substring(0, 21).TrimEnd() + "...";
            }

            return $"{safeLabel} [{distance:0}m]";
        }

        private void ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return;
            }

            targetCamera = Camera.main;
            if (targetCamera != null)
            {
                return;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
            for (var i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                {
                    targetCamera = cameras[i];
                    return;
                }
            }
        }

        private void DisableLegacy()
        {
            var legacy = Object.FindAnyObjectByType<WorldMarkerHud>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }
        }
    }
}
