using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        private GameObject _collectionPointUIInstance;
        private readonly ReactiveProperty<bool> _visibility = new(false);
        private readonly CancellationTokenSource _lifetimeCancellation = new();
        private Tween _fadeTween;
        private AsyncOperationHandle<GameObject> _uiHandle;
        private ExchangeItemCursor _itemCursor;
        private GameObject _popupUIInstance;
        private AsyncOperationHandle<GameObject> _popupHandle;
        private bool _isPopupOpen;
        private bool _isPopupLoading;

        public bool IsVisible => _visibility.Value;
        /// <summary>詳細パネルの表示中、または表示を要求してロード中ならtrue</summary>
        public bool IsPopupOpen => _isPopupOpen;
        public Observable<bool> OnVisibilityChanged => _visibility;

        /// <summary>一覧を読み込み、非表示の状態で初期化する</summary>
        /// <param name="cancellationToken">ショップのScope破棄時にキャンセルされるトークン</param>
        /// <returns>初期化の完了を待つUniTask</returns>
        /// <example>PresenterのStartAsyncからawaitする</example>
        public async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            try
            {
                (_collectionPointUIInstance, _uiHandle) =
                    await LoadPanelAsync(_exchangeUIRef, _sortingOrder, cancellationToken);

                _canvasGroup = _collectionPointUIInstance.GetComponent<CanvasGroup>();
                _itemCursor = new ExchangeItemCursor(
                    _collectionPointUIInstance.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true));
                HideImmediately();
                _collectionPointUIInstance.SetActive(true);
            }
            catch (OperationCanceledException)
            {
                // ScopeまたはViewの破棄による中断は通常の終了として扱う
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        /// <summary>Prefabを読み込み、非表示のパネルと所有するハンドルを返す</summary>
        /// <param name="reference">生成するパネルのAddressables参照</param>
        /// <param name="sortingOrder">Canvasの表示順</param>
        /// <param name="cancellationToken">呼び出し元の終了を通知するトークン</param>
        /// <returns>非表示のインスタンスと、破棄時に解放するロードハンドル</returns>
        /// <example>一覧と詳細の両方のロードに使用する</example>
        private async UniTask<(GameObject Instance, AsyncOperationHandle<GameObject> Handle)> LoadPanelAsync(
            AssetReference reference, int sortingOrder, CancellationToken cancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCancellation.Token);
            var token = linkedCancellation.Token;
            token.ThrowIfCancellationRequested();

            var handle = Addressables.LoadAssetAsync<GameObject>(reference);
            GameObject instance = null;
            try
            {
                // ハンドルはViewが解放し、UniTask側との二重解放を避ける
                var prefab = await handle.ToUniTask(cancellationToken: token,
                    cancelImmediately: true, autoReleaseWhenCanceled: false);
                token.ThrowIfCancellationRequested();

                instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                var canvas = instance.GetComponentInChildren<Canvas>(true);
                if (canvas == null) throw new InvalidOperationException("交換UIにCanvasが必要");

                canvas.sortingOrder = sortingOrder;
                return (instance, handle);
            }
            catch
            {
                // 成功して所有権を返すまでは、このメソッド内で後始末する
                if (instance != null) Destroy(instance);
                ReleaseHandle(ref handle);
                throw;
            }
        }

        /// <summary>詳細パネルを開閉し、初回だけPrefabを非同期で読み込む</summary>
        /// <param name="visible">trueで開き、falseでロード待ちを含めて閉じる</param>
        /// <example>一覧表示中のJでtrue、詳細表示中のEscでfalseを渡す</example>
        public void SetPopupVisible(bool visible)
        {
            if (visible && (!IsVisible || !isActiveAndEnabled || _isPopupOpen)) return;

            // ロード中も詳細表示の要求を保持し、Escで取り消せるようにする
            _isPopupOpen = visible;
            if (_canvasGroup != null) _canvasGroup.interactable = IsVisible && !visible;
            if (_popupUIInstance != null)
            {
                _popupUIInstance.SetActive(visible);
                return;
            }

            if (visible && !_isPopupLoading) LoadPopupAsync().Forget();
        }

        /// <summary>詳細パネルを読み込み、現在の表示要求だけを反映する</summary>
        /// <returns>ロードと表示更新の完了を待つUniTask</returns>
        /// <example>ロード中に閉じた場合は次回用に非表示で保持する</example>
        private async UniTask LoadPopupAsync()
        {
            _isPopupLoading = true;
            try
            {
                (_popupUIInstance, _popupHandle) =
                    await LoadPanelAsync(_exchangePopupUIRef, _sortingOrder + 1, CancellationToken.None);
                _popupUIInstance.SetActive(_isPopupOpen && IsVisible && isActiveAndEnabled);
            }
            catch (OperationCanceledException)
            {
                // View破棄後は表示状態を書き換えない
            }
            catch (Exception ex)
            {
                SetPopupVisible(false);
                Debug.LogException(ex, this);
            }
            finally
            {
                _isPopupLoading = false;
            }
        }

        /// <summary>保持しているロードハンドルを一度だけ解放する</summary>
        /// <param name="handle">解放してdefaultへ戻すハンドル</param>
        /// <example>ロード失敗時とViewの破棄時に呼ぶ</example>
        private static void ReleaseHandle(ref AsyncOperationHandle<GameObject> handle)
        {
            if (!handle.IsValid()) return;

            Addressables.Release(handle);
            handle = default;
        }

        /// <summary>一覧の表示状態をフェードで切り替える</summary>
        /// <param name="visible">trueで表示し、falseで詳細も含めて閉じる</param>
        /// <example>集荷場でJを押したときはtrue、一覧でEscを押したときはfalseを渡す</example>
        public void SetVisible(bool visible)
        {
            if (_collectionPointUIInstance == null || !isActiveAndEnabled || IsVisible == visible) return;

            if (!visible) SetPopupVisible(false);
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;

            // 表示を整えてから通知し、歩行停止とカーソル入力を同期する
            if (visible) _itemCursor?.SelectFirst();
            else _itemCursor?.ClearSelection();
            _visibility.Value = visible;

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

        /// <summary>フェードと操作を止め、一覧と詳細を直ちに非表示にする</summary>
        /// <example>一覧の初期化時とViewの無効化時に呼ぶ</example>
        private void HideImmediately()
        {
            _fadeTween?.Kill();
            _fadeTween = null;
            SetPopupVisible(false);
            _itemCursor?.ClearSelection();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0.0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            _visibility.Value = false;
        }

        /// <summary>表示中のショップの商品カーソルを移動する</summary>
        /// <param name="direction">上下左右の単位ベクトル</param>
        /// <example>Dキーを押したときはVector2Int.rightを渡す</example>
        public void MoveSelection(Vector2Int direction)
        {
            if (IsVisible && !_isPopupOpen) _itemCursor?.Move(direction);
        }

        /// <summary>無効化時に表示と入力を直ちに止める</summary>
        /// <example>集荷場のGameObjectを無効化したときにUnityが呼ぶ</example>
        private void OnDisable() => HideImmediately();

        /// <summary>ロードを中断し、生成したUIと通知ストリームを解放する</summary>
        /// <example>シーンをアンロードしたときにUnityが呼ぶ</example>
        private void OnDestroy()
        {
            HideImmediately();
            _lifetimeCancellation.Cancel();

            // ロード済みのパネルはViewが所有し、ロード途中のものはLoadPanelAsyncが解放する
            if (_collectionPointUIInstance != null) Destroy(_collectionPointUIInstance);
            if (_popupUIInstance != null) Destroy(_popupUIInstance);
            ReleaseHandle(ref _uiHandle);
            ReleaseHandle(ref _popupHandle);
            _lifetimeCancellation.Dispose();
            _visibility.Dispose();
        }
    }
}
