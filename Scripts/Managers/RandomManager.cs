using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

[DisallowMultipleComponent]
public class RandomManager : MonoBehaviour
{
    public static RandomManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;

    private readonly byte[] randomBytes = new byte[8];
    private RandomNumberGenerator randomNumberGenerator;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        randomNumberGenerator = RandomNumberGenerator.Create();

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        randomNumberGenerator?.Dispose();
        Instance = null;
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        long range = (long)maxExclusive - minInclusive;

        if (range <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));

        ulong limit = ulong.MaxValue - (ulong.MaxValue % (ulong)range);
        ulong value;

        do
        {
            randomNumberGenerator.GetBytes(randomBytes);
            value = BitConverter.ToUInt64(randomBytes, 0);
        }
        while (value >= limit);

        return minInclusive + (int)(value % (ulong)range);
    }

    public float Range(float minInclusive, float maxExclusive)
    {
        return Mathf.Lerp(minInclusive, maxExclusive, Value01());
    }

    public double Range(double minInclusive, double maxExclusive)
    {
        return minInclusive + ((maxExclusive - minInclusive) * Value01Double());
    }

    public bool Bool()
    {
        return (Byte() & 1) == 0;
    }

    public byte Byte()
    {
        randomNumberGenerator.GetBytes(randomBytes);
        return randomBytes[0];
    }

    public void Bytes(byte[] buffer)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        randomNumberGenerator.GetBytes(buffer);
    }

    public float Value01()
    {
        return (float)Value01Double();
    }

    public double Value01Double()
    {
        randomNumberGenerator.GetBytes(randomBytes);
        ulong raw = BitConverter.ToUInt64(randomBytes, 0);
        raw >>= 11;
        return raw * (1.0 / 9007199254740992.0);
    }

    public Vector2 Vector2(float minX, float maxX, float minY, float maxY)
    {
        return new Vector2(Range(minX, maxX), Range(minY, maxY));
    }

    public Vector3 Vector3(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        return new Vector3(Range(minX, maxX), Range(minY, maxY), Range(minZ, maxZ));
    }

    public Color Color(bool randomAlpha = false)
    {
        float alpha = randomAlpha ? Value01() : 1f;
        return new Color(Value01(), Value01(), Value01(), alpha);
    }

    public T Item<T>(IList<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        if (items.Count == 0)
            throw new InvalidOperationException("Collection is empty.");

        return items[Range(0, items.Count)];
    }

    public bool Chance(float chance)
    {
        return Value01() < Mathf.Clamp01(chance);
    }
}
