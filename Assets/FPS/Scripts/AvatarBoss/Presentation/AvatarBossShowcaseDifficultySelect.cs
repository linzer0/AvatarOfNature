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
        Button m_EasyButton;
        Button m_NormalButton;
        Button m_HardButton;

        static readonly Color EasyColor = new Color(0.12f, 0.52f, 0.36f);
        static readonly Color NormalColor = new Color(0.16f, 0.38f, 0.72f);
        static readonly Color HardColor = new Color(0.68f, 0.18f, 0.15f);

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
            panelImage.color = new Color(0.012f, 0.022f, 0.038f, 0.985f);
            var panelRect = m_Panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1060f, 610f);

            AddText("AVATAR OF NATURE", 48, new Vector2(0f, 218f), new Vector2(960f, 62f), new Color(0.95f, 0.82f, 0.35f, 1f));
            AddText("CHOOSE YOUR PACE", 25, new Vector2(0f, 170f), new Vector2(960f, 38f), Color.white);
            AddText("Every fight is readable. Hard adds pressure, mix-ups and extra marked cells.", 20,
                new Vector2(0f, 135f), new Vector2(960f, 32f), new Color(0.62f, 0.72f, 0.82f, 1f));
            m_EasyButton = AddDifficultyButton("EASY", AvatarBossDifficulty.Easy, new Vector2(-310f, 40f), EasyColor);
            m_NormalButton = AddDifficultyButton("NORMAL", AvatarBossDifficulty.Normal, new Vector2(0f, 40f), NormalColor);
            m_HardButton = AddDifficultyButton("HARD", AvatarBossDifficulty.Hard, new Vector2(310f, 40f), HardColor);
            AddText("SLOWER TEMPO\nONE MARKED CELL", 17, new Vector2(-310f, -42f), new Vector2(260f, 48f), new Color(0.62f, 0.9f, 0.76f, 1f));
            AddText("THE INTENDED DUEL\nTWO MARKED CELLS", 17, new Vector2(0f, -42f), new Vector2(260f, 48f), new Color(0.62f, 0.78f, 1f, 1f));
            AddText("MIXED COMBOS\nTHREE MARKED CELLS", 17, new Vector2(310f, -42f), new Vector2(260f, 48f), new Color(1f, 0.62f, 0.5f, 1f));
            AddButton("START BOSS FIGHT", new Vector2(0f, -158f), new Vector2(500f, 78f), new Color(0.88f, 0.58f, 0.12f, 1f))
                .onClick.AddListener(() => BeginFight(m_Selected));
            AddText("SELECTED: NORMAL", 24, new Vector2(0f, -105f), new Vector2(960f, 36f), Color.white).name = "SelectedDifficultyLabel";
            AddText("WASD MOVE   ·   MOUSE AIM   ·   SHOOT TO BUILD STAGGER", 16,
                new Vector2(0f, -220f), new Vector2(960f, 30f), new Color(0.42f, 0.52f, 0.62f, 1f));
            RefreshSelectionVisuals();
        }

        Button AddDifficultyButton(string label, AvatarBossDifficulty difficulty, Vector2 position, Color color)
        {
            var button = AddButton(label, position, new Vector2(240f, 78f), color);
            button.onClick.AddListener(() =>
            {
                m_Selected = difficulty;
                if (m_Difficulty != null)
                    m_Difficulty.ApplyDifficulty(difficulty);
                var selected = m_Panel.transform.Find("SelectedDifficultyLabel");
                if (selected != null)
                    selected.GetComponent<Text>().text = "SELECTED: " + label;
                RefreshSelectionVisuals();
            });
            return button;
        }

        void RefreshSelectionVisuals()
        {
            SetDifficultyButtonVisual(m_EasyButton, AvatarBossDifficulty.Easy, EasyColor);
            SetDifficultyButtonVisual(m_NormalButton, AvatarBossDifficulty.Normal, NormalColor);
            SetDifficultyButtonVisual(m_HardButton, AvatarBossDifficulty.Hard, HardColor);
        }

        void SetDifficultyButtonVisual(Button button, AvatarBossDifficulty difficulty, Color baseColor)
        {
            if (button == null || button.targetGraphic == null)
                return;
            bool selected = m_Selected == difficulty;
            button.targetGraphic.color = selected
                ? Color.Lerp(baseColor, Color.white, 0.18f)
                : Color.Lerp(baseColor, Color.black, 0.18f);
            var colors = button.colors;
            colors.normalColor = button.targetGraphic.color;
            colors.highlightedColor = Color.Lerp(button.targetGraphic.color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(button.targetGraphic.color, Color.black, 0.14f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
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
