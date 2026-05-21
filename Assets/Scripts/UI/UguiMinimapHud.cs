using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;
using UnityEngine.UI;

namespace MobilOfl.UI
{
    public class UguiMinimapHud : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Vector2 worldMin = new Vector2(-22f, -10f);
        [SerializeField] private Vector2 worldMax = new Vector2(22f, 30f);

        private RectTransform _root;
        private RectTransform _mapRect;
        private Text _zoneText;
        private RectTransform _markerRoot;
        private RectTransform _gridRoot;
        private bool _built;
        private float _nextRefreshAt;
        private float _pulseTimer;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void OnEnable()
        {
            BuildIfNeeded();
            DisableLegacy();
        }

        private void Update()
        {
            BuildIfNeeded();
            DisableLegacy();
            ResolvePlayerTarget();
            if (_root != null)
            {
                _root.gameObject.SetActive(!MainMenuHud.IsBlockingGameplay && !CaseNotebookHud.IsAnyNotebookOpen);
            }

            if (Time.unscaledTime >= _nextRefreshAt)
            {
                Refresh();
                _nextRefreshAt = Time.unscaledTime + 0.15f;
            }
        }

        private void BuildIfNeeded()
        {
            if (_built)
            {
                return;
            }

            var canvasTransform = transform.Find("UguiMinimapCanvas") as RectTransform;
            if (canvasTransform == null)
            {
                canvasTransform = RuntimeUiFactory.CreateUiRoot("UguiMinimapCanvas", transform);
            }

            var canvas = canvasTransform.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasTransform.gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 71;

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

            var card = RuntimeUiFactory.CreateCard("MinimapCard", _root, ModernGuiTheme.PanelColor, ModernGuiTheme.AccentWarmColor);
            card.anchorMin = new Vector2(1f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(1f, 1f);
            card.anchoredPosition = new Vector2(-16f, -108f);
            card.sizeDelta = new Vector2(210f, 178f);
            RuntimeUiFactory.AddVerticalLayout(card, 3f, new RectOffset(11, 11, 11, 9));
            RuntimeUiFactory.CreateText("Title", card, "HARITA", 14, ModernGuiTheme.TextColor, FontStyle.Bold, TextAnchor.UpperLeft);
            _zoneText = RuntimeUiFactory.CreateText("Zone", card, string.Empty, 12, ModernGuiTheme.MutedTextColor, FontStyle.Bold, TextAnchor.UpperLeft);

            _mapRect = RuntimeUiFactory.CreateUiRoot("Map", card);
            RuntimeUiFactory.EnsureLayoutElement(_mapRect, preferredHeight: 118f);
            RuntimeUiFactory.AddImage(_mapRect.gameObject, new Color(0.06f, 0.09f, 0.12f, 0.96f));
            RuntimeUiFactory.AddOutline(_mapRect.gameObject, new Color(0f, 0f, 0f, 0.45f), new Vector2(1f, -1f));
            BuildGrid(_mapRect);
            BuildStaticMap(_mapRect);

            _markerRoot = RuntimeUiFactory.CreateUiRoot("Markers", _mapRect);
            RuntimeUiFactory.Stretch(_markerRoot);
            _built = true;
        }

        private void BuildGrid(RectTransform parent)
        {
            _gridRoot = RuntimeUiFactory.CreateUiRoot("Grid", parent);
            RuntimeUiFactory.Stretch(_gridRoot);
            const int gridLines = 5;
            for (var i = 1; i < gridLines; i++)
            {
                var t = i / (float)gridLines;

                // Horizontal
                var hLine = RuntimeUiFactory.CreateUiRoot($"HLine{i}", _gridRoot);
                hLine.anchorMin = new Vector2(0f, t);
                hLine.anchorMax = new Vector2(1f, t);
                hLine.pivot = new Vector2(0.5f, 0.5f);
                hLine.anchoredPosition = Vector2.zero;
                hLine.sizeDelta = new Vector2(0f, 1f);
                RuntimeUiFactory.AddImage(hLine.gameObject, new Color(1f, 1f, 1f, 0.04f)).raycastTarget = false;

                // Vertical
                var vLine = RuntimeUiFactory.CreateUiRoot($"VLine{i}", _gridRoot);
                vLine.anchorMin = new Vector2(t, 0f);
                vLine.anchorMax = new Vector2(t, 1f);
                vLine.pivot = new Vector2(0.5f, 0.5f);
                vLine.anchoredPosition = Vector2.zero;
                vLine.sizeDelta = new Vector2(1f, 0f);
                RuntimeUiFactory.AddImage(vLine.gameObject, new Color(1f, 1f, 1f, 0.04f)).raycastTarget = false;
            }
        }

        private void BuildStaticMap(RectTransform parent)
        {
            CreateZone(parent, new Vector2(0f, 4f), new Vector2(20f, 24f), new Color(0.2f, 0.26f, 0.3f, 0.85f), "ANA BLOK");
            CreateZone(parent, new Vector2(0f, 23f), new Vector2(18f, 10f), new Color(0.16f, 0.3f, 0.22f, 0.82f), "AVLU");
            CreateZone(parent, new Vector2(-15f, 4f), new Vector2(10f, 16f), new Color(0.18f, 0.22f, 0.34f, 0.82f), "LAB");
            CreateZone(parent, new Vector2(15f, 4f), new Vector2(10f, 16f), new Color(0.28f, 0.22f, 0.16f, 0.82f), "SPOR");
        }

        private void CreateZone(RectTransform parent, Vector2 center, Vector2 size, Color color, string label)
        {
            var min = WorldToMap(new Vector3(center.x - size.x * 0.5f, 0f, center.y - size.y * 0.5f));
            var max = WorldToMap(new Vector3(center.x + size.x * 0.5f, 0f, center.y + size.y * 0.5f));
            var zone = RuntimeUiFactory.CreateUiRoot(label, parent);
            zone.anchorMin = min;
            zone.anchorMax = max;
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            RuntimeUiFactory.AddImage(zone.gameObject, color).raycastTarget = false;
            var text = RuntimeUiFactory.CreateText("Label", zone, label, 13, new Color(1f, 1f, 1f, 0.85f), FontStyle.Bold, TextAnchor.MiddleCenter);
            RuntimeUiFactory.Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void Refresh()
        {
            if (_mapRect == null)
            {
                return;
            }

            if (playerTarget == null)
            {
                _zoneText.text = "Oyuncu bekleniyor";
                RuntimeUiFactory.ClearChildren(_markerRoot);
                return;
            }

            _zoneText.text = SchoolLocationUtility.GetZoneTitle(playerTarget.position);
            RuntimeUiFactory.ClearChildren(_markerRoot);
            DrawMarkers();
            DrawPlayer();
        }

        private void DrawMarkers()
        {
            var evidenceList = Object.FindObjectsByType<EvidenceInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < evidenceList.Length; i++)
            {
                var evidence = evidenceList[i];
                if (evidence == null || !evidence.IsMarkerVisible)
                {
                    continue;
                }

                DrawPoint(evidence.transform.position, evidence.MarkerColor, 10f);
            }

            var npcList = Object.FindObjectsByType<NpcInteractable>(FindObjectsInactive.Exclude);
            for (var i = 0; i < npcList.Length; i++)
            {
                var npc = npcList[i];
                if (npc == null || !npc.IsMarkerVisible)
                {
                    continue;
                }

                DrawPoint(npc.transform.position, npc.MarkerColor, 11f);
            }
        }

        private void DrawPlayer()
        {
            _pulseTimer += Time.deltaTime;
            var pulse = 1f + Mathf.Sin(_pulseTimer * 3.5f) * 0.25f;
            DrawPoint(playerTarget.position, ModernGuiTheme.AccentWarmColor, 13f * pulse);
            var forward = playerTarget.forward;
            var direction = new Vector2(forward.x, forward.z).normalized;
            for (var i = 1; i <= 3; i++)
            {
                var sampleWorld = playerTarget.position + new Vector3(direction.x, 0f, direction.y) * (1.2f * i);
                DrawPoint(sampleWorld, new Color(1f, 0.85f, 0.35f, 0.92f - i * 0.18f), 5f);
            }

            // Draw teammates in online
            DrawTeammates();
        }

        private void DrawTeammates()
        {
            var avatars = Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsInactive.Exclude);
            for (var i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] == null || avatars[i].IsOwner)
                {
                    continue;
                }

