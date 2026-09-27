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
            .Where(_ => _fishingSpot.CanInteractShadow)
            .Where(_ => _targetProvider.FishDefinition != null)
            .Subscribe(_ => _miniGameFlowPresenter.StartMiniGame(_targetProvider.FishDefinition))
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

}
