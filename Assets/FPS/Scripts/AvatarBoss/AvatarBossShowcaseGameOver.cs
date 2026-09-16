using Unity.FPS.AvatarBoss;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Scene-aware Game Over flow for AvatarBossShowcase only.
    /// Intercepts the moment before the player health hits zero, freezes the
    /// gameplay state (no PlayerDeathEvent, so the Microgame GameFlowManager
    /// never loads LoseScene), shows a custom overlay with a restart loop.
    /// MainScene keeps its normal GameFlowManager behaviour untouched.
    /// </summary>
    public class AvatarBossShowcaseGameOver : MonoBehaviour
    {
        Canvas m_Overlay;
        Text m_TitleText;
        Text m_SubTitleText;
        GameObject m_Player;
        Health m_PlayerHealth;
        AvatarBossController m_Boss;
        AvatarBossSummonController m_Summons;
        bool m_Shown;
        bool m_Subscribed;

        void Awake()
        {
            // only meaningful inside the boss showcase
            if (SceneManager.GetActiveScene().name != "AvatarBossShowcase")
            {
                enabled = false;
                return;
            }
        }

        void Start()
        {
            var player = GameObject.Find("Player");
            m_Boss = GetComponentInParent<AvatarBossController>();
            if (player == null || m_Boss == null)
            {
                enabled = false;
                return;
            }

            // showcase-only adaptation: give the re-added GameFlowManager a dummy fade group
            // so its Update never throws, and keep its loss-flow serialized state quiet
            var gfm = FindFirstObjectByType<GameFlowManager>();
            if (gfm != null && gfm.EndGameFadeCanvasGroup == null)
            {
                var dummy = new GameObject("GameFlowManagerFadeDummy");
                dummy.transform.SetParent(transform, false);
                var cg = dummy.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
                gfm.EndGameFadeCanvasGroup = cg;
            }

            m_PlayerHealth = player.GetComponent<Health>();
            m_PlayerHealth.OnDie += OnPlayerDie;
            m_Subscribed = true;
        }

        void OnDestroy()
        {
            if (m_Subscribed && m_PlayerHealth != null)
            {
                                m_PlayerHealth.OnDie -= OnPlayerDie;
            }
        }

        void OnPlayerDie()
        {
            // direct safety net: if death slipped through before the interception
            if (!m_Shown)
                ShowOverlay();
        }

        void ShowOverlay()
        {
            if (m_Shown)
                return;
            m_Shown = true;

            if (m_Overlay == null)
                BuildUI();

            // freeze real gameplay without breaking the boss state machine
            var player = GameObject.Find("Player");
            if (player != null)
            {
                var pc = player.GetComponent<Unity.FPS.Gameplay.PlayerCharacterController>();
                var input = player.GetComponent<Unity.FPS.Gameplay.PlayerInputHandler>();
                if (input != null) input.enabled = false;
                if (pc != null) pc.enabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // stop boss schedule + summon phase deterministically (no scene jump)
            m_Boss.Scheduler.Shutdown();
            foreach (var wp in m_Boss.WeakPoints)
                wp.SetExposed(false);

            var summons = GetComponentInChildren<AvatarBossSummonController>();
            if (summons != null)
                summons.ForceClearSummonsForTest(); // test-only hook, safe outside auto-run

            if (m_Overlay != null)
                m_Overlay.gameObject.SetActive(true);
        }

        /// <summary>Reloads the active showcase scene: boss HP, stagger, phase,
        /// weak points, summons, telegraphs, bot and the player all reset by Unity.</summary>
        public void RestartBossFight()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void BuildUI()
        {
            var root = new GameObject("ShowcaseGameOverUI");
            m_Overlay = root.AddComponent<Canvas>();
            m_Overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Overlay.sortingOrder = 100;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            m_Overlay.gameObject.SetActive(false);

            var dim = new GameObject("Dim");
            dim.transform.SetParent(root.transform, false);
            var dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.75f);
            var dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;

            var titleGo = new GameObject("GameOverTitle");
            titleGo.transform.SetParent(root.transform, false);
            m_TitleText = titleGo.AddComponent<Text>();
            m_TitleText.text = "BOSS FIGHT FAILED";
            m_TitleText.fontSize = 64;
            m_TitleText.alignment = TextAnchor.MiddleCenter;
            m_TitleText.color = new Color(1f, 0.4f, 0.35f, 1f);
            var rt = titleGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.72f);
            rt.anchorMax = new Vector2(0.5f, 0.72f);
            rt.sizeDelta = new Vector2(900, 90);

            var subGo = new GameObject("SubText");
            subGo.transform.SetParent(root.transform, false);
            m_SubTitleText = subGo.AddComponent<Text>();
            m_SubTitleText.text = "AVATAR OF NATURE PREVAILED";
            m_SubTitleText.fontSize = 24;
            m_SubTitleText.alignment = TextAnchor.MiddleCenter;
            m_SubTitleText.color = Color.white;
            var ws = subGo.GetComponent<RectTransform>();
            ws.anchorMin = new Vector2(0.5f, 0.62f);
            ws.anchorMax = new Vector2(0.5f, 0.62f);
            ws.sizeDelta = new Vector2(900, 40);

            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(root.transform, false);
            var image = btnGo.AddComponent<Image>();
            image.color = new Color(0.18f, 0.3f, 0.22f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(RestartBossFight);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.45f);
            btnRect.anchorMax = new Vector2(0.5f, 0.45f);
            btnRect.sizeDelta = new Vector2(460, 60);

            var labelGo = new GameObject("ButtonLabel");
            labelGo.transform.SetParent(btnGo.transform, false);
            var label = labelGo.AddComponent<Text>();
            label.text = "RESTART BOSS FIGHT";
            label.fontSize = 28;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
        }
    }
}
