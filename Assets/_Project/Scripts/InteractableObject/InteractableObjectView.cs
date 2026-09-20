using DG.Tweening;
using UnityEngine;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>操作対象の頭上へ追従するマークをフェード表示する</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [DefaultExecutionOrder(10000)]
    public class InteractableObjectView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image _interactionImage;
        [SerializeField] private Transform _displayAnchor;
        [SerializeField] private Camera _worldCamera;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private Tween _fadeTween;
        private bool _showRequested;
        private bool _isVisible;

        /// <summary>マークとキー表示を初期非表示にし、UI参照を保持する</summary>
        /// <example>Canvas直下のImageに付け、表示位置のTransformを指定する</example>
        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            // 画像と子のキー表示をCanvasGroupでまとめてフェードさせる
            if (_interactionImage != null)
            {
                _interactionImage.raycastTarget = false;
                _canvas = _interactionImage.canvas;
                if (_canvas != null) _canvasRect = _canvas.transform as RectTransform;
            }
        }

        /// <summary>接近判定から表示要求を受け取る</summary>
        /// <param name="isShow">操作範囲内ならtrue、範囲外ならfalse</param>
        /// <example>PresenterからShowInteractionImage(connector.IsPlayerNearby)を呼ぶ</example>
        public void ShowInteractionImage(bool isShow)
        {
            _showRequested = isShow;
        }

        /// <summary>カメラ移動後の位置にマークを追従させ、表示状態を更新する</summary>
        /// <example>画面外やカメラ背面にある対象のマークは表示しない</example>
        private void LateUpdate()
        {
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_interactionImage == null || _displayAnchor == null || !_displayAnchor.gameObject.activeInHierarchy ||
                _worldCamera == null || _canvas == null || _canvasRect == null)
            {
                HideImmediately();
                return;
            }

            // 背面や画面外の座標をUIに投影しない
            Vector3 viewport = _worldCamera.WorldToViewportPoint(_displayAnchor.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
            {
                HideImmediately();
                return;
            }

            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector3 screenPoint = _worldCamera.WorldToScreenPoint(_displayAnchor.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, uiCamera, out var localPoint))
            {
                _interactionImage.rectTransform.position = _canvasRect.TransformPoint(localPoint);
            }

            SetVisible(_showRequested);
        }

        /// <summary>表示状態が変わったときだけフェードを開始する</summary>
        /// <param name="visible">フェードインする場合はtrue</param>
        /// <example>接近直後に離れても進行中のTweenを止めてフェードアウトする</example>
        private void SetVisible(bool visible)
        {
            if (_isVisible == visible) return;
            _isVisible = visible;

            // 以前のTweenを止め、現在の透明度から切り替える
            _fadeTween?.Kill();
            _fadeTween = null;
            if (_fadeDuration <= 0f)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                return;
            }

            _fadeTween = _canvasGroup.DOFade(visible ? 1f : 0f, _fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() => _fadeTween = null);
        }

        /// <summary>進行中のフェードを止めて表示を直ちに消す</summary>
        /// <example>表示対象が破棄された場合やカメラの背面へ移動した場合に使用する</example>
        private void HideImmediately()
        {
            _fadeTween?.Kill();
            _fadeTween = null;
            _isVisible = false;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        /// <summary>無効化時にTweenと透明度をリセットする</summary>
        /// <example>シーン切り替えやPrefabの無効化でTweenを残さない</example>
        private void OnDisable() => HideImmediately();
    }
}
