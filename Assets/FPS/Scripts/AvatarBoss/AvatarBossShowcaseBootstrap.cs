using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss
{
    public static class AvatarBossShowcaseSession
    {
        public static bool HasSelection { get; private set; }
        public static AvatarBossDifficulty SelectedDifficulty { get; private set; } = AvatarBossDifficulty.Normal;

        public static void SetDifficulty(AvatarBossDifficulty difficulty)
        {
            SelectedDifficulty = difficulty;
            HasSelection = true;
        }
    }

    /// Loads the boss showcase only after the player has chosen a difficulty.
    public sealed class AvatarBossShowcaseBootstrap : MonoBehaviour
    {
        [SerializeField] string ShowcaseSceneName = "AvatarBossDuelArena";

        Button m_StartButton;
        Text m_SelectedLabel;
        Text m_StatusLabel;
        AvatarBossDifficulty m_Selected = AvatarBossDifficulty.Normal;
        Button m_EasyButton;
        Button m_NormalButton;
        Button m_HardButton;

        static readonly Color EasyColor = new Color(0.12f, 0.52f, 0.36f);
        static readonly Color NormalColor = new Color(0.16f, 0.38f, 0.72f);
        static readonly Color HardColor = new Color(0.68f, 0.18f, 0.15f);

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            BuildUi();
            SelectDifficulty(AvatarBossDifficulty.Normal);
        }

        void BuildUi()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasObject = new GameObject("BootstrapCanvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            var panel = new GameObject("ShowcaseDifficultyBootstrap");
            panel.transform.SetParent(canvas.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.012f, 0.022f, 0.038f, 0.985f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1060f, 610f);

            AddText(panel.transform, "AVATAR OF NATURE", 48, new Vector2(0f, 218f), new Vector2(960f, 62f), new Color(0.95f, 0.82f, 0.35f));
            AddText(panel.transform, "CHOOSE YOUR PACE", 25, new Vector2(0f, 170f), new Vector2(960f, 38f), Color.white);
            AddText(panel.transform, "Every fight is readable. Hard adds pressure, mix-ups and extra marked cells.", 20,
                new Vector2(0f, 135f), new Vector2(960f, 32f), new Color(0.62f, 0.72f, 0.82f));
            m_EasyButton = AddDifficultyButton(panel.transform, "EASY", AvatarBossDifficulty.Easy, new Vector2(-310f, 40f), EasyColor);
            m_NormalButton = AddDifficultyButton(panel.transform, "NORMAL", AvatarBossDifficulty.Normal, new Vector2(0f, 40f), NormalColor);
            m_HardButton = AddDifficultyButton(panel.transform, "HARD", AvatarBossDifficulty.Hard, new Vector2(310f, 40f), HardColor);
            AddText(panel.transform, "SLOWER TEMPO\nONE MARKED CELL", 17, new Vector2(-310f, -42f), new Vector2(260f, 48f), new Color(0.62f, 0.9f, 0.76f));
            AddText(panel.transform, "THE INTENDED DUEL\nTWO MARKED CELLS", 17, new Vector2(0f, -42f), new Vector2(260f, 48f), new Color(0.62f, 0.78f, 1f));
            AddText(panel.transform, "MIXED COMBOS\nTHREE MARKED CELLS", 17, new Vector2(310f, -42f), new Vector2(260f, 48f), new Color(1f, 0.62f, 0.5f));
            m_SelectedLabel = AddText(panel.transform, "SELECTED: NORMAL", 24, new Vector2(0f, -105f), new Vector2(960f, 36f), Color.white);
            AddText(panel.transform, "WASD MOVE   ·   MOUSE AIM   ·   SHOOT TO BUILD STAGGER", 16,
                new Vector2(0f, -220f), new Vector2(960f, 30f), new Color(0.42f, 0.52f, 0.62f));
            m_StatusLabel = AddText(panel.transform, "", 20, new Vector2(0f, -248f), new Vector2(960f, 32f), new Color(0.7f, 0.8f, 0.9f));
            m_StartButton = AddButton(panel.transform, "START BOSS FIGHT", new Vector2(0f, -158f), new Vector2(500f, 78f), new Color(0.88f, 0.58f, 0.12f));
            m_StartButton.onClick.AddListener(() => StartCoroutine(LoadShowcase()));
            RefreshSelectionVisuals();
        }

        void SelectDifficulty(AvatarBossDifficulty difficulty)
        {
            m_Selected = difficulty;
            if (m_SelectedLabel != null)
                m_SelectedLabel.text = "SELECTED: " + difficulty.ToString().ToUpperInvariant();
            RefreshSelectionVisuals();
        }

        Button AddDifficultyButton(Transform parent, string label, AvatarBossDifficulty difficulty, Vector2 position, Color color)
        {
            var button = AddButton(parent, label, position, new Vector2(240f, 78f), color);
            button.onClick.AddListener(() => SelectDifficulty(difficulty));
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

        Button AddButton(Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            AddText(go.transform, label, 25, Vector2.zero, size, Color.white);
            return button;
        }

        Text AddText(Transform parent, string label, int size, Vector2 position, Vector2 dimensions, Color color)
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

        IEnumerator LoadShowcase()
        {
            m_StartButton.interactable = false;
            m_StatusLabel.text = "LOADING SHOWCASE...";
            AvatarBossShowcaseSession.SetDifficulty(m_Selected);
            var operation = SceneManager.LoadSceneAsync(ShowcaseSceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                m_StatusLabel.text = "SHOWCASE SCENE NOT FOUND";
                m_StartButton.interactable = true;
                yield break;
            }

            operation.allowSceneActivation = false;
            while (operation.progress < 0.9f)
                yield return null;
            operation.allowSceneActivation = true;
        }
    }
}
