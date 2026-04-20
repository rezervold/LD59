using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraShakeManager : MonoBehaviour
{
    public enum ShakeType
    {
        Noise,
        Sine
    }

    [Serializable]
    public class ShakeSettings
    {
        [SerializeField] private ShakeType type = ShakeType.Noise;
        [SerializeField] public float amplitude = 0.2f;
        [SerializeField] public float frequency = 10f;
        [SerializeField] public float rotationIntensity = 1f;
        [SerializeField] public float attackTime = 0.05f;
        [SerializeField] private AnimationCurve attackCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] public float sustainTime = 0.1f;
        [SerializeField] public float decayTime = 0.2f;
        [SerializeField] private AnimationCurve decayCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private bool useUnscaledTime = true;

        public ShakeType Type => type;
        public float Amplitude => amplitude;
        public float Frequency => frequency;
        public float RotationIntensity => rotationIntensity;
        public float AttackTime => attackTime;
        public AnimationCurve AttackCurve => attackCurve;
        public float SustainTime => sustainTime;
        public float DecayTime => decayTime;
        public AnimationCurve DecayCurve => decayCurve;
        public bool UseUnscaledTime => useUnscaledTime;

        public static ShakeSettings Default => new ShakeSettings();
    }

    public struct ShakeFrameData
    {
        public Vector2 PositionOffset;
        public float RotationOffset;
    }

    private class ShakeInstance
    {
        private readonly ShakeSettings settings;
        private readonly float totalDuration;
        private readonly Vector2 noiseSeed;
        private readonly float rotationSeed;

        private float timer;

        public ShakeInstance(ShakeSettings settings)
        {
            this.settings = settings;
            totalDuration = settings.AttackTime + settings.SustainTime + settings.DecayTime;
            timer = totalDuration;
            noiseSeed = new Vector2(UnityEngine.Random.value * 1000f, UnityEngine.Random.value * 1000f);
            rotationSeed = UnityEngine.Random.value * 1000f;
        }

        public bool IsFinished => timer <= 0f;

        public void Update(ref Vector2 positionOffset, ref float rotationOffset)
        {
            float deltaTime = settings.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float sampleTime = settings.UseUnscaledTime ? Time.unscaledTime : Time.time;

            timer -= deltaTime;

            float elapsed = totalDuration - timer;
            float envelope = EvaluateEnvelope(elapsed);
            float input = sampleTime * settings.Frequency;

            float posX;
            float posY;
            float rot;

            if (settings.Type == ShakeType.Sine)
            {
                posX = Mathf.Sin(input);
                posY = Mathf.Cos(input * 0.9f);
                rot = Mathf.Sin(input * 1.1f);
            }
            else
            {
                posX = Mathf.PerlinNoise(noiseSeed.x, input) * 2f - 1f;
                posY = Mathf.PerlinNoise(noiseSeed.y, input) * 2f - 1f;
                rot = Mathf.PerlinNoise(rotationSeed, input) * 2f - 1f;
            }

            positionOffset.x += posX * settings.Amplitude * envelope;
            positionOffset.y += posY * settings.Amplitude * envelope;
            rotationOffset += rot * settings.RotationIntensity * envelope;
        }

        private float EvaluateEnvelope(float elapsed)
        {
            if (elapsed <= settings.AttackTime)
            {
                float t = settings.AttackTime > 0f ? elapsed / settings.AttackTime : 1f;
                return settings.AttackCurve.Evaluate(t);
            }

            if (elapsed <= settings.AttackTime + settings.SustainTime)
                return 1f;

            float decayElapsed = elapsed - settings.AttackTime - settings.SustainTime;
            float decayT = settings.DecayTime > 0f ? decayElapsed / settings.DecayTime : 1f;
            return settings.DecayCurve.Evaluate(decayT);
        }
    }

    public static CameraShakeManager Instance { get; private set; }

    public static event Action<ShakeFrameData> OnShakeUpdated;

    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly List<ShakeInstance> activeShakes = new List<ShakeInstance>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        Vector2 positionOffset = Vector2.zero;
        float rotationOffset = 0f;

        for (int i = activeShakes.Count - 1; i >= 0; i--)
        {
            activeShakes[i].Update(ref positionOffset, ref rotationOffset);

            if (activeShakes[i].IsFinished)
                activeShakes.RemoveAt(i);
        }

        OnShakeUpdated?.Invoke(new ShakeFrameData
        {
            PositionOffset = positionOffset,
            RotationOffset = rotationOffset
        });
    }

    public void Shake(ShakeSettings settings)
    {
        activeShakes.Add(new ShakeInstance(settings));
    }

    public void Shake(float intensity, float duration, float rotationIntensity = 0f)
    {
        ShakeSettings settings = ShakeSettings.Default;
        float clampedDuration = Mathf.Max(duration, 0f);

        settings.amplitude = intensity;
        settings.rotationIntensity = rotationIntensity;
        settings.attackTime = clampedDuration * 0.2f;
        settings.sustainTime = clampedDuration * 0.2f;
        settings.decayTime = clampedDuration * 0.6f;
        Shake(settings);
    }

    public void StopAllShakes()
    {
        activeShakes.Clear();
    }
}
