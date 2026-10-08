using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>魚影の出現と移動から、海面に残って広がる波紋を共有の波面へ送る</summary>
    [DisallowMultipleComponent]
    public sealed class FishRippleEmitter : MonoBehaviour
    {
        [Tooltip("海面の高さと勾配を描く波紋用Material")]
        [SerializeField] private Material _material;
        [Tooltip("波紋が発生してから消えるまでのゲーム時間")]
        [SerializeField, Min(0.01f)] private float _duration = 3.2f;
        [Tooltip("波紋の高さ、単位はメートル")]
        [SerializeField, Min(0f)] private float _strength = 0.028f;
        [Tooltip("同じ魚影から続けて波紋を出す最小のゲーム時間")]
        [SerializeField, Min(0f)] private float _minimumInterval = 0.18f;

        private float _opacity;
        private float _lastEmissionTime = float.NegativeInfinity;

        /// <summary>これから発生させる移動波の強さを魚影の透明度に合わせる</summary>
        /// <param name="opacity">魚影の不透明度、0〜1に制限する</param>
        /// <example>FishShadow.SetOpacityから同じ不透明度を渡す</example>
        public void SetOpacity(float opacity) => _opacity = Mathf.Clamp01(opacity);

        /// <summary>出現または移動による波紋を共有の波面に登録する</summary>
        /// <param name="position">発生地点のワールド座標、魚影の移動後も波はこの地点に残る</param>
        /// <param name="size">魚影の横幅、単位はメートル</param>
        /// <param name="velocity">海面のX・Z方向の移動速度、単位はメートル毎秒</param>
        /// <param name="isAppearance">出現波ならtrue、移動波ならfalse</param>
        /// <returns>波面へ波紋を登録できた場合はtrue、停止中や発生間隔内ならfalse</returns>
        /// <example>Emit(transform.position, 2f, new Vector2(0.2f, 0f), false)で移動波を出す</example>
        public bool Emit(Vector3 position, float size, Vector2 velocity = default, bool isAppearance = true)
        {
            if (!isActiveAndEnabled || _material == null || Time.deltaTime <= 0f) return false;

            // 移動波は停止時に追加せず、同じ魚の波頭が過密にならない間隔を保つ
            if (!isAppearance && (velocity.sqrMagnitude <= 0.000001f || _opacity <= 0f)) return false;
            if (Time.time - _lastEmissionTime < Mathf.Max(0f, _minimumInterval)) return false;

            // 出現直後は魚影が透明でも波を出し、登録後の波は魚影のフェードから独立させる
            float strength = Mathf.Max(0f, _strength) * (isAppearance ? 1f : _opacity);
            if (strength <= 0f || !OceanRippleField.Emit(_material, position,
                    Mathf.Max(0.01f, size), velocity, strength, Mathf.Max(0.01f, _duration),
                    isAppearance, gameObject.scene)) return false;

            _lastEmissionTime = Time.time;
            return true;
        }

        /// <summary>魚影の再出現に備えて発生間隔と透明度を初期化する</summary>
        /// <example>魚影をプールへ戻しても共有の波面に登録済みの波紋は自然に消える</example>
        private void OnDisable()
        {
            _lastEmissionTime = float.NegativeInfinity;
            _opacity = 0f;
        }
    }
}
