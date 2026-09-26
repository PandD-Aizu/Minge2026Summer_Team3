using System.Collections.Generic;
using UnityEngine;

namespace ItemMenus
{
    /// <summary>商品一覧内の枠を選び、赤い強調表示とスクロールを管理する</summary>
    public sealed class ItemMenuCursor
    {
        private readonly UnityEngine.UI.ScrollRect _scrollRect;
        private readonly List<RectTransform> _items = new();
        private readonly List<Vector2> _positions = new();
        private UnityEngine.UI.Graphic _selectedGraphic;
        private Color _originalColor;
        private int _selectedIndex = -1;
        private RectTransform _selectionFrame;

        public RectTransform SelectedItem => _selectedIndex >= 0 ? _items[_selectedIndex] : null;

        /// <summary>商品一覧のスクロール領域を保持する</summary>
        /// <param name="scrollRect">商品枠をContent直下に持つScrollRect</param>
        /// <example>交換UIのPrefabを生成した直後に渡す</example>
        public ItemMenuCursor(UnityEngine.UI.ScrollRect scrollRect)
        {
            _scrollRect = scrollRect;
        }

        /// <summary>現在のレイアウトを確定し、先頭の有効な商品枠を選択する</summary>
        /// <example>ショップを開くたびに呼ぶ</example>
        public void SelectFirst()
        {
            ClearSelection();
            _items.Clear();
            if (_scrollRect == null || _scrollRect.content == null) return;

            // タブやスクロールバーを含めず、商品枠だけを選択対象にする
            foreach (Transform child in _scrollRect.content)
            {
                var button = child.GetComponent<UnityEngine.UI.Button>();
                if (child.gameObject.activeInHierarchy && child is RectTransform rect &&
                    (button == null || button.isActiveAndEnabled && button.IsInteractable()))
                    _items.Add(rect);
            }

            Canvas.ForceUpdateCanvases();
            if (_items.Count > 0) Select(0);
        }

        /// <summary>同じ行または列の隣接する商品枠へ移動する</summary>
        /// <param name="direction">上下左右の単位ベクトル</param>
        /// <example>右端で右を押した場合は選択を変更しない</example>
        /// <returns>隣接する枠へ移動できた場合はtrue</returns>
        public bool Move(Vector2Int direction)
        {
            if (_selectedIndex < 0 || _scrollRect == null) return false;

            // 画面幅で列数が変わっても、実際の枠の位置から隣を探す
            _positions.Clear();
            foreach (var item in _items)
            {
                var button = item != null ? item.GetComponent<UnityEngine.UI.Button>() : null;
                if (item == null || !item.gameObject.activeInHierarchy ||
                    (button != null && (!button.isActiveAndEnabled || !button.IsInteractable())))
                {
                    _positions.Add(new Vector2(float.NaN, float.NaN));
                    continue;
                }

                var rect = (RectTransform)item.transform;
                _positions.Add(_scrollRect.content.InverseTransformPoint(rect.TransformPoint(rect.rect.center)));
            }

            int next = FindNextIndex(_positions, _selectedIndex, direction);
            if (next == _selectedIndex) return false;
            Select(next);
            return true;
        }

        /// <summary>同じ行・列にある、指定方向で最も近い枠の番号を返す</summary>
        /// <param name="positions">Content座標系の商品枠の中心位置</param>
        /// <param name="current">現在の選択番号</param>
        /// <param name="direction">上下左右の単位ベクトル</param>
        /// <returns>移動先の番号、候補がなければ現在の番号</returns>
        /// <example>2列のグリッドで下を渡すと次の行の同じ列へ移る</example>
        public static int FindNextIndex(IReadOnlyList<Vector2> positions, int current, Vector2Int direction)
        {
            if (current < 0 || current >= positions.Count || direction == Vector2Int.zero) return current;

            int next = current;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 delta = positions[i] - positions[current];
                float forward = delta.x * direction.x + delta.y * direction.y;
                float sideways = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                if (forward > 0.01f && sideways < 1f && forward < nearest)
                {
                    next = i;
                    nearest = forward;
                }
            }

            return next;
        }

        /// <summary>以前の色へ戻して選択状態を解除する</summary>
        /// <example>ショップを閉じるときや別の枠を選ぶ前に呼ぶ</example>
        public void ClearSelection()
        {
            if (_selectedGraphic != null) _selectedGraphic.color = _originalColor;
            if (_selectionFrame != null) _selectionFrame.gameObject.SetActive(false);
            _selectedGraphic = null;
            _selectedIndex = -1;
        }

        /// <summary>指定した商品枠を赤くし、表示領域内へスクロールする</summary>
        /// <param name="index">商品枠の番号</param>
        /// <example>先頭選択と方向キーによる移動から呼ぶ</example>
        private void Select(int index)
        {
            Highlight(_items[index]);
            _selectedIndex = index;

            // 選択枠が隠れる分だけスクロールし、慣性移動を止める
            var viewport = _scrollRect.viewport != null ? _scrollRect.viewport : (RectTransform)_scrollRect.transform;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, _items[index].transform);
            Rect visible = viewport.rect;
            Vector3 offset = Vector3.zero;
            if (_scrollRect.horizontal)
                offset.x = bounds.min.x < visible.xMin ? visible.xMin - bounds.min.x
                    : bounds.max.x > visible.xMax ? visible.xMax - bounds.max.x : 0f;
            if (_scrollRect.vertical)
                offset.y = bounds.max.y > visible.yMax ? visible.yMax - bounds.max.y
                    : bounds.min.y < visible.yMin ? visible.yMin - bounds.min.y : 0f;

            _scrollRect.StopMovement();
            _scrollRect.content.position += viewport.TransformVector(offset);
        }

        /// <summary>商品枠またはタブを赤い枠で強調する</summary>
        /// <param name="target">強調するUIのRectTransform</param>
        /// <example>タブへ移動したときはボタンのRectTransformを渡す</example>
        public void Highlight(RectTransform target)
        {
            ClearSelection();
            var button = target.GetComponent<UnityEngine.UI.Button>();
            _selectedGraphic = button != null ? button.targetGraphic : null;
            if (_selectedGraphic != null)
            {
                _originalColor = _selectedGraphic.color;
                _selectedGraphic.color = Color.red;
            }

            ShowSelectionFrame(target);
        }

        /// <summary>生成した枠線を破棄する</summary>
        /// <example>別のタブのカーソルを生成する前に呼ぶ</example>
        public void Dispose()
        {
            ClearSelection();
            if (_selectionFrame != null) Object.Destroy(_selectionFrame.gameObject);
        }

        /// <summary>選択した商品枠の内側に赤い枠線を重ねる</summary>
        /// <param name="item">選択中の商品枠</param>
        /// <example>商品画像やラベルより手前に4辺を表示する</example>
        private void ShowSelectionFrame(RectTransform item)
        {
            if (_selectionFrame == null)
            {
                _selectionFrame = (RectTransform)new GameObject("SelectionFrame", typeof(RectTransform)).transform;
                AddBorder("Top", new Vector2(0, 1), Vector2.one, new Vector2(0, -4), Vector2.zero);
                AddBorder("Bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 4));
                AddBorder("Left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(4, 0));
                AddBorder("Right", Vector2.right, Vector2.one, new Vector2(-4, 0), Vector2.zero);
            }

            _selectionFrame.SetParent(item, false);
            _selectionFrame.anchorMin = Vector2.zero;
            _selectionFrame.anchorMax = Vector2.one;
            _selectionFrame.offsetMin = Vector2.zero;
            _selectionFrame.offsetMax = Vector2.zero;
            _selectionFrame.SetAsLastSibling();
            _selectionFrame.gameObject.SetActive(true);
        }

        /// <summary>入力を遮らない赤い枠線を1辺生成する</summary>
        /// <param name="name">辺の名前</param>
        /// <param name="anchorMin">アンカーの最小位置</param>
        /// <param name="anchorMax">アンカーの最大位置</param>
        /// <param name="offsetMin">左下のオフセット</param>
        /// <param name="offsetMax">右上のオフセット</param>
        /// <example>上辺なら上端へ固定し、内側へ4pxの厚さを指定する</example>
        private void AddBorder(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var edge = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)edge.transform;
            rect.SetParent(_selectionFrame, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = edge.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.red;
            image.raycastTarget = false;
        }
    }
}
