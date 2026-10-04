using _Project.Scripts.Core;
using UnityEngine;
using UnityEngine.AI;

namespace _Project.Scripts.Enemy
{
    public class VisionEnemyNavigator : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent _navMeshAgent;

        /// <summary>
        /// 目的地(プレイヤーなど)を設定する
        /// </summary>
        /// <param name="destination"></param>
        public void SetDestination(Transform destination) => _navMeshAgent.SetDestination(destination.position);

        //
        public void SetActive(bool night) => _navMeshAgent.isStopped = !night;
    }
}
