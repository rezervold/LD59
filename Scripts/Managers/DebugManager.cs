using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DebugManager : MonoBehaviour
{
    [Serializable]
    public class FloatEvent : UnityEvent<float>
    {
    }

    [Serializable]
    private class DebugAdjustableValue
    {
        [SerializeField] private string name;
        [SerializeField] private KeyCode selectKey = KeyCode.None;
        [SerializeField] private float startValue;
        [SerializeField] private float step = 1f;
        [SerializeField] private bool clampValue;
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 10f;
        [SerializeField] private bool applyStartValueOnAwake = true;
        [SerializeField] private FloatEvent onValueChanged;

        public string Name => name;
        public KeyCode SelectKey => selectKey;
        public float CurrentValue { get; private set; }

        public void Initialize()
        {
            CurrentValue = startValue;

            if (applyStartValueOnAwake)
                onValueChanged.Invoke(CurrentValue);
        }

        public void Increase()
        {
            SetValue(CurrentValue + step);
        }

        public void Decrease()
        {
            SetValue(CurrentValue - step);
        }

        public void SetValue(float value)
        {
            CurrentValue = clampValue ? Mathf.Clamp(value, minValue, maxValue) : value;
            onValueChanged.Invoke(CurrentValue);
        }
    }

    [Serializable]
    private class DebugHotkeyAction
    {
        [SerializeField] private KeyCode key = KeyCode.None;
        [SerializeField] private UnityEvent action;

        public KeyCode Key => key;

        public void Trigger()
        {
            action.Invoke();
        }
    }

    private enum DebugValueTarget
    {
        TimeScale,
        Gravity,
        Extra
    }

    public static DebugManager Instance { get; private set; }

    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private KeyCode decreaseKey = KeyCode.Minus;
    [SerializeField] private KeyCode increaseKey = KeyCode.Equals;
    [SerializeField] private KeyCode reloadSceneKey = KeyCode.R;

    [SerializeField] private KeyCode selectTimeScaleKey = KeyCode.T;
    [SerializeField] private float timeScaleStep = 0.2f;
    [SerializeField] private float minTimeScale = 0f;
    [SerializeField] private float maxTimeScale = 5f;

    [SerializeField] private KeyCode selectGravityKey = KeyCode.G;
    [SerializeField] private float gravityStep = 0.5f;
    [SerializeField] private float minGravity = 0f;
    [SerializeField] private float maxGravity = 50f;

    [SerializeField] private List<DebugAdjustableValue> extraAdjustableValues = new List<DebugAdjustableValue>();
    [SerializeField] private List<DebugHotkeyAction> extraHotkeyActions = new List<DebugHotkeyAction>();

    public string CurrentTargetName { get; private set; }

    private DebugValueTarget currentTarget = DebugValueTarget.TimeScale;
    private int currentExtraValueIndex = -1;
    private float currentTimeScale;
    private float currentGravity;
    private float defaultFixedDeltaTime;

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

        defaultFixedDeltaTime = Time.fixedDeltaTime;
        currentTimeScale = Time.timeScale;
        currentGravity = Mathf.Abs(Physics.gravity.y);
        CurrentTargetName = "TimeScale";

        for (int i = 0; i < extraAdjustableValues.Count; i++)
            extraAdjustableValues[i].Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        HandleTargetSelection();
        HandleValueChange();
        HandleHotkeyActions();
    }

    public void ReloadCurrentScene()
    {
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.ReloadCurrentScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void SetTimeScale(float value)
    {
        currentTimeScale = Mathf.Clamp(value, minTimeScale, maxTimeScale);
        Time.timeScale = currentTimeScale;
        Time.fixedDeltaTime = currentTimeScale > 0f ? defaultFixedDeltaTime * currentTimeScale : 0f;
    }

    public void SetGravity(float value)
    {
        currentGravity = Mathf.Clamp(value, minGravity, maxGravity);
        Physics.gravity = new Vector3(Physics.gravity.x, -currentGravity, Physics.gravity.z);
    }

    private void HandleTargetSelection()
    {
        if (Input.GetKeyDown(selectTimeScaleKey))
        {
            currentTarget = DebugValueTarget.TimeScale;
            currentExtraValueIndex = -1;
            CurrentTargetName = "TimeScale";
        }

        if (Input.GetKeyDown(selectGravityKey))
        {
            currentTarget = DebugValueTarget.Gravity;
            currentExtraValueIndex = -1;
            CurrentTargetName = "Gravity";
        }

        for (int i = 0; i < extraAdjustableValues.Count; i++)
        {
            if (!Input.GetKeyDown(extraAdjustableValues[i].SelectKey))
                continue;

            currentTarget = DebugValueTarget.Extra;
            currentExtraValueIndex = i;
            CurrentTargetName = extraAdjustableValues[i].Name;
        }
    }

    private void HandleValueChange()
    {
        if (Input.GetKeyDown(increaseKey))
            ChangeActiveValue(true);

        if (Input.GetKeyDown(decreaseKey))
            ChangeActiveValue(false);
    }

    private void HandleHotkeyActions()
    {
        if (Input.GetKeyDown(reloadSceneKey))
            ReloadCurrentScene();

        for (int i = 0; i < extraHotkeyActions.Count; i++)
        {
            if (Input.GetKeyDown(extraHotkeyActions[i].Key))
                extraHotkeyActions[i].Trigger();
        }
    }

    private void ChangeActiveValue(bool increase)
    {
        switch (currentTarget)
        {
            case DebugValueTarget.TimeScale:
                SetTimeScale(currentTimeScale + (increase ? timeScaleStep : -timeScaleStep));
                break;

            case DebugValueTarget.Gravity:
                SetGravity(currentGravity + (increase ? gravityStep : -gravityStep));
                break;

            case DebugValueTarget.Extra:
                if (currentExtraValueIndex < 0 || currentExtraValueIndex >= extraAdjustableValues.Count)
                    return;

                if (increase)
                    extraAdjustableValues[currentExtraValueIndex].Increase();
                else
                    extraAdjustableValues[currentExtraValueIndex].Decrease();
                break;
        }
    }
}
