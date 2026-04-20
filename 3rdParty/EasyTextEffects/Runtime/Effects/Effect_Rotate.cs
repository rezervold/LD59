using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace EasyTextEffects.Effects
{
    [CreateAssetMenu(fileName = "Rotate", menuName = "Easy Text Effects/3. Rotate", order = 3)]
    public class Effect_Rotate : TextEffectInstance
    {
        [SerializeField, HideInInspector]
        private bool m_MigratedAngles = false;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("startAngle")]
        private float m_LegacyStartAngle = 90f;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("endAngle")]
        private float m_LegacyEndAngle = 0f;

        [Space(10)]
        [Header("Rotate")]
        public Vector2 centerOffset = Vector2.zero;
        public ParticleSystem.MinMaxCurve startAngle = new ParticleSystem.MinMaxCurve(90f);
        public ParticleSystem.MinMaxCurve endAngle = new ParticleSystem.MinMaxCurve(0f);
        
        [Space(5)]
        public bool randomizeDirection;

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            if (!m_MigratedAngles)
            {
                m_MigratedAngles = true;
                startAngle = new ParticleSystem.MinMaxCurve(m_LegacyStartAngle);
                endAngle = new ParticleSystem.MinMaxCurve(m_LegacyEndAngle);
            }
        }

        public override void ApplyEffect(TMP_TextInfo _textInfo, int _charIndex, int _startVertex = 0, int _endVertex = 3)
        {
            if (!CheckCanApplyEffect(_charIndex)) return;

            TMP_CharacterInfo charInfo = _textInfo.characterInfo[_charIndex];
            var materialIndex = charInfo.materialReferenceIndex;
            var verts = _textInfo.meshInfo[materialIndex].vertices;

            var angle = Interpolate(startAngle, endAngle, _charIndex);
            if (randomizeDirection) angle *= GetRandomSign(_charIndex);
            
            // Reason: wholeText uses the shared text center as pivot so all chars
            // rotate together. Per-char mode uses each char's own center.
            Vector3 center = wholeText
                ? cachedTextCenter + new Vector3(centerOffset.x, centerOffset.y, 0)
                : CharCenter(charInfo, verts) + new Vector3(centerOffset.x, centerOffset.y, 0);
            
            for (var v = _startVertex; v <= _endVertex; v++)
            {
                var vertexIndex = charInfo.vertexIndex + v;
                Vector3 fromCenter = verts[vertexIndex] - center;
                verts[vertexIndex] = center + Quaternion.Euler(0, 0, angle) * fromCenter;
            }
        }
    }
}