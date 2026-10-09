using System;
using _Project.Scripts.Fishing;
using Fishing;
using MiniGame;
using R3;
using VContainer.Unity;

public class FishingInteractPresenter : IDisposable, IInitializable
{
    private readonly PlayerInputReader _inputReader;
    private readonly FishingSpot _fishingSpot;
    private readonly FishingTargetProvider _targetProvider;
    private readonly MiniGameFlowPresenter _miniGameFlowPresenter;

    private readonly CompositeDisposable _disposables = new();

    public FishingInteractPresenter(
        PlayerInputReader inputReader,
        FishingSpot fishingSpot,
        FishingTargetProvider targetProvider,
        MiniGameFlowPresenter miniGameFlowPresenter)
    {
        _inputReader = inputReader;
        _fishingSpot = fishingSpot;
        _targetProvider = targetProvider;
        _miniGameFlowPresenter = miniGameFlowPresenter;
    }

    public void Initialize()
    {
        _inputReader.OnInteractPressed
            .Where(_ => _inputReader.CanStartGameplayAction && _fishingSpot.CanInteractShadow)
            .Subscribe(_ =>
            {
                // 釣り開始直前に他のスポットの釣果も反映して重複を避ける
                var fish = _targetProvider.GetFishForCatch();
                if (fish != null) _miniGameFlowPresenter.StartMiniGame(fish);
            })
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

}
