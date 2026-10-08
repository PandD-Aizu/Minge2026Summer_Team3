using System;
using System.Threading;
using Controller;
using Cysharp.Threading.Tasks;
using Dialogue;
using R3;
using UnityEngine;
using VContainer;

/// <summary>チュートリアル段階に応じて釣り場で主人公の独り言を再生する</summary>
public sealed class FishingMonologuePresenter : MonoBehaviour
{
    [Serializable]
    private sealed class StepDialogue
    {
        [SerializeField] private TutorialStep _step = TutorialStep.None;
        [SerializeField] private DialogueData _dialogue;

        public TutorialStep Step => _step;
        public DialogueData Dialogue => _dialogue;
    }

    [SerializeField] private StepDialogue[] _stepDialogues = Array.Empty<StepDialogue>();

    private GameplayDialogueController _dialogueController;
    private IDisposable _stepSubscription;
    private CancellationTokenSource _playCancellation;

    /// <summary>チュートリアル進行と会話再生を接続し、段階変更を購読する</summary>
    /// <param name="tutorialController">進行段階の通知元</param>
    /// <param name="dialogueController">操作を制限して会話を再生するController</param>
    /// <example>FishingSceneLifetimeScopeからこのコンポーネントへInjectする</example>
    [Inject]
    public void Construct(TutorialController tutorialController, GameplayDialogueController dialogueController)
    {
        _dialogueController = dialogueController;
        _stepSubscription?.Dispose();
        _stepSubscription = tutorialController.OnStepChanged.Subscribe(PlayForStep);
    }

    /// <summary>変更後の段階に対応する独り言を再生する</summary>
    /// <param name="step">変更後のチュートリアル段階</param>
    /// <example>初回釣果後のReturnToCollectionAtNightを受けて呼ぶ</example>
    private void PlayForStep(TutorialStep step)
    {
        if (_stepDialogues == null) return;
        if (_playCancellation != null) return;

        foreach (var entry in _stepDialogues)
        {
            if (entry == null || entry.Step != step || entry.Dialogue == null) continue;

            PlayAsync(entry.Dialogue).Forget();
            return;
        }
    }

    /// <summary>指定した独り言を再生し、Presenter無効化時に中断する</summary>
    /// <param name="dialogue">再生するDialogueData</param>
    /// <returns>会話終了まで待つUniTask</returns>
    /// <example>段階に一致したStepDialogueから呼ぶ</example>
    private async UniTask PlayAsync(DialogueData dialogue)
    {
        var cancellation = new CancellationTokenSource();
        _playCancellation = cancellation;

        try
        {
            await _dialogueController.PlayAsync(dialogue, cancellation.Token);
        }
        finally
        {
            _playCancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>無効化時に再生中の独り言を中断する</summary>
    /// <example>釣り場を離れてPresenterが無効になるときにUnityが呼ぶ</example>
    private void OnDisable() => _playCancellation?.Cancel();

    /// <summary>段階変更の購読を解放する</summary>
    /// <example>シーンアンロード時にUnityが呼ぶ</example>
    private void OnDestroy() => _stepSubscription?.Dispose();
}
