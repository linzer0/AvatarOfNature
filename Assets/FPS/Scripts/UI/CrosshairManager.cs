using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.FPS.UI
{
    public class CrosshairManager : MonoBehaviour
    {
        public Image CrosshairImage;
        public Sprite NullCrosshairSprite;
        public float CrosshairUpdateshrpness = 5f;

        [Header("Boss hit feedback")]
        public bool ShowBossHitmarker = true;
        public float HitmarkerDistance = 18f;
        public float HitmarkerFadeSpeed = 8f;

        PlayerWeaponsManager m_WeaponsManager;
        bool m_WasPointingAtEnemy;
        RectTransform m_CrosshairRectTransform;
        CrosshairData m_CrosshairDataDefault;
        CrosshairData m_CrosshairDataTarget;
        CrosshairData m_CurrentCrosshair;
        RectTransform m_HitmarkerRoot;
        CanvasGroup m_HitmarkerGroup;
        readonly Image[] m_HitmarkerTicks = new Image[4];
        float m_HitmarkerPulse;
        Color m_HitmarkerColor = Color.white;

        void Start()
        {
            m_WeaponsManager = FindFirstObjectByType<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullFindObject<PlayerWeaponsManager, CrosshairManager>(m_WeaponsManager, this);

            OnWeaponChanged(m_WeaponsManager.GetActiveWeapon());

            m_WeaponsManager.OnSwitchedToWeapon += OnWeaponChanged;

            EventManager.AddListener<BossHitFeedbackEvent>(OnBossHit);

            if (ShowBossHitmarker)
                BuildHitmarker();
        }

        void OnDestroy()
        {
            if (m_WeaponsManager != null)
                m_WeaponsManager.OnSwitchedToWeapon -= OnWeaponChanged;
            EventManager.RemoveListener<BossHitFeedbackEvent>(OnBossHit);
        }

        void Update()
        {
            UpdateCrosshairPointingAtEnemy(false);
            m_WasPointingAtEnemy = m_WeaponsManager.IsPointingAtEnemy;
        }

        void UpdateCrosshairPointingAtEnemy(bool force)
        {
            if (m_CrosshairDataDefault.CrosshairSprite == null)
                return;

            if ((force || !m_WasPointingAtEnemy) && m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataTarget;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }
            else if ((force || m_WasPointingAtEnemy) && !m_WeaponsManager.IsPointingAtEnemy)
            {
                m_CurrentCrosshair = m_CrosshairDataDefault;
                CrosshairImage.sprite = m_CurrentCrosshair.CrosshairSprite;
                m_CrosshairRectTransform.sizeDelta = m_CurrentCrosshair.CrosshairSize * Vector2.one;
            }

            CrosshairImage.color = Color.Lerp(CrosshairImage.color, m_CurrentCrosshair.CrosshairColor,
                Time.deltaTime * CrosshairUpdateshrpness);

            m_CrosshairRectTransform.sizeDelta = Mathf.Lerp(m_CrosshairRectTransform.sizeDelta.x,
                m_CurrentCrosshair.CrosshairSize,
                Time.deltaTime * CrosshairUpdateshrpness) * Vector2.one;

            UpdateHitmarker();
        }

        void BuildHitmarker()
        {
            if (CrosshairImage == null || m_HitmarkerRoot != null)
                return;

            var root = new GameObject("BossHitmarker", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(CrosshairImage.transform, false);
            m_HitmarkerRoot = root.GetComponent<RectTransform>();
            m_HitmarkerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            m_HitmarkerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            m_HitmarkerRoot.pivot = new Vector2(0.5f, 0.5f);
            m_HitmarkerRoot.anchoredPosition = Vector2.zero;
            m_HitmarkerRoot.sizeDelta = Vector2.zero;
            m_HitmarkerGroup = root.GetComponent<CanvasGroup>();
            m_HitmarkerGroup.alpha = 0f;

            for (int i = 0; i < m_HitmarkerTicks.Length; i++)
            {
                var tick = new GameObject($"Tick_{i}", typeof(RectTransform), typeof(Image));
                tick.transform.SetParent(m_HitmarkerRoot, false);
                var rect = tick.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(2.5f, 12f);
                float angle = 45f + i * 90f;
                float radians = angle * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * HitmarkerDistance;
                rect.localRotation = Quaternion.Euler(0f, 0f, angle);
                var image = tick.GetComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;
                m_HitmarkerTicks[i] = image;
            }
        }

        void OnBossHit(BossHitFeedbackEvent hit)
        {
            if (!ShowBossHitmarker || m_HitmarkerRoot == null)
                return;

            m_HitmarkerPulse = Mathf.Clamp(m_HitmarkerPulse + (hit.IsWeakPoint ? 1.35f : 1f), 0f, 2.2f);
            m_HitmarkerColor = hit.IsWeakPoint
                ? new Color(0.35f, 1f, 0.55f, 1f)
                : new Color(1f, 0.92f, 0.65f, 1f);
            m_HitmarkerGroup.alpha = 1f;
        }

        void UpdateHitmarker()
        {
            if (m_HitmarkerRoot == null)
                return;

            float pulse = Mathf.Max(0f, m_HitmarkerPulse);
            m_HitmarkerGroup.alpha = Mathf.Clamp01(pulse);
            float scale = Mathf.Lerp(0.82f, 1.18f, Mathf.Clamp01(pulse));
            m_HitmarkerRoot.localScale = Vector3.one * scale;
            for (int i = 0; i < m_HitmarkerTicks.Length; i++)
                if (m_HitmarkerTicks[i] != null)
                    m_HitmarkerTicks[i].color = m_HitmarkerColor;

            m_HitmarkerPulse = Mathf.MoveTowards(m_HitmarkerPulse, 0f,
                Time.unscaledDeltaTime * HitmarkerFadeSpeed);
        }

        void OnWeaponChanged(WeaponController newWeapon)
        {
            if (newWeapon)
            {
                CrosshairImage.enabled = true;
                m_CrosshairDataDefault = newWeapon.CrosshairDataDefault;
                m_CrosshairDataTarget = newWeapon.CrosshairDataTargetInSight;
                m_CrosshairRectTransform = CrosshairImage.GetComponent<RectTransform>();
                DebugUtility.HandleErrorIfNullGetComponent<RectTransform, CrosshairManager>(m_CrosshairRectTransform,
                    this, CrosshairImage.gameObject);
            }
            else
            {
                if (NullCrosshairSprite)
                {
                    CrosshairImage.sprite = NullCrosshairSprite;
                }
                else
                {
                    CrosshairImage.enabled = false;
                }
            }

            UpdateCrosshairPointingAtEnemy(true);
            if (m_HitmarkerRoot == null && ShowBossHitmarker)
                BuildHitmarker();
        }
    }
}
