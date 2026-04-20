using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace EasyTextEffects.Effects
{
    [CreateAssetMenu(fileName = "Scale", menuName = "Easy Text Effects/4. Scale", order = 4)]
    public class Effect_Scale : TextEffectInstance
    {
        [SerializeField, HideInInspector]
        private bool m_MigratedScale = false;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("startScale")]
        private float m_LegacyStartScale = 0f;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("endScale")]
        private float m_LegacyEndScale = 1f;

        [Space(10)]
        [Header("Scale")]
        public ParticleSystem.MinMaxCurve startScale = new ParticleSystem.MinMaxCurve(0f);
        public ParticleSystem.MinMaxCurve endScale = new ParticleSystem.MinMaxCurve(1f);

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            if (!m_MigratedScale)
            {
                m_MigratedScale = true;
                startScale = new ParticleSystem.MinMaxCurve(m_LegacyStartScale);
                endScale = new ParticleSystem.MinMaxCurve(m_LegacyEndScale);
            }
        }

        public override void ApplyEffect(TMP_TextInfo _textInfo, int _charIndex, int _startVertex = 0, int _endVertex = 3)
        {
            if (!CheckCanApplyEffect(_charIndex)) return;

            TMP_CharacterInfo charInfo = _textInfo.characterInfo[_charIndex];
            var materialIndex = charInfo.materialReferenceIndex;
            var verts = _textInfo.meshInfo[materialIndex].vertices;
            // Reason: wholeText uses the shared text center as pivot so all chars
            // scale together. Per-char mode uses each char's own center.
            Vector3 center = wholeText ? cachedTextCenter : CharCenter(charInfo, verts);

            var scale = Interpolate(startScale, endScale, _charIndex);

            for (var v = _startVertex; v <= _endVertex; v++)
            {
                var vertexIndex = charInfo.vertexIndex + v;
                Vector3 fromCenter = verts[vertexIndex] - center;
                verts[vertexIndex] = center + fromCenter * scale;
            }
        }
    }
}