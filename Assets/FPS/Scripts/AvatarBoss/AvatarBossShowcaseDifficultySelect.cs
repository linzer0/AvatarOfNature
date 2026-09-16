using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

namespace Unity.FPS.AvatarBoss
{
    /// Showcase-only preflight screen. Reuses the existing boss HUD Canvas and
    /// gates the scheduler until the player explicitly starts the fight.
    public sealed class AvatarBossShowcaseDifficultySelect : MonoBehaviour
    {
        public bool FightStarted { get; private set; }
        public event Action FightStartedEvent;

        AvatarBossDifficultyController m_Difficulty;
        Canvas m_Canvas;
        GameObject m_Panel;
        AvatarBossDifficulty m_Selected = AvatarBossDifficulty.Normal;

        void Awake()
        {
            m_Difficulty = GetComponent<AvatarBossDifficultyController>();
            if (AvatarBossShowcaseSession.HasSelection)
                BeginFight(AvatarBossShowcaseSession.SelectedDifficulty);
        }

        void Start()
        {
            StartCoroutine(BuildWhenHudIsReady());
        }

        void LateUpdate()
        {
            if (!FightStarted && m_Panel != null && m_Panel.activeSelf)
            {
                // InGameMenuManager can relock the cursor on a gameplay click.
                // Keep pointer input available for this preflight screen only.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        System.Collections.IEnumerator BuildWhenHudIsReady()
        {
            for (int i = 0; i < 30 && m_Canvas == null; i++)
            {
                var hud = GetComponentInChildren<AvatarBossPresentationHUD>(true);
                if (hud != null)
                    m_Canvas = hud.GetComponentInChildren<Canvas>(true);
                if (m_Canvas == null)
                    yield return null;
            }

            if (m_Canvas != null && !FightStarted)
            {
                BuildPanel();
                PrepareUiInput();
            }
        }

        public void BeginFight(AvatarBossDifficulty difficulty, bool testOnly = false)
        {
            if (FightStarted)
                return;
            m_Selected = difficulty;
            if (m_Difficulty != null)
                m_Difficulty.ApplyDifficulty(difficulty);
            FightStarted = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (m_Panel != null)
                m_Panel.SetActive(false);
            FightStartedEvent?.Invoke();
            if (testOnly)
                Debug.Log("[AvatarOfNature] Difficulty select bypassed by explicit test-only Normal hook.", this);
        }

        void PrepareUiInput()
        {
            // PlayerInputHandler locks the cursor during its Start. The selector
            // runs after that and must explicitly own pointer input until Start.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (EventSystem.current != null)
            {
                var normal = m_Panel.transform.Find("NORMALButton");
                if (normal != null)
                    EventSystem.current.SetSelectedGameObject(normal.gameObject);
            }
        }

        void BuildPanel()
        {
            m_Panel = new GameObject("ShowcaseDifficultySelect");
            m_Panel.transform.SetParent(m_Canvas.transform, false);
            var panelImage = m_Panel.AddComponent<Image>();
            panelImage.color = new Color(0.015f, 0.025f, 0.04f, 0.96f);
            var panelRect = m_Panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 520f);

            AddText("SHOWCASE DIFFICULTY", 42, new Vector2(0f, 170f), new Vector2(760f, 58f), Color.white);
            AddText("Choose your pace before the Avatar awakens", 22, new Vector2(0f, 125f), new Vector2(760f, 36f), new Color(0.65f, 0.75f, 0.85f));
            AddDifficultyButton("EASY", AvatarBossDifficulty.Easy, new Vector2(-210f, 35f), new Color(0.18f, 0.55f, 0.38f));
            AddDifficultyButton("NORMAL", AvatarBossDifficulty.Normal, new Vector2(0f, 35f), new Color(0.22f, 0.42f, 0.75f));
            AddDifficultyButton("HARD", AvatarBossDifficulty.Hard, new Vector2(210f, 35f), new Color(0.7f, 0.22f, 0.18f));
            AddButton("START BOSS FIGHT", new Vector2(0f, -145f), new Vector2(440f, 72f), new Color(0.82f, 0.58f, 0.16f))
                .onClick.AddListener(() => BeginFight(m_Selected));
            AddText("SELECTED: NORMAL", 24, new Vector2(0f, -55f), new Vector2(760f, 36f), Color.white).name = "SelectedDifficultyLabel";
        }

        void AddDifficultyButton(string label, AvatarBossDifficulty difficulty, Vector2 position, Color color)
        {
            var button = AddButton(label, position, new Vector2(180f, 66f), color);
            button.onClick.AddListener(() =>
            {
                m_Selected = difficulty;
                if (m_Difficulty != null)
                    m_Difficulty.ApplyDifficulty(difficulty);
                var selected = m_Panel.transform.Find("SelectedDifficultyLabel");
                if (selected != null)
                    selected.GetComponent<Text>().text = "SELECTED: " + label;
            });
        }

        Button AddButton(string label, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(m_Panel.transform, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            AddTextTo(go.transform, label, 25, Vector2.zero, size, Color.white);
            return button;
        }

        Text AddText(string label, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            return AddTextTo(m_Panel.transform, label, size, position, dimensions, color);
        }

        Text AddTextTo(Transform parent, string label, int size, Vector2 position, Vector2 dimensions, Color color)
        {
            var go = new GameObject(label + "Label");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = label;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            return text;
        }
    }
}
