using _Project.Scripts.Core;
using UnityEngine;

namespace InventoryData
{
    /// <summary>シーンをまたぐ松明と気配消しの使用期限を管理する</summary>
    public sealed class ConsumableEffects
    {
        public const int TorchId = 4000;
        public const int PotionId = 3000;
        private readonly GameProgress _clock;
        private readonly Inventory _inventory;
        private float _torchUntil;
        private float _cloakUntil;

        public bool TorchLit => Time.time < _torchUntil;
        public bool Cloaked => _clock.GameSeconds.CurrentValue < _cloakUntil;

        /// <summary>地面への設置が成功した松明を手から離す</summary>
        /// <example>設置時には点火済みの1本を移すため、所持数は再度減らさない</example>
        public void ReleaseTorch() => _torchUntil = 0f;

        /// <summary>Rootが共有する時計と所持品を使用する</summary>
        /// <param name="clock">ゲーム内の累積秒数</param>
        /// <param name="inventory">保存を担当する所持品</param>
        /// <example>GameLifetimeScopeでSingletonとして生成する</example>
        public ConsumableEffects(GameProgress clock, Inventory inventory)
        {
            _clock = clock;
            _inventory = inventory;
        }

        /// <summary>効果の重複を防ぎ、消費の保存に成功した場合だけ発動する</summary>
        /// <param name="id">選択したアイテムID</param>
        /// <returns>新たに発動した場合はtrue</returns>
        /// <example>インベントリの決定時にTryUse(PotionId)を呼ぶ</example>
        public bool TryUse(int id)
        {
            switch (id)
            {
                case TorchId when !TorchLit:
                    if (!_inventory.Remove(id)) return false;
                    _torchUntil = Time.time + 60f;
                    return true;

                case PotionId when !Cloaked:
                    if (!_inventory.Remove(id)) return false;
                    // ゲーム時計で次の明け方（4時）を求め、深夜の日付変更をまたいで維持する
                    const float day = 24f * 3600f;
                    const float dawn = 4f * 3600f;
                    _cloakUntil = (Mathf.Floor((_clock.GameSeconds.CurrentValue - dawn) / day) + 1f) * day + dawn;
                    return true;

                default:
                    return false;
            }
        }
    }
}
