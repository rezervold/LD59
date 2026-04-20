using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    [Serializable]
    public class SoundData
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private Vector2 volumeRange = Vector2.one;
        [SerializeField] private Vector2 pitchRange = Vector2.one;
        [SerializeField] private AudioMixerGroup mixerGroup;
        [SerializeField] [Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private bool loop;

        public AudioClip[] Clips => clips;
        public Vector2 VolumeRange => volumeRange;
        public Vector2 PitchRange => pitchRange;
        public AudioMixerGroup MixerGroup => mixerGroup;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public bool Loop => loop;
    }

    private class PooledSource
    {
        public AudioSource source;
        public int playId;
    }

    public static AudioManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private int initialPoolSize = 8;

    private readonly List<PooledSource> pooledSources = new List<PooledSource>();

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

        for (int i = 0; i < initialPoolSize; i++)
            CreateSource();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public AudioSource Play(SoundData soundData)
    {
        return Play(soundData, transform.position, false);
    }

    public AudioSource Play(SoundData soundData, Vector3 worldPosition)
    {
        return Play(soundData, worldPosition, true);
    }

    public void Stop(AudioSource source)
    {
        for (int i = 0; i < pooledSources.Count; i++)
        {
            if (pooledSources[i].source != source)
                continue;

            pooledSources[i].playId++;
            ReleaseSource(pooledSources[i]);
            return;
        }
    }

    private AudioSource Play(SoundData soundData, Vector3 worldPosition, bool useWorldPosition)
    {
        AudioClip clip = GetRandomClip(soundData.Clips);

        if (clip == null)
            return null;

        PooledSource pooledSource = GetAvailableSource();
        pooledSource.playId++;

        AudioSource source = pooledSource.source;
        source.gameObject.SetActive(true);
        source.transform.SetParent(transform, false);

        if (useWorldPosition)
            source.transform.position = worldPosition;
        else
            source.transform.localPosition = Vector3.zero;

        source.clip = clip;
        source.outputAudioMixerGroup = soundData.MixerGroup;
        source.volume = GetRandomFloat(soundData.VolumeRange);
        source.pitch = GetRandomFloat(soundData.PitchRange);
        source.loop = soundData.Loop;
        source.spatialBlend = soundData.SpatialBlend;
        source.minDistance = soundData.MinDistance;
        source.maxDistance = soundData.MaxDistance;
        source.Play();

        if (!soundData.Loop)
        {
            float duration = clip.length / Mathf.Max(Mathf.Abs(source.pitch), 0.01f);
            StartCoroutine(ReleaseSourceRoutine(pooledSource, pooledSource.playId, duration));
        }

        return source;
    }

    private IEnumerator ReleaseSourceRoutine(PooledSource pooledSource, int playId, float duration)
    {
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);

        if (pooledSource.playId != playId)
            yield break;

        ReleaseSource(pooledSource);
    }

    private void ReleaseSource(PooledSource pooledSource)
    {
        AudioSource source = pooledSource.source;
        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.loop = false;
        source.gameObject.SetActive(false);
        source.transform.SetParent(transform, false);
    }

    private PooledSource GetAvailableSource()
    {
        for (int i = 0; i < pooledSources.Count; i++)
        {
            if (!pooledSources[i].source.gameObject.activeSelf)
                return pooledSources[i];
        }

        return CreateSource();
    }

    private PooledSource CreateSource()
    {
        GameObject sourceObject = new GameObject("PooledAudioSource");
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        sourceObject.SetActive(false);

        PooledSource pooledSource = new PooledSource
        {
            source = source
        };

        pooledSources.Add(pooledSource);
        return pooledSource;
    }

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int index = RandomManager.Instance != null
            ? RandomManager.Instance.Range(0, clips.Length)
            : UnityEngine.Random.Range(0, clips.Length);

        return clips[index];
    }

    private float GetRandomFloat(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);

        if (Mathf.Approximately(min, max))
            return min;

        return RandomManager.Instance != null
            ? RandomManager.Instance.Range(min, max)
            : UnityEngine.Random.Range(min, max);
    }
}
