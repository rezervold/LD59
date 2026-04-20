using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class Juice : MonoBehaviour
{
    private enum JuiceItemType
    {
        Delay,
        ToggleGameObject,
        Destination,
        Particle,
        Audio,
        CameraShake
    }

    [Serializable]
    private class AxisSelection
    {
        [SerializeField] private bool x = true;
        [SerializeField] private bool y = true;
        [SerializeField] private bool z = true;

        public bool X => x;
        public bool Y => y;
        public bool Z => z;
    }

    [Serializable]
    private class DelayFeedback
    {
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private bool useUnscaledTime = true;

        public float Duration => duration;
        public bool UseUnscaledTime => useUnscaledTime;
    }

    [Serializable]
    private class ToggleGameObjectFeedback
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool active = true;

        public GameObject Target => target;
        public bool Active => active;
    }

    [Serializable]
    private class DestinationFeedback
    {
        [SerializeField] private Transform target;
        [SerializeField] private Transform startTransform;
        [SerializeField] private Transform endTransform;
        [SerializeField] private bool useLocalSpace = true;
        [SerializeField] private AxisSelection positionAxes = new AxisSelection();
        [SerializeField] private AxisSelection rotationAxes = new AxisSelection();
        [SerializeField] private float duration = 0.35f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private bool useUnscaledTime = true;

        public Transform Target => target;
        public Transform StartTransform => startTransform;
        public Transform EndTransform => endTransform;
        public bool UseLocalSpace => useLocalSpace;
        public AxisSelection PositionAxes => positionAxes;
        public AxisSelection RotationAxes => rotationAxes;
        public float Duration => duration;
        public AnimationCurve Curve => curve;
        public bool UseUnscaledTime => useUnscaledTime;
    }

    [Serializable]
    private class ParticleFeedback
    {
        [SerializeField] private ParticleSystem particleSystem;
        [SerializeField] private bool restartBeforePlay = true;

        public ParticleSystem ParticleSystem => particleSystem;
        public bool RestartBeforePlay => restartBeforePlay;
    }

    [Serializable]
    private class AudioFeedback
    {
        [SerializeField] private AudioManager.SoundData sound;
        [SerializeField] private bool playAtTargetPosition;
        [SerializeField] private Transform targetPosition;

        public AudioManager.SoundData Sound => sound;
        public bool PlayAtTargetPosition => playAtTargetPosition;
        public Transform TargetPosition => targetPosition;
    }

    [Serializable]
    private class CameraShakeFeedback
    {
        [SerializeField] private CameraShakeManager.ShakeSettings settings = CameraShakeManager.ShakeSettings.Default;

        public CameraShakeManager.ShakeSettings Settings => settings;
    }

    [Serializable]
    private class JuiceItem
    {
        [SerializeField] private JuiceItemType type;
        [SerializeField] private DelayFeedback delay = new DelayFeedback();
        [SerializeField] private ToggleGameObjectFeedback toggleGameObject = new ToggleGameObjectFeedback();
        [SerializeField] private DestinationFeedback destination = new DestinationFeedback();
        [SerializeField] private ParticleFeedback particle = new ParticleFeedback();
        [SerializeField] private AudioFeedback audio = new AudioFeedback();
        [SerializeField] private CameraShakeFeedback cameraShake = new CameraShakeFeedback();

        public JuiceItemType Type => type;
        public DelayFeedback Delay => delay;
        public ToggleGameObjectFeedback ToggleGameObject => toggleGameObject;
        public DestinationFeedback Destination => destination;
        public ParticleFeedback Particle => particle;
        public AudioFeedback Audio => audio;
        public CameraShakeFeedback CameraShake => cameraShake;
    }

    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool restartIfAlreadyPlaying = true;
    [SerializeField] private List<JuiceItem> items = new List<JuiceItem>();

    public bool IsPlaying { get; private set; }

    private Coroutine playRoutine;
    private Tween activeTween;
    private readonly List<AudioSource> activeLoopSources = new List<AudioSource>();

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (IsPlaying)
        {
            if (!restartIfAlreadyPlaying)
                return;

            Stop();
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void PlayItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return;

        if (IsPlaying)
            Stop();

        playRoutine = StartCoroutine(PlaySingleItemRoutine(index));
    }

    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();

        activeTween = null;
        StopActiveLoopSources();
        IsPlaying = false;
    }

    private IEnumerator PlayRoutine()
    {
        IsPlaying = true;

        for (int i = 0; i < items.Count; i++)
            yield return PlayItemRoutine(items[i]);

        playRoutine = null;
        IsPlaying = false;
    }

    private IEnumerator PlaySingleItemRoutine(int index)
    {
        IsPlaying = true;
        yield return PlayItemRoutine(items[index]);
        playRoutine = null;
        IsPlaying = false;
    }

    private IEnumerator PlayItemRoutine(JuiceItem item)
    {
        switch (item.Type)
        {
            case JuiceItemType.Delay:
                yield return PlayDelay(item.Delay);
                break;

            case JuiceItemType.ToggleGameObject:
                item.ToggleGameObject.Target.SetActive(item.ToggleGameObject.Active);
                break;

            case JuiceItemType.Destination:
                yield return PlayDestination(item.Destination);
                break;

            case JuiceItemType.Particle:
                PlayParticle(item.Particle);
                break;

            case JuiceItemType.Audio:
                PlayAudio(item.Audio);
                break;

            case JuiceItemType.CameraShake:
                PlayCameraShake(item.CameraShake);
                break;
        }
    }

    private IEnumerator PlayDelay(DelayFeedback delay)
    {
        if (delay.Duration <= 0f)
            yield break;

        if (delay.UseUnscaledTime)
            yield return new WaitForSecondsRealtime(delay.Duration);
        else
            yield return new WaitForSeconds(delay.Duration);
    }

    private IEnumerator PlayDestination(DestinationFeedback destination)
    {
        Transform target = destination.Target;

        Vector3 initialPosition = destination.UseLocalSpace ? target.localPosition : target.position;
        Vector3 initialRotation = destination.UseLocalSpace ? target.localEulerAngles : target.eulerAngles;

        Vector3 startPosition = destination.UseLocalSpace ? destination.StartTransform.localPosition : destination.StartTransform.position;
        Vector3 endPosition = destination.UseLocalSpace ? destination.EndTransform.localPosition : destination.EndTransform.position;
        Vector3 startRotation = destination.UseLocalSpace ? destination.StartTransform.localEulerAngles : destination.StartTransform.eulerAngles;
        Vector3 endRotation = destination.UseLocalSpace ? destination.EndTransform.localEulerAngles : destination.EndTransform.eulerAngles;

        SetPosition(target, MergePosition(initialPosition, startPosition, destination.PositionAxes), destination.UseLocalSpace);
        SetRotation(target, MergeRotation(initialRotation, startRotation, destination.RotationAxes), destination.UseLocalSpace);

        if (destination.Duration <= 0f)
        {
            SetPosition(target, MergePosition(initialPosition, endPosition, destination.PositionAxes), destination.UseLocalSpace);
            SetRotation(target, MergeRotation(initialRotation, endRotation, destination.RotationAxes), destination.UseLocalSpace);
            yield break;
        }

        activeTween = DOVirtual.Float(0f, 1f, destination.Duration, value =>
        {
            float curveValue = destination.Curve.Evaluate(value);
            Vector3 currentPosition = Vector3.LerpUnclamped(startPosition, endPosition, curveValue);
            Vector3 currentRotation = new Vector3(
                Mathf.LerpAngle(startRotation.x, endRotation.x, curveValue),
                Mathf.LerpAngle(startRotation.y, endRotation.y, curveValue),
                Mathf.LerpAngle(startRotation.z, endRotation.z, curveValue)
            );

            SetPosition(target, MergePosition(initialPosition, currentPosition, destination.PositionAxes), destination.UseLocalSpace);
            SetRotation(target, MergeRotation(initialRotation, currentRotation, destination.RotationAxes), destination.UseLocalSpace);
        })
        .SetEase(Ease.Linear)
        .SetUpdate(destination.UseUnscaledTime);

        yield return activeTween.WaitForCompletion();
        activeTween = null;
    }

    private void PlayParticle(ParticleFeedback particle)
    {
        if (particle.RestartBeforePlay)
            particle.ParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        particle.ParticleSystem.Play(true);
    }

    private void PlayAudio(AudioFeedback audio)
    {
        AudioSource source;

        if (audio.PlayAtTargetPosition)
            source = AudioManager.Instance?.Play(audio.Sound, audio.TargetPosition.position);
        else
            source = AudioManager.Instance?.Play(audio.Sound);

        if (source == null || audio.Sound == null || !audio.Sound.Loop)
            return;

        if (!activeLoopSources.Contains(source))
            activeLoopSources.Add(source);
    }

    private void PlayCameraShake(CameraShakeFeedback cameraShake)
    {
        CameraShakeManager.Instance?.Shake(cameraShake.Settings);
    }

    private Vector3 MergePosition(Vector3 currentValue, Vector3 targetValue, AxisSelection axes)
    {
        return new Vector3(
            axes.X ? targetValue.x : currentValue.x,
            axes.Y ? targetValue.y : currentValue.y,
            axes.Z ? targetValue.z : currentValue.z
        );
    }

    private Vector3 MergeRotation(Vector3 currentValue, Vector3 targetValue, AxisSelection axes)
    {
        return new Vector3(
            axes.X ? targetValue.x : currentValue.x,
            axes.Y ? targetValue.y : currentValue.y,
            axes.Z ? targetValue.z : currentValue.z
        );
    }

    private void SetPosition(Transform target, Vector3 value, bool useLocalSpace)
    {
        if (useLocalSpace)
            target.localPosition = value;
        else
            target.position = value;
    }

    private void SetRotation(Transform target, Vector3 value, bool useLocalSpace)
    {
        if (useLocalSpace)
            target.localEulerAngles = value;
        else
            target.eulerAngles = value;
    }

    private void StopActiveLoopSources()
    {
        for (int i = 0; i < activeLoopSources.Count; i++)
        {
            if (activeLoopSources[i] == null)
                continue;

            AudioManager.Instance?.Stop(activeLoopSources[i]);
        }

        activeLoopSources.Clear();
    }
}
