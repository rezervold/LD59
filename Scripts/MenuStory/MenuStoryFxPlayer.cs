using DG.Tweening;
using UnityEngine;

public enum MenuStoryFxCue
{
    None = 0,
    Signal = 1,
    Crack = 2
}

[DisallowMultipleComponent]
public class MenuStoryFxPlayer : MonoBehaviour
{
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField] private Juice signalJuice;
    [SerializeField] private Juice crackJuice;
    [SerializeField] private Juice letterJuice;
    [SerializeField] private Juice greetingJuice;
    [SerializeField] private Juice mechanicJuice;
    [SerializeField] private Juice hugeHordeJuice;
    private Tween shakeTween;
    private Vector2 shakeRestPosition;

    private void OnDestroy()
    {
        if (shakeTween != null && shakeTween.IsActive())
            shakeTween.Kill();
    }

    public void Play(MenuStoryFxCue cue)
    {
        switch (cue)
        {
            case MenuStoryFxCue.Signal:
                PlaySignal();
                break;

            case MenuStoryFxCue.Crack:
                PlayCrack();
                break;
        }
    }

    public void PlaySignal()
    {
        if (signalJuice != null)
            signalJuice.Play();

        Shake(24f, 0.4f, 24);
    }

    public void StopSignal()
    {
        if (signalJuice != null)
            signalJuice.Stop();
    }

    public void PlayCrack()
    {
        if (crackJuice != null)
            crackJuice.Play();

        Shake(42f, 0.34f, 28);
    }

    public void PlayLetter(char character)
    {
        if (!char.IsLetter(character))
            return;

        if (letterJuice != null)
            letterJuice.Play();
    }

    public void PlayGreeting()
    {
        if (greetingJuice != null)
            greetingJuice.Play();
    }

    public void PlayMechanic()
    {
        if (mechanicJuice != null)
            mechanicJuice.Play();
    }

    public void PlayHugeHorde()
    {
        if (hugeHordeJuice != null)
            hugeHordeJuice.Play();
    }

    private void Shake(float strength, float duration, int vibrato)
    {
        if (shakeTarget == null)
            return;

        if (shakeTween != null && shakeTween.IsActive())
        {
            shakeTween.Kill();
            shakeTarget.anchoredPosition = shakeRestPosition;
        }

        shakeRestPosition = shakeTarget.anchoredPosition;

        shakeTween = shakeTarget
            .DOShakeAnchorPos(duration, new Vector2(strength, strength * 0.45f), vibrato, 90f, false, true)
            .SetUpdate(true)
            .OnComplete(() => shakeTarget.anchoredPosition = shakeRestPosition);
    }
}
