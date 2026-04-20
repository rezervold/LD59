using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public enum MenuNarratorMood
{
    Calm = 0,
    Scared = 1
}

[DisallowMultipleComponent]
public class MenuNarratorPortrait : MonoBehaviour
{
    [SerializeField] private RectTransform portraitRoot;
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite calmSprite;
    [SerializeField] private Sprite scaredSprite;
    [SerializeField] private Sprite backSprite;
    [SerializeField] private AudioSource[] voiceSources;
    [SerializeField] private float appearDuration = 0.2f;
    [SerializeField] private float appearOffsetY = 18f;
    [SerializeField] private float awayDuration = 0.55f;
    [SerializeField] private float fastAwayDuration = 0.32f;
    [SerializeField] private float awayOffsetY = -92f;
    [SerializeField] private float awayScale = 0.72f;
    [SerializeField] private float awayRotationShake = 10f;
    [SerializeField] private float returnDuration = 0.48f;
    [SerializeField] private float voiceFadeDuration = 0.4f;
    [SerializeField] private float awayVoiceVolumeMultiplier = 0.2f;

    private Vector2 visiblePosition;
    private Vector3 visibleScale;
    private Color visibleColor;
    private MenuNarratorMood currentMood = MenuNarratorMood.Calm;
    private bool isVisible;
    private float[] cachedVoiceVolumes;

    private Tween positionTween;
    private Tween scaleTween;
    private Tween colorTween;
    private Tween rotationTween;
    private Tween[] voiceTweens;

    private void Awake()
    {
        if (portraitRoot == null)
            portraitRoot = transform as RectTransform;

        if (portraitImage == null)
            portraitImage = GetComponentInChildren<Image>(true);

        visiblePosition = portraitRoot.anchoredPosition;
        visibleScale = portraitRoot.localScale;
        visibleColor = portraitImage != null ? portraitImage.color : Color.white;

        if (voiceSources != null && voiceSources.Length > 0)
        {
            cachedVoiceVolumes = new float[voiceSources.Length];

            for (int i = 0; i < voiceSources.Length; i++)
            {
                if (voiceSources[i] == null)
                    continue;

                cachedVoiceVolumes[i] = voiceSources[i].volume;
            }
        }

        HideImmediate();
    }

    public float AwayDuration => awayDuration;
    public float FastAwayDuration => fastAwayDuration;
    public float ReturnDuration => returnDuration;

    public void HideImmediate()
    {
        KillTweens();
        isVisible = false;

        if (portraitRoot != null)
        {
            portraitRoot.anchoredPosition = visiblePosition;
            portraitRoot.localScale = visibleScale;
            portraitRoot.localRotation = Quaternion.identity;
        }

        if (portraitImage != null)
        {
            Color hiddenColor = visibleColor;
            hiddenColor.a = 0f;
            portraitImage.color = hiddenColor;
            portraitImage.enabled = false;
        }

        RestoreVoiceImmediate();
    }

