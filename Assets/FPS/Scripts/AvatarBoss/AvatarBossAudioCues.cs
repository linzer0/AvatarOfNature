using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossCue
    {
        Telegraph,
        StaggerBreak,
        WeakPointOpen,
        PhaseTwo,
        BossDeath,
        Summon,
    }

    /// Minimal audio cues for the Avatar of Nature. Designer clips are optional;
    /// when a clip slot is empty a small deterministic synthesized tone is used -
    /// no exceptions, no missing-asset warnings.
    public class AvatarBossAudioCues : MonoBehaviour
    {
        [Header("Designer Clips (optional; safe tone stubs are used when empty)")]
        public AudioClip TelegraphClip;
        public AudioClip StaggerBreakClip;
        public AudioClip WeakPointOpenClip;
        public AudioClip PhaseTwoClip;
        public AudioClip BossDeathClip;
        public AudioClip SummonClip;

        public void Play(AvatarBossCue cue)
        {
            AudioClip clip = GetClipFor(cue);
            if (clip == null)
                return;

            // standalone player so the cue works with or without the Microgame AudioManager
            AudioSource.PlayClipAtPoint(clip, transform.position, 0.6f);
        }

        AudioClip GetClipFor(AvatarBossCue cue)
        {
            switch (cue)
            {
                case AvatarBossCue.Telegraph:
                    return TelegraphClip != null ? TelegraphClip : GetOrCreateTone(ref m_ToneTelegraph, 520f, 0.12f);
                case AvatarBossCue.StaggerBreak:
                    return StaggerBreakClip != null ? StaggerBreakClip : GetOrCreateTone(ref m_ToneBreak, 180f, 0.2f);
                case AvatarBossCue.WeakPointOpen:
                    return WeakPointOpenClip != null ? WeakPointOpenClip : GetOrCreateTone(ref m_ToneWeakPoint, 940f, 0.1f);
                case AvatarBossCue.PhaseTwo:
                    return PhaseTwoClip != null ? PhaseTwoClip : GetOrCreateTone(ref m_TonePhaseTwo, 320f, 0.25f);
                case AvatarBossCue.Summon:
                    return SummonClip != null ? SummonClip : GetOrCreateTone(ref m_ToneSummon, 660f, 0.3f);
                case AvatarBossCue.BossDeath:
                    return BossDeathClip != null ? BossDeathClip : GetOrCreateTone(ref m_ToneDeath, 110f, 0.5f);
                default:
                    return null;
            }
        }

        AudioClip m_ToneTelegraph;
        AudioClip m_ToneBreak;
        AudioClip m_ToneWeakPoint;
        AudioClip m_TonePhaseTwo;
        AudioClip m_ToneDeath;
        AudioClip m_ToneSummon;

        static AudioClip GetOrCreateTone(ref AudioClip cache, float frequency, float seconds)
        {
            if (cache == null)
                cache = SynthTone("AvatarBossCue_" + frequency.ToString("0"), frequency, seconds);
            return cache;
        }

        static AudioClip SynthTone(string name, float frequency, float seconds)
        {
            const int sampleRate = 22050;
            int length = Mathf.CeilToInt(seconds * sampleRate);
            var clip = AudioClip.Create(name, length, 1, sampleRate, false);
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                // smooth attack + decay prevents click artifacts
                float envelope = Mathf.Sin(Mathf.PI * (float)i / length);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
            }
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
