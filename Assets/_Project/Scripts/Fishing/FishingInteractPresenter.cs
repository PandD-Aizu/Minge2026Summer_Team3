using System;
using _Project.Scripts.Fishing;
using _Project.Scripts.InteractableObject;
using MiniGame;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;


public class FishingInteractPresenter : IDisposable, IInitializable
{
    private readonly PlayerInputReader _inputReader;
    private readonly RotationMiniGameController _rotationMiniGameController;
    private readonly FishingSpot _fishingSpot;

    private readonly CompositeDisposable _disposables = new ();


    public FishingInteractPresenter(PlayerInputReader inputReader,
        RotationMiniGameController gameController, FishingSpot fishingSpot)
    {
        _inputReader = inputReader;
        _fishingSpot = fishingSpot;
        _rotationMiniGameController = gameController;
    }

    public void Initialize()
    {
        _inputReader.OnInteractPressed
            .Where(_ => !_inputReader.IsMenuOpen && _fishingSpot.CanInteractShadow)
            .Subscribe(_ => _rotationMiniGameController.StartGame())
            .AddTo(_disposables);

        _inputReader.OnInteractPressed.Subscribe(_ => Test()).AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private void Test()
    {
        Debug.Log("Jが押された");
    }





}
