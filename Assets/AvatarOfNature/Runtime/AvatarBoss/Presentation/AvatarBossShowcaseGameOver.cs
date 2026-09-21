using Unity.FPS.AvatarBoss;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Scene-aware Game Over flow for the boss arena scenes.
    /// Owns the player-death state for the boss arena, freezes the gameplay
    /// state and shows the Avatar of Nature Game Over overlay. The shared
    /// Microgame GameFlowManager is inert in these scenes.
    /// MainScene keeps its normal GameFlowManager behaviour untouched.
    /// </summary>
    public class AvatarBossShowcaseGameOver : MonoBehaviour
    {
        Canvas m_Overlay;
        Text m_TitleText;
        Text m_SubTitleText;
        Text m_QuoteText;
        Text m_QuoteAuthorText;
        Text m_CountdownText;
        Text m_RestartLabel;
        Button m_ContinueButton;
        Text m_ContinueLabel;
        Button m_RestartButton;
        GameObject m_Player;
        Health m_PlayerHealth;
        AvatarBossController m_Boss;
        AvatarBossSummonController m_Summons;
        bool m_Shown;
        bool m_Victory;
        bool m_PauseInfoHidden;
        bool m_Subscribed;
        Coroutine m_VictoryRoutine;
        float m_RestartAvailableAt;
        const float RestartDelay = 0f;
        const float VictoryPresentationDelay = 1.5f;

        static readonly string[] DeathQuotes =
        {
            "«Ничто в жизни так не заводит, как то, что в тебя стреляют и не попадают».",
            "«Патриотизм — это вечная верность родине и верность правительству, когда оно того заслуживает».",
            "«Война мила лишь тем, кто её не ведал».",
            "«Совершая месть, человек становится вровень со своим врагом, а прощая врага — превосходит его».",
            "«Многие погибают, пытаясь погубить других».",
            "«Старики объявляют войну. Но воевать и умирать должны молодые»."
        };

        static readonly string[] DeathQuoteAuthors =
        {
            "Уинстон Черчилль",
            "Марк Твен",
            "Эразм Роттердамский",
            "Фрэнсис Бэкон",
            "Томас Мор",
            "Герберт Гувер"
        };

        void Awake()
        {
            // Only meaningful inside the boss arena scenes. The dedicated duel
            // arena replaced the old showcase scene, but keeps this shared flow.
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "AvatarBossShowcase" && sceneName != "AvatarBossDuelArena")
            {
                enabled = false;
                return;
            }
        }

        void Start()
        {
            var player = GameObject.Find("Player");
            m_Boss = GetComponentInParent<AvatarBossController>();
            if (m_Boss == null)
                m_Boss = FindFirstObjectByType<AvatarBossController>();
            if (player == null || m_Boss == null)
            {
                enabled = false;
                return;
            }

            m_PlayerHealth = player.GetComponent<Health>();
            m_PlayerHealth.OnDie += OnPlayerDie;
            if (m_Boss.BossHealth != null)
                m_Boss.BossHealth.OnDie += OnBossDie;
            m_Subscribed = true;

            // The legacy FPS pause/options card is not part of the Boss Duel.
            var behaviours = FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pauseMenu in behaviours)
            {
                if (pauseMenu == null || pauseMenu.GetType().Name != "InGameMenuManager")
                    continue;
                pauseMenu.SendMessage("ClosePauseMenu", SendMessageOptions.DontRequireReceiver);
                pauseMenu.enabled = false;
            }

            var pauseInfo = GameObject.Find("GameManager/GameHUD/HUD/PauseMenuInfo");
            if (pauseInfo != null)
                pauseInfo.SetActive(false);
        }

        void OnDestroy()
        {
            if (m_Subscribed && m_PlayerHealth != null)
                m_PlayerHealth.OnDie -= OnPlayerDie;
            if (m_Subscribed && m_Boss != null && m_Boss.BossHealth != null)
                m_Boss.BossHealth.OnDie -= OnBossDie;
        }

        void OnPlayerDie()
        {
            m_Boss?.GetComponent<AvatarBossDuelController>()?.LogFightSummary("player-dead");
            // direct safety net: if death slipped through before the interception
            if (!m_Shown)
                ShowOverlay();
        }

        void OnBossDie()
        {
            m_Boss?.GetComponent<AvatarBossDuelController>()?.LogFightSummary("victory");
            ScheduleVictoryOverlay();
        }

        void Update()
        {
            // Keep a polling fallback because the boss controller may process
            // Health.OnDie before this scene-owned presentation subscribes.
            if (!m_Shown && m_Boss != null && m_Boss.IsDead)
                ScheduleVictoryOverlay();

            if (!m_Shown || m_RestartButton == null)
                return;

            if (m_RestartButton.interactable)
                return;

            float remaining = Mathf.Max(0f, m_RestartAvailableAt - Time.unscaledTime);
            if (remaining > 0f)
            {
                if (m_CountdownText != null)
                    m_CountdownText.text = string.Empty;
                return;
            }

            m_RestartButton.interactable = true;
            if (m_RestartLabel != null)
                m_RestartLabel.text = m_Victory ? GetContinueLabel() : "RESTART BOSS FIGHT";
            if (m_CountdownText != null)
                m_CountdownText.text = string.Empty;
        }

        void LateUpdate()
        {
            HideLegacyPauseInfo();
            if (m_Shown)
                KeepDeathScreenInputAvailable();
        }

        void HideLegacyPauseInfo()
        {
            if (m_PauseInfoHidden)
                return;
            var pauseInfo = GameObject.Find("PauseMenuInfo");
            if (pauseInfo == null)
                return;
            pauseInfo.SetActive(false);
            m_PauseInfoHidden = true;
        }

        void ScheduleVictoryOverlay()
        {
            if (m_Shown || m_VictoryRoutine != null)
                return;
            m_VictoryRoutine = StartCoroutine(ShowVictoryAfterPresentation());
        }

        System.Collections.IEnumerator ShowVictoryAfterPresentation()
        {
            // Health.OnDie is raised before the final boss-death presentation
            // has settled. Let the player see that moment before freezing the arena.
            yield return new WaitForSecondsRealtime(VictoryPresentationDelay);
            m_VictoryRoutine = null;
            if (!m_Shown && m_Boss != null && m_Boss.IsDead)
                ShowOverlay(true);
        }

        void ShowOverlay(bool victory = false)
        {
            if (m_Shown)
                return;
            m_Shown = true;
            m_Victory = victory;

            if (m_Overlay == null)
                BuildUI();

            if (m_Victory)
            {
                if (m_TitleText != null) m_TitleText.text = "ПОБЕДА!";
                if (m_SubTitleText != null) m_SubTitleText.text = "Ты одолел Аватара Природы.";
                if (m_QuoteText != null) m_QuoteText.text = "Сильная победа. Готов поднять планку?";
                if (m_QuoteAuthorText != null) m_QuoteAuthorText.text = string.Empty;
                if (m_RestartLabel != null) m_RestartLabel.text = GetContinueLabel();
                if (m_RestartButton != null)
                {
                    m_RestartButton.onClick.RemoveAllListeners();
                    m_RestartButton.onClick.AddListener(ContinueOnHarderDifficulty);
                }
                if (m_ContinueButton != null)
                {
                    m_ContinueButton.gameObject.SetActive(true);
                    m_ContinueButton.onClick.AddListener(ReturnToDifficultySelect);
                }
                if (m_ContinueLabel != null)
                    m_ContinueLabel.text = "BACK TO DIFFICULTY SELECT";
            }
            else
            {
                int quoteIndex = Random.Range(0, DeathQuotes.Length);
                if (m_QuoteText != null)
                    m_QuoteText.text = DeathQuotes[quoteIndex];
                if (m_QuoteAuthorText != null)
                    m_QuoteAuthorText.text = "— " + DeathQuoteAuthors[quoteIndex];
                if (m_ContinueButton != null)
                    m_ContinueButton.gameObject.SetActive(false);
                if (m_RestartButton != null)
                {
                    m_RestartButton.onClick.RemoveAllListeners();
                    m_RestartButton.onClick.AddListener(RestartBossFight);
                }
            }

            // Freeze the entire arena. The Game Over screen is a static death
            // moment, so camera/VFX/HUD updates must not continue behind it.
            Time.timeScale = 0f;

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

            HideArenaHud();

            if (m_Overlay != null)
                m_Overlay.gameObject.SetActive(true);

            KeepDeathScreenInputAvailable();
        }

        string GetContinueLabel()
        {
            var difficulty = FindFirstObjectByType<AvatarBossDifficultyController>();
            if (difficulty == null || difficulty.CurrentDifficulty != AvatarBossDifficulty.Hard)
                return "CONTINUE ON HARDER DIFFICULTY";
            return "REPLAY HARD MODE";
        }

        public void ContinueOnHarderDifficulty()
        {
            var difficulty = FindFirstObjectByType<AvatarBossDifficultyController>();
            var current = difficulty != null ? difficulty.CurrentDifficulty : AvatarBossDifficulty.Normal;
            var next = current == AvatarBossDifficulty.Easy
                ? AvatarBossDifficulty.Normal
                : AvatarBossDifficulty.Hard;
            AvatarBossShowcaseSession.SetDifficulty(next);
            RestoreArenaHud();
            Time.timeScale = 1f;
            SceneManager.LoadScene("AvatarBossDuelArena");
        }

        public void ReturnToDifficultySelect()
        {
            RestoreArenaHud();
            Time.timeScale = 1f;
            SceneManager.LoadScene("AvatarBossShowcaseBootstrap");
        }

        void KeepDeathScreenInputAvailable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.enabled = true;
                var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (inputModule != null)
                    inputModule.enabled = true;
            }

            if (m_Overlay != null)
            {
                var raycaster = m_Overlay.GetComponent<GraphicRaycaster>();
                if (raycaster != null)
                    raycaster.enabled = true;
            }
        }

        void HideArenaHud()
        {
            // The boss presentation canvas is generated under the boss and would
            // otherwise remain visible behind the death card.
            var bossHud = FindFirstObjectByType<AvatarBossPresentationHUD>();
            var bossCanvas = bossHud != null ? bossHud.transform.Find("BossPresentationHUD") : null;
            if (bossCanvas != null)
                bossCanvas.gameObject.SetActive(false);

            // Remove the legacy player HUD from the death frame as well. Keep the
            // GameManager alive so pause/options and scene reload remain intact.
            var playerHud = GameObject.Find("GameManager/GameHUD/HUD");
            if (playerHud != null)
                playerHud.SetActive(false);
        }

        /// <summary>Reloads the active showcase scene: boss HP, stagger, phase,
        /// weak points, summons, telegraphs, bot and the player all reset by Unity.</summary>
        public void RestartBossFight()
        {
            if (m_RestartButton != null && !m_RestartButton.interactable)
                return;
            RestoreArenaHud();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void RestoreArenaHud()
        {
            // GameObject.Find does not return inactive objects. Resolve the HUD
            // through its still-active parent so a restarted fight gets it back.
            var gameHud = GameObject.Find("GameManager/GameHUD");
            var playerHud = gameHud != null ? gameHud.transform.Find("HUD") : null;
            if (playerHud != null)
                playerHud.gameObject.SetActive(true);

            var bossHud = FindFirstObjectByType<AvatarBossPresentationHUD>(FindObjectsInactive.Include);
            var bossCanvas = bossHud != null ? bossHud.transform.Find("BossPresentationHUD") : null;
            if (bossCanvas != null)
                bossCanvas.gameObject.SetActive(true);
        }

        void BuildUI()
        {
            if (TryBuildPrefabUI())
                return;

            var root = new GameObject("ShowcaseGameOverUI");
            m_Overlay = root.AddComponent<Canvas>();
            m_Overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Overlay.sortingOrder = 100;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            m_Overlay.gameObject.SetActive(false);

            var dim = new GameObject("Dim");
            dim.transform.SetParent(root.transform, false);
            var dimImage = dim.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.86f);
            var dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;

            var cardGo = new GameObject("GameOverCard");
            cardGo.transform.SetParent(root.transform, false);
            var cardImage = cardGo.AddComponent<Image>();
            cardImage.color = new Color(0.025f, 0.045f, 0.065f, 0.98f);
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(700f, 360f);

            var accentGo = new GameObject("CardAccent");
            accentGo.transform.SetParent(cardGo.transform, false);
            var accentImage = accentGo.AddComponent<Image>();
            accentImage.color = new Color(0.85f, 0.2f, 0.18f, 1f);
            var accentRect = accentGo.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 6f);
            accentRect.anchoredPosition = Vector2.zero;

            var quoteGo = new GameObject("DeathQuote");
            quoteGo.transform.SetParent(cardGo.transform, false);
            m_QuoteText = quoteGo.AddComponent<Text>();
            m_QuoteText.fontSize = 20;
            m_QuoteText.fontStyle = FontStyle.Italic;
            m_QuoteText.alignment = TextAnchor.MiddleCenter;
            m_QuoteText.horizontalOverflow = HorizontalWrapMode.Wrap;
            m_QuoteText.verticalOverflow = VerticalWrapMode.Overflow;
            m_QuoteText.color = new Color(0.9f, 0.88f, 0.78f, 1f);
            var quoteRect = quoteGo.GetComponent<RectTransform>();
            quoteRect.anchorMin = quoteRect.anchorMax = new Vector2(0.5f, 0.5f);
            quoteRect.sizeDelta = new Vector2(620f, 110f);
            quoteRect.anchoredPosition = new Vector2(0f, 75f);

            var authorGo = new GameObject("DeathQuoteAuthor");
            authorGo.transform.SetParent(cardGo.transform, false);
            m_QuoteAuthorText = authorGo.AddComponent<Text>();
            m_QuoteAuthorText.fontSize = 17;
            m_QuoteAuthorText.alignment = TextAnchor.MiddleCenter;
            m_QuoteAuthorText.color = new Color(0.65f, 0.74f, 0.78f, 1f);
            var authorRect = authorGo.GetComponent<RectTransform>();
            authorRect.anchorMin = authorRect.anchorMax = new Vector2(0.5f, 0.5f);
            authorRect.sizeDelta = new Vector2(620f, 28f);
            authorRect.anchoredPosition = new Vector2(0f, 10f);

            var btnGo = new GameObject("RestartButton");
            btnGo.transform.SetParent(cardGo.transform, false);
            var image = btnGo.AddComponent<Image>();
            image.color = new Color(0.18f, 0.3f, 0.22f, 1f);
            m_RestartButton = btnGo.AddComponent<Button>();
            m_RestartButton.targetGraphic = image;
            m_RestartButton.onClick.AddListener(RestartBossFight);
            m_RestartButton.interactable = true;
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(460, 66);
            btnRect.anchoredPosition = new Vector2(0f, -105f);

            var labelGo = new GameObject("ButtonLabel");
            labelGo.transform.SetParent(btnGo.transform, false);
            m_RestartLabel = labelGo.AddComponent<Text>();
            m_RestartLabel.text = "RESTART BOSS FIGHT";
            m_RestartLabel.fontSize = 25;
            m_RestartLabel.alignment = TextAnchor.MiddleCenter;
            m_RestartLabel.color = Color.white;
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;

            m_RestartAvailableAt = Time.unscaledTime + RestartDelay;
        }

        bool TryBuildPrefabUI()
        {
            var prefab = Resources.Load<GameObject>("AvatarBossGameOver");
            if (prefab == null)
                return false;

            var root = Instantiate(prefab);
            root.name = "ShowcaseGameOverUI";
            m_Overlay = root.GetComponent<Canvas>();
            m_TitleText = root.transform.Find("GameOverCard/GameOverTitle")?.GetComponent<Text>();
            m_SubTitleText = root.transform.Find("GameOverCard/SubText")?.GetComponent<Text>();
            m_QuoteText = root.transform.Find("GameOverCard/DeathQuote")?.GetComponent<Text>();
            m_QuoteAuthorText = root.transform.Find("GameOverCard/DeathQuoteAuthor")?.GetComponent<Text>();
            m_CountdownText = root.transform.Find("GameOverCard/RestartCountdown")?.GetComponent<Text>();
            m_RestartButton = root.transform.Find("GameOverCard/RestartButton")?.GetComponent<Button>();
            m_RestartLabel = root.transform.Find("GameOverCard/RestartButton/ButtonLabel")?.GetComponent<Text>();
            var card = root.transform.Find("GameOverCard");
            if (m_TitleText == null && card != null)
                m_TitleText = AddRuntimeText(card, "GameOverTitle", "", 42, new Color(0.95f, 0.82f, 0.35f, 1f), new Vector2(0f, 125f), new Vector2(650f, 54f), FontStyle.Bold);
            if (m_SubTitleText == null && card != null)
                m_SubTitleText = AddRuntimeText(card, "SubText", "", 23, Color.white, new Vector2(0f, 88f), new Vector2(650f, 34f), FontStyle.Normal);
            if (m_QuoteText != null)
                m_QuoteText.rectTransform.anchoredPosition = new Vector2(0f, 32f);
            if (m_QuoteAuthorText != null)
                m_QuoteAuthorText.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            if (m_ContinueButton == null && card != null)
            {
                var continueGo = new GameObject("ContinueButton");
                continueGo.transform.SetParent(card, false);
                var image = continueGo.AddComponent<Image>();
                image.color = new Color(0.12f, 0.28f, 0.42f, 1f);
                m_ContinueButton = continueGo.AddComponent<Button>();
                m_ContinueButton.targetGraphic = image;
                var rect = continueGo.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(460f, 54f);
                rect.anchoredPosition = new Vector2(0f, -155f);
                m_ContinueLabel = AddRuntimeText(continueGo.transform, "ButtonLabel", "BACK TO DIFFICULTY SELECT", 19, Color.white, Vector2.zero, Vector2.zero, FontStyle.Normal, true);
            }

            if (m_Overlay == null || m_RestartButton == null || m_RestartLabel == null)
            {
                Destroy(root);
                return false;
            }

            m_RestartButton.onClick.AddListener(RestartBossFight);
            var buttonImage = m_RestartButton.GetComponent<Image>();
            if (buttonImage != null)
                m_RestartButton.targetGraphic = buttonImage;
            m_RestartButton.interactable = true;
            m_RestartLabel.text = "RESTART BOSS FIGHT";
            m_Overlay.gameObject.SetActive(false);
            m_RestartAvailableAt = Time.unscaledTime + RestartDelay;
            return true;
        }

        Text AddRuntimeText(Transform parent, string name, string value, int fontSize, Color color,
            Vector2 position, Vector2 size, FontStyle style, bool stretch = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            var rect = go.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
            return text;
        }
    }
}
