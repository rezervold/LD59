using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MenuStoryDirector : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string GameplaySceneName = "ArenaPit";
    private const string EndSceneName = "EndScene";

    private static readonly char[] CensorSymbols = { '@', '!', '#', '0' };
    private static readonly Regex FuckRegex = new Regex("(?i)fucking|fuck", RegexOptions.Compiled);

    [SerializeField] private MenuDialogueScreen dialogueScreen;
    [SerializeField] private MenuStoryFxPlayer fxPlayer;
    [SerializeField] private UpgradesScreen upgradesScreen;
    [SerializeField] private MenuNarratorPortrait narratorPortrait;
    [SerializeField] private Image fadeOverlay;
    [SerializeField] private float characterDelay = 0.026f;
    [SerializeField] private float defaultAutoSkipDelay = 0.9f;
    [SerializeField] private float postUpgradeDialogueDelay = 1.5f;
    [SerializeField] private float firstEncounterFadeDuration = 9f;
    [SerializeField] private float finalEncounterCrackDelay = 2f;
    [SerializeField] private float finalEncounterSignalStopDelay = 1f;
    [SerializeField] private float finalEncounterSilenceDelay = 2f;
    [SerializeField] private float endSceneFadeDuration = 1.1f;

    private Coroutine storyRoutine;
    private Tween fadeOverlayTween;
    private bool advanceRequested;
    private bool firstUpgradePurchased;
    private bool lastLineWasAdvancedByPlayer;

    private readonly List<InlinePauseMarker> inlinePauseMarkers = new List<InlinePauseMarker>();

    private readonly struct DialogueChunk
    {
        public readonly string Text;
        public readonly float PauseAfter;

        public DialogueChunk(string text, float pauseAfter = 0f)
        {
            Text = text ?? string.Empty;
            PauseAfter = Mathf.Max(0f, pauseAfter);
        }
    }

    private readonly struct InlinePauseMarker
    {
        public readonly int CharacterIndex;
        public readonly float PauseAfter;

        public InlinePauseMarker(int characterIndex, float pauseAfter)
        {
            CharacterIndex = characterIndex;
            PauseAfter = pauseAfter;
        }
    }

    private sealed class DialogueLineSpec
    {
        public MenuDialogueLineStyle Style;
        public DialogueChunk[] Chunks;
        public bool WaitForAdvance;
        public float AutoContinueDelay;
        public bool CensorProfanity;
        public bool IsNarratorLine;
        public MenuNarratorMood NarratorMood;
    }

    private void Start()
    {
        storyRoutine = StartCoroutine(InitializeRoutine());
    }

    private void Update()
    {
        if (dialogueScreen == null || !dialogueScreen.IsInputActive)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            advanceRequested = true;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            advanceRequested = true;
            return;
        }

        if (Input.touchCount <= 0)
            return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase != TouchPhase.Began)
                continue;

            advanceRequested = true;
            return;
        }
    }

    private void OnDestroy()
    {
        if (storyRoutine != null)
            StopCoroutine(storyRoutine);

        if (fadeOverlayTween != null && fadeOverlayTween.IsActive())
            fadeOverlayTween.Kill();

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.UpgradeStateChanged -= HandleUpgradeStateChanged;
    }

    private IEnumerator InitializeRoutine()
    {
        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
        {
            enabled = false;
            yield break;
        }

        if (dialogueScreen == null)
        {
            Debug.LogError("MenuStoryDirector requires a MenuDialogueScreen reference.", this);
            enabled = false;
            yield break;
        }

        ResetEncounterVisuals();
        yield return RunStoryForCurrentLevel();
    }

    private IEnumerator RunStoryForCurrentLevel()
    {
        int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;

        switch (currentLevel)
        {
            case 1:
                yield return RunEncounterOne();
                break;

            case 2:
                yield return RunEncounterTwo();
                break;

            case 3:
                yield return RunEncounterThree();
                break;

            case 4:
                yield return RunEncounterFour();
                break;

            default:
                ResetEncounterVisuals();
                SetContinueVisible(true);
                break;
        }
    }

    private IEnumerator RunEncounterOne()
    {
        ResetEncounterVisuals();
        SetContinueVisible(false);

        if (fxPlayer != null)
            fxPlayer.PlayGreeting();

        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("We live in a shelter.", 0.5f),
            Chunk(" Not too shabby. ", .5f),
            Chunk("I'd even call it cozy"));

        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("But the second we hear that SIGNAL...", 1f),
            Chunk(" all that cozy shit goes right out the fucking window"));

        yield return PlaySignalLine();

        yield return PlayNarratorLine(
            MenuNarratorMood.Scared,
            false,
            Chunk("What the fuck?!", 0.5f),
            Chunk(" You gotta be shitting me!"));

        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("Whole fucking day is ruined now."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Get your lazy ass out of that..."));

        yield return PlayNarratorLine(
            MenuNarratorMood.Scared,
            false,
            Chunk("...what even is that? ", 0.5f),
            Chunk("A fucking gamer chair?!"));

        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Get up and do your fucking job!"));

        if (narratorPortrait != null)
            narratorPortrait.GoAway(false);

        StartFadeToGameplay();

        bool outroFastForwarded = false;

        yield return PlayCensoredLine(true, Chunk("God bless our fucking shelter..."));
        outroFastForwarded |= lastLineWasAdvancedByPlayer;
        yield return PlayCensoredLine(true, Chunk("Signal goes off every fucking Tuesday. Why always Tuesday?"));
        outroFastForwarded |= lastLineWasAdvancedByPlayer;
        yield return PlayCensoredLine(true, Chunk("Wait, last time it was Monday"));
        outroFastForwarded |= lastLineWasAdvancedByPlayer;
        yield return PlayCensoredLine(true, Chunk("Fuck Mondays. ", 2f), Chunk("Fuck Tuesdays."));
        outroFastForwarded |= lastLineWasAdvancedByPlayer;

        if (outroFastForwarded && fadeOverlayTween != null && fadeOverlayTween.IsActive())
        {
            fadeOverlayTween.Kill();
            fadeOverlayTween = null;
            LoadGameplayScene();
            yield break;
        }

        if (fadeOverlay == null)
            LoadGameplayScene();
    }

    private IEnumerator RunEncounterTwo()
    {
        ResetEncounterVisuals();
        SetContinueVisible(false);

        yield return PlayWeekLine("One week later");

        if (fxPlayer != null)
            fxPlayer.PlayMechanic();

        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("Hey. Our mechanic has a...", 1f),
            Chunk(" weird fucking fetish"));

        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("He loves collecting zombie chunks. Don't ask me why"));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("I don't wanna fucking know. He gets the job done, so whatever.."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Bring him enough chunks, and he'll pimp out your ride"));

        dialogueScreen.Hide();
        yield return WaitForFirstUpgradePurchase();
        yield return WaitRealtime(postUpgradeDialogueDelay);

        dialogueScreen.Show();
        yield return PlaySignalLine();
        yield return PlayNarratorLine(
            MenuNarratorMood.Scared,
            false,
            Chunk("Oh my.."));
        
        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("Well..", 0.5f),
            Chunk(" You know what the fuck to do"));

        dialogueScreen.Hide();
        SetContinueVisible(true);
    }

    private IEnumerator RunEncounterThree()
    {
        ResetEncounterVisuals();
        SetContinueVisible(false);

        yield return PlayWeekLine("One week later");
        yield return PlaySignalLine();

        if (fxPlayer != null)
            fxPlayer.PlayHugeHorde();

        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("Jesus Christ..."));
        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("My boy...", 1f),
        Chunk(" the radar says this one is fucking massive"));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Look,", .5f),
        Chunk(" I m not good at this emotional bullshit", .5f),
        Chunk(", but..."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("It was an honor knowing you, kid.", .8f),
        Chunk(" Seriously."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("..."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("..."));

        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("If you die, I'm taking your fucking boots.", .8f),
            Chunk(" Just leave them here, you know... ", 1f),
            Chunk("Ah, screw it"));

        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Now go out there and be a hero."));

        dialogueScreen.Hide();
        SetContinueVisible(true);
    }

    private IEnumerator RunEncounterFour()
    {
        ResetEncounterVisuals();
        SetContinueVisible(false);

        yield return PlayWeekLine("One week later");
        yield return PlaySignalLine();

        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("GOD DAMN IT!"));
        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("FUCK THAT NOISE!"));
        yield return PlayNarratorLine(MenuNarratorMood.Scared, false, Chunk("Why don't you smash that fucking megaphone already?!"));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("..."));
        yield return PlayNarratorLine(
            MenuNarratorMood.Calm,
            false,
            Chunk("You know what?", 0.5f),
            Chunk(" FUCK IT!"));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("I'LL DO IT MY FUCKING SELF!"));

        float awayDelay = narratorPortrait != null ? narratorPortrait.GoAway(true) : 0f;

        if (awayDelay > 0f)
            yield return WaitRealtime(awayDelay);

        if (finalEncounterCrackDelay > 0f)
            yield return WaitRealtime(finalEncounterCrackDelay);

        if (fxPlayer != null)
            fxPlayer.PlayCrack();

        if (finalEncounterSignalStopDelay > 0f)
            yield return WaitRealtime(finalEncounterSignalStopDelay);

        if (fxPlayer != null)
            fxPlayer.StopSignal();

        if (finalEncounterSilenceDelay > 0f)
            yield return WaitRealtime(finalEncounterSilenceDelay);

        float returnDelay = narratorPortrait != null ? narratorPortrait.Return(MenuNarratorMood.Calm) : 0f;

        if (returnDelay > 0f)
            yield return WaitRealtime(returnDelay);

        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Ah..."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("Much fucking better."));
        yield return PlayNarratorLine(MenuNarratorMood.Calm, false, Chunk("I love Tuesdays."));

        yield return FadeToScene(EndSceneName, endSceneFadeDuration);
    }

    private IEnumerator PlayWeekLine(string text)
    {
        yield return PlayLine(CreateLine(
            MenuDialogueLineStyle.Week,
            true,
            0f,
            false,
            false,
            MenuNarratorMood.Calm,
            Chunk(text)));
    }

    private IEnumerator PlaySignalLine()
    {
        if (fxPlayer != null)
            fxPlayer.PlaySignal();

        if (narratorPortrait != null)
            narratorPortrait.EnsureVisible(MenuNarratorMood.Scared);

        yield return PlayLine(CreateLine(
            MenuDialogueLineStyle.Signal,
            true,
            0f,
            false,
            false,
            MenuNarratorMood.Calm,
            Chunk("*SIGNAL*")));
    }

    private IEnumerator PlayNarratorLine(MenuNarratorMood mood, bool autoSkip, params DialogueChunk[] chunks)
    {
        yield return PlayLine(CreateLine(
            MenuDialogueLineStyle.Default,
            !autoSkip,
            autoSkip ? defaultAutoSkipDelay : 0f,
            true,
            true,
            mood,
            chunks));
    }

    private IEnumerator PlayCensoredLine(bool autoSkip, params DialogueChunk[] chunks)
    {
        yield return PlayLine(CreateLine(
            MenuDialogueLineStyle.Default,
            !autoSkip,
            autoSkip ? defaultAutoSkipDelay : 0f,
            true,
            false,
            MenuNarratorMood.Calm,
            chunks));
    }

    private IEnumerator PlayLine(DialogueLineSpec line)
    {
        if (dialogueScreen == null)
            yield break;

        advanceRequested = false;
        lastLineWasAdvancedByPlayer = false;
        bool lineWasAdvancedByPlayer = false;

        if (line.IsNarratorLine)
        {
            if (narratorPortrait != null)
                narratorPortrait.EnsureVisible(line.NarratorMood);
        }

        dialogueScreen.Show();
        dialogueScreen.SetSpeaker(string.Empty);
        dialogueScreen.SetLineStyle(line.Style);

        string fullText = BuildLineText(line.Chunks, inlinePauseMarkers);

        if (line.CensorProfanity)
            fullText = CensorFuckWords(fullText);

        dialogueScreen.SetText(fullText);

        int totalCharacters = dialogueScreen.GetCharacterCount();
        int visibleCharacters = 0;
        int nextPauseIndex = 0;
        bool skipRequested = false;

        while (visibleCharacters < totalCharacters)
        {
            if (ConsumeAdvance())
            {
                lineWasAdvancedByPlayer = true;
                skipRequested = true;
                break;
            }

            visibleCharacters += 1;
            dialogueScreen.SetVisibleCharacters(visibleCharacters);
            PlayLetterJuice(visibleCharacters - 1);

            while (nextPauseIndex < inlinePauseMarkers.Count &&
                   inlinePauseMarkers[nextPauseIndex].CharacterIndex <= visibleCharacters)
            {
                InlinePauseMarker marker = inlinePauseMarkers[nextPauseIndex];
                nextPauseIndex += 1;

                if (marker.PauseAfter > 0f)
                    yield return WaitSkippable(marker.PauseAfter, () =>
                    {
                        lineWasAdvancedByPlayer = true;
                        skipRequested = true;
                    });

                if (skipRequested)
                    break;
            }

            if (skipRequested)
                break;

            yield return WaitSkippable(characterDelay, () =>
            {
                lineWasAdvancedByPlayer = true;
                skipRequested = true;
            });

            if (skipRequested)
                break;
        }

        dialogueScreen.SetVisibleCharacters(totalCharacters);
        advanceRequested = false;

        if (line.AutoContinueDelay > 0f)
            yield return WaitSkippable(line.AutoContinueDelay, () => lineWasAdvancedByPlayer = true);

        if (line.WaitForAdvance)
            yield return WaitForAdvance(() => lineWasAdvancedByPlayer = true);

        lastLineWasAdvancedByPlayer = lineWasAdvancedByPlayer;
    }

    private IEnumerator WaitForAdvance(System.Action onAdvance)
    {
        while (true)
        {
            if (ConsumeAdvance())
            {
                onAdvance?.Invoke();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForFirstUpgradePurchase()
    {
        while (UpgradeManager.Instance == null)
            yield return null;

        firstUpgradePurchased = false;
        UpgradeManager.Instance.UpgradeStateChanged += HandleUpgradeStateChanged;

        while (!firstUpgradePurchased)
            yield return null;

        UpgradeManager.Instance.UpgradeStateChanged -= HandleUpgradeStateChanged;
    }

    private void HandleUpgradeStateChanged(PlayerUpgradeType upgradeType, bool isActive)
    {
        if (!isActive)
            return;

        firstUpgradePurchased = true;
    }

    private IEnumerator WaitSkippable(float duration, System.Action onSkip)
    {
        float time = 0f;

        while (time < duration)
        {
            if (ConsumeAdvance())
            {
                onSkip?.Invoke();
                yield break;
            }

            time += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitRealtime(float duration)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool ConsumeAdvance()
    {
        if (!advanceRequested)
            return false;

        advanceRequested = false;
        return true;
    }

    private void ResetEncounterVisuals()
    {
        advanceRequested = false;
        lastLineWasAdvancedByPlayer = false;

        if (fxPlayer != null)
            fxPlayer.StopSignal();

        if (dialogueScreen != null)
        {
            dialogueScreen.HideImmediate();
            dialogueScreen.SetSpeaker(string.Empty);
            dialogueScreen.SetLineStyle(MenuDialogueLineStyle.Default);
        }

        if (narratorPortrait != null)
            narratorPortrait.HideImmediate();

        ResetFadeOverlay();
    }

    private void ResetFadeOverlay()
    {
        if (fadeOverlayTween != null && fadeOverlayTween.IsActive())
            fadeOverlayTween.Kill();

        fadeOverlayTween = null;

        if (fadeOverlay == null)
            return;

        Color color = fadeOverlay.color;
        color.a = 0f;
        fadeOverlay.color = color;
    }

    private void StartFadeToGameplay()
    {
        if (fadeOverlay == null || firstEncounterFadeDuration <= 0f)
        {
            LoadGameplayScene();
            return;
        }

        if (fadeOverlayTween != null && fadeOverlayTween.IsActive())
            fadeOverlayTween.Kill();

        Color color = fadeOverlay.color;
        color.a = 0f;
        fadeOverlay.color = color;

        fadeOverlayTween = fadeOverlay
            .DOFade(1f, firstEncounterFadeDuration)
            .SetUpdate(true)
            .OnComplete(LoadGameplayScene);
    }

    private IEnumerator FadeToScene(string sceneName, float duration)
    {
        if (fadeOverlay == null || duration <= 0f)
        {
            LoadScene(sceneName);
            yield break;
        }

        if (fadeOverlayTween != null && fadeOverlayTween.IsActive())
            fadeOverlayTween.Kill();

        Color color = fadeOverlay.color;
        color.a = 0f;
        fadeOverlay.color = color;

        bool completed = false;

        fadeOverlayTween = fadeOverlay
            .DOFade(1f, duration)
            .SetUpdate(true)
            .OnComplete(() => completed = true);

        while (!completed)
            yield return null;

        LoadScene(sceneName);
    }

    private string BuildLineText(IReadOnlyList<DialogueChunk> chunks, List<InlinePauseMarker> markers)
    {
        markers.Clear();

        if (chunks == null || chunks.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < chunks.Count; i++)
        {
            DialogueChunk chunk = chunks[i];
            builder.Append(chunk.Text);

            if (chunk.PauseAfter > 0f)
                markers.Add(new InlinePauseMarker(builder.Length, chunk.PauseAfter));
        }

        return builder.ToString();
    }

    private string CensorFuckWords(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return FuckRegex.Replace(text, match => CensorWord(match.Value));
    }

    private string CensorWord(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length <= 3)
            return word;

        char[] chars = word.ToCharArray();
        chars[1] = CensorSymbols[GetRandomIndex(CensorSymbols.Length)];
        chars[2] = CensorSymbols[GetRandomIndex(CensorSymbols.Length)];

        return new string(chars);
    }

    private void PlayLetterJuice(int characterIndex)
    {
        if (fxPlayer == null || dialogueScreen == null || characterIndex < 0)
            return;

        if (!dialogueScreen.TryGetCharacter(characterIndex, out char character))
            return;

        fxPlayer.PlayLetter(character);
    }

    private int GetRandomIndex(int maxExclusive)
    {
        if (maxExclusive <= 1)
            return 0;

        if (RandomManager.Instance != null)
            return RandomManager.Instance.Range(0, maxExclusive);

        return Random.Range(0, maxExclusive);
    }

    private void SetContinueVisible(bool visible)
    {
        if (upgradesScreen == null)
            return;

        upgradesScreen.SetContinueVisible(visible);
    }

    private void LoadGameplayScene()
    {
        LoadScene(GameplaySceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning(sceneName + " is not available in Build Settings.");
            return;
        }

        if (GameManager.Instance != null && sceneName == GameplaySceneName)
        {
            GameManager.Instance.LoadGameplayScene();
            return;
        }

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private DialogueLineSpec CreateLine(
        MenuDialogueLineStyle style,
        bool waitForAdvance,
        float autoContinueDelay,
        bool censorProfanity,
        bool isNarratorLine,
        MenuNarratorMood narratorMood,
        params DialogueChunk[] chunks)
    {
        return new DialogueLineSpec
        {
            Style = style,
            WaitForAdvance = waitForAdvance,
            AutoContinueDelay = Mathf.Max(0f, autoContinueDelay),
            CensorProfanity = censorProfanity,
            IsNarratorLine = isNarratorLine,
            NarratorMood = narratorMood,
            Chunks = chunks ?? System.Array.Empty<DialogueChunk>()
        };
    }

    private static DialogueChunk Chunk(string text, float pauseAfter = 0f)
    {
        return new DialogueChunk(text, pauseAfter);
    }
}
