using DG.Tweening;
using TMPro;
using UnityEngine;

public enum MenuDialogueLineStyle
{
    Default = 0,
    Week = 1,
    Signal = 2
}

[DisallowMultipleComponent]
public class MenuDialogueScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private CanvasGroup boxCanvasGroup;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Color defaultBodyColor = Color.white;
    [SerializeField] private Color weekBodyColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    [SerializeField] private Color signalBodyColor = new Color(0.88f, 0.16f, 0.16f, 1f);
    private Tween rootTween;
    private Tween boxTween;

    public bool IsInputActive => rootCanvasGroup != null && rootCanvasGroup.blocksRaycasts;

    private void Reset()
    {
        if (rootCanvasGroup == null)
            rootCanvasGroup = GetComponent<CanvasGroup>();

        if (boxCanvasGroup == null)
            boxCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (speakerText == null || bodyText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);

            if (texts.Length > 0 && speakerText == null)
                speakerText = texts[0];

            if (texts.Length > 1 && bodyText == null)
                bodyText = texts[1];
        }
    }

    public void Show()
    {
        if (rootCanvasGroup == null || boxCanvasGroup == null)
            return;

        if (rootCanvasGroup.blocksRaycasts && boxCanvasGroup.alpha >= 0.99f)
        {
            KillTweens();
            rootCanvasGroup.alpha = 1f;
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
            return;
        }

        KillTweens();
        rootCanvasGroup.alpha = 1f;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;
        boxCanvasGroup.alpha = 0f;
        boxTween = boxCanvasGroup.DOFade(1f, 0.18f).SetUpdate(true);
    }

    public void Hide()
    {
        if (rootCanvasGroup == null || boxCanvasGroup == null)
            return;

        KillTweens();
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        rootTween = rootCanvasGroup.DOFade(0f, 0.16f).SetUpdate(true);
        boxTween = boxCanvasGroup.DOFade(0f, 0.16f).SetUpdate(true);
    }

    public void HideImmediate()
    {
        if (rootCanvasGroup == null || boxCanvasGroup == null)
            return;

        KillTweens();
        rootCanvasGroup.alpha = 0f;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        boxCanvasGroup.alpha = 0f;
    }

    public void SetSpeaker(string speaker)
    {
        if (speakerText == null)
            return;

        bool hasSpeaker = !string.IsNullOrEmpty(speaker);
        speakerText.gameObject.SetActive(hasSpeaker);
        speakerText.text = hasSpeaker ? speaker : string.Empty;
    }

    public void SetText(string text)
    {
        if (bodyText == null)
            return;

        bodyText.text = text ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
    }

    public int GetCharacterCount()
    {
        if (bodyText == null)
            return 0;

        bodyText.ForceMeshUpdate();
        return bodyText.textInfo.characterCount;
    }

    public void SetVisibleCharacters(int count)
    {
        if (bodyText == null)
            return;

        bodyText.maxVisibleCharacters = count;
    }

    public bool TryGetCharacter(int index, out char character)
    {
        character = '\0';

        if (bodyText == null || index < 0)
            return false;

        bodyText.ForceMeshUpdate();

        if (index >= bodyText.textInfo.characterCount)
            return false;

        character = bodyText.textInfo.characterInfo[index].character;
        return true;
    }

    public void SetLineStyle(MenuDialogueLineStyle style)
    {
        if (bodyText == null)
            return;

        switch (style)
        {
            case MenuDialogueLineStyle.Week:
                bodyText.color = weekBodyColor;
                break;

            case MenuDialogueLineStyle.Signal:
                bodyText.color = signalBodyColor;
                break;

            default:
                bodyText.color = defaultBodyColor;
                break;
        }
    }

    private void KillTweens()
    {
        if (rootTween != null && rootTween.IsActive())
            rootTween.Kill();

        if (boxTween != null && boxTween.IsActive())
            boxTween.Kill();

        rootTween = null;
        boxTween = null;
    }
}