    public void EnsureVisible(MenuNarratorMood mood)
    {
        currentMood = mood;

        if (portraitRoot == null || portraitImage == null)
            return;

        KillTweens();
        portraitImage.enabled = true;
        portraitImage.sprite = ResolveSprite(mood, false);

        if (!isVisible)
        {
            Color startColor = visibleColor;
            startColor.a = 0f;
            portraitImage.color = startColor;
            portraitRoot.anchoredPosition = visiblePosition + new Vector2(0f, appearOffsetY);
            portraitRoot.localScale = visibleScale * 0.96f;
            portraitRoot.localRotation = Quaternion.identity;

            positionTween = portraitRoot.DOAnchorPos(visiblePosition, appearDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            scaleTween = portraitRoot.DOScale(visibleScale, appearDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            colorTween = portraitImage.DOColor(visibleColor, appearDuration).SetUpdate(true);
        }
        else
        {
            positionTween = portraitRoot.DOAnchorPos(visiblePosition, returnDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            scaleTween = portraitRoot.DOScale(visibleScale, returnDuration).SetEase(Ease.OutCubic).SetUpdate(true);
            colorTween = portraitImage.DOColor(visibleColor, returnDuration).SetUpdate(true);
            portraitRoot.localRotation = Quaternion.identity;
        }

        isVisible = true;
        FadeVoices(false);
    }

    public float GoAway(bool fast)
    {
        if (portraitRoot == null || portraitImage == null)
            return 0f;

        float duration = fast ? fastAwayDuration : awayDuration;

        KillTweens();
        isVisible = true;
        portraitImage.enabled = true;
        portraitImage.sprite = ResolveSprite(currentMood, true);

        positionTween = portraitRoot
            .DOAnchorPos(visiblePosition + new Vector2(0f, awayOffsetY), duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);

        scaleTween = portraitRoot
            .DOScale(visibleScale * awayScale, duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);

        colorTween = portraitImage
            .DOColor(Color.black, duration)
            .SetUpdate(true);

        rotationTween = portraitRoot
            .DOShakeRotation(duration, new Vector3(0f, 0f, awayRotationShake), 18, 90f, false)
            .SetUpdate(true)
            .OnComplete(() => portraitRoot.localRotation = Quaternion.identity);

        FadeVoices(true);
        return duration;
    }

    public float Return(MenuNarratorMood mood)
    {
        if (portraitRoot == null || portraitImage == null)
            return 0f;

        currentMood = mood;
        KillTweens();
        isVisible = true;
        portraitImage.enabled = true;
        portraitImage.sprite = ResolveSprite(mood, false);

        positionTween = portraitRoot
            .DOAnchorPos(visiblePosition, returnDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        scaleTween = portraitRoot
            .DOScale(visibleScale, returnDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true);

        colorTween = portraitImage
            .DOColor(visibleColor, returnDuration)
            .SetUpdate(true);

        portraitRoot.localRotation = Quaternion.identity;
        FadeVoices(false);
        return returnDuration;
    }

    private Sprite ResolveSprite(MenuNarratorMood mood, bool back)
    {
        if (back && backSprite != null)
            return backSprite;

        switch (mood)
        {
            case MenuNarratorMood.Scared:
                if (scaredSprite != null)
                    return scaredSprite;
                break;
        }

        return calmSprite;
    }

    private void FadeVoices(bool toAway)
    {
        if (voiceSources == null || voiceSources.Length == 0 || cachedVoiceVolumes == null)
            return;

        if (voiceTweens == null || voiceTweens.Length != voiceSources.Length)
            voiceTweens = new Tween[voiceSources.Length];

        for (int i = 0; i < voiceSources.Length; i++)
        {
            AudioSource source = voiceSources[i];

            if (source == null)
                continue;

            if (voiceTweens[i] != null && voiceTweens[i].IsActive())
                voiceTweens[i].Kill();

            float targetVolume = cachedVoiceVolumes[i] * (toAway ? awayVoiceVolumeMultiplier : 1f);
            voiceTweens[i] = source.DOFade(targetVolume, voiceFadeDuration).SetUpdate(true);
        }
    }

    private void RestoreVoiceImmediate()
    {
        if (voiceSources == null || cachedVoiceVolumes == null)
            return;

        for (int i = 0; i < voiceSources.Length && i < cachedVoiceVolumes.Length; i++)
        {
            if (voiceSources[i] == null)
                continue;

            voiceSources[i].volume = cachedVoiceVolumes[i];
        }
    }

    private void KillTweens()
    {
        if (positionTween != null && positionTween.IsActive())
            positionTween.Kill();

        if (scaleTween != null && scaleTween.IsActive())
            scaleTween.Kill();

        if (colorTween != null && colorTween.IsActive())
            colorTween.Kill();

        if (rotationTween != null && rotationTween.IsActive())
            rotationTween.Kill();

        if (voiceTweens != null)
        {
            for (int i = 0; i < voiceTweens.Length; i++)
            {
                if (voiceTweens[i] != null && voiceTweens[i].IsActive())
                    voiceTweens[i].Kill();
            }
        }

        positionTween = null;
        scaleTween = null;
        colorTween = null;
        rotationTween = null;
    }
}
