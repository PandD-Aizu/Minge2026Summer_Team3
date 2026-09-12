using UnityEngine;

namespace _Project.Scripts.Data.Enemy
{
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Game/Data/Enemy/Catalog")]
    public class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private EnemyDefinition[] enemies;
        
        public EnemyDefinition[] Enemies => enemies;
    }
}