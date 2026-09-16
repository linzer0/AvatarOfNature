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
        [SerializeField] string ShowcaseSceneName = "AvatarBossShowcase";

        Button m_StartButton;
        Text m_SelectedLabel;
        Text m_StatusLabel;
        AvatarBossDifficulty m_Selected = AvatarBossDifficulty.Normal;

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
            image.color = new Color(0.015f, 0.025f, 0.04f, 0.98f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 520f);

            AddText(panel.transform, "AVATAR OF NATURE", 44, new Vector2(0f, 175f), new Vector2(760f, 58f), new Color(1f, 0.78f, 0.2f));
            AddText(panel.transform, "Choose your pace before the Avatar awakens", 22, new Vector2(0f, 125f), new Vector2(760f, 36f), new Color(0.65f, 0.75f, 0.85f));
            AddDifficultyButton(panel.transform, "EASY", AvatarBossDifficulty.Easy, new Vector2(-210f, 35f), new Color(0.18f, 0.55f, 0.38f));
            AddDifficultyButton(panel.transform, "NORMAL", AvatarBossDifficulty.Normal, new Vector2(0f, 35f), new Color(0.22f, 0.42f, 0.75f));
            AddDifficultyButton(panel.transform, "HARD", AvatarBossDifficulty.Hard, new Vector2(210f, 35f), new Color(0.7f, 0.22f, 0.18f));
            m_SelectedLabel = AddText(panel.transform, "SELECTED: NORMAL", 24, new Vector2(0f, -55f), new Vector2(760f, 36f), Color.white);
            m_StatusLabel = AddText(panel.transform, "", 20, new Vector2(0f, -195f), new Vector2(760f, 32f), new Color(0.7f, 0.8f, 0.9f));
            m_StartButton = AddButton(panel.transform, "START BOSS FIGHT", new Vector2(0f, -145f), new Vector2(440f, 72f), new Color(0.82f, 0.58f, 0.16f));
            m_StartButton.onClick.AddListener(() => StartCoroutine(LoadShowcase()));
        }

        void SelectDifficulty(AvatarBossDifficulty difficulty)
        {
            m_Selected = difficulty;
            if (m_SelectedLabel != null)
                m_SelectedLabel.text = "SELECTED: " + difficulty.ToString().ToUpperInvariant();
        }

        void AddDifficultyButton(Transform parent, string label, AvatarBossDifficulty difficulty, Vector2 position, Color color)
        {
            var button = AddButton(parent, label, position, new Vector2(180f, 66f), color);
            button.onClick.AddListener(() => SelectDifficulty(difficulty));
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
