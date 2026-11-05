using System.Threading;
using Cysharp.Threading.Tasks;
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

        [Header("釣り上げの動き")]
        [SerializeField, Min(0.1f)] private float _retrieveDuration = 0.9f;
        [SerializeField, Min(0f)] private float _retrieveArcHeight = 1.5f;
        [SerializeField, Min(0f)] private float _retrieveHoldDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float _caughtFishSize = 0.8f;

        [Header("釣り上げの水しぶき")]
        [SerializeField, Range(0.1f, 2f)] private float _catchSplashScale = 0.7f;

        private OceanCaptureParticles _catchSplash;
        private bool _splashPending;
        private LineRenderer _hook;
        private SpriteRenderer _caughtFish;
        private readonly Vector3[] _hookPoints = new Vector3[12];
        private Camera _retrieveCamera;
        private bool _isRetrieving;
        private float _retrieveElapsed;
        private Vector3 _retrieveStart;
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

            if (_isRetrieving)
            {
                _retrieveElapsed += Time.deltaTime;
                UpdateRetrieve();
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

        /// <summary>海面から釣り針と、成功した場合だけ魚を引き上げる</summary>
        /// <param name="fishSprite">獲得した魚の画像、失敗時はnull</param>
        /// <param name="cancellation">釣り終了やシーン破棄を通知するトークン</param>
        /// <returns>引き上げと道具の非表示が完了するまでの待機</returns>
        /// <example>成功時はawait PlayRetrieveAsync(fish.ItemImage, token)を呼ぶ</example>
        public async UniTask PlayRetrieveAsync(Sprite fishSprite, CancellationToken cancellation)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, this.GetCancellationTokenOnDestroy());
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                if (!IsConfigured || !_isFishing) return;
                EnsureRetrieveVisuals();
                SetBiting(false);
                _retrieveCamera = Camera.main;
                _retrieveStart = _bobber.position;
                _retrieveElapsed = 0f;
                _isRetrieving = true;
                _bobber.gameObject.SetActive(false);
                _hook.gameObject.SetActive(true);
                _caughtFish.sprite = fishSprite;
                _caughtFish.gameObject.SetActive(fishSprite != null);
                _splashPending = fishSprite != null;
                if (fishSprite != null)
                {
                    // Resourcesの材質参照でビルドにも水しぶきのシェーダーを含める
                    if (_catchSplash == null)
                    {
                        var material = Resources.Load<Material>("FishingCatchSplash");
                        if (material != null)
                            _catchSplash = new OceanCaptureParticles(material.shader, gameObject.scene, useUnscaledTime: false);
                    }

                    // 魚ごとに画像の解像度が違っても、演出中の最大寸法は揃える
                    float extent = Mathf.Max(fishSprite.bounds.size.x, fishSprite.bounds.size.y);
                    _caughtFish.transform.localScale = Vector3.one * (_caughtFishSize / Mathf.Max(0.001f, extent));
                }
                UpdateRetrieve();
                await UniTask.WaitUntil(() => !_isFishing ||
                    _retrieveElapsed >= Mathf.Max(0.1f, _retrieveDuration) + Mathf.Max(0f, _retrieveHoldDuration),
                    cancellationToken: linked.Token);
            }
            finally
            {
                if (this != null) Hide();
            }
        }

        /// <summary>再利用する釣り針と魚の描画オブジェクトを初回だけ生成する</summary>
        /// <example>引き上げ開始時に呼ぶ</example>
        private void EnsureRetrieveVisuals()
        {
            if (_hook != null) return;
            var hookObject = new GameObject("RetrieveHook", typeof(LineRenderer));
            hookObject.transform.SetParent(transform, false);
            _hook = hookObject.GetComponent<LineRenderer>();
            _hook.sharedMaterial = _line.sharedMaterial;
            _hook.useWorldSpace = true;
            _hook.positionCount = _hookPoints.Length;
            _hook.startWidth = _hook.endWidth = 0.025f;
            _hook.numCapVertices = 3;
            _hook.numCornerVertices = 3;
            _hook.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hook.receiveShadows = false;

            var fishObject = new GameObject("RetrievedFish", typeof(SpriteRenderer));
            fishObject.transform.SetParent(transform, false);
            _caughtFish = fishObject.GetComponent<SpriteRenderer>();
            _caughtFish.sortingOrder = 5;
            _caughtFish.gameObject.SetActive(false);
        }

        /// <summary>竿を起こし、放物線で手元へ戻る釣り針と魚を同じ位置から更新する</summary>
        /// <example>引き上げ中のLateUpdateから呼び、ポーズ中は経過時間を止める</example>
        private void UpdateRetrieve()
        {
            float progress = Mathf.Clamp01(_retrieveElapsed / Mathf.Max(0.1f, _retrieveDuration));
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            _rod.SetPositionAndRotation(_handAnchor.position,
                _aim * Quaternion.Euler(Mathf.Lerp(-_holdAngle, -75f, eased), 0f, 0f));
            Vector3 destination = _handAnchor.position + _aim * Vector3.forward * 0.5f + Vector3.up * 0.7f;
            Vector3 position = Vector3.Lerp(_retrieveStart, destination, eased)
                + Vector3.up * (4f * _retrieveArcHeight * progress * (1f - progress));
            Quaternion facing = _retrieveCamera != null ? _retrieveCamera.transform.rotation : Quaternion.identity;
            float sway = Mathf.Sin(_retrieveElapsed * 16f) * Mathf.Lerp(18f, 5f, eased);
            Quaternion rotation = facing * Quaternion.Euler(0f, 0f, sway);

            // J字の針をカメラに向け、糸の終端と針の上端を一致させる
            _hookPoints[0] = position;
            _hookPoints[1] = position + rotation * new Vector3(0f, -0.20f, 0f);
            for (int i = 2; i < _hookPoints.Length; i++)
            {
                float angle = Mathf.Lerp(Mathf.PI, Mathf.PI * 2.15f, (i - 2f) / (_hookPoints.Length - 3f));
                _hookPoints[i] = position + rotation * new Vector3(0.09f + Mathf.Cos(angle) * 0.09f,
                    -0.20f + Mathf.Sin(angle) * 0.09f, 0f);
            }
            _hook.SetPositions(_hookPoints);
            // Spriteのピボットが中心でなくても画像の上端を針先に合わせる
            Vector3 fishOffset = Vector3.down * 0.25f;
            if (_caughtFish.sprite != null)
            {
                var bounds = _caughtFish.sprite.bounds;
                float scale = _caughtFish.transform.localScale.x;
                fishOffset += new Vector3(-bounds.center.x * scale, -bounds.max.y * scale, 0f);
            }
            _caughtFish.transform.SetPositionAndRotation(position + rotation * fishOffset, rotation);

            // 魚の上端が海面を抜けるタイミングで、元の釣り地点に一度だけ飛沫を出す
            if (_splashPending && position.y - 0.25f >= _waterSurfaceHeight)
            {
                _splashPending = false;
                var splashPosition = new Vector3(_retrieveStart.x, _waterSurfaceHeight + 0.03f, _retrieveStart.z);
                _catchSplash?.Splash(splashPosition, scale: _catchSplashScale);
            }

            for (int i = 0; i < _linePoints.Length; i++)
            {
                float t = i / (float)(_linePoints.Length - 1);
                _linePoints[i] = Vector3.Lerp(_rodTip.position, position, t)
                    + Vector3.down * (4f * _lineSag * 0.35f * t * (1f - t));
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
            _isRetrieving = false;
            _splashPending = false;
            _catchSplash?.Clear();
            if (_hook != null) _hook.gameObject.SetActive(false);
            if (_caughtFish != null)
            {
                _caughtFish.gameObject.SetActive(false);
                _caughtFish.sprite = null;
            }
            if (_biteIndicator != null) _biteIndicator.Hide();
            if (_rod != null) _rod.gameObject.SetActive(false);
            if (_bobber != null) _bobber.gameObject.SetActive(false);
            if (_line != null) _line.gameObject.SetActive(false);
        }

        /// <summary>主人公が無効になったときに釣り道具も片付ける</summary>
        /// <example>再有効化した際に以前の投射を再開しない</example>
        private void OnDisable() => Hide();

        /// <summary>シーン終了時に水しぶきの粒子と専用材質を解放する</summary>
        /// <example>プレイヤーの破棄時にUnityから呼ばれる</example>
        private void OnDestroy()
        {
            _catchSplash?.Dispose();
            _catchSplash = null;
        }
    }
}
