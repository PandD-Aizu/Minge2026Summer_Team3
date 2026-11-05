using System.Threading;
using Cysharp.Threading.Tasks;
using _Project.Scripts.InteractableObject;
using Fishing;
using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>接近判定を変形させず、海面の魚影だけに素材と向きを適用する</summary>
    [DisallowMultipleComponent]
    public sealed class FishShadow : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _visual;
        [SerializeField] private InteractableConnector _connector;
        [SerializeField] private FishRippleEmitter _ripples;
        [SerializeField, Tooltip("Sprite矩形内の頭の位置、左下が(0,0)、右上が(1,1)")]
        private Vector2 _headPoint = new(0.95f, 0.5f);

        private float _rippleSize;

        public bool IsPlayerNearby => _connector.IsPlayerNearby;

        /// <summary>Spriteの大きさ、ピボット、表示倍率を反映した頭のワールド座標</summary>
        public Vector3 HeadPosition
        {
            get
            {
                if (_visual == null || _visual.sprite == null) return transform.position;
                Bounds bounds = _visual.sprite.bounds;
                Vector3 point = new(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(_headPoint.x)),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(_headPoint.y)), 0f);
                if (_visual.flipX) point.x = -point.x;
                if (_visual.flipY) point.y = -point.y;
                return _visual.transform.TransformPoint(point);
            }
        }

        /// <summary>魚影の頭が向いている海面上の方向</summary>
        public Vector3 HeadDirection
        {
            get
            {
                Vector3 direction = HeadPosition - VisualCenter;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            }
        }

        /// <summary>ピボットや子Transformのオフセットを含む魚影の中心</summary>
        public Vector3 VisualCenter
        {
            get
            {
                if (_visual == null || _visual.sprite == null) return transform.position;
                Vector3 center = _visual.sprite.bounds.center;
                if (_visual.flipX) center.x = -center.x;
                if (_visual.flipY) center.y = -center.y;
                return _visual.transform.TransformPoint(center);
            }
        }

        /// <summary>接近判定を回さず、魚影の頭を進行方向へ向ける</summary>
        /// <param name="direction">海面上の進行方向、Y成分は無視する</param>
        /// <param name="maxDegrees">この更新で回転できる最大角度</param>
        /// <example>FaceDirection(displacement, 180f * Time.deltaTime)</example>
        public void FaceDirection(Vector3 direction, float maxDegrees)
        {
            direction.y = 0f;
            if (_visual == null || direction.sqrMagnitude < 0.0001f) return;

            // 実際の頭方向との差分を回転させ、素材の左右反転にも追従する
            float angle = Vector3.SignedAngle(HeadDirection, direction, Vector3.up);
            float step = Mathf.Clamp(angle, -Mathf.Max(0f, maxDegrees), Mathf.Max(0f, maxDegrees));
            _visual.transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * _visual.transform.rotation;
        }

        /// <summary>頭を指定位置へ合わせるための魚影ルート座標を求める</summary>
        /// <param name="headPosition">頭の目標ワールド座標</param>
        /// <returns>表示用の高さオフセットも含めて頭を合わせたルート座標</returns>
        /// <example>transform.position = RootPositionForHead(bobberPosition)</example>
        public Vector3 RootPositionForHead(Vector3 headPosition)
        {
            return transform.position + headPosition - HeadPosition;
        }

        /// <summary>現在位置へ出現時または移動中の波紋を発生させる</summary>
        /// <param name="isAppearance">出現波ならtrue、移動による航跡ならfalse</param>
        /// <param name="velocity">海面のX・Z方向の移動速度、単位はメートル毎秒、出現時は省略できる</param>
        /// <returns>海面へ波紋を登録できた場合はtrue</returns>
        /// <example>魚影を有効化した直後にEmitRipple(true)を呼ぶ</example>
        public bool EmitRipple(bool isAppearance, Vector2 velocity = default)
        {
            return _ripples != null && _ripples.Emit(transform.position, _rippleSize, velocity, isAppearance);
        }

        /// <summary>現在の透明度から指定した透明度へ変化させる</summary>
        /// <param name="opacity">目標の不透明度、0で透明、1で不透明</param>
        /// <param name="duration">変化にかけるゲーム時間、0以下なら即時反映</param>
        /// <param name="cancellationToken">スポットの無効化でキャンセルするトークン</param>
        /// <returns>フェード完了まで待機する処理</returns>
        /// <example>await shadow.FadeToAsync(0f, 0.4f, cancellationToken)で消滅を待つ</example>
        public async UniTask FadeToAsync(float opacity, float duration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_visual == null) return;

            float start = _visual.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetOpacity(Mathf.Lerp(start, opacity, Mathf.Clamp01(elapsed / duration)));
                await UniTask.NextFrame(cancellationToken);
                if (_visual == null) return;
            }

            SetOpacity(opacity);
        }

        /// <summary>画像の色を保ったまま透明度を設定する</summary>
        /// <param name="opacity">不透明度、0〜1に制限して適用する</param>
        /// <example>出現前にSetOpacity(0f)で透明にする</example>
        public void SetOpacity(float opacity)
        {
            if (_visual == null) return;
            Color color = _visual.color;
            color.a = Mathf.Clamp01(opacity);
            _visual.color = color;
            if (_ripples != null) _ripples.SetOpacity(opacity);
        }

        /// <summary>接近判定を切り替え、消滅開始時にプロンプトも非表示へ移行させる</summary>
        /// <param name="available">魚影への接近を受け付ける場合はtrue</param>
        /// <example>フェードアウトの開始前にSetInteractionAvailable(false)を呼ぶ</example>
        public void SetInteractionAvailable(bool available)
        {
            if (_connector != null) _connector.enabled = available;
        }

        /// <summary>非表示中の魚影に次の出現時の見た目を設定する</summary>
        /// <param name="sprite">表示する大小いずれかの魚影素材</param>
        /// <param name="width">補正前の魚影の横幅、単位はワールド座標</param>
        /// <param name="aspectCorrection">素材の横方向と縦方向の表示倍率</param>
        /// <param name="heading">海面上での向き、単位は度</param>
        /// <example>Configure(bigSprite, 2f, new Vector2(1f, 1.5f), 15f)</example>
        public void Configure(Sprite sprite, float width, Vector2 aspectCorrection, float heading)
        {
            if (_visual == null || sprite == null) return;

            // Spriteの元の縦横比を保ちつつ、カメラによるつぶれを補正する
            _visual.sprite = sprite;
            _rippleSize = Mathf.Max(0.01f, width) * Mathf.Max(0.01f, aspectCorrection.x);
            float scale = Mathf.Max(0.01f, width) / Mathf.Max(0.001f, sprite.bounds.size.x);
            _visual.transform.localScale = new Vector3(
                scale * Mathf.Max(0.01f, aspectCorrection.x),
                scale * Mathf.Max(0.01f, aspectCorrection.y), 1f);
            _visual.transform.rotation = Quaternion.Euler(0f, heading, 0f) * Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
