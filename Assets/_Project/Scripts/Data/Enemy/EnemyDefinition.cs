using UnityEngine;
using _Project.Scripts.Data.Enum;

namespace _Project.Scripts.Data.Enemy
{
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Game/Data/Enemy/EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private int enemyId;
        [SerializeField] private string enemyName;
        [SerializeField] private EnemyType enemyType;
        [SerializeField] private float moveSpeed;
        //[SerializeField] private Sprite enemyImage;

        public int EnemyId => enemyId;
        public string EnemyName => enemyName;
        public EnemyType EnemyType => enemyType;
        public float MoveSpeed => moveSpeed;
        //public Sprite EnemyImage => enemyImage;

    }
}