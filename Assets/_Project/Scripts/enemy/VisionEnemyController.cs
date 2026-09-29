using _Project.Scripts.Data.Enemy;
using UnityEngine;
using VContainer.Unity;

public class VisionEnemyController : MonoBehaviour
{
    [SerializeField]private PlayerMovement _playerPosition;
    private Transform _playerCurrentPosition;
    [SerializeField]private EnemyDefinition _enemyDefinition;
    private Transform _enemyPosition;
    [SerializeField]private float stopDistance = 1f;


    public void Start()
    {
        _enemyPosition = this.transform;
    }
    private void Update()
    {
        _playerCurrentPosition = _playerPosition?.PlayerPosition;
        Vector3 offset = _playerCurrentPosition.position - _enemyPosition.position;
        offset.y = 0;
        float distance = offset.magnitude;
        if (distance <= stopDistance) return;
        Vector3 direction = offset.normalized;
        float step = Mathf.Min(
            _enemyDefinition.MoveSpeed * Time.deltaTime,
            distance - stopDistance
        );

        Vector3 movement = step * direction;
        _enemyPosition.position = _enemyPosition.position + movement;

    }
}
