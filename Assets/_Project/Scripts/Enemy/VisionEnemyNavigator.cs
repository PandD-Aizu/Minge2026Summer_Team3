using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Enemy
{
    /// <summary>有効なNavMesh上でだけ追跡経路を更新する</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VisionEnemyNavigator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _navMeshAgent;
        [SerializeField, Min(0.1f)] private float _sampleDistance = 2f;
        [SerializeField, Min(0.02f)] private float _repathInterval = 0.2f;
        [Header("逃げ切った後の再配置")]
        [SerializeField, Min(1f)] private float _respawnPlayerDistance = 24f;
        [SerializeField, Min(1f)] private float _respawnTravelDistance = 10f;
        private NavMeshPath _respawnPath;
        private bool _active;
        private float _nextRepathTime;
        private float _nextRecoveryTime;

        /// <summary>参照が未設定の場合は同じオブジェクトのAgentを使用する</summary>
        /// <example>Unityが生成時に呼ぶ</example>
        private void Awake()
        {
            if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        /// <summary>目的地を近傍の歩行面へ投影し、一定間隔で追跡経路を更新する</summary>
        /// <param name="destination">追跡対象のTransform</param>
        /// <example>追跡中にSetDestination(playerTransform)を呼ぶ</example>
        public void SetDestination(Transform destination)
        {
            if (!_active || destination == null || Time.time < _nextRepathTime || !EnsureOnNavMesh()) return;
            _nextRepathTime = Time.time + _repathInterval;

            // 歩行面外へ出た対象に対して以前の経路を走り続けない
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _navMeshAgent.agentTypeID,
                areaMask = _navMeshAgent.areaMask
            };
            if (!NavMesh.SamplePosition(destination.position, out var hit, _sampleDistance, filter)
                || !_navMeshAgent.SetDestination(hit.position))
            {
                _navMeshAgent.ResetPath();
            }
        }

        /// <summary>追跡の開始と停止を切り替え、停止時は古い経路を消す</summary>
        /// <param name="active">追跡を許可する場合はtrue</param>
        /// <example>捕獲時と見失ったときにSetActive(false)を呼ぶ</example>
        public void SetActive(bool active)
        {
            if (_active != active) _nextRepathTime = 0f;
            _active = active;
            if (_navMeshAgent == null) _navMeshAgent = GetComponent<NavMeshAgent>();
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled || !_navMeshAgent.isOnNavMesh) return;

            _navMeshAgent.isStopped = !active;
            if (!active && (_navMeshAgent.hasPath || _navMeshAgent.pathPending)) _navMeshAgent.ResetPath();
        }

        /// <summary>プレイヤーと現在地から離れた到達可能な歩行面へ再配置する</summary>
        /// <param name="playerPosition">再出現先から距離を確保するプレイヤー位置</param>
        /// <returns>別の場所への再配置に成功した場合はtrue</returns>
        /// <example>完全に透明な間にTryRespawn(player.position)を呼ぶ</example>
        public bool TryRespawn(Vector3 playerPosition)
        {
            SetActive(false);
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled) return false;
            _respawnPath ??= new NavMeshPath();
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _navMeshAgent.agentTypeID,
                areaMask = _navMeshAgent.areaMask
            };
            if (!NavMesh.SamplePosition(playerPosition, out var playerHit, _sampleDistance, filter)) return false;

            // 島全体のベイク結果から候補を取り、別階や孤立した面は経路判定で除く
            var mesh = NavMesh.CalculateTriangulation();
            var triangleCount = mesh.indices.Length / 3;
            if (triangleCount == 0) return false;
            var origin = transform.position;
            for (var attempt = 0; attempt < 48; attempt++)
            {
                var index = Random.Range(0, triangleCount) * 3;
                var a = mesh.vertices[mesh.indices[index]];
                var b = mesh.vertices[mesh.indices[index + 1]];
                var c = mesh.vertices[mesh.indices[index + 2]];
                var u = Mathf.Sqrt(Random.value);
                var v = Random.value;
                var candidate = (1f - u) * a + u * (1f - v) * b + u * v * c;
                if (!NavMesh.SamplePosition(candidate, out var hit, _sampleDistance, filter)) continue;

                var fromPlayer = hit.position - playerPosition;
                var fromOrigin = hit.position - origin;
                fromPlayer.y = 0f;
                fromOrigin.y = 0f;
                if (fromPlayer.sqrMagnitude < _respawnPlayerDistance * _respawnPlayerDistance
                    || fromOrigin.sqrMagnitude < _respawnTravelDistance * _respawnTravelDistance) continue;
                if (!NavMesh.CalculatePath(playerHit.position, hit.position, filter, _respawnPath)
                    || _respawnPath.status != NavMeshPathStatus.PathComplete) continue;
                if (!_navMeshAgent.Warp(hit.position)) continue;

                _navMeshAgent.isStopped = true;
                _navMeshAgent.ResetPath();
                _nextRepathTime = 0f;
                _nextRecoveryTime = 0f;
                return true;
            }

            return false;
        }

        /// <summary>初期配置のずれを同じAgent種別の近傍歩行面へ補正する</summary>
        /// <returns>移動APIを安全に使用できる場合はtrue</returns>
        /// <example>目的地を設定する直前に呼ぶ</example>
        private bool EnsureOnNavMesh()
        {
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled) return false;
            if (_navMeshAgent.isOnNavMesh) return true;
            if (Time.time < _nextRecoveryTime) return false;
            _nextRecoveryTime = Time.time + 1f;

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = _navMeshAgent.agentTypeID,
                areaMask = _navMeshAgent.areaMask
            };
            if (!NavMesh.SamplePosition(transform.position, out var hit, _sampleDistance, filter)
                || !_navMeshAgent.Warp(hit.position) || !_navMeshAgent.isOnNavMesh) return false;

            _navMeshAgent.isStopped = !_active;
            return true;
        }
    }
}
