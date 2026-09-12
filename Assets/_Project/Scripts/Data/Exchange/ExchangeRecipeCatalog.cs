namespace _Project.Scripts.Data
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "ExchangeRecipeCatalog",
        menuName = "Game/Data/Exchange/Exchange Recipe Catalog")]
    public class ExchangeRecipeCatalog : ScriptableObject
    {
        [SerializeField]
        private ExchangeRecipeDefinition[] recipes;

        public ExchangeRecipeDefinition[] Recipes => recipes;
    }
}