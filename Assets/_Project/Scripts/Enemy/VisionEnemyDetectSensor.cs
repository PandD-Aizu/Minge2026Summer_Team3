using R3;
using UnityEngine;

namespace Enemy
{

    /// <summary>発見範囲へのプレイヤーの出入りを通知する</summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class VisionEnemyDetectSensor : MonoBehaviour
    {
        private readonly Subject<Unit> _detectCollisionEnter = new();
        public Observable<Unit> DetectCollisionEnter => _detectCollisionEnter;
        private readonly Subject<Unit> _detectCollisionExit = new();
        public Observable<Unit> DetectCollisionExit => _detectCollisionExit;
        private SphereCollider _trigger;
        private bool _playerInside;

        /// <summary>同じGameObjectにある発見用Triggerを取得する</summary>
        /// <example>Unityがコンポーネント生成時に呼ぶ</example>
        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
        }

        /// <summary>発見範囲の判定を切り替える</summary>
        public void SetSensing(bool enabled)
        {
            if (!enabled) _playerInside = false;
            if (_trigger == null) _trigger = GetComponent<SphereCollider>();
            _trigger.enabled = enabled;
        }

        /// <summary>プレイヤーが発見範囲に入ったことを通知する</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!_playerInside && other.gameObject.CompareTag("Player"))
            {
                _playerInside = true;
                _detectCollisionEnter.OnNext(Unit.Default);
            }
        }

        /// <summary>プレイヤーが発見範囲から出たことを通知する</summary>
        private void OnTriggerExit(Collider other)
        {
            if (_playerInside && other.gameObject.CompareTag("Player"))
            {
                _playerInside = false;
                _detectCollisionExit.OnNext(Unit.Default);
            }
        }

        private void OnDestroy()
        {
            _detectCollisionEnter.Dispose();
            _detectCollisionExit.Dispose();
        }
    }
}
