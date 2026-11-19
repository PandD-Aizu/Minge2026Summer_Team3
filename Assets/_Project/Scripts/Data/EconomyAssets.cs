using _Project.Scripts.Data.Item;
using TMPro;
using UnityEngine;
using Upgrades;

namespace _Project.Scripts.Data
{
    /// <summary>交換・強化の正規データと交換所の表示素材を同じバンドルから取得する</summary>
    public sealed class EconomyAssets : ScriptableObject
    {
        public const string Address = "EconomyAssets";

        [SerializeField] private ItemCatalog _items;
        [SerializeField] private ExchangeRecipeDefinition[] _recipes;
        [SerializeField] private UpgradeDefinition[] _upgrades;
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Sprite _shell;
        [SerializeField] private Sprite _cooler;

        public ItemCatalog Items => _items;
        public ExchangeRecipeDefinition[] Recipes => _recipes;
        public UpgradeDefinition[] Upgrades => _upgrades;
        public TMP_FontAsset Font => _font;
        public Sprite Shell => _shell;
        public Sprite Cooler => _cooler;
    }
}
