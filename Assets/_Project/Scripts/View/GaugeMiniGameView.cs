using UnityEngine;

namespace MiniGame
{
    /// <summary>正規化された魚、操作バー、捕獲ゲージをCanvasに表示する</summary>
    public sealed class GaugeMiniGameView : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _lane;
        [SerializeField] private RectTransform _bar;
        [SerializeField] private RectTransform _fish;
        [SerializeField] private UnityEngine.UI.Image _fishImage;
        [SerializeField] private UnityEngine.UI.Image _barImage;
        [SerializeField] private RectTransform _progress;
        private RectTransform _timeProgress;

        /// <summary>起動時に表示を隠す</summary>
        /// <example>Unityがシーン生成時に呼ぶ</example>
        private void Awake() => Hide();

        /// <summary>魚の画像を設定して表示する</summary>
        /// <param name="sprite">対象魚のアイコン</param>
        /// <example>ゲーム開始時にShow(fish.ItemImage)を呼ぶ</example>
        public void Show(Sprite sprite)
        {
            EnsureTimeGauge();
            _timeProgress.anchorMax = new Vector2(1f, 0f);
            _fishImage.sprite = sprite;
            _canvas.enabled = true;
        }

        /// <summary>位置と捕獲進行を画面サイズに合わせて反映する</summary>
        /// <param name="model">現在のゲーム状態</param>
        /// <example>モデル更新後にRender(model)を呼ぶ</example>
        public void Render(GaugeMiniGameModel model)
        {
            EnsureTimeGauge();
            float height = _lane.rect.height;
            _bar.sizeDelta = new Vector2(_bar.sizeDelta.x, height * model.BarSize);
            _bar.anchoredPosition = new Vector2(0, height * model.BarPosition);
            _fish.anchoredPosition = new Vector2(0, height * model.FishPosition);
            _barImage.color = model.IsInside ? new Color(0.48f, 0.95f, 0.08f, 0.9f) : new Color(0.38f, 0.65f, 0.13f, 0.8f);
            _progress.anchorMax = new Vector2(1, model.Progress);
            _timeProgress.anchorMax = new Vector2(1, model.TimeProgress);
        }

        /// <summary>既存の捕獲ゲージの背面に、下から伸びる赤い時間ゲージを一度だけ追加する</summary>
        /// <example>初回Showで生成し、次回以降の釣りでも再利用する</example>
        private void EnsureTimeGauge()
        {
            if (_timeProgress != null) return;

            // 既存Prefabの緑ゲージと同じ親と座標系を使い、その直前に描画する
            _timeProgress = new GameObject("TimeFill", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<RectTransform>();
            _timeProgress.SetParent(_progress.parent, false);
            _timeProgress.SetSiblingIndex(_progress.GetSiblingIndex());
            _timeProgress.anchorMin = Vector2.zero;
            _timeProgress.anchorMax = new Vector2(1f, 0f);
            _timeProgress.offsetMin = _timeProgress.offsetMax = Vector2.zero;
            var image = _timeProgress.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color32(224, 52, 46, 255);
            image.raycastTarget = false;

            // 緑が赤より高い間も、両端から時間経過を読み取れるようにする
            _progress.offsetMin = new Vector2(2f, 0f);
            _progress.offsetMax = new Vector2(-2f, 0f);
        }

        /// <summary>結果や中断の際に画面を閉じる</summary>
        /// <example>Controllerのfinallyから呼ぶ</example>
        public void Hide() { if (_canvas != null) _canvas.enabled = false; }
    }
}
