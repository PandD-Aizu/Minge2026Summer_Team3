using System;
using _Project.Scripts.Core;
using VContainer.Unity;
using R3;



public class TimeOfDayPresenter : IInitializable, IDisposable
{
    private readonly GameProgress _gameProgress;
    private readonly TimeOfDayView _timeOfDayView;
    private IDisposable _subscription;

    public TimeOfDayPresenter(GameProgress gameProgress, TimeOfDayView timeOfDayView)
    {
        _gameProgress =  gameProgress;
        _timeOfDayView = timeOfDayView;

    }

    public void Initialize()
    {
        ChangeTime(_gameProgress.CurrentTimeOfDay);
        _subscription = _gameProgress.TimeOfDayChanged.Subscribe(ChangeTime);
    }

    private void ChangeTime(TimeOfDay timeOfDay)
    {
        _timeOfDayView.ApplyTimeToSky(timeOfDay);
    }


    public void Dispose()
    {
        _subscription?.Dispose();
    }




}
