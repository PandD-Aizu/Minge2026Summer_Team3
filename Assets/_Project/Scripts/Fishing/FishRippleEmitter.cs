using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Scripts.Fishing
{
    /// <summary>海面上の発生地点に残り、広がって消える波紋を再利用して描画する</summary>
    [DisallowMultipleComponent]
    public sealed class FishRippleEmitter : MonoBehaviour
    {
        private const int RippleCount = 6;
        private const int SegmentCount = 48;

        private static readonly Vector3[] CirclePoints = CreateCirclePoints();

        [SerializeField] private Material _material;
        [SerializeField] private Color _color = new(0.65f, 0.9f, 1f, 0.45f);
        [SerializeField, Min(0.01f)] private float _duration = 1.2f;
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.025f;

        private readonly LineRenderer[] _lines = new LineRenderer[RippleCount];
        private readonly Vector3[][] _points = new Vector3[RippleCount][];
        private readonly Vector3[] _centers = new Vector3[RippleCount];
        private readonly float[] _ages = new float[RippleCount];
        private readonly float[] _sizes = new float[RippleCount];
        private int _nextRipple;
        private float _opacity;

        /// <summary>すべての波紋で共有する単位円の頂点を一度だけ計算する</summary>
        /// <returns>X・Z平面上の単位円の頂点配列</returns>
        /// <example>描画時に半径を掛けて発生位置へ移動する</example>
        private static Vector3[] CreateCirclePoints()
        {
            var points = new Vector3[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                float angle = i * (Mathf.PI * 2f / SegmentCount);
                points[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            return points;
        }

        /// <summary>波紋用の描画オブジェクトを一度だけ用意する</summary>
        /// <example>魚影の初回出現時に6本のLineRendererを準備する</example>
        private void Awake()
        {
            for (int i = 0; i < RippleCount; i++)
            {
                var ripple = new GameObject($"Ripple_{i + 1:00}");
                ripple.transform.SetParent(transform, false);
                var line = ripple.AddComponent<LineRenderer>();
                line.sharedMaterial = _material;
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = SegmentCount;
                line.widthMultiplier = _lineWidth;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _lines[i] = line;
                _points[i] = new Vector3[SegmentCount];
            }
        }

        /// <summary>魚影のフェードに波紋の透明度を合わせる</summary>
        /// <param name="opacity">魚影の不透明度、0〜1</param>
        /// <example>FishShadow.SetOpacityから同じ不透明度を渡す</example>
        public void SetOpacity(float opacity) => _opacity = Mathf.Clamp01(opacity);

        /// <summary>指定した海面位置に波紋を発生させる</summary>
        /// <param name="position">発生時点のワールド座標、移動後も波紋はこの地点に残る</param>
        /// <param name="size">波紋の大きさを決める魚影の幅</param>
        /// <example>Emit(transform.position, 2f)で大きい魚影の出現波紋を表示する</example>
        public void Emit(Vector3 position, float size)
        {
            if (!isActiveAndEnabled || _material == null) return;

            int index = _nextRipple;
            _nextRipple = (_nextRipple + 1) % RippleCount;
            _centers[index] = position + Vector3.up * 0.015f;
            _sizes[index] = Mathf.Max(0.01f, size);
            _ages[index] = 0f;
            _lines[index].enabled = true;
            DrawRipple(index);
        }

        /// <summary>発生済みの波紋を拡大し、寿命に合わせて薄くする</summary>
        /// <example>停止中の魚影には新しい波紋を追加せず、残った波紋だけを更新する</example>
        private void Update()
        {
            for (int i = 0; i < RippleCount; i++)
            {
                if (!_lines[i].enabled) continue;
                _ages[i] += Time.deltaTime;
                if (_ages[i] >= Mathf.Max(0.01f, _duration)) _lines[i].enabled = false;
                else DrawRipple(i);
            }
        }

        /// <summary>1つの波紋の半径と透明度を更新する</summary>
        /// <param name="index">再利用する波紋配列のインデックス</param>
        /// <example>EmitとUpdateから呼び出し、頂点配列を再利用する</example>
        private void DrawRipple(int index)
        {
            float progress = Mathf.Clamp01(_ages[index] / Mathf.Max(0.01f, _duration));
            float radius = _sizes[index] * Mathf.Lerp(0.25f, 0.75f, progress);
            for (int i = 0; i < SegmentCount; i++)
            {
                _points[index][i] = _centers[index] + CirclePoints[i] * radius;
            }

            Color color = _color;
            color.a *= (1f - progress) * _opacity;
            _lines[index].startColor = color;
            _lines[index].endColor = color;
            _lines[index].SetPositions(_points[index]);
        }

        /// <summary>無効化時にすべての波紋を消し、次回の出現へ備える</summary>
        /// <example>魚影の消滅やスポットの無効化で古い波紋を残さない</example>
        private void OnDisable()
        {
            foreach (var line in _lines)
            {
                if (line != null) line.enabled = false;
            }

            _nextRipple = 0;
            _opacity = 0f;
        }
    }
}
