using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace EasyTextEffects.Effects
{
    [CreateAssetMenu(fileName = "Move", menuName = "Easy Text Effects/2. Move", order = 2)]
    public class Effect_Move : TextEffectInstance
    {
        [SerializeField, HideInInspector]
        private bool m_MigratedMove = false;
        
        [SerializeField, HideInInspector, FormerlySerializedAs("startOffset")]
        private Vector2 m_LegacyStartOffset = new Vector2(0f, -10f);
        
        [SerializeField, HideInInspector, FormerlySerializedAs("endOffset")]
        private Vector2 m_LegacyEndOffset = Vector2.zero;

        [Space(10)]
        [Header("Move X")]
        public ParticleSystem.MinMaxCurve startOffsetX = new ParticleSystem.MinMaxCurve(0f);
        public ParticleSystem.MinMaxCurve endOffsetX = new ParticleSystem.MinMaxCurve(0f);

        [Space(5)]
        [Header("Move Y")]
        public ParticleSystem.MinMaxCurve startOffsetY = new ParticleSystem.MinMaxCurve(-10f);
        public ParticleSystem.MinMaxCurve endOffsetY = new ParticleSystem.MinMaxCurve(0f);

        [Space(5)]
        public bool randomizeDirectionX;
        public bool randomizeDirectionY;

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            if (!m_MigratedMove)
            {
                m_MigratedMove = true;
                startOffsetX = new ParticleSystem.MinMaxCurve(m_LegacyStartOffset.x);
                endOffsetX = new ParticleSystem.MinMaxCurve(m_LegacyEndOffset.x);
                startOffsetY = new ParticleSystem.MinMaxCurve(m_LegacyStartOffset.y);
                endOffsetY = new ParticleSystem.MinMaxCurve(m_LegacyEndOffset.y);
            }
        }

        public override void ApplyEffect(TMP_TextInfo _textInfo, int _charIndex, int _startVertex = 0, int _endVertex = 3)
        {
            if (!CheckCanApplyEffect(_charIndex)) return;

            TMP_CharacterInfo charInfo = _textInfo.characterInfo[_charIndex];
            var materialIndex = charInfo.materialReferenceIndex;
            var verts = _textInfo.meshInfo[materialIndex].vertices;

            Vector2 offset = Interpolate(startOffsetX, endOffsetX, startOffsetY, endOffsetY, _charIndex);

            if (randomizeDirectionX) offset.x *= GetRandomSign(_charIndex, 80000);
            if (randomizeDirectionY) offset.y *= GetRandomSign(_charIndex, 90000);

            for (var v = _startVertex; v <= _endVertex; v++)
            {
                var vertexIndex = charInfo.vertexIndex + v;
                verts[vertexIndex] += new Vector3(offset.x, offset.y, 0);
            }
        }
    }
}