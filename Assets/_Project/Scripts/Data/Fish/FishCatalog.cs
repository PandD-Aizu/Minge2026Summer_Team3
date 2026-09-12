namespace _Project.Scripts.Data.Fish
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "FishCatalog",
        menuName = "Game/Data/Fish/Catalog")]
    public class FishCatalog : ScriptableObject
    {
        [SerializeField]
        private FishDefinition[] fishDefinitions;

        public FishDefinition[] Fish => fishDefinitions;
    }
}