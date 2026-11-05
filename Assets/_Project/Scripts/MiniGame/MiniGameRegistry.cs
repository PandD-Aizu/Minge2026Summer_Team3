using System;
using System.Collections.Generic;
using _Project.Scripts.Data.Enum;

namespace MiniGame
{
    /// <summary>DIで登録されたゲームを種類から解決する</summary>
    public sealed class MiniGameRegistry
    {
        private readonly Dictionary<MiniGameType, IMiniGameController> _controllers = new();

        /// <summary>すべての実行元を登録し、同じ種類の二重登録を拒否する</summary>
        /// <param name="controllers">VContainerが集めた実行元</param>
        /// <example>新しいゲームはLifetimeScopeのIMiniGameController登録だけで追加する</example>
        public MiniGameRegistry(IEnumerable<IMiniGameController> controllers)
        {
            foreach (var controller in controllers)
                if (!_controllers.TryAdd(controller.GameType, controller))
                    throw new ArgumentException($"ミニゲームの登録が重複: {controller.GameType}");
        }

        /// <summary>設定に対応する実行元を取得する</summary>
        /// <param name="settings">確認する設定</param>
        /// <param name="controller">対応する実行元、未対応ならnull</param>
        /// <returns>実行可能ならtrue</returns>
        /// <example>抽選候補に含める前にTryGetを呼ぶ</example>
        public bool TryGet(MiniGameSettings settings, out IMiniGameController controller)
        {
            controller = null;
            return settings != null && _controllers.TryGetValue(settings.GameType, out controller)
                && controller.CanPlay(settings);
        }
    }
}
