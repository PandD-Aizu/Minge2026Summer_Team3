using _Project.Scripts.Data.Enum;
using UnityEngine;
using R3;

namespace Enemy
{
    /// <summary>プレイヤーが捕獲範囲に入ったことを通知する</summary>
    [RequireComponent(typeof(MeshCollider))]
    public sealed class VisionEnemyDeathSensor : MonoBehaviour
    {
        private readonly Subject<EnemyType> _deathCollisionEnter = new();
        public Observable<EnemyType> DeathCollisionEnter => _deathCollisionEnter;
        private MeshCollider _trigger;

        /// <summary>同じGameObjectにある捕獲用Triggerを取得する</summary>
        private void Awake()
        {
            _trigger = GetComponent<MeshCollider>();
        }

        /// <summary>捕獲範囲の判定を切り替える</summary>
        public void SetSensing(bool enabled)
        {
            if (_trigger == null) _trigger = GetComponent<MeshCollider>();
            _trigger.enabled = enabled;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_trigger != null && _trigger.enabled && other.gameObject.CompareTag("Player"))
            {
                _deathCollisionEnter.OnNext(EnemyType.Vision);
            }
        }

        private void OnDestroy() => _deathCollisionEnter.Dispose();
    }
}
