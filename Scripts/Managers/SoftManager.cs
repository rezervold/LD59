using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SoftManager : MonoBehaviour
{
    public static SoftManager Instance { get; private set; }

    public int Balance { get; private set; }

    public event Action<int, int> BalanceChanged;

    public static SoftManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("SoftManager");
        return root.AddComponent<SoftManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Balance = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Add(int amount)
    {
        if (amount <= 0)
            return;

        SetBalance(Balance + amount);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0)
            return true;

        if (Balance < amount)
            return false;

        SetBalance(Balance - amount);
        return true;
    }

    public void SetBalance(int amount)
    {
        int nextValue = Mathf.Max(0, amount);
        int delta = nextValue - Balance;

        if (delta == 0)
            return;

        Balance = nextValue;
        BalanceChanged?.Invoke(Balance, delta);
    }
}
