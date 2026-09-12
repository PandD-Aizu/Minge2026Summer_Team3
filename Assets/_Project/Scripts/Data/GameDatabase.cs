using _Project.Scripts.Data.Enemy;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;

namespace _Project.Scripts.Data
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "GameDatabase",
        menuName = "Game/Data/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField]
        private ItemCatalog itemCatalog;

        [SerializeField]
        private FishCatalog fishCatalog;

        [SerializeField]
        private EnemyCatalog enemyCatalog;

        [SerializeField]
        private ExchangeRecipeCatalog exchangeRecipeCatalog;

        public ItemCatalog ItemCatalog => itemCatalog;
        public FishCatalog FishCatalog => fishCatalog;
        public EnemyCatalog EnemyCatalog => enemyCatalog;
        public ExchangeRecipeCatalog ExchangeRecipeCatalog => exchangeRecipeCatalog;
    }
}