                DrawPoint(avatars[i].transform.position, ModernGuiTheme.AccentColor, 10f);
            }
        }

        private void DrawPoint(Vector3 worldPosition, Color color, float size)
        {
            var mapPosition = SchoolLocationUtility.NormalizeToPrototypeSpace(worldPosition);
            if (mapPosition.x < worldMin.x || mapPosition.x > worldMax.x || mapPosition.z < worldMin.y || mapPosition.z > worldMax.y)
            {
                return;
            }

            var normalized = WorldToMap(mapPosition);
            var point = RuntimeUiFactory.CreateUiRoot("Point", _markerRoot);
            point.anchorMin = normalized;
            point.anchorMax = normalized;
            point.pivot = new Vector2(0.5f, 0.5f);
            point.anchoredPosition = Vector2.zero;
            point.sizeDelta = new Vector2(size, size);
            RuntimeUiFactory.AddImage(point.gameObject, color).raycastTarget = false;
        }

        private Vector2 WorldToMap(Vector3 worldPosition)
        {
            var mapPosition = SchoolLocationUtility.NormalizeToPrototypeSpace(worldPosition);
            var normalizedX = Mathf.InverseLerp(worldMin.x, worldMax.x, mapPosition.x);
            var normalizedY = Mathf.InverseLerp(worldMin.y, worldMax.y, mapPosition.z);
            return new Vector2(normalizedX, normalizedY);
        }

        private void ResolvePlayerTarget()
        {
            if (playerTarget != null && playerTarget.gameObject.activeInHierarchy)
            {
                return;
            }

            var avatars = Object.FindObjectsByType<NetworkPlayerAvatar>(FindObjectsInactive.Exclude);
            for (var i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] != null && avatars[i].IsOwner)
                {
                    playerTarget = avatars[i].transform;
                    return;
                }
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                playerTarget = player.transform;
            }
        }

        private void DisableLegacy()
        {
            var legacy = Object.FindAnyObjectByType<SchoolMinimapHud>();
            if (legacy != null)
            {
                legacy.enabled = false;
            }
        }
    }
}
