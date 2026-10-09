using R3;
using UnityEngine;
using System.Collections.Generic;

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
        private readonly HashSet<Collider> _playerColliders = new();
        public bool PlayerInside => _playerColliders.Count > 0;

        /// <summary>同じGameObjectにある発見用Triggerを取得する</summary>
        /// <example>Unityがコンポーネント生成時に呼ぶ</example>
        private void Awake()
        {
            _trigger = GetComponent<SphereCollider>();
        }

        /// <summary>発見範囲の判定を切り替える</summary>
        public void SetSensing(bool enabled)
        {
            if (!enabled) _playerColliders.Clear();
            if (_trigger == null) _trigger = GetComponent<SphereCollider>();
            _trigger.enabled = enabled;
        }

        /// <summary>プレイヤーが発見範囲に入ったことを通知する</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_trigger.enabled && other.gameObject.CompareTag("Player") && _playerColliders.Add(other)
                && _playerColliders.Count == 1)
            {
                _detectCollisionEnter.OnNext(Unit.Default);
            }
        }

        /// <summary>プレイヤーが発見範囲から出たことを通知する</summary>
        private void OnTriggerExit(Collider other)
        {
            if (_playerColliders.Remove(other) && !PlayerInside)
            {
                _detectCollisionExit.OnNext(Unit.Default);
            }
        }

        /// <summary>昼夜の切替で範囲内に残っていたプレイヤーも検知する</summary>
        /// <param name="other">範囲内に滞在しているCollider</param>
        /// <example>Triggerを再有効化した次の物理更新で呼ばれる</example>
        private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

        private void OnDestroy()
        {
            _detectCollisionEnter.Dispose();
            _detectCollisionExit.Dispose();
        }
    }
}
