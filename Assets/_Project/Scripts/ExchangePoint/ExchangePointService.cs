using _Project.Scripts.Data;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;
using InventoryData;

namespace ExchangePoint
{
    /// <summary>UIに依存せず売却、購入、直接交換を確定する</summary>
    public sealed class ExchangePointService
    {
        private readonly Inventory _inventory;
        private readonly ItemCatalog _catalog;
        public Inventory Inventory => _inventory;
        public ItemCatalog Catalog => _catalog;
        /// <summary>共有所持品と正規アイテムのカタログを受け取る</summary>
        /// <param name="inventory">所持品</param><param name="catalog">商品定義</param>
        /// <example>Root Scopeから生成する</example>
        public ExchangePointService(Inventory inventory, ItemCatalog catalog) { _inventory = inventory; _catalog = catalog; }

        /// <summary>指定した魚を売却する</summary>
        /// <param name="fish">正規の魚データ</param><param name="quantity">売却数</param><param name="error">失敗理由</param>
        /// <returns>保存まで成功した場合true</returns><example>TrySell(fish, 2, out error)</example>
        public bool TrySell(FishDefinition fish, int quantity, out string error)
        {
            error = "売却対象または数量が不正";
            return fish != null && _catalog.FindById(fish.ItemId) == fish && quantity > 0 && fish.SellPrice > 0
                && _inventory.TryChange(fish.ItemId, -(long)quantity, 0, 0, (long)fish.SellPrice * quantity, out error);
        }

        /// <summary>魚以外の商品を残高で購入する</summary>
        /// <param name="item">商品データ</param><param name="quantity">購入数</param><param name="error">失敗理由</param>
        /// <returns>保存まで成功した場合true</returns><example>TryBuy(item, 1, out error)</example>
        public bool TryBuy(ItemDefinition item, int quantity, out string error)
        {
            error = "購入対象または数量が不正";
            return item != null && !(item is FishDefinition) && _catalog.FindById(item.ItemId) == item && quantity > 0 && item.PurchasePrice > 0
                && _inventory.TryChange(item.ItemId, quantity, 0, 0, -(long)item.PurchasePrice * quantity, out error);
        }

        /// <summary>魚をレシピの報酬へ直接交換する</summary>
        /// <param name="recipe">交換レシピ</param><param name="quantity">交換回数</param><param name="error">失敗理由</param>
        /// <returns>保存まで成功した場合true</returns><example>TryExchange(recipe, 1, out error)</example>
        public bool TryExchange(ExchangeRecipeDefinition recipe, int quantity, out string error)
        {
            error = "交換レシピまたは数量が不正";
            if (recipe == null || quantity <= 0 || recipe.CostFish == null || recipe.RewardItem == null
                || recipe.CostAmount <= 0 || recipe.RewardAmount <= 0
                || _catalog.FindById(recipe.CostFish.ItemId) != recipe.CostFish
                || _catalog.FindById(recipe.RewardItem.ItemId) != recipe.RewardItem) return false;
            return _inventory.TryChange(recipe.CostFish.ItemId, -(long)recipe.CostAmount * quantity,
                recipe.RewardItem.ItemId, (long)recipe.RewardAmount * quantity, 0, out error);
        }
    }
}
