using _Project.Scripts.Core;
using Controller;
using VContainer.Unity;
using UnityEngine;

public class GameClock : ITickable
{
    private readonly GameProgress _gameProgress;
    private readonly ClockController _clockController;

    /// <summary>共有の進行データと時計の停止管理を受け取る</summary>
    /// <param name="gameProgress">累積ゲーム時間の更新先</param>
    /// <param name="clockController">タイトルや会話などによる停止要求の共有先</param>
    /// <example>GameLifetimeScopeのEntryPointとして生成する</example>
    public GameClock(GameProgress gameProgress, ClockController clockController)
    {
        _gameProgress = gameProgress;
        _clockController = clockController;
    }

    /// <summary>停止要求がなければ、timeScale適用後の経過秒数の60倍だけ時計を進める</summary>
    /// <example>VContainerが毎フレーム呼ぶ ポーズ中はTime.deltaTimeが0になる</example>
    public void Tick()
    {
        // 自動進行だけを止め、チュートリアルなどからの明示的な時刻変更は許可する
        if (_clockController.IsPaused) return;

        _gameProgress.AdvanceGameTimeSeconds(Time.deltaTime * 60.0f);
    }


}
