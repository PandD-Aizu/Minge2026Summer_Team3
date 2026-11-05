using InventoryData;

namespace ExchangePoint
{
    /// <summary>所持品と共有する保存済み残高への窓口</summary>
    public sealed class Wallet
    {
        private readonly Inventory _inventory;
        public int Balance => _inventory.Balance;
        /// <summary>共有所持品から残高を参照する</summary>
        /// <param name="inventory">Rootの所持品</param>
        /// <example>VContainerから生成する</example>
        public Wallet(Inventory inventory) => _inventory = inventory;
        /// <summary>正の金額を入金して保存する</summary>
        /// <param name="amount">入金額</param><returns>成功時true</returns>
        /// <example>TryDeposit(100)</example>
        public bool TryDeposit(int amount) => amount > 0 && _inventory.TryChange(0, 0, 0, 0, amount, out _);
        /// <summary>残高の範囲内で支払う</summary>
        /// <param name="amount">支払額</param><returns>成功時true</returns>
        /// <example>TrySpend(100)</example>
        public bool TrySpend(int amount) => amount > 0 && _inventory.TryChange(0, 0, 0, 0, -amount, out _);
    }
}
