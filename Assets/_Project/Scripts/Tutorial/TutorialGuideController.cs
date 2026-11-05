
using System;
using _Project.Scripts.InteractableObject;
using R3;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// ラジオとの距離に応じて操作案内を切り替える
    /// </summary>
    public sealed class TutorialGuideController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TutorialGuideView _guideView;
        [SerializeField] private InteractableConnector _radioConnector;

        private IDisposable _nearbySubscription;
        private bool _guideStarted;

        private void Awake()
        {
            if (_guideView == null)
                Debug.LogError("TutorialGuideViewが設定されていません", this);

            if (_radioConnector == null)
                Debug.LogError("RadioConnectorが設定されていません", this);
        }

        private void OnEnable()
        {
            if (_radioConnector == null) return;

            _nearbySubscription = _radioConnector.OnPlayerNearbyChanged
                .Subscribe(OnPlayerNearbyChanged);
        }

        /// <summary>導入会話終了後に操作案内を開始する</summary>
        public void BeginGuide()
        {
            if (_guideView == null || _radioConnector == null) return;

            _guideStarted = true;
            UpdateGuide(_radioConnector.IsPlayerNearby);
        }

        /// <summary>操作案内を終了する</summary>
        public void EndGuide()
        {
            _guideStarted = false;
            _guideView?.Hide();
        }

        private void OnPlayerNearbyChanged(bool nearby)
        {
            if (!_guideStarted) return;

            UpdateGuide(nearby);
        }

        private void UpdateGuide(bool nearby)
        {
            if (nearby)
                _guideView.ShowInteractGuide();
            else
                _guideView.ShowMoveGuide();
        }

        private void OnDisable()
        {
            _nearbySubscription?.Dispose();
            _nearbySubscription = null;
        }
    }
}
