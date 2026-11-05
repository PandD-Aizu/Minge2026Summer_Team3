using System.Threading;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;

namespace MiniGame
{
    /// <summary>個別ゲームの抽選と実行を担当し、釣り道具や入力モードには依存しない</summary>
    public sealed class MiniGameFlowPresenter
    {
        private readonly MiniGameRegistry _registry;
        private readonly MiniGameSelector _selector;

        /// <summary>登録済みゲームと抽選器を受け取る</summary>
        /// <param name="registry">ゲームの登録一覧</param>
        /// <param name="selector">魚ごとの抽選器</param>
        /// <example>VContainerから生成する</example>
        public MiniGameFlowPresenter(MiniGameRegistry registry, MiniGameSelector selector)
        { _registry = registry; _selector = selector; }

        /// <summary>合わせ成功後にゲームを一度抽選して実行する</summary>
        /// <param name="fish">確保した対象魚</param>
        /// <param name="cancellation">釣り全体の中断トークン</param>
        /// <returns>ゲームの成績</returns>
        /// <example>await flow.PlayAsync(fish, token)</example>
        public async UniTask<MiniGameResult> PlayAsync(FishDefinition fish, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var settings = _selector.Select(fish, () => UnityEngine.Random.value);
            _registry.TryGet(settings, out var controller);
            return await controller.PlayAsync(settings, fish, cancellation);
        }
    }
}
