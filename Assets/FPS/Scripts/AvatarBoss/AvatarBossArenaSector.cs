using System;
using System.Collections;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>Runtime state of a destructible boss arena sector.</summary>
    public enum AvatarBossArenaSectorState
    {
        /// <summary>The sector is fully usable.</summary>
        Intact,
        /// <summary>The sector has been damaged but has not started collapsing.</summary>
        Damaged,
        /// <summary>The sector is in the short transition before destruction.</summary>
        Collapsing,
        /// <summary>The sector is no longer usable.</summary>
        Destroyed
    }

    /// <summary>Owns the state and presentation references for one arena sector.</summary>
    public sealed class AvatarBossArenaSector : MonoBehaviour
    {
        [SerializeField] int m_Index;
        [SerializeField] AvatarBossArenaSectorState m_State = AvatarBossArenaSectorState.Intact;
        [SerializeField] GameObject m_IntactVisual;
        [SerializeField] GameObject m_DamagedVisual;
        [SerializeField] GameObject m_DestroyedVisual;

        Coroutine m_CollapseRoutine;
        Action m_CollapseFinished;

        /// <summary>Zero-based index assigned by the arena controller.</summary>
        public int Index => m_Index;

        /// <summary>Current state of this sector.</summary>
        public AvatarBossArenaSectorState State => m_State;

        /// <summary>Visual used while the sector is intact.</summary>
        public GameObject IntactVisual { get => m_IntactVisual; set => m_IntactVisual = value; }

        /// <summary>Visual used while the sector is damaged or collapsing.</summary>
        public GameObject DamagedVisual { get => m_DamagedVisual; set => m_DamagedVisual = value; }

        /// <summary>Visual used after the sector is destroyed.</summary>
        public GameObject DestroyedVisual { get => m_DestroyedVisual; set => m_DestroyedVisual = value; }

        /// <summary>Assigns the controller-owned index.</summary>
        public void SetIndex(int index)
        {
            m_Index = index;
        }

        /// <summary>Applies a state and updates the configured visuals.</summary>
        public void ForceState(AvatarBossArenaSectorState state)
        {
            StopCollapse();
            m_State = state;
            RefreshVisuals();
        }

        /// <summary>Starts the collapse transition and invokes the callback when it reaches Destroyed.</summary>
        public void BeginCollapse(float duration, Action collapseFinished)
        {
            StopCollapse();
            m_State = AvatarBossArenaSectorState.Collapsing;
            RefreshVisuals();
            m_CollapseFinished = collapseFinished;

            if (duration <= 0f)
            {
                m_State = AvatarBossArenaSectorState.Destroyed;
                RefreshVisuals();
                var callback = m_CollapseFinished;
                m_CollapseFinished = null;
                callback?.Invoke();
                return;
            }

            m_CollapseRoutine = StartCoroutine(CollapseAfterDelay(duration));
        }

        /// <summary>Stops a pending collapse without destroying any referenced visual objects.</summary>
        public void StopCollapse()
        {
            if (m_CollapseRoutine != null)
            {
                StopCoroutine(m_CollapseRoutine);
                m_CollapseRoutine = null;
            }

            m_CollapseFinished = null;
        }

        void Awake()
        {
            RefreshVisuals();
        }

        IEnumerator CollapseAfterDelay(float duration)
        {
            yield return new WaitForSeconds(duration);
            m_CollapseRoutine = null;
            ForceState(AvatarBossArenaSectorState.Destroyed);

            var callback = m_CollapseFinished;
            m_CollapseFinished = null;
            callback?.Invoke();
        }

        void RefreshVisuals()
        {
            if (m_IntactVisual != null)
                m_IntactVisual.SetActive(m_State == AvatarBossArenaSectorState.Intact);
            if (m_DamagedVisual != null)
                m_DamagedVisual.SetActive(m_State == AvatarBossArenaSectorState.Damaged ||
                                          m_State == AvatarBossArenaSectorState.Collapsing);
            if (m_DestroyedVisual != null)
                m_DestroyedVisual.SetActive(m_State == AvatarBossArenaSectorState.Destroyed);
        }
    }
}
