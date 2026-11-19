using System;
using System.Collections.Generic;
using InventoryData;
using UnityEngine;
using VContainer;

namespace Upgrades
{
    /// <summary>共有の購入段階から能力倍率を求め、強化購入を確定する</summary>
    public sealed class UpgradeService
    {
        private readonly Inventory _inventory;
        private readonly IReadOnlyList<UpgradeDefinition> _definitions;
        public IReadOnlyList<UpgradeDefinition> Definitions => _definitions;
        public float MoveSpeedMultiplier => GetMultiplier(UpgradeEffect.MoveSpeed);
        public float FishingGainMultiplier => GetMultiplier(UpgradeEffect.FishingGain);

        /// <summary>Rootの共有所持状態とAddressables内の強化定義を接続する</summary>
        /// <param name="inventory">取引と同じ保存状態</param>
        /// <example>VContainerから生成する</example>
        /// <param name="assets">ロード済みの経済アセット</param>
        [Inject]
        public UpgradeService(Inventory inventory, _Project.Scripts.Data.EconomyAssets assets) : this(inventory, assets.Upgrades) { }

        /// <summary>指定した定義で強化サービスを作る</summary>
        /// <param name="inventory">共有状態</param><param name="definitions">正規の強化定義</param>
        /// <example>テストでは一時セーブのInventoryを渡す</example>
        public UpgradeService(Inventory inventory, UpgradeDefinition[] definitions)
        {
            _inventory = inventory;
            var copy = (UpgradeDefinition[])definitions.Clone();
            Array.Sort(copy, (a, b) => (a != null ? a.Id : 0).CompareTo(b != null ? b.Id : 0));
            var ids = new HashSet<int>();
            var effects = new HashSet<UpgradeEffect>();
            foreach (var definition in copy)
                if (definition == null || !definition.IsValid() || !ids.Add(definition.Id) || !effects.Add(definition.Effect))
                    throw new InvalidOperationException("強化SOの設定不足、IDまたは効果種類の重複");
            _definitions = Array.AsReadOnly(copy);
        }

        /// <summary>強化の購入済み回数を返す</summary>
        /// <param name="definition">強化定義</param><returns>未購入なら0</returns>
        /// <example>一覧の段階表示に使用する</example>
        public int GetLevel(UpgradeDefinition definition) => _inventory.GetUpgradeLevel(definition.Id);

        /// <summary>画面で確認した段階が変わっていない場合だけ次の1段階を購入する</summary>
        /// <param name="definition">正規の強化定義</param><param name="expectedLevel">表示時の購入済み回数</param>
        /// <param name="error">失敗理由</param><returns>支払いと保存に成功した場合true</returns>
        /// <example>TryBuy(definition, displayedLevel, out error)</example>
        public bool TryBuy(UpgradeDefinition definition, int expectedLevel, out string error)
        {
            error = "強化の設定が不正";
            if (definition == null || !definition.IsValid()) return false;
            bool registered = false;
            foreach (var candidate in _definitions) if (candidate == definition) registered = true;
            if (!registered) return false;
            error = "強化は上限に達している";
            var stage = definition.GetNextStage(expectedLevel);
            if (stage == null) return false;
            return _inventory.TryPurchaseUpgrade(definition.Id, expectedLevel, stage.Price, out error);
        }

        /// <summary>指定能力の基礎値に掛ける倍率を返す</summary>
        /// <param name="effect">対象能力</param><returns>未強化なら1</returns>
        /// <example>移動と釣りがそれぞれの倍率を参照する</example>
        private float GetMultiplier(UpgradeEffect effect)
        {
            foreach (var definition in _definitions)
                if (definition.Effect == effect) return 1f + definition.GetTotalEffect(GetLevel(definition));
            return 1f;
        }
    }
}
