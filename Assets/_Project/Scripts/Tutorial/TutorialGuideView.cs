
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// チュートリアルの操作案内UIを表示・非表示にする
    /// </summary>
    public sealed class TutorialGuideView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _guidePanel;
        [SerializeField] private GameObject _moveGuide;
        [SerializeField] private GameObject _interactGuide;

        private void Awake()
        {
            Hide();
        }

        /// <summary>移動案内を表示する</summary>
        public void ShowMoveGuide()
        {
            if (_guidePanel == null) return;

            _guidePanel.SetActive(true);
            _moveGuide?.SetActive(true);
            _interactGuide?.SetActive(false);
        }

        /// <summary>ラジオの操作案内を表示する</summary>
        public void ShowInteractGuide()
        {
            if (_guidePanel == null) return;

            _guidePanel.SetActive(true);
            _moveGuide?.SetActive(false);
            _interactGuide?.SetActive(true);
        }

        /// <summary>操作案内をすべて非表示にする</summary>
        public void Hide()
        {
            if (_guidePanel == null) return;

            _guidePanel.SetActive(false);
            _moveGuide?.SetActive(false);
            _interactGuide?.SetActive(false);
        }
    }
}
