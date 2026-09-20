using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss.Editor
{
    /// Creates the editable Game Over presentation used by the boss arena.
    /// The prefab is generated once so the visual can be tuned directly in Unity.
    [InitializeOnLoad]
    public static class AvatarBossGameOverPrefabBuilder
    {
        const string AssetPath = "Assets/AvatarOfNature/Runtime/AvatarBoss/Presentation/Resources/AvatarBossGameOver.prefab";

        static AvatarBossGameOverPrefabBuilder()
        {
            EditorApplication.delayCall += EnsurePrefab;
        }

        [MenuItem("Avatar of Nature/Create Game Over Prefab")]
        public static void CreatePrefabFromMenu()
        {
            CreatePrefab(true);
        }

        static void EnsurePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath) == null)
                CreatePrefab(false);
        }

        static void CreatePrefab(bool force)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath) != null)
                return;

            var directory = Path.GetDirectoryName(AssetPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath);
            if (existing != null)
                AssetDatabase.DeleteAsset(AssetPath);

            var root = new GameObject("AvatarBossGameOver",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = 5;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = AddImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.86f));
            Stretch(dim.rectTransform);

            var card = AddImage(root.transform, "GameOverCard", new Color(0.025f, 0.045f, 0.065f, 0.98f));
            Center(card.rectTransform, new Vector2(700f, 360f), Vector2.zero);

            var accent = AddImage(card.transform, "CardAccent", new Color(0.85f, 0.2f, 0.18f, 1f));
            TopStretch(accent.rectTransform, 6f);

            AddText(card.transform, "DeathQuote", "", 20,
                new Color(0.9f, 0.88f, 0.78f, 1f), new Vector2(0.5f, 0.5f), new Vector2(620f, 110f), new Vector2(0f, 75f), FontStyle.Italic);
            AddText(card.transform, "DeathQuoteAuthor", "", 17,
                new Color(0.65f, 0.74f, 0.78f, 1f), new Vector2(0.5f, 0.5f), new Vector2(620f, 28f), new Vector2(0f, 10f), FontStyle.Normal);

            var button = AddImage(card.transform, "RestartButton", new Color(0.18f, 0.3f, 0.22f, 1f));
            Center(button.rectTransform, new Vector2(460f, 66f), new Vector2(0f, -105f));
            var buttonComponent = button.gameObject.AddComponent<Button>();
            buttonComponent.targetGraphic = button;
            AddText(button.transform, "ButtonLabel", "PLEASE WAIT...", 25,
                Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, FontStyle.Normal, stretch: true);
            PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static Image AddImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Text AddText(Transform parent, string name, string value, int fontSize, Color color,
            Vector2 anchor, Vector2 size, Vector2 position, FontStyle style, bool stretch = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
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
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = anchor;
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
            }
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void TopStretch(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
        }

        static void Center(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
