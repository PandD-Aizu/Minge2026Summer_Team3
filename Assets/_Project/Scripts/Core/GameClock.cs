using _Project.Scripts.Core;
using VContainer.Unity;
using Unity;
using UnityEngine;

public class GameClock : ITickable
{
    private GameProgress _gameProgress;

    public GameClock(GameProgress gameProgress)
    {
        _gameProgress = gameProgress;
    }

    public void Tick()
    {
        _gameProgress.AdvanceGameTimeSeconds(Time.deltaTime * 60.0f);
    }


}
