using _Project.Scripts.Data.Enum;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Project.Scripts.Data.Fish;

namespace MiniGame
{
    public interface IMiniGameController
    {
        MiniGameType GameType { get; }
        /// <summary>設定がこの実行元に対応するかを返す</summary>
        /// <param name="settings">魚から選んだ設定</param>
        /// <returns>実行可能ならtrue</returns>
        /// <example>抽選前の登録検証で使用する</example>
        bool CanPlay(MiniGameSettings settings);

        /// <summary>ゲームを実行し、UIを片付けて成績を返す 中断はキャンセル例外で通知する</summary>
        /// <param name="settings">今回だけ使用する読み取り専用設定</param>
        /// <param name="fish">表示対象の魚</param>
        /// <param name="cancellation">共通フローが所有する中断トークン</param>
        /// <returns>成功または失敗の成績</returns>
        /// <example>await controller.PlayAsync(settings, fish, token)</example>
        UniTask<MiniGameResult> PlayAsync(MiniGameSettings settings, FishDefinition fish, CancellationToken cancellation);
    }
}
