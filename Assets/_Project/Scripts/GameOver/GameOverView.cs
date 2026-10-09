using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;


namespace GameOver
{
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;
        [SerializeField, Min(0f)] private float _impactBlackoutDuration = 0.06f;
        [SerializeField, Min(0f)] private float _blackoutHoldDuration = 0.65f;
        private UnityEngine.UI.Graphic[] _hiddenGraphics;
        private bool[] _graphicEnabled;

        /// <summary>暗転用の画像を残し、主観演出中の操作案内やHUDを隠す</summary>
        /// <example>死亡通知を受けて操作を止めた直後に呼ぶ</example>
        public void HideGameplayUI()
        {
            if (_hiddenGraphics != null) return;
            var graphics = new System.Collections.Generic.List<UnityEngine.UI.Graphic>();
            foreach (var graphic in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (graphic.gameObject.scene != gameObject.scene || graphic.transform.IsChildOf(_canvasGroup.transform)) continue;
                graphics.Add(graphic);
            }
            _hiddenGraphics = graphics.ToArray();
            _graphicEnabled = new bool[_hiddenGraphics.Length];
            for (var i = 0; i < _hiddenGraphics.Length; i++)
            {
                _graphicEnabled[i] = _hiddenGraphics[i].enabled;
                _hiddenGraphics[i].enabled = false;
            }
        }

        /// <summary>演出中断時にHUDの表示状態を元に戻す</summary>
        /// <example>シーン読み込み失敗時やPresenter破棄時に呼ぶ</example>
        public void RestoreGameplayUI()
        {
            if (_hiddenGraphics == null) return;
            for (var i = 0; i < _hiddenGraphics.Length; i++)
                if (_hiddenGraphics[i] != null) _hiddenGraphics[i].enabled = _graphicEnabled[i];
            _hiddenGraphics = null;
            _graphicEnabled = null;
        }

        /// <summary>突進の衝撃で急に暗転し、暗闇を残してから帰還する</summary>
        /// <param name="cancellation">シーン破棄時の中断トークン</param>
        /// <returns>暗転と余韻が終わるまでの待機</returns>
        /// <example>敵の突進直後に呼ぶ</example>
        public async UniTask ImpactBlackoutAsync(CancellationToken cancellation)
        {
            await FadeAsync(1f, cancellation, _impactBlackoutDuration);
            await UniTask.Delay(System.TimeSpan.FromSeconds(_blackoutHoldDuration),
                DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellation);
        }

        /// <summary>シーン開始時は視界を覆わない</summary>
        private void Awake()
        {
            ClearImmediately();
        }

        /// <summary>現在の明るさから暗転する</summary>
        /// <param name="cancellation">シーン破棄時の中断トークン</param>
        /// <returns>暗転完了までの待機</returns>
        public UniTask FadeToBlackAsync(CancellationToken cancellation)
        {
            return FadeAsync(1f, cancellation);
        }

        /// <summary>現在の暗さから視界を戻す</summary>
        /// <param name="cancellation">シーン破棄時の中断トークン</param>
        /// <returns>明転完了までの待機</returns>
        public UniTask FadeToWhiteAsync(CancellationToken cancellation)
        {
            return FadeAsync(0f, cancellation);
        }

        /// <summary>演出が中断されたときに視界をすぐ戻す</summary>
        public void ClearImmediately()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>停止中でも動く時間でCanvasGroupの透明度を補間する</summary>
        /// <param name="target">目標の透明度</param>
        /// <param name="cancellation">中断トークン</param>
        /// <param name="duration">補間秒数、省略時は通常の暗転時間を使う</param>
        /// <returns>補間完了までの待機</returns>
        private async UniTask FadeAsync(float target, CancellationToken cancellation, float duration = -1f)
        {
            cancellation.ThrowIfCancellationRequested();
            if (duration < 0f) duration = _fadeDuration;
            var start = _canvasGroup.alpha;
            var elapsed = 0f;
            _canvasGroup.blocksRaycasts = target > 0f;

            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            }

            _canvasGroup.alpha = target;
            _canvasGroup.blocksRaycasts = target > 0f;
        }
    }
}
