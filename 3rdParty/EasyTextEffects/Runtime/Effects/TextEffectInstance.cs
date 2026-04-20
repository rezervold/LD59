using EasyTextEffects.Editor.EditorDocumentation;
using EasyTextEffects.Editor.MyBoxCopy.Attributes;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using static EasyTextEffects.Editor.EditorDocumentation.FoldBoxAttribute;

namespace EasyTextEffects.Effects
{
    public abstract class TextEffectInstance : TextEffect_Base, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private bool m_MigratedToMinMaxCurve = false;

        [SerializeField, HideInInspector, FormerlySerializedAs("durationPerChar")]
        private float m_LegacyDurationPerChar = 0.5f;

        [SerializeField, HideInInspector, FormerlySerializedAs("timeBetweenChars")]
        private float m_LegacyTimeBetweenChars = 0.05f;

        [SerializeField, HideInInspector, FormerlySerializedAs("easingCurve")]
        private AnimationCurve m_LegacyEasingCurve = null;

        public virtual void OnBeforeSerialize() { }
        public virtual void OnAfterDeserialize()
        {
            if (!m_MigratedToMinMaxCurve)
            {
                m_MigratedToMinMaxCurve = true;
                duration = new ParticleSystem.MinMaxCurve(m_LegacyDurationPerChar);
                delayBetweenChars = new ParticleSystem.MinMaxCurve(m_LegacyTimeBetweenChars);
                if (m_LegacyEasingCurve != null)
                {
                    easeCurve = new ParticleSystem.MinMaxCurve(1f, m_LegacyEasingCurve);
                }
            }
        }

        public enum AnimationType
        {
            Loop,
            LoopFixedDuration,
            PingPong,
            OneTime
        }

        [Space(10)]
        [Header("Type")]
        [FoldBox("",
            new[]
            {
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/onetime.png, 200",
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/notonetime.png, 300",
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/fixed.png, 300",
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/loopvspingpong.png, 300",
            },
            new[] { ContentType.Image })]
        public AnimationType animationType = AnimationType.PingPong;

        [ConditionalField(nameof(animationType), true, AnimationType.OneTime)]
        public int loopsCount = 0;

        [ConditionalField(nameof(animationType), false, AnimationType.LoopFixedDuration)]
        public float fixedDuration;


        [Space(10)]
        [FormerlySerializedAs("duration")]
        [Header("Timing")]
        [FoldBox("Timing Explained",
            new[] { "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/time.png, 300" },
            new[] { ContentType.Image })]
        public ParticleSystem.MinMaxCurve duration = new ParticleSystem.MinMaxCurve(0.5f);

        [FoldBox("Timing Explained",
            new[] { "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/time.png, 300" },
            new[] { ContentType.Image })]
        public ParticleSystem.MinMaxCurve delayBetweenChars = new ParticleSystem.MinMaxCurve(0.05f);

        [FoldBox("No Delay Explained",
            new[]
            {
                "If enabled, the effect will start immediately for all characters, instead of waiting for the previous character to finish.",
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/nodelay.png, 300"
            }, new[] { ContentType.Text, ContentType.Image })]
        public bool noDelayForChars;

        [FoldBox("Reverse Char Order Explained",
            new[]
            {
                "If enabled, the effect will start from the last character instead of the first. This is useful for exit animations.",
                "Packages/com.qiaozhilei.easy-text-effects/Documentation/Images/reverse.png, 300"
            }, new[] { ContentType.Text, ContentType.Image })]
        public bool reverseCharOrder;

        [Space(5)]
        [Tooltip("When enabled, all characters animate as a single unit (same timing, shared pivot).")]
        public bool wholeText;

