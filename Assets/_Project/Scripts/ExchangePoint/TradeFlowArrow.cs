using UnityEngine;
using UnityEngine.UI;

namespace ExchangePoint
{
    /// <summary>取引の受け渡し方向を示す曲線矢印をUIのメッシュで描く</summary>
    public sealed class TradeFlowArrow : MaskableGraphic
    {
        private bool _right;
        private bool _upper;

        /// <summary>矢印の向きと曲がる側を更新する</summary>
        /// <param name="right">右へ渡す場合はtrue</param><param name="upper">上側へふくらむ場合はtrue</param>
        /// <example>売却の入金にはConfigure(false, true)を使う</example>
        public void Configure(bool right, bool upper)
        {
            _right = right;
            _upper = upper;
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>曲線を帯状の四角形と終端の三角形として描画する</summary>
        /// <param name="mesh">Unityから渡されるUIメッシュ</param>
        /// <example>レイアウトや方向が変わるとUnityが呼ぶ</example>
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            const int segments = 24;
            Vector2 previous = Point(rect, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = Point(rect, i / (float)segments);
                Vector2 direction = (next - previous).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x) * 2.5f;
                int index = mesh.currentVertCount;
                mesh.AddVert(previous - normal, color, Vector2.zero);
                mesh.AddVert(previous + normal, color, Vector2.zero);
                mesh.AddVert(next + normal, color, Vector2.zero);
                mesh.AddVert(next - normal, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
                if (i == segments)
                {
                    Vector2 wing = new Vector2(-direction.y, direction.x) * 12f;
                    index = mesh.currentVertCount;
                    mesh.AddVert(next + direction * 4f, color, Vector2.zero);
                    mesh.AddVert(next - direction * 20f + wing, color, Vector2.zero);
                    mesh.AddVert(next - direction * 20f - wing, color, Vector2.zero);
                    mesh.AddTriangle(index, index + 1, index + 2);
                }
                previous = next;
            }
        }

        /// <summary>進行度から曲線上のUI座標を返す</summary>
        /// <param name="rect">描画領域</param><param name="t">始点0から終点1</param>
        /// <returns>ローカル座標</returns><example>メッシュの各頂点を求める際に使う</example>
        private Vector2 Point(Rect rect, float t)
        {
            float x = Mathf.Lerp(0.06f, 0.94f, _right ? t : 1f - t);
            float arc = 4f * t * (1f - t);
            float y = _upper ? 0.15f + 0.65f * arc : 0.85f - 0.65f * arc;
            return new Vector2(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
        }
    }
}
