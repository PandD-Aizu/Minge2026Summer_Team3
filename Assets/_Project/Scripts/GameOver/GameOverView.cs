using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;


namespace GameOver
{
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField, Min(0f)] private float _fadeDuration = 0.5f;

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
        /// <returns>補間完了までの待機</returns>
        private async UniTask FadeAsync(float target, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var start = _canvasGroup.alpha;
            var elapsed = 0f;
            _canvasGroup.blocksRaycasts = target > 0f;

            while (elapsed < _fadeDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / _fadeDuration);
            }

            _canvasGroup.alpha = target;
            _canvasGroup.blocksRaycasts = target > 0f;
        }
    }
}
