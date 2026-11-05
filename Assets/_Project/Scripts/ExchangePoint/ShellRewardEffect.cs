using UnityEngine;
using UnityEngine.UI;

namespace ExchangePoint
{
    /// <summary>集荷所から飛び出した貝殻が残高アイコンへ集まる表示専用の演出</summary>
    public sealed class ShellRewardEffect : MonoBehaviour
    {
        private const int Capacity = 24;
        private readonly RectTransform[] _icons = new RectTransform[Capacity];
        private readonly Vector2[] _bursts = new Vector2[Capacity];
        private RectTransform _source, _target, _space;
        private float _elapsed;
        private int _count;
        public bool IsPlaying { get; private set; }

        /// <summary>入金済みの額に応じた数の貝殻を放出する</summary>
        /// <param name="source">集荷所の表示</param><param name="target">残高アイコン</param>
        /// <param name="sprite">貝殻画像</param><param name="amount">確定済み入金額</param>
        /// <example>売却成功後にPlay(station, wallet, shell, 100)を呼ぶ</example>
        public void Play(RectTransform source, RectTransform target, Sprite sprite, int amount)
        {
            Clear();
            if (amount <= 0 || source == null || target == null || sprite == null) return;
            _space = (RectTransform)transform;
            _source = source; _target = target;
            _count = Mathf.Clamp(12 + Mathf.FloorToInt(Mathf.Sqrt(amount) * 0.3f), 12, Capacity);
            for (int i = 0; i < _count; i++)
            {
                if (_icons[i] == null)
                {
                    var go = new GameObject("RewardShell", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false);
                    go.layer = gameObject.layer;
                    _icons[i] = (RectTransform)go.transform;
                    _icons[i].sizeDelta = Vector2.one * 42f;
                    var image = go.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
                }
                _icons[i].GetComponent<Image>().sprite = sprite;
                // ゲーム本体の乱数を使わず、扇状に散らしてから吸い込む
                float angle = i * 2.399963f;
                float radius = 65f + (i % 5) * 24f;
                _bursts[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + 50f);
            }
            _elapsed = 0f;
            IsPlaying = true;
            Advance(0f);
        }

        /// <summary>UIの演出をポーズに依存しない時間で進める</summary>
        /// <example>表示中にUnityが毎フレーム呼ぶ</example>
        private void Update() => Advance(Time.unscaledDeltaTime);

        /// <summary>放出、散開、残高への吸収を順番に描画する</summary>
        /// <param name="deltaTime">前回からの経過秒数</param>
        /// <example>プレビュー検証ではAdvance(0.1f)で段階的に確認できる</example>
        public void Advance(float deltaTime)
        {
            if (!IsPlaying) return;
            if (_source == null || _target == null) { Clear(); return; }
            _elapsed += Mathf.Max(0f, deltaTime);
            Vector2 start = _space.InverseTransformPoint(_source.TransformPoint(_source.rect.center));
            Vector2 end = _space.InverseTransformPoint(_target.TransformPoint(_target.rect.center));
            bool any = false;
            for (int i = 0; i < _count; i++)
            {
                float age = _elapsed - i * 0.025f;
                bool visible = age >= 0f && age < 1.1f;
                _icons[i].gameObject.SetActive(visible);
                if (age < 1.1f) any = true;
                if (!visible) continue;
                Vector2 burst = start + _bursts[i];
                float gather = Mathf.Clamp01((age - 0.30f) / 0.80f);
                Vector2 position = age < 0.30f
                    ? Vector2.Lerp(start, burst, 1f - Mathf.Pow(1f - age / 0.30f, 3f))
                    : Vector2.Lerp(burst, end, gather * gather)
                        + Vector2.up * (90f * 4f * gather * (1f - gather));
                _icons[i].localPosition = new Vector3(position.x, position.y, 0f);
                _icons[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, gather);
                _icons[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp((i % 7 - 3) * 16f, 0f, gather));
            }
            if (!any) Clear();
        }

        /// <summary>再実行や画面を閉じた際に飛行中の貝殻を即座に片付ける</summary>
        /// <example>残高や所持品は変更せず表示だけを終了する</example>
        public void Clear()
        {
            IsPlaying = false;
            foreach (var icon in _icons) if (icon != null) icon.gameObject.SetActive(false);
        }

        /// <summary>詳細パネルの非表示時に演出を中止する</summary>
        /// <example>Escで閉じるとUnityが呼ぶ</example>
        private void OnDisable() => Clear();
    }
}
