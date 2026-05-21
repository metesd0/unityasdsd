using MobilOfl.Gameplay;
using MobilOfl.Online;
using UnityEngine;

namespace MobilOfl.UI
{
    public class InvestigationWaypointHud : MonoBehaviour
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Camera targetCamera;

        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;

        private void Update()
        {
            ResolveReferences();
        }

        private void OnGUI()
        {
            if (MainMenuHud.IsBlockingGameplay || CaseNotebookHud.IsAnyNotebookOpen)
            {
                return;
            }

            ResolveReferences();
            var session = CaseSessionManager.Instance;
            if (playerTarget == null || session == null || session.ActiveCase == null || session.IsCaseResolved)
            {
                return;
            }

            if (!TryGetNextTarget(session, out var title, out var worldPosition, out var subtitle))
            {
                return;
            }

            worldPosition = SchoolLocationUtility.PrototypeToWorldPosition(worldPosition, playerTarget.position);
            EnsureStyles();

            var directionHint = GetDirectionHint(worldPosition);
            var distance = Vector3.Distance(playerTarget.position, worldPosition);
            var width = Mathf.Min(520f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 132f, width, 94f);

            GUILayout.BeginArea(rect, _panelStyle);
            GUILayout.Label($"SONRAKI HEDEF: {title}", _titleStyle);
            GUILayout.Label(subtitle, _bodyStyle);
            GUILayout.Label($"Yon: {directionHint}   Uzaklik: {distance:0}m", _mutedStyle);
            GUILayout.EndArea();
            ModernGuiTheme.DrawPanelChrome(rect, ModernGuiTheme.AccentWarmColor);
        }

        private bool TryGetNextTarget(CaseSessionManager session, out string title, out Vector3 worldPosition, out string subtitle)
        {
            title = string.Empty;
            worldPosition = Vector3.zero;
            subtitle = string.Empty;

            if (!session.HasEvidence("evidence.security-log"))
            {
                title = "Guvenlik Odasi";
                worldPosition = new Vector3(-7f, 1f, 10f);
                subtitle = "Gece hareketlerini teyit eden ilk dijital kayit burada.";
                return true;
            }

            if (!session.HasEvidence("evidence.answer-key-note"))
            {
                title = "Kutuphane Masasi";
                worldPosition = new Vector3(7f, 1f, -2f);
                subtitle = "Yazili not ve fiziksel kagit izi kutuphane tarafinda.";
                return true;
            }

            if (!session.HasEvidence("evidence.library-alibi"))
            {
                title = "Bilisim Oturum Kaydi";
                worldPosition = new Vector3(7.8f, 1f, 9.5f);
                subtitle = "Okul ag kaydi, kutuphane ogrencisinin mazeretini dogrular.";
                return true;
            }

            if (!session.HasEvidence("evidence.student-testimony"))
            {
                title = "Kutuphane Ogrencisi";
                worldPosition = new Vector3(5f, 1f, -2f);
                subtitle = "Oturum kaydindan sonra ogrenci yanlis hedef olmadigini anlatir.";
                return true;
            }

            if (!session.HasEvidence("evidence.canteen-receipt"))
            {
                title = "Kantin Fisi";
                worldPosition = new Vector3(13.2f, 1f, 8.8f);
                subtitle = "Kasa ustundeki fis, kantin ifadesindeki saat tutarsizligini acar.";
                return true;
            }

            if (!session.HasEvidence("evidence.canteen-testimony"))
            {
                title = "Kantin Calisani";
                worldPosition = new Vector3(13.2f, 1f, 8.8f);
                subtitle = "Fisle birlikte konusursan ilk yalan ifade kirilir.";
                return true;
            }

            if (!session.HasTool("tool.archive-pass"))
            {
                title = "Arsiv Gecis Karti";
                worldPosition = new Vector3(7.8f, 1f, 9.5f);
                subtitle = "Kantin ifadesinden sonra karti al. Arsiv kapisi ve raf aramasi acilir.";
                return true;
            }

            if (!session.HasEvidence("evidence.archive-ledger"))
            {
                title = "Arsiv Kanadi";
                worldPosition = new Vector3(-15f, 1f, 9f);
                subtitle = "Kart sende. Raf kutusunu arayip giris defterini ortaya cikar.";
                return true;
            }

            if (!session.HasTool("tool.lockpick"))
            {
                title = "Maymuncuk Seti";
                worldPosition = new Vector3(-8.1f, 1f, 9.8f);
                subtitle = "Kilitli cekmeceyi aramak icin guvenlik tarafindaki ekipmani al.";
                return true;
            }

            if (!session.HasEvidence("evidence.locker-key"))
            {
                title = "Ogretmenler Odasi";
                worldPosition = new Vector3(7f, 1f, 10f);
                subtitle = "Yedek anahtar supheli erisim zincirini tamamlar.";
                return true;
            }

            if (!session.HasEvidence("evidence.security-drawer-note"))
            {
                title = "Guvenlik Cekmecesi";
                worldPosition = new Vector3(-8.1f, 1f, 9.8f);
                subtitle = "Maymuncukla kilitli cekmeceyi ac; nobet notu kamera boslugunu tamamlar.";
                return true;
            }

            if (!session.HasEvidence("evidence.guard-testimony"))
            {
                title = "Guvenlik Gorevlisi";
                worldPosition = new Vector3(-2f, 1f, 9f);
                subtitle = "Nobet notundan sonra guvenlik gorevlisi kamera kaydini net ifade eder.";
                return true;
            }

            if (session.HasAnyAccusableSuspect())
            {
                title = "Guvenlik Gorevlisi";
                worldPosition = new Vector3(-2f, 1f, 9f);
                subtitle = "Son ifade vakayi kapatacak. Guvenlik gorevlisine geri don.";
                return true;
            }

            title = "Koridor Tarama";
            worldPosition = new Vector3(0f, 1f, 4f);
            subtitle = "Takim notlarini kontrol et ve eksik ipucunu yeniden tara.";
            return true;
        }

        private string GetDirectionHint(Vector3 worldPosition)
        {
            var localTarget = playerTarget.InverseTransformPoint(worldPosition);
            if (localTarget.z < -1f)
            {
                return localTarget.x < 0f ? "ARKA SOL" : "ARKA SAG";
            }

            if (localTarget.x < -1.25f)
            {
                return "SOLA DON";
            }

            if (localTarget.x > 1.25f)
            {
                return "SAGA DON";
            }

            return "DUZ ILERI";
        }

        private void ResolveReferences()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                targetCamera = Camera.main;
            }

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

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = ModernGuiTheme.CreatePanelStyle(new RectOffset(14, 14, 10, 10));
            _titleStyle = ModernGuiTheme.CreateLabelStyle(18, true, false);
            _bodyStyle = ModernGuiTheme.CreateLabelStyle(14, false, false);
            _mutedStyle = ModernGuiTheme.CreateLabelStyle(12, false, false, ModernGuiTheme.MutedTextColor);
        }
    }
}

