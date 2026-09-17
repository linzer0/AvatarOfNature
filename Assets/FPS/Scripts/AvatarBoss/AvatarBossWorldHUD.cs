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
        Text m_NotificationText;
        Transform m_Camera;
        string m_NotificationTextName = "AVATAR OF NATURE";
        AvatarBossSummonController m_Summons;
        const float k_TextRefreshInterval = 0.1f;
        float m_NextTextRefresh;
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
            m_Summons = m_Boss.GetComponentInChildren<AvatarBossSummonController>();
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
            // dark backing plate for readability against any arena background
            var bgGo = new GameObject("PanelBackground");
            bgGo.transform.SetParent(root.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.5f);
            bgGo.GetComponent<RectTransform>().sizeDelta = PanelSize;
            bgGo.transform.SetAsFirstSibling();
            var nameGo = new GameObject("BossName");
            nameGo.transform.SetParent(root.transform, false);
            m_NameText = nameGo.AddComponent<Text>();
            m_NameText.text = "AVATAR OF NATURE";
            m_NameText.fontSize = 24;
            m_NameText.alignment = TextAnchor.MiddleCenter;
            m_NameText.color = new Color(0.85f, 0.75f, 0.4f, 1f); // gold
            var nrt = nameGo.GetComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.5f, 1f);
            nrt.anchorMax = new Vector2(0.5f, 1f);
            nrt.sizeDelta = new Vector2(PanelSize.x, 0.5f);
            nrt.anchoredPosition = new Vector2(0f, -0.1f);
            // health bar: dark frame + bright red fill
            m_HpFill = AddBar(root.transform, "HpBar", 1.15f, new Color(0.85f, 0.2f, 0.15f, 1f));
            m_HpBaseColor = m_HpFill.color;
            // stagger bar below
            m_StaggerFill = AddBar(root.transform, "StaggerBar", 0.45f, new Color(0.95f, 0.7f, 0.1f, 1f));
            m_StaggerBaseColor = m_StaggerFill.color;
            // notification strip under the bars
            m_NotificationText = AddTextBlock(root.transform, "NotificationText", 0.55f, 32,
                new Color(1f, 0.85f, 0.3f, 1f));
            var statusGo = new GameObject("StateText");
            statusGo.transform.SetParent(root.transform, false);
            m_StatusText = statusGo.AddComponent<Text>();
            m_StatusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_StatusText.fontSize = 26;
            m_StatusText.alignment = TextAnchor.MiddleCenter;
            m_StatusText.color = Color.white;
            var st = statusGo.GetComponent<RectTransform>();
            st.anchorMin = new Vector2(0.5f, 0f);
            st.anchorMax = new Vector2(0.5f, 0f);
            st.sizeDelta = new Vector2(PanelSize.x, 0.4f);
            st.anchoredPosition = new Vector2(0f, 0.15f);
            var weakGo = new GameObject("WeakPointText");
            weakGo.transform.SetParent(root.transform, false);
            m_WeakPointText = weakGo.AddComponent<Text>();
            m_WeakPointText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_WeakPointText.fontSize = 18;
            m_WeakPointText.alignment = TextAnchor.MiddleCenter;
            m_WeakPointText.color = new Color(0.55f, 0.95f, 0.6f, 1f);
            var wk = weakGo.GetComponent<RectTransform>();
            wk.anchorMin = new Vector2(0.5f, 0f);
            wk.anchorMax = new Vector2(0.5f, 0f);
            wk.sizeDelta = new Vector2(PanelSize.x, 0.35f);
            wk.anchoredPosition = new Vector2(0f, -0.12f);
        }
        Text AddTextBlock(Transform parent, string goName, float yOffset, int fontSize, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PanelSize.x, 0.5f);
            rect.anchoredPosition = new Vector2(0f, yOffset);
            return text;
        }
        Text m_NameText;
        Color m_HpBaseColor;
        Color m_StaggerBaseColor;
        int m_LastOpenWeakPoints = -1;
        Color m_NotificationTintColor = new Color(1f, 0.85f, 0.3f, 1f);
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
            {
                float ratio = Mathf.Clamp01(m_Boss.BossHealth.GetRatio());
                m_HpFill.fillAmount = ratio;
                // feedback: critical HP pulses hot red-orange
                if (ratio < 0.3f)
                    m_HpFill.color = Color.Lerp(m_HpBaseColor, new Color(1f, 0.45f, 0.1f, 1f),
                        (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f);
                else
                    m_HpFill.color = m_HpBaseColor;
            }
            if (m_StaggerFill != null && m_Boss.Stagger != null)
            {
                float sRatio = Mathf.Clamp01(m_Boss.Stagger.Ratio);
                m_StaggerFill.fillAmount = sRatio;
                // feedback: near-break the stagger bar glows white-gold
                if (sRatio > 0.8f)
                    m_StaggerFill.color = Color.Lerp(m_StaggerBaseColor, Color.white,
                        (Mathf.Sin(Time.time * 10f) + 1f) * 0.5f * (sRatio - 0.8f) / 0.2f);
                else
                    m_StaggerFill.color = m_StaggerBaseColor;
            }
            bool refreshText = Time.unscaledTime >= m_NextTextRefresh;
            if (refreshText)
            {
                m_NextTextRefresh = Time.unscaledTime + k_TextRefreshInterval;
                if (m_StatusText != null)
                    m_StatusText.text = DescribeStatus();
                if (m_WeakPointText != null)
                    m_WeakPointText.text = DescribeWeakPoints();
            }
            // notifications: no spam, visible for NotificationSeconds only
            if (m_NotificationText != null)
            {
                if (m_NotificationTimer > 0f)
                {
                    m_NotificationTimer -= Time.deltaTime;
                    m_NotificationText.color = m_NotificationTintColor;
                    m_NotificationText.fontSize = Mathf.CeilToInt(Mathf.Lerp(18, 34, Mathf.Clamp01(m_NotificationTimer / NotificationSeconds)));
                }
                else if (refreshText && m_Boss.SummonsActive)
                {
                    // persistent summon objective line with a live counter
                    int left = ActiveSummonCount();
                    m_NotificationText.text = left > 0 ? "DEFEAT THE SUMMONS — " + left + " LEFT" : "SUMMONS CLEARED";
                    m_NotificationTintColor = new Color(0.95f, 0.75f, 0.2f, 1f);
                    m_NotificationText.color = m_NotificationTintColor;
                    m_NotificationText.fontSize = 24;
                }
                else if (refreshText)
                    m_NotificationText.text = "";
            }
            if (refreshText)
                PollEventNotifications();
        }

        int ActiveSummonCount()
        {
            return m_Summons != null ? m_Summons.ActiveSummonCount : 0;
        }
        [Header("Notifications")]
        public float NotificationSeconds = 2.5f;
        float m_NotificationTimer;
        bool m_WasStaggeredShown;
        bool m_WasPhaseTwoShown;
        bool m_WasSummonActive;
        bool m_WasDeathShown;
        void PollEventNotifications()
        {
            if (m_Boss == null || m_Boss.BossHealth == null)
                return;
            if (m_Boss.IsDead)
            {
                if (!m_WasDeathShown)
                    ShowNotification("BOSS DEFEATED", new Color(0.2f, 1f, 0.4f, 1f), NotificationSeconds * 2f);
                m_WasDeathShown = true;
                return;
            }
            if (m_Boss.IsDead) return; // unreachable: death handled early

            // weak point feedback: message on every open/close change
            int open = 0; int total = 0;
            foreach (var wp in m_Boss.WeakPoints)
                if (wp != null)
                {
                    total++;
                    if (wp.IsExposed)
                        open++;
                }
            if (m_LastOpenWeakPoints >= 0 && open != m_LastOpenWeakPoints)
            {
                if (open > m_LastOpenWeakPoints)
                    ShowNotification("WEAK POINTS EXPOSED — SHOOT THE GLOW", new Color(0.2f, 1f, 0.4f, 1f), 2f);
                else if (open > 0)
                    ShowNotification("WEAK POINT DESTROYED — " + open + "/" + total + " LEFT", new Color(0.2f, 1f, 0.4f, 1f), 2.2f);
            }
            m_LastOpenWeakPoints = open;

            bool staggerFull = m_Boss.Stagger != null && m_Boss.Stagger.IsFull;
            if (staggerFull && !m_WasStaggeredShown)
            {
                ShowNotification("STAGGER BREAK — WEAK POINTS EXPOSED", new Color(0.2f, 1f, 0.4f, 1f));
                m_WasStaggeredShown = true;
            }
            if (m_Boss.PhaseTwo && !m_WasPhaseTwoShown)
            {
                ShowNotification("PHASE 2", new Color(1f, 0.3f, 0.2f, 1f));
                m_WasPhaseTwoShown = true;
            }
            if (m_Boss.SummonsActive && !m_WasSummonActive)
            {
                ShowNotification("DEFEAT THE SUMMONS", new Color(0.95f, 0.75f, 0.2f, 1f));
                m_WasSummonActive = true;
            }
            else if (!m_Boss.SummonsActive && m_WasSummonActive)
            {
                ShowNotification("BOSS VULNERABLE", new Color(0.2f, 1f, 0.4f, 1f));
                m_WasSummonActive = false;
            }
        }
        /// <summary>Event-driven message: sets the centre notification for a short window.</summary>
        void ShowNotification(string message, Color color, float seconds = 0f)
        {
            if (m_NotificationText == null)
                return;
            m_NotificationText.text = message;
            m_NotificationText.color = color;
            m_NotificationTimer = seconds > 0f ? seconds : NotificationSeconds;
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
        string DescribeStatus()
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
            if (vuln || stateWord == "STAGGER")
                stateWord = "VULNERABLE - SHOOT THE GREEN CORE";

            if (m_Boss.GetComponent<AvatarBossDuelController>()?.DuelWindowActive == true)
                stateWord = "DUEL WINDOW - SHOOT THE GREEN CORE";
            else if (!m_Boss.PhaseTwo && !vuln && stateWord == "IDLE")
                stateWord = "BUILD STAGGER OR BREAK MARKED CELLS";

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
                   (open > 0 ? "  -> SHOOT THE GREEN GLOW" : "");
        }
    }
}
