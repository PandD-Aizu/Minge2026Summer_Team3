using _Project.Scripts.Data.Enum;
using R3;

namespace MiniGame
{
    public interface IMiniGameController
    {
        MiniGameType GameType { get; }
        Observable<MiniGameResult> Completed { get; }
        Observable<Unit> Canceled { get; }

        void StartGame(MiniGameSettings settings);
        void EndGame();
    }
}
