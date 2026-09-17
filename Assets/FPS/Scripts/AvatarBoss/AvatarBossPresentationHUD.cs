using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Presentation pass: single screen-space boss HUD at the top of the screen
    /// (title, HP bar, stagger bar, phase + current attack line) and large
    /// event messages fired on state transitions only, with fade in/out.
    /// Built once in Start from real boss data; no Canvas per frame, no debug
    /// clutter, no gameplay logic changed.
    /// </summary>
    [ExecuteAlways]
    public class AvatarBossPresentationHUD : MonoBehaviour
    {
        const float k_MessageFadeIn = 0.15f;
        const float k_MessageFadeOut = 0.45f;
        const float k_StatusRefreshInterval = 0.1f;

        AvatarBossController m_Boss;
        Canvas m_Canvas;

        Text m_Title;
        Image m_HpFill;
        Image m_StaggerFill;
        Text m_HpLabel;
        Text m_StaggerLabel;
        Text m_StatusLine;
        Text m_EventMessage;
        CanvasGroup m_EventGroup;

        // non-spamming event sentinels
        AvatarBossElement m_LastElement;
        AvatarBossSchedulerState m_LastState;
        int m_LastOpenWeakPoints = -1;
        bool m_LastStaggerFull;
        bool m_LastPhaseTwo;
        bool m_LastSummonsActive;
        bool m_LastDead;
        bool m_Initialized;
        float m_NextStatusRefresh;

        void Start()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            if (m_Boss == null)
            {
                enabled = false;
                return;
            }
            if (m_Initialized)
                return;
            BuildUI();
            m_Initialized = true;
            if (!Application.isPlaying)
                return;
            HideMicrogameCompass();
            m_Boss.OnBossHit += OnBossHit;
            if (m_Boss.HealingOrbs != null)
            {
                m_Boss.HealingOrbs.RecoveryStarted += OnRecoveryStarted;
                m_Boss.HealingOrbs.OrbDestroyed += OnOrbDestroyed;
                m_Boss.HealingOrbs.BossHealed += OnBossHealed;
                m_Boss.HealingOrbs.OrbReachedWithoutHealing += OnOrbReachedWithoutHealing;
            }
        }

        void OnDestroy()
        {
            m_Initialized = false;
            if (!Application.isPlaying || m_Boss == null)
                return;
            m_Boss.OnBossHit -= OnBossHit;
            if (m_Boss.HealingOrbs != null)
            {
                m_Boss.HealingOrbs.RecoveryStarted -= OnRecoveryStarted;
                m_Boss.HealingOrbs.OrbDestroyed -= OnOrbDestroyed;
                m_Boss.HealingOrbs.BossHealed -= OnBossHealed;
                m_Boss.HealingOrbs.OrbReachedWithoutHealing -= OnOrbReachedWithoutHealing;
            }
        }

        void OnRecoveryStarted() => ShowMessage("BOSS RECOVERING · DESTROY THE ORBS", new Color(0.3f, 1f, 0.65f, 1f), 2f);
        void OnOrbDestroyed(int index) => ShowMessage("ORB DESTROYED", new Color(0.7f, 1f, 0.8f, 1f));
        void OnBossHealed(float amount) => ShowMessage($"BOSS HEALED +{amount / m_Boss.BossHealth.MaxHealth * 100f:F0}%", new Color(0.4f, 1f, 0.5f, 1f));
        void OnOrbReachedWithoutHealing() => ShowMessage("PHASE 2 — ORBS ARE A DISTRACTION", new Color(1f, 0.45f, 0.25f, 1f), 1.8f);

        float m_LastHitMessage;

        /// <summary>Event-driven hit message: no spam, short hold, clear of crosshair.
        /// After DEFEATED no new messages (OnBossHit stops firing once dead).</summary>
        void OnBossHit(Vector3 pos, float damage, bool weakPointHit)
        {
            if (m_Boss == null || m_Boss.IsDead)
                return;
            if (Time.unscaledTime - m_LastHitMessage < 0.25f)
                return;
            m_LastHitMessage = Time.unscaledTime;

            if (weakPointHit)
                ShowMessage($"WEAK POINT HIT: x2 · -{damage:F0} HP", new Color(0.35f, 1f, 0.5f, 1f), 1.2f);
            else if (m_Boss.Stagger != null && !m_Boss.Stagger.IsFull)
                ShowMessage($"HIT: -{damage:F0} HP · STAGGER +{damage * m_Boss.Stagger.StaggerGainPerDamage:F0}",
                    new Color(1f, 0.95f, 0.85f, 1f), 1.0f);
            else
                ShowMessage($"HIT: -{damage:F0} HP", new Color(1f, 0.95f, 0.85f, 1f), 1.0f);
        }

        /// <summary>Compass stays active (summons reference its element nodes)
        /// but is invisible in the showcase: the boss HUD replaces it.</summary>
        void HideMicrogameCompass()
        {
            var compass = GameObject.Find("GameManager/GameHUD/HUD/Compass");
            if (compass == null)
                return;
            var group = compass.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        RectTransform Panel(float height, float width, float yTop)
        {
            var go = new GameObject("BossHudPanel");
            go.transform.SetParent(m_Canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, yTop);
            return rt;
        }

        Image AddRect(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Text AddText(Transform parent, string name, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        Image AddBar(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill)
        {
            var bg = AddRect(parent, name + "BG", new Color(0f, 0f, 0f, 0.65f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = anchor;
            bgRt.anchorMax = anchor;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = size;
            bgRt.anchoredPosition = pos;

            var child = AddRect(bg.transform, name + "Fill", fill);
            var rt = child.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(5f, 3f);
            rt.offsetMax = new Vector2(-5f, -3f);
            child.type = Image.Type.Filled;
            child.fillMethod = Image.FillMethod.Horizontal;
            return child;
        }

        void BuildUI()
        {
            var existing = transform.Find("BossPresentationHUD");
            if (existing != null)
            {
                m_Canvas = existing.GetComponent<Canvas>();
                m_HpFill = existing.GetComponentInChildren<Image>();
                m_HpFill = null;
                foreach (var img in existing.GetComponentsInChildren<Image>(true))
                    if (img.name == "HpBarFill") m_HpFill = img;
                foreach (var img in existing.GetComponentsInChildren<Image>(true))
                    if (img.name == "StaggerBarFill") m_StaggerFill = img;
                foreach (var txt in existing.GetComponentsInChildren<Text>(true))
                {
                    if (txt.name == "HpLabel") m_HpLabel = txt;
                    if (txt.name == "StaggerLabel") m_StaggerLabel = txt;
                    if (txt.name == "StatusLine") m_StatusLine = txt;
                    if (txt.name == "EventMessage") { m_EventMessage = txt; m_EventGroup = txt.GetComponent<CanvasGroup>(); }
                }
                var existingPanel = existing.transform.Find("BossHudPanel");
                if (existingPanel != null)
                {
                    if (m_HpLabel == null)
                        m_HpLabel = AddBarLabel(existingPanel, "HpLabel", "BOSS HEALTH", new Vector2(0f, -42f), 1050f);
                    if (m_StaggerLabel == null)
                        m_StaggerLabel = AddBarLabel(existingPanel, "StaggerLabel", "STAGGER", new Vector2(0f, -94f), 1050f);
                }
                return;
            }
            var root = new GameObject("BossPresentationHUD");
            root.transform.SetParent(transform, false);
            m_Canvas = root.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 60;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            const float width = 1050f; // ~55% of reference width
            var panel = Panel(210f, width, -26f);

            m_Title = AddText(panel, "Title", 44, new Color(0.95f, 0.82f, 0.35f), TextAnchor.MiddleCenter);
            m_Title.text = "AVATAR OF NATURE";
            var titleRt = m_Title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(width, 48f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);

            m_HpFill = AddBar(panel, "HpBar", new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(width, 40f), new Color(0.88f, 0.22f, 0.18f, 1f));
            m_StaggerFill = AddBar(panel, "StaggerBar", new Vector2(0.5f, 1f), new Vector2(0f, -112f),
                new Vector2(width, 22f), new Color(0.98f, 0.78f, 0.25f, 1f));
            m_HpLabel = AddBarLabel(panel, "HpLabel", "BOSS HEALTH", new Vector2(0f, -42f), width);
            m_StaggerLabel = AddBarLabel(panel, "StaggerLabel", "STAGGER", new Vector2(0f, -94f), width);

            m_StatusLine = AddText(panel, "StatusLine", 30, Color.white, TextAnchor.MiddleCenter);
            var stRt = m_StatusLine.GetComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.5f, 1f);
            stRt.anchorMax = new Vector2(0.5f, 1f);
            stRt.sizeDelta = new Vector2(width, 34f);
            stRt.anchoredPosition = new Vector2(0f, -146f);

            // big event message: upper-middle, clear of crosshair and HUD panel
            var msgGo = new GameObject("EventMessage");
            msgGo.transform.SetParent(m_Canvas.transform, false);
            m_EventMessage = msgGo.AddComponent<Text>();
            m_EventMessage.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            m_EventMessage.fontSize = 56;
            m_EventMessage.alignment = TextAnchor.MiddleCenter;
            m_EventMessage.text = "";
            var msgRt = msgGo.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.5f, 1f);
            msgRt.anchorMax = new Vector2(0.5f, 1f);
            msgRt.sizeDelta = new Vector2(1600f, 80f);
            msgRt.anchoredPosition = new Vector2(0f, -250f);
            m_EventGroup = msgGo.AddComponent<CanvasGroup>();
            m_EventGroup.alpha = 0f;
        }

        Text AddBarLabel(Transform parent, string name, string label, Vector2 position, float width)
        {
            var text = AddText(parent, name, 16, new Color(0.9f, 0.94f, 1f, 0.92f), TextAnchor.MiddleLeft);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width - 22f, 22f);
            rect.anchoredPosition = position + new Vector2(11f, 0f);
            text.text = label;
            return text;
        }

        void Update()
        {
            if (m_Boss == null)
                m_Boss = GetComponentInParent<AvatarBossController>();
            if (m_Boss == null)
                return;
            if (!Application.isPlaying)
            {
                // static editor preview: real scene settings, bars frozen at designer values
                if (m_Initialized)
                    return;
                m_Initialized = true;
                return;
            }
            if (!m_Initialized || m_Boss.BossHealth == null)
                return;

            float hp = Mathf.Clamp01(m_Boss.BossHealth.GetRatio());
            m_HpFill.fillAmount = hp;
            m_HpFill.color = hp < 0.3f
                ? Color.Lerp(new Color(0.88f, 0.22f, 0.18f, 1f), new Color(1f, 0.5f, 0.12f, 1f),
                    (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f)
                : new Color(0.88f, 0.22f, 0.18f, 1f);

            float stagger = m_Boss.Stagger != null ? Mathf.Clamp01(m_Boss.Stagger.Ratio) : 0f;
            m_StaggerFill.fillAmount = stagger;

            if (Time.unscaledTime >= m_NextStatusRefresh)
            {
                m_NextStatusRefresh = Time.unscaledTime + k_StatusRefreshInterval;
                if (m_HpLabel != null)
                    m_HpLabel.text = $"BOSS HEALTH  {hp * 100f:F0}%";
                if (m_StaggerLabel != null)
                    m_StaggerLabel.text = $"STAGGER  {stagger * 100f:F0}%";
                if (m_Boss.SummonsActive)
                    m_StatusLine.text = "PHASE 2 · DEFEAT THE SUMMONS · " + ActiveSummons() + " LEFT";
                else
                    m_StatusLine.text = DescribeStatus();
                PollEvents();
            }
            UpdateMessageFade();
        }

        int ActiveSummons()
        {
            var summons = m_Boss.GetComponentInChildren<AvatarBossSummonController>();
            return summons != null ? summons.ActiveSummonCount : 0;
        }

        string DescribeStatus()
        {
            if (m_Boss.IsDead)
                return "DEFEATED";
            var difficulty = m_Boss.GetComponent<AvatarBossDifficultyController>();
            string difficultyText = difficulty != null
                ? difficulty.CurrentDifficulty.ToString().ToUpperInvariant() + " · "
                : "";
            var duel = m_Boss.GetComponent<AvatarBossDuelController>();
            if (duel != null && duel.DuelWindowActive)
                return difficultyText + "DUEL WINDOW · ATTACK THE BOSS";
            var healing = m_Boss.HealingOrbs;
            if (healing != null && healing.RecoveryActive)
            {
                if (m_Boss.PhaseTwo && !healing.HealEnabledInPhaseTwo)
                    return difficultyText + "PHASE 2 — ORBS ARE A DISTRACTION · ORBS: " + healing.ActiveOrbCount + " / " + healing.OrbCount;
                return difficultyText + "BOSS RECOVERING · DESTROY THE ORBS · ORBS: "
                    + healing.ActiveOrbCount + " / " + healing.OrbCount;
            }
            string phase = m_Boss.PhaseTwo ? "PHASE 2" : "PHASE 1";
            string state = "IDLE";
            var sched = m_Boss.Scheduler;
            if (m_Boss.Stagger != null && m_Boss.Stagger.IsFull)
                state = "STAGGERED";
            else if (m_Boss.Scheduler != null)
            {
                switch (sched.State)
                {
                    case AvatarBossSchedulerState.Telegraph:
                        state = "TELEGRAPH: " + ElementName(sched);
                        break;
                    case AvatarBossSchedulerState.Windup:
                        state = "WINDUP: " + ElementName(sched);
                        break;
                    case AvatarBossSchedulerState.Execute:
                    case AvatarBossSchedulerState.Recover:
                        state = "ATTACK: " + ElementName(sched);
                        break;
                }
            }
            string orbSuffix = healing != null && healing.SpawnedOrbCount > 0
                ? " · ORBS: " + healing.ActiveOrbCount + " / " + healing.OrbCount
                : "";
            return difficultyText + phase + " · " + state + " · BREAK MARKED CELLS" + orbSuffix;
        }

        string ElementName(AvatarBossAttackScheduler sched)
        {
            return sched.CurrentAttack != null ? sched.CurrentAttack.Element.ToString() : "UNKNOWN";
        }

        void PollEvents()
        {
            var sched = m_Boss.Scheduler;
            var element = sched != null && sched.CurrentAttack != null
                ? sched.CurrentAttack.Element : (AvatarBossElement)(-1);
            var state = sched != null ? sched.State : (AvatarBossSchedulerState)(-1);

            if (m_Initialized && (element != m_LastElement || state != m_LastState))
            {
                if (state == AvatarBossSchedulerState.Telegraph && m_LastState != AvatarBossSchedulerState.Telegraph)
                    ShowMessage(ElementTitle(element), ElementColor(element));
            }
            m_LastElement = element;
            m_LastState = state;

            int open = 0;
            foreach (var wp in m_Boss.WeakPoints)
                if (wp != null && wp.IsExposed)
                    open++;
            if (m_Initialized && m_LastOpenWeakPoints >= 0 && open > m_LastOpenWeakPoints)
                ShowMessage("WEAK POINTS EXPOSED", new Color(0.25f, 1f, 0.5f, 1f));
            m_LastOpenWeakPoints = open;

            if (m_Initialized && m_Boss.Stagger != null && m_Boss.Stagger.IsFull && !m_LastStaggerFull)
                ShowMessage("STAGGER BREAK", new Color(0.98f, 0.85f, 0.3f, 1f));
            m_LastStaggerFull = m_Boss.Stagger != null && m_Boss.Stagger.IsFull;

            if (m_Boss.PhaseTwo && !m_LastPhaseTwo && m_Initialized)
                ShowMessage("PHASE 2", new Color(1f, 0.3f, 0.25f, 1f));
            m_LastPhaseTwo = m_Boss.PhaseTwo;

            if (m_Boss.SummonsActive && !m_LastSummonsActive && m_Initialized)
                ShowMessage("DEFEAT THE SUMMONS", new Color(0.98f, 0.78f, 0.25f, 1f));
            if (!m_Boss.SummonsActive && m_LastSummonsActive && m_Initialized)
                ShowMessage("BOSS RESUMED", new Color(0.25f, 1f, 0.5f, 1f));
            m_LastSummonsActive = m_Boss.SummonsActive;

            if (m_Boss.IsDead && !m_LastDead && m_Initialized)
                ShowMessage("BOSS DEFEATED", new Color(0.95f, 0.85f, 0.4f, 1f), 3.5f);
            m_LastDead = m_Boss.IsDead;
        }

        string ElementTitle(AvatarBossElement element)
        {
            switch (element)
            {
                case AvatarBossElement.Shockwave: return "SHOCKWAVE";
                case AvatarBossElement.Fire: return "METEOR RAIN";
                default: return "EARTH ATTACK";
            }
        }

        Color ElementColor(AvatarBossElement element)
        {
            switch (element)
            {
                case AvatarBossElement.Shockwave: return new Color(0.4f, 0.8f, 1f, 1f);
                case AvatarBossElement.Fire: return new Color(1f, 0.38f, 0.2f, 1f);
                default: return new Color(1f, 0.62f, 0.25f, 1f);
            }
        }

        float m_MessageTime;
        float m_MessageHold;

        void ShowMessage(string message, Color color, float hold = 1.4f)
        {
            m_EventMessage.text = message;
            m_EventMessage.color = color;
            m_MessageHold = hold;
            m_MessageTime = 0f;
        }

        void UpdateMessageFade()
        {
            if (m_MessageHold <= 0f)
                return;
            m_MessageTime += Time.unscaledDeltaTime;
            float a;
            if (m_MessageTime < k_MessageFadeIn)
                a = m_MessageTime / k_MessageFadeIn;
            else if (m_MessageTime < m_MessageHold)
                a = 1f;
            else if (m_MessageTime < m_MessageHold + k_MessageFadeOut)
                a = 1f - (m_MessageTime - m_MessageHold) / k_MessageFadeOut;
            else
                a = 0f;
            m_EventGroup.alpha = a;
            if (m_MessageTime >= m_MessageHold + k_MessageFadeOut)
                m_MessageHold = 0f;
        }
    }
}