        [Space(10)] [FormerlySerializedAs("curve")] [Header("Curve")]
        public ParticleSystem.MinMaxCurve easeCurve = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));

        public bool clampBetween0And1;

        [Header("Time")]
        [FoldBox("Time Explained",
            new[] { "ScaledTime -- dependent on time scale \nUnscaledTime -- independent of time scale", },
            new[] { ContentType.Text })]
        public TimeUtil.TimeType timeType = TimeUtil.TimeType.ScaledTime;


        protected float startTime;
        internal bool started;
        protected bool isComplete;
        private TextEffectEntry currentEntry;
        private int _lastCompletedCycle;

        // Reason: A unique seed generated on each StartEffect so math functions
        // output completely different random behaviors each play session!
        private int _playSeed;
        
        private float _totalEffectDuration;

        protected bool CheckCanApplyEffect(int _charIndex)
        {
            if (isComplete) return false;
            return started && _charIndex >= startCharIndex && _charIndex < startCharIndex + charLength;
        }

        private void SetWrapMode(ParticleSystem.MinMaxCurve minMaxCurve, WrapMode pre, WrapMode post)
        {
            if (minMaxCurve.curve != null)
            {
                minMaxCurve.curve.preWrapMode = pre;
                minMaxCurve.curve.postWrapMode = post;
            }
            if (minMaxCurve.curveMin != null)
            {
                minMaxCurve.curveMin.preWrapMode = pre;
                minMaxCurve.curveMin.postWrapMode = post;
            }
            if (minMaxCurve.curveMax != null)
            {
                minMaxCurve.curveMax.preWrapMode = pre;
                minMaxCurve.curveMax.postWrapMode = post;
            }
        }

        public virtual void StartEffect(TextEffectEntry entry)
        {
            currentEntry = entry;

            started = true;
            startTime = TimeUtil.GetTime(timeType);
            isComplete = false;
            _lastCompletedCycle = 0;
            _playSeed = Random.Range(0, 100000);

            CalculateTotalDuration();

            if (animationType == AnimationType.OneTime || animationType == AnimationType.LoopFixedDuration)
            {
                SetWrapMode(easeCurve, WrapMode.Clamp, WrapMode.Clamp);
            }
            else if (animationType == AnimationType.Loop)
            {
                SetWrapMode(easeCurve, noDelayForChars ? WrapMode.Loop : WrapMode.Clamp, WrapMode.Loop);
            }
            else if (animationType == AnimationType.PingPong)
            {
                SetWrapMode(easeCurve, noDelayForChars ? WrapMode.PingPong : WrapMode.Clamp, WrapMode.PingPong);
            }
        }

        private void CalculateTotalDuration()
        {
            _totalEffectDuration = -1f;

            int count = wholeText ? 1 : charLength;

            if (animationType == AnimationType.OneTime)
            {
                float maxEnd = 0f;
                for (int i = 0; i < count; i++)
                {
                    float d = GetCumulativeDelay(i, 0);
                    float dur = duration.Evaluate(0f, GetStableRandom(i, 0));
                    maxEnd = Mathf.Max(maxEnd, d + dur);
                }
                _totalEffectDuration = maxEnd;
            }
            else if (loopsCount >= 1)
            {
                float maxEnd = 0f;
                if (animationType == AnimationType.LoopFixedDuration && fixedDuration > 0f)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int lastCycle = loopsCount - 1;
                        float d = GetCumulativeDelay(i, lastCycle);
                        float start = lastCycle * fixedDuration + d;
                        float dur = duration.Evaluate(0f, GetStableRandom(i, lastCycle));
                        maxEnd = Mathf.Max(maxEnd, start + dur);
                    }
                }
                else // Loop or PingPong
                {
                    for (int i = 0; i < count; i++)
                    {
                        float d = GetCumulativeDelay(i, 0);
                        float totalDur = d;
                        for (int c = 0; c < loopsCount; c++)
                        {
                            totalDur += duration.Evaluate(0f, GetStableRandom(i, c));
                        }
                        maxEnd = Mathf.Max(maxEnd, totalDur);
                    }
                }
                _totalEffectDuration = maxEnd;
            }
        }

        public virtual void StopEffect()
        {
            started = false;
            isComplete = true;
        }

        protected float GetStableRandom(int seed1, int seed2)
        {
            float val = Mathf.Sin((seed1 + _playSeed) * 12.9898f + seed2 * 78.233f) * 43758.5453f;
            return Mathf.Abs(val - Mathf.Floor(val));
        }

        private float GetCumulativeDelay(int localIdx, int cycleIndex)
        {
            float total = 0f;
            for (int i = 1; i <= localIdx; i++)
                total += delayBetweenChars.Evaluate(0f, GetStableRandom(i, cycleIndex));
            return total;
        }

        protected float GetRandomSign(int _charIndex, int seedOffset = 60000)
        {
            var effectiveIndex = wholeText ? startCharIndex : _charIndex;
            GetTimeForChar(effectiveIndex, out int cycleIndex);
            
            var localIdx = effectiveIndex - startCharIndex;
            if (reverseCharOrder) localIdx = charLength - localIdx - 1;
            
            return GetStableRandom(localIdx, cycleIndex + seedOffset) > 0.5f ? -1f : 1f;
        }

        private float GetTimeForChar(int _charIndex, out int cycleIndex)
        {
            cycleIndex = 0;
            float time = TimeUtil.GetTime(timeType);
            float globalElapsed = time - startTime;

            if (_totalEffectDuration >= 0f && !isComplete)
            {
                if (globalElapsed >= _totalEffectDuration)
                {
                    isComplete = true;
                    currentEntry?.InvokeCompleted();
                }
            }

            int localIdx = _charIndex - startCharIndex;
            if (reverseCharOrder) localIdx = charLength - localIdx - 1;

            if (animationType == AnimationType.OneTime)
            {
                return globalElapsed - GetCumulativeDelay(localIdx, 0);
            }
            
            if (animationType == AnimationType.LoopFixedDuration && fixedDuration > 0f)
            {
                int globalCycle = (int)(globalElapsed / fixedDuration);
                
                while (_lastCompletedCycle < globalCycle)
                {
                    _lastCompletedCycle++;
                    if (loopsCount < 1 || _lastCompletedCycle <= loopsCount)
                    {
                        currentEntry?.InvokeCompleted();
                    }
                }

                int searchCycle = loopsCount >= 1 ? Mathf.Min(globalCycle, loopsCount - 1) : globalCycle;
                while (searchCycle > 0)
                {
                    float cDelay = GetCumulativeDelay(localIdx, searchCycle);
                    float cStart = searchCycle * fixedDuration + cDelay;
                    
                    if (globalElapsed >= cStart)
                    {
                        cycleIndex = searchCycle;
                        return globalElapsed - cStart;
                    }
                    
                    // It's before the start of cycle c. Search backwards.
                    float prevDelay = GetCumulativeDelay(localIdx, searchCycle - 1);
                    float prevStart = (searchCycle - 1) * fixedDuration + prevDelay;
                    float prevDur = duration.Evaluate(0f, GetStableRandom(localIdx, searchCycle - 1));
                    
                    if (globalElapsed < prevStart + prevDur)
                    {
                        // Active inside c-1 or before c-1. Continue searching backwards!
                        searchCycle--;
                    }
                    else
                    {
                        // Reached the dead gap resting space exclusively between c-1 and c!
                        cycleIndex = searchCycle;
                        return globalElapsed - cStart;
                    }
                }
                
                cycleIndex = 0;
                return globalElapsed - GetCumulativeDelay(localIdx, 0);
            }

            // Loop and PingPong continuous tracking
            float initialDelay = GetCumulativeDelay(localIdx, 0);
            float charElapsed = globalElapsed - initialDelay;
            
            if (charElapsed <= 0f) return charElapsed;

            int c = 0;
            float timeRemaining = charElapsed;
            while (true)
            {
                float dur = duration.Evaluate(0f, GetStableRandom(localIdx, c));
                if (dur <= 0.001f) { dur = 0.001f; } // Prevent infinite loop lock
                
                if (loopsCount >= 1 && c >= loopsCount - 1)
                {
                    if (timeRemaining >= dur)
                    {
                        cycleIndex = c;
                        return dur;
                    }
                }
                
                if (timeRemaining < dur)
                {
                    cycleIndex = c;
                    return timeRemaining;
                }
                
                timeRemaining -= dur;
                c++;
                
                // Safety guard
                if (c > 1000) { cycleIndex = c; return timeRemaining; }
            }
        }

        protected float EvaluateCurve(int _charIndex)
        {
            var effectiveIndex = wholeText ? startCharIndex : _charIndex;
            var elapsed = GetTimeForChar(effectiveIndex, out int cycleIndex);
            
            var localIdx = effectiveIndex - startCharIndex;
            if (reverseCharOrder) localIdx = charLength - localIdx - 1;
            
            float dur = duration.Evaluate(0f, GetStableRandom(localIdx, cycleIndex));
            float tValue = dur > 0f ? elapsed / dur : 0f;

            // Reason: Align evaluating timescale mathematically exactly with AnimationCurve wrapping 
            if (animationType == AnimationType.Loop || animationType == AnimationType.PingPong)
            {
                tValue = cycleIndex + tValue; 
            }
            
            float blend = GetStableRandom(localIdx, cycleIndex + 10000);
            
            float tFinal = easeCurve.Evaluate(tValue, blend);

            if (clampBetween0And1) tFinal = Mathf.Clamp01(tFinal);
            return tFinal;
        }

        protected float Interpolate(ParticleSystem.MinMaxCurve _start, ParticleSystem.MinMaxCurve _end, int _charIndex, int seedOffsetStart = 30000, int seedOffsetEnd = 40000)
        {
            var effectiveIndex = wholeText ? startCharIndex : _charIndex;
            var elapsed = GetTimeForChar(effectiveIndex, out int cycleIndex);
            
            var localIdx = effectiveIndex - startCharIndex;
            if (reverseCharOrder) localIdx = charLength - localIdx - 1;
            
            float dur = duration.Evaluate(0f, GetStableRandom(localIdx, cycleIndex));
            float tValue = dur > 0f ? elapsed / dur : 0f;

            if (animationType == AnimationType.Loop || animationType == AnimationType.PingPong)
            {
                tValue = cycleIndex + tValue; 
            }
            
            float blendCurve = GetStableRandom(localIdx, cycleIndex + 10000);
            float t = easeCurve.Evaluate(tValue, blendCurve);
            if (clampBetween0And1) t = Mathf.Clamp01(t);

            float blendStart = GetStableRandom(localIdx, cycleIndex + seedOffsetStart);
            float blendEnd = GetStableRandom(localIdx, cycleIndex + seedOffsetEnd);

            float s = _start.Evaluate(tValue, blendStart);
            float e = _end.Evaluate(tValue, blendEnd);

            return s * (1 - t) + e * t;
        }

        protected Vector2 Interpolate(
            ParticleSystem.MinMaxCurve _startX, ParticleSystem.MinMaxCurve _endX, 
            ParticleSystem.MinMaxCurve _startY, ParticleSystem.MinMaxCurve _endY, 
            int _charIndex, 
            int seedOffsetX = 50000, int seedOffsetY = 60000)
        {
            var effectiveIndex = wholeText ? startCharIndex : _charIndex;
            var elapsed = GetTimeForChar(effectiveIndex, out int cycleIndex);
            
            var localIdx = effectiveIndex - startCharIndex;
            if (reverseCharOrder) localIdx = charLength - localIdx - 1;
            
            float dur = duration.Evaluate(0f, GetStableRandom(localIdx, cycleIndex));
            float tValue = dur > 0f ? elapsed / dur : 0f;

            if (animationType == AnimationType.Loop || animationType == AnimationType.PingPong)
            {
                tValue = cycleIndex + tValue; 
            }
            
            float blendCurve = GetStableRandom(localIdx, cycleIndex + 10000);
            float t = easeCurve.Evaluate(tValue, blendCurve);
            if (clampBetween0And1) t = Mathf.Clamp01(t);

            float blendStartX = GetStableRandom(localIdx, cycleIndex + seedOffsetX + 1);
            float blendEndX = GetStableRandom(localIdx, cycleIndex + seedOffsetX + 2);
            float blendStartY = GetStableRandom(localIdx, cycleIndex + seedOffsetY + 1);
            float blendEndY = GetStableRandom(localIdx, cycleIndex + seedOffsetY + 2);

            float sx = _startX.Evaluate(tValue, blendStartX);
            float ex = _endX.Evaluate(tValue, blendEndX);
            float sy = _startY.Evaluate(tValue, blendStartY);
            float ey = _endY.Evaluate(tValue, blendEndY);

            return new Vector2(sx * (1 - t) + ex * t, sy * (1 - t) + ey * t);
        }

        protected float Interpolate(float _start, float _end, int _charIndex)
        {
            float t = EvaluateCurve(_charIndex);
            return _start * (1 - t) + _end * t;
        }

        protected Vector2 Interpolate(Vector2 _start, Vector2 _end, int _charIndex)
        {
            float t = EvaluateCurve(_charIndex);
            return _start * (1 - t) + _end * t;
        }

        protected Color Interpolate(Color _start, Color _end, int _charIndex)
        {
            float t = EvaluateCurve(_charIndex);
            return _start * (1 - t) + _end * t;
        }

        public virtual bool IsComplete => isComplete;

        public virtual TextEffectInstance Instantiate()
        {
            return Instantiate(this);
        }
    }
}