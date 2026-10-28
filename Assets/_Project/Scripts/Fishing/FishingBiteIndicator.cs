using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>食いつき受付中だけ、主人公の頭上にドットのビックリマークを表示する</summary>
    [DisallowMultipleComponent]
    public sealed class FishingBiteIndicator : MonoBehaviour
    {
        private Transform _player;
        private Canvas _canvas;
        private RectTransform _mark;
        private Camera _camera;
        private Vector3 _offset;

        /// <summary>主人公が所有する表示専用Canvasとドットのマークを一度だけ作る</summary>
        /// <param name="player">頭上表示の基準と、生成したUIの親</param>
        /// <returns>非表示で初期化した食いつきマーク</returns>
        /// <example>初回の食いつき時にCreate(transform)で作り、以降はShowとHideで再利用する</example>
        public static FishingBiteIndicator Create(Transform player)
        {
            var root = new GameObject("FishingBiteIndicator", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(player, false);
            var indicator = root.AddComponent<FishingBiteIndicator>();
            indicator._player = player;
            indicator._canvas = root.GetComponent<Canvas>();
            indicator._canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            indicator._canvas.sortingOrder = 65;
            indicator._canvas.pixelPerfect = true;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 7×18のドット格子で縁取り、黄色の本体、左上のハイライトを描く
            indicator._mark = new GameObject("ExclamationMark", typeof(RectTransform)).GetComponent<RectTransform>();
            indicator._mark.SetParent(root.transform, false);
            indicator._mark.anchorMin = indicator._mark.anchorMax = new Vector2(0.5f, 0.5f);
            indicator._mark.pivot = new Vector2(0.5f, 0f);
            indicator._mark.sizeDelta = new Vector2(7f, 18f);
            Color outline = new Color32(49, 31, 24, 255);
            Color shadow = new Color32(213, 116, 20, 255);
            Color gold = new Color32(255, 194, 38, 255);
            Color light = new Color32(255, 238, 133, 255);
            AddBlock(indicator._mark, "StemOutline", new Rect(1, 6, 5, 12), outline);
            AddBlock(indicator._mark, "StemShadow", new Rect(2, 7, 3, 10), shadow);
            AddBlock(indicator._mark, "StemGold", new Rect(2, 8, 2, 9), gold);
            AddBlock(indicator._mark, "StemHighlight", new Rect(2, 10, 1, 7), light);
            AddBlock(indicator._mark, "DotOutline", new Rect(1, 0, 5, 5), outline);
            AddBlock(indicator._mark, "DotShadow", new Rect(2, 1, 3, 3), shadow);
            AddBlock(indicator._mark, "DotGold", new Rect(2, 2, 2, 2), gold);
            AddBlock(indicator._mark, "DotHighlight", new Rect(2, 3, 1, 1), light);
            indicator.Hide();
            return indicator;
        }

        /// <summary>食いつき受付の開始に合わせてマークを表示する</summary>
        /// <param name="offset">主人公の基準位置から頭上までのワールド座標オフセット</param>
        /// <param name="pixelSize">基準解像度での1ドットの大きさ</param>
        /// <example>Show(new Vector3(0f, 2.6f, 0f), 3f)</example>
        public void Show(Vector3 offset, float pixelSize)
        {
            _offset = offset;
            _mark.localScale = Vector3.one * Mathf.Max(1f, pixelSize);
            _camera = Camera.main;
            gameObject.SetActive(true);
            UpdatePosition();
        }

        /// <summary>合わせ成功、時間切れ、中断の際にマークを消す</summary>
        /// <example>SetBiting(false)または釣り道具のHideから呼ぶ</example>
        public void Hide() => gameObject.SetActive(false);

        /// <summary>カメラの更新後に頭上の画面座標を反映する購読を開始する</summary>
        /// <example>Showによる再有効化時にも購読する</example>
        private void OnEnable() => Canvas.willRenderCanvases += UpdatePosition;

        /// <summary>非表示や主人公の破棄で描画前コールバックを解除する</summary>
        /// <example>シーンを切り替えても古い主人公の参照を残さない</example>
        private void OnDisable() => Canvas.willRenderCanvases -= UpdatePosition;

        /// <summary>主人公の頭上へ追従し、カメラの背面ではマークを隠す</summary>
        /// <example>Cinemachineのカメラ移動後、Canvasの描画直前に呼ばれる</example>
        private void UpdatePosition()
        {
            if (_canvas == null || _mark == null || _player == null) return;
            if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (_camera == null) { _mark.gameObject.SetActive(false); return; }

            Vector3 screen = _camera.WorldToScreenPoint(_player.position + _offset);
            _mark.gameObject.SetActive(screen.z > 0f);
            if (screen.z <= 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform,
                screen, null, out Vector2 local);
            _mark.anchoredPosition = local;
        }

        /// <summary>格子上の矩形を、操作を遮らない単色のドットとして追加する</summary>
        /// <param name="parent">マークのルート</param>
        /// <param name="name">矩形の役割を表す名前</param>
        /// <param name="pixels">左下基準の位置とサイズ、単位はドット</param>
        /// <param name="color">ドットの色</param>
        /// <example>AddBlock(mark, "DotOutline", new Rect(1, 0, 5, 5), outline)</example>
        private static void AddBlock(RectTransform parent, string name, Rect pixels, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = pixels.position;
            rect.sizeDelta = pixels.size;
            var image = rect.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
