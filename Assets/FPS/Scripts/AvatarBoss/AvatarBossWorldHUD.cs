using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss
{
    /// World-space boss HUD in front of the arena: health bar, stagger bar,
    /// phase / state text and weak point status. Built from primitives, no
    /// ScriptableObjects, no reflection. Visible in Play Mode, HUD-aware.
    public class AvatarBossWorldHUD : MonoBehaviour
    {
        [Header("Layout")]
        public Vector3 Offset = new Vector3(0f, 10.5f, 0f);
        public Vector2 PanelSize = new Vector2(6f, 1.6f);

        AvatarBossController m_Boss;
        Canvas m_Canvas;
        RectTransform m_Panel;
        Image m_HpFill;
        Image m_StaggerFill;
        Text m_StatusText;
        Text m_WeakPointText;
        Transform m_Camera;

        void Start()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            if (m_Boss == null)
            {
                enabled = false;
                return;
            }
            BuildUI();
            m_Camera = UnityEngine.Camera.main.transform;
        }

        void BuildUI()
        {
            var root = new GameObject("BossWorldHUD");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Offset;

            m_Canvas = root.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.WorldSpace;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            root.AddComponent<GraphicRaycaster>();

            m_Panel = root.GetComponent<RectTransform>();
            m_Panel.sizeDelta = PanelSize;

            // health bar: dark frame + bright red fill
            m_HpFill = AddBar(root.transform, "HpBar", 1.15f, new Color(0.85f, 0.2f, 0.15f, 1f));
            // stagger bar below
            m_StaggerFill = AddBar(root.transform, "StaggerBar", 0.45f, new Color(0.95f, 0.7f, 0.1f, 1f));

            var statusGo = new GameObject("StateText");
            statusGo.transform.SetParent(root.transform, false);
            m_StatusText = statusGo.AddComponent<Text>();
            m_StatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_StatusText.fontSize = 28;
            m_StatusText.alignment = TextAnchor.MiddleCenter;
            m_StatusText.color = Color.white;
            var st = statusGo.GetComponent<RectTransform>();
            st.anchorMin = new Vector2(0.5f, 1f);
            st.anchorMax = new Vector2(0.5f, 1f);
            st.sizeDelta = new Vector2(6f, 0.5f);
            st.anchoredPosition = new Vector2(0f, 0.05f);

            var weakGo = new GameObject("WeakPointText");
            weakGo.transform.SetParent(root.transform, false);
            m_WeakPointText = weakGo.AddComponent<Text>();
            m_WeakPointText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_WeakPointText.fontSize = 20;
            m_WeakPointText.alignment = TextAnchor.MiddleCenter;
            m_WeakPointText.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            var wk = weakGo.GetComponent<RectTransform>();
            wk.anchorMin = new Vector2(0.5f, 0f);
            wk.anchorMax = new Vector2(0.5f, 0f);
            wk.sizeDelta = new Vector2(6f, 0.35f);
            wk.anchoredPosition = new Vector2(0f, -0.1f);
        }

        Image AddBar(Transform parent, string barName, float yOffset, Color fillColor)
        {
            var bg = new GameObject(barName + "BG");
            bg.transform.SetParent(parent, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.55f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(PanelSize.x - 0.4f, 0.36f);
            bgRect.anchoredPosition = new Vector2(0f, yOffset);

            var fillGo = new GameObject(barName + "Fill");
            fillGo.transform.SetParent(bg.transform, false);
            var fillImage = fillGo.AddComponent<Image>();
            fillImage.color = fillColor;
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 2f);
            fillRect.offsetMax = new Vector2(-4f, -2f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            return fillImage;
        }

        void Update()
        {
            if (m_Boss == null || m_Boss.BossHealth == null)
                return;

            // billboard toward the game camera
            if (m_Camera != null)
            {
                Vector3 look = m_Camera.position;
                look.y = m_Panel.position.y;
                m_Panel.transform.LookAt(look);
            }

            if (m_HpFill != null)
                m_HpFill.fillAmount = Mathf.Clamp01(m_Boss.BossHealth.GetRatio());
            if (m_StaggerFill != null && m_Boss.Stagger != null)
                m_StaggerFill.fillAmount = Mathf.Clamp01(m_Boss.Stagger.Ratio);

            if (m_StatusText != null)
                m_StatusText.text = DescribeState();
            if (m_WeakPointText != null)
                m_WeakPointText.text = DescribeWeakPoints();
        }

        string DescribeState()
        {
            if (m_Boss.IsDead)
                return "DEFEATED";

            var sched = m_Boss.Scheduler;
            string phaseText = m_Boss.PhaseTwo ? "PHASE 2" : "PHASE 1";

            string stateWord = "IDLE";
            if (m_Boss.SummonsActive)
                stateWord = "SUMMON";
            else if (m_Boss.Stagger != null && m_Boss.Stagger.IsFull)
                stateWord = "STAGGER";
            else if (sched != null)
            {
                switch (sched.State)
                {
                    case AvatarBossSchedulerState.Telegraph:
                        stateWord = "TELEGRAPH: " + ElementName(sched);
                        break;
                    case AvatarBossSchedulerState.Windup:
                        stateWord = "WINDUP: " + ElementName(sched);
                        break;
                    case AvatarBossSchedulerState.Execute:
                    case AvatarBossSchedulerState.Recover:
                        stateWord = "ATTACK: " + ElementName(sched);
                        break;
                }
            }
            bool vuln = AnyWeakPointExposed();
            if (stateWord == "STAGGER" || vuln)
                stateWord = "VULNERABLE! " + stateWord;

            return phaseText + " - " + stateWord;
        }

        string ElementName(AvatarBossAttackScheduler sched)
        {
            return sched.CurrentAttack != null ? sched.CurrentAttack.Element.ToString() : "unknown";
        }

        bool AnyWeakPointExposed()
        {
            bool any = false;
            foreach (var wp in m_Boss.WeakPoints)
                if (wp != null && wp.IsExposed)
                    any = true;
            return any;
        }

        string DescribeWeakPoints()
        {
            int open = 0; int total = 0;
            foreach (var wp in m_Boss.WeakPoints)
                if (wp != null)
                {
                    total++;
                    if (wp.IsExposed)
                        open++;
                }
            return "Weak points open: " + open + "/" + total +
                   (open > 0 ? "  -> SHOOT THE GLOWING SPHERE" : "");
        }
    }
}
