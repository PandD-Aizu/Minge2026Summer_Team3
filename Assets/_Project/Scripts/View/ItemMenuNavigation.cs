using R3;
using UnityEngine;

namespace ItemMenus
{
    /// <summary>アイテム枠とタブの選択、およびタブ別の画面切り替えを管理する</summary>
    public sealed class ItemMenuNavigation : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] _tabs;
        [SerializeField] private UnityEngine.UI.ScrollRect[] _pages;

        private ItemMenuCursor _cursor;
        private bool _monochrome;
        private int _activeTab;
        private int _focusedTab = -1;
        private Vector2Int _heldDirection;
        private float _nextMoveTime;

        private readonly Subject<int> _tabSelected = new();
        public Observable<int> OnTabSelected => _tabSelected;
        public RectTransform SelectedItem => _focusedTab < 0 ? _cursor?.SelectedItem : null;
        public int ActiveTab => _activeTab;
        public UnityEngine.UI.ScrollRect[] Pages => _pages;

        /// <summary>既存の購入画面を売却、購入、交換の3タブへ拡張する</summary>
        /// <example>集荷場のロード後に一度呼ぶ</example>
        public void ConfigureTradingTabs()
        {
            _monochrome = true;
            if (_tabs.Length != 2 || _pages.Length != 2) return;
            var tab = Instantiate(_tabs[1], _tabs[1].transform.parent);
            var page = Instantiate(_pages[1], _pages[1].transform.parent);
            _tabs = new[] { _tabs[0], _tabs[1], tab };
            _pages = new[] { _pages[0], _pages[1], page };
            tab.onClick.RemoveAllListeners();
            tab.OnClickAsObservable().Subscribe(_ => SelectTab(2)).AddTo(this);
            string[] labels = { "売却", "購入", "直接交換" };
            for (int i = 0; i < 3; i++)
            {
                var label = _tabs[i].GetComponentInChildren<TMPro.TMP_Text>();
                label.font = Resources.Load<TMPro.TMP_FontAsset>("Economy/TradeFont");
                label.text = labels[i];
                var rect = (RectTransform)_tabs[i].transform;
                rect.anchorMin = new Vector2(i * 0.8f / 3f, rect.anchorMin.y);
                rect.anchorMax = new Vector2((i + 1) * 0.8f / 3f, rect.anchorMax.y);
                rect.offsetMin = new Vector2(8, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-8, rect.offsetMax.y);
            }
            SelectTab(0);
        }

        /// <summary>マウスからもキーボードと同じタブ切り替えを呼べるようにする</summary>
        /// <example>Prefabの生成時にUnityが呼ぶ</example>
        private void Awake()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                _tabs[i].OnClickAsObservable().Subscribe(_ => SelectTab(index)).AddTo(this);
                _tabs[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            }

            SelectTab(0);
        }

        /// <summary>指定したタブの画面を表示し、タブ上に選択枠を残す</summary>
        /// <param name="index">Prefabで対応付けたタブと画面の番号</param>
        /// <example>建築素材タブの決定時はSelectTab(1)を呼ぶ</example>
        public void SelectTab(int index)
        {
            if (index < 0 || index >= _tabs.Length || index >= _pages.Length) return;

            _cursor?.Dispose();
            _activeTab = index;
            for (int i = 0; i < _pages.Length; i++) _pages[i].gameObject.SetActive(i == index);
            _cursor = new ItemMenuCursor(_pages[index], _monochrome);
            FocusTab(index);
            _tabSelected.OnNext(index);
        }

        /// <summary>画面を開いたときに先頭のアイテム枠、空なら現在のタブを選ぶ</summary>
        /// <example>インベントリのShowから呼ぶ</example>
        public void SelectFirst()
        {
            ResetRepeat();
            _focusedTab = -1;
            _cursor?.SelectFirst();
            if (_cursor?.SelectedItem == null) FocusTab(_activeTab);
        }

        /// <summary>WASDの押し始めと長押しを、一定間隔の選択移動に変換する</summary>
        /// <param name="input">入力アクションが返す移動ベクトル</param>
        /// <example>表示中だけPresenterのTickから呼ぶ</example>
        public void Navigate(Vector2 input)
        {
            var direction = input.sqrMagnitude < 0.25f ? Vector2Int.zero
                : Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                    ? new Vector2Int(input.x > 0 ? 1 : -1, 0)
                    : new Vector2Int(0, input.y > 0 ? 1 : -1);
            if (direction == Vector2Int.zero)
            {
                ResetRepeat();
                return;
            }

            bool changed = direction != _heldDirection;
            if (!changed && Time.unscaledTime < _nextMoveTime) return;
            Move(direction);
            _heldDirection = direction;
            _nextMoveTime = Time.unscaledTime + (changed ? 0.35f : 0.12f);
        }

        /// <summary>アイテムの上端からタブへ、タブから下でアイテムへ選択を移す</summary>
        /// <param name="direction">上下左右の単位ベクトル</param>
        /// <example>タブ上ではAとDで隣のタブを選び、Jを押すまで画面は切り替えない</example>
        public void Move(Vector2Int direction)
        {
            if (_focusedTab >= 0)
            {
                if (direction.x != 0) FocusTab(Mathf.Clamp(_focusedTab + direction.x, 0, _tabs.Length - 1));
                else if (direction.y < 0) SelectFirst();
            }
            else if (_cursor != null && !_cursor.Move(direction) && direction.y > 0)
            {
                FocusTab(_activeTab);
            }
        }

        /// <summary>タブを確定するか、アイテムの操作を呼び出し元へ委ねる</summary>
        /// <returns>アイテムを選択中ならtrue、タブの確定または未選択ならfalse</returns>
        /// <example>J入力時にtrueなら使用処理や商品詳細の表示を行う</example>
        public bool ConfirmSelection()
        {
            if (_focusedTab < 0) return SelectedItem != null;
            SelectTab(_focusedTab);
            return false;
        }

        /// <summary>指定したタブに選択枠だけを移す</summary>
        /// <param name="index">選択先のタブ番号</param>
        /// <example>Wでアイテム一覧からタブ列へ戻るときに呼ぶ</example>
        private void FocusTab(int index)
        {
            _focusedTab = index;
            _cursor?.Highlight((RectTransform)_tabs[index].transform);
        }

        /// <summary>キーリピートの状態を解除する</summary>
        /// <example>詳細画面を開いたときやキーを離したときに呼ぶ</example>
        public void ResetRepeat()
        {
            _heldDirection = Vector2Int.zero;
            _nextMoveTime = 0;
        }

        /// <summary>選択枠と長押し状態を解除する</summary>
        /// <example>画面を閉じたときに呼ぶ</example>
        public void ClearSelection()
        {
            _cursor?.ClearSelection();
            ResetRepeat();
        }

        /// <summary>非表示になった画面の選択を解除する</summary>
        /// <example>シーン切り替え時にUnityが呼ぶ</example>
        private void OnDisable() => ClearSelection();

        /// <summary>生成した選択枠を解放する</summary>
        /// <example>Prefabの破棄時にUnityが呼ぶ</example>
        private void OnDestroy()
        {
            _cursor?.Dispose();
            _tabSelected.Dispose();
        }
    }
}
