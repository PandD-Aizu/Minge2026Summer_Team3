using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>主人公の手元から竿を振り、魚影へ浮きと釣り糸を展開する</summary>
    [DisallowMultipleComponent]
    public sealed class FishingCastView : MonoBehaviour
    {
        [Header("モデルと接続位置")]
        [SerializeField] private Transform _handAnchor;
        [SerializeField] private Transform _rod;
        [SerializeField] private Transform _rodTip;
        [SerializeField] private Transform _bobber;
        [SerializeField] private LineRenderer _line;

        [Header("投げる動き")]
        [SerializeField, Min(0.01f)] private float _castDuration = 0.55f;
        [SerializeField, Min(0f)] private float _arcHeight = 1.2f;
        [SerializeField, Range(0f, 90f)] private float _holdAngle = 30f;
        [SerializeField, Min(0f)] private float _lineSag = 0.12f;

        [Header("着水後の浮き")]
        [SerializeField, Tooltip("海面のワールドY座標、魚影の深さとは別に設定する")]
        private float _waterSurfaceHeight = 0.3f;
        [SerializeField] private float _surfaceOffset = 0.06f;
        [SerializeField, Min(0f)] private float _bobHeight = 0.025f;
        [SerializeField, Min(0.01f)] private float _bobPeriod = 1.6f;

        [Header("食いつきの頭上マーク")]
        [SerializeField, Tooltip("主人公の基準位置からビックリマークの下端までのワールド座標オフセット")]
        private Vector3 _biteIndicatorOffset = new(0f, 2.6f, 0f);
        [SerializeField, Min(1f), Tooltip("1920×1080を基準とした1ドットの大きさ")]
        private float _biteIndicatorPixelSize = 3f;

        private readonly Vector3[] _linePoints = new Vector3[16];
        private FishingBiteIndicator _biteIndicator;
        private PlayerInputReader _input;
        private Vector3 _target;
        private Vector3 _launchPosition;
        private Quaternion _aim;
        private float _elapsed;
        private bool _isFishing;
        private bool _isBiting;
        private float _biteHeight;
        private float _biteRetreat;
        /// <summary>着水後に固定される浮きの基準位置、魚の接近先として使う</summary>
        public Vector3 WaterPosition => _target;
        public bool HasLanded => _isFishing && _elapsed >= _castDuration;
        public bool IsConfigured => isActiveAndEnabled && _handAnchor != null && _rod != null
            && _rodTip != null && _bobber != null && _line != null;

        /// <summary>待機中と食いつき中の浮きの揺れを切り替える</summary>
        /// <param name="biting">食いつき中ならtrue</param>
        /// <param name="height">上下動の振幅</param>
        /// <example>SetBiting(true, 0.14f)の後にSetBiteRetreatで魚と揺れを同期する</example>
        public void SetBiting(bool biting, float height = 0.14f)
        {
            _isBiting = biting;
            _biteHeight = height;
            _biteRetreat = 0f;

            // 浮きが食いつきの動きへ切り替わる瞬間だけ、頭上表示を開始する
            if (biting && _isFishing && isActiveAndEnabled)
            {
                if (_biteIndicator == null) _biteIndicator = FishingBiteIndicator.Create(transform);
                _biteIndicator.Show(_biteIndicatorOffset, _biteIndicatorPixelSize);
            }
            else if (_biteIndicator != null) _biteIndicator.Hide();
        }

        /// <summary>魚が頭を引く動きに合わせて浮きの沈み込みを戻す</summary>
        /// <param name="retreat">0なら頭が接触して沈み、1なら頭が離れて浮きが戻る</param>
        /// <example>突っつき更新時にSetBiteRetreat(retreat)を呼ぶ</example>
        public void SetBiteRetreat(float retreat) => _biteRetreat = Mathf.Clamp01(retreat);

        /// <summary>入力元を取得し、通常時には釣り道具を隠す</summary>
        /// <example>Player Prefabのルートに配置して使用する</example>
        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            Hide();
        }

        /// <summary>魚影の位置を着水先として竿と浮きの投射を開始する</summary>
        /// <param name="waterPosition">開始時点の魚影のワールド座標、XとZを着水先に使用する</param>
        /// <example>釣りの開始が受理された後にBeginCast(spot.CastPosition)を呼ぶ</example>
        public void BeginCast(Vector3 waterPosition)
        {
            if (!isActiveAndEnabled || _handAnchor == null || _rod == null ||
                _rodTip == null || _bobber == null || _line == null) return;

            // 魚影が消えたり移動したりしても着水先は開始時の海面位置に保つ
            _target = waterPosition + Vector3.up * _surfaceOffset;
            _target.y = _waterSurfaceHeight + _surfaceOffset;
            Vector3 direction = _target - _handAnchor.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            _aim = Quaternion.LookRotation(direction, Vector3.up);
            _elapsed = 0f;
            _isFishing = true;
            _isBiting = false;
            if (_biteIndicator != null) _biteIndicator.Hide();

            _line.useWorldSpace = true;
            _line.positionCount = _linePoints.Length;
            _rod.gameObject.SetActive(true);
            _bobber.gameObject.SetActive(true);
            _line.gameObject.SetActive(true);
            UpdateRod(0f);
            _launchPosition = _rodTip.position;
            UpdateTackle();
        }

        /// <summary>主人公の移動後に竿、浮き、糸を更新する</summary>
        /// <example>ポーズ中は投射と浮きの揺れも停止する</example>
        private void LateUpdate()
        {
            if (!_isFishing) return;

            // 捕獲や会話が操作を占有した場合は道具をその場に残さない
            if (_input != null && (!_input.isActiveAndEnabled || _input.IsGameplayInputBlocked))
            {
                Hide();
                return;
            }

            _elapsed += Time.deltaTime;
            UpdateTackle();
        }

        /// <summary>投射中の放物線、着水後の上下動、竿先からの糸を反映する</summary>
        /// <example>開始時と各LateUpdateで同じ配置処理を使用する</example>
        private void UpdateTackle()
        {
            float progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, _castDuration));
            UpdateRod(progress);

            Vector3 bobberPosition = Vector3.Lerp(_launchPosition, _target, progress);
            if (progress < 1f)
                bobberPosition.y += 4f * _arcHeight * progress * (1f - progress);
            else if (_isBiting)
                bobberPosition.y -= _biteHeight * (1f - _biteRetreat);
            else
                bobberPosition.y += Mathf.Sin((_elapsed - _castDuration) * Mathf.PI * 2f /
                    Mathf.Max(0.01f, _bobPeriod)) * _bobHeight;

            _bobber.SetPositionAndRotation(bobberPosition, Quaternion.identity);

            // 配列とLineRendererを再利用し、毎フレームの割り当てを避ける
            Vector3 tipPosition = _rodTip.position;
            for (int i = 0; i < _linePoints.Length; i++)
            {
                float t = i / (float)(_linePoints.Length - 1);
                _linePoints[i] = Vector3.Lerp(tipPosition, bobberPosition, t)
                    + Vector3.down * (4f * _lineSag * t * (1f - t));
            }
            _line.SetPositions(_linePoints);
        }

        /// <summary>手元を支点に竿を振り下ろし、魚影へ向けて構える</summary>
        /// <param name="progress">投射の進行度、0から1</param>
        /// <example>竿モデルはローカル+Z方向を竿先、原点を握り位置として配置する</example>
        private void UpdateRod(float progress)
        {
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 2f));
            float angle = Mathf.Lerp(-105f, -_holdAngle, swing);
            _rod.SetPositionAndRotation(_handAnchor.position, _aim * Quaternion.Euler(angle, 0f, 0f));
        }

        /// <summary>釣り道具を非表示にして、次の投射に備える</summary>
        /// <example>成功、失敗、キャンセル、シーン終了のいずれでも呼ぶ</example>
        public void Hide()
        {
            _isFishing = false;
            _isBiting = false;
            if (_biteIndicator != null) _biteIndicator.Hide();
            if (_rod != null) _rod.gameObject.SetActive(false);
            if (_bobber != null) _bobber.gameObject.SetActive(false);
            if (_line != null) _line.gameObject.SetActive(false);
        }

        /// <summary>主人公が無効になったときに釣り道具も片付ける</summary>
        /// <example>再有効化した際に以前の投射を再開しない</example>
        private void OnDisable() => Hide();
    }
}
