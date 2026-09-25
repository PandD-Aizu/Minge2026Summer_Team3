using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ExchangePoint
{
    /// <summary>
    /// 無人集荷場のUI
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class ExchangePointView : MonoBehaviour
    {
        [Header("生成するウィンドウのアドレス")]
        [SerializeField] private AssetReference _exchangeUIRef;
        [SerializeField] private AssetReference _exchangePopupUIRef;

        [SerializeField, Tooltip("UIの表示順")] private int _sortingOrder = 11;
        [SerializeField, Min(0.0f)] private float _fadeDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private GameObject _collectionPointUIInstance;
        private bool _isVisible;
        private Tween _fadeTween;

        public bool IsVisible => _isVisible;

        private void Awake()
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(_exchangeUIRef);
            try
            {
                handle.Completed += (op) =>
                {
                    if (op.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                    {
                        _collectionPointUIInstance = Instantiate(op.Result, transform);
                        _canvas = _collectionPointUIInstance.GetComponentInChildren<Canvas>();
                        _canvas.sortingOrder = _sortingOrder;
                        _canvasGroup = _collectionPointUIInstance.GetComponent<CanvasGroup>();
                        SetVisible(false);
                    }
                    else
                    {
                        Debug.LogError($"Failed to load Collection Point UI: {op.OperationException}");
                    }
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);

                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        /// <summary>
        /// UIの表示状態をフェードで切り替える
        /// </summary>
        /// <param name="visible">true: 表示する、false: 非表示する</param>
        public void SetVisible(bool visible)
        {
            if (_collectionPointUIInstance == null) return;

            _isVisible = visible;

            _fadeTween?.Kill();
            _fadeTween = null;
            if (_fadeDuration <= 0.0f)
            {
                _canvasGroup.alpha = visible ? 1.0f : 0.0f;
                return;
            }

            _fadeTween = _canvasGroup.DOFade(visible ? 1.0f : 0.0f, _fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .OnKill(() => _fadeTween = null);
        }

        /// <summary>
        /// 進行中のフェードを止めて表示を直ちに消す
        /// </summary>
        private void HideImmediately()
        {
            _fadeTween?.Kill();
            _fadeTween = null;
            _isVisible = false;
            if (_canvasGroup != null) _canvasGroup.alpha = 0.0f;
        }

        private void OnDisable() => HideImmediately();
    }
}
