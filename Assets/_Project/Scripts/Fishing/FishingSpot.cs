using System.Threading;
using Cysharp.Threading.Tasks;
using Fishing;
using R3;
using UnityEngine;
using Controller;
using VContainer;

namespace _Project.Scripts.Fishing
{
    /// <summary>海面の指定範囲にゆっくり動く魚影を1体ずつ出現させる</summary>
    [DisallowMultipleComponent]
    public sealed class FishingSpot : MonoBehaviour
    {
        [Header("魚影")]
        [SerializeField] private FishShadow _shadowPrefab;
        [SerializeField] private Sprite _bigSprite;
        [SerializeField] private Sprite _smallSprite;
        [SerializeField, Range(0f, 1f)] private float _bigProbability = 0.5f;
        [SerializeField, Min(0.01f)] private float _bigWidth = 2f;
        [SerializeField, Min(0.01f)] private float _smallWidth = 1f;
        [SerializeField] private Vector2 _aspectCorrection = new(1f, 1.5f);
        [SerializeField] private Vector2 _headingRange = new(-25f, 25f);

        [Header("出現範囲")]
        [Tooltip("このTransformを中心とするローカルX・Z方向の幅")]
        [SerializeField] private Vector2 _areaSize = new(3f, 0.5f);
        [Tooltip("Y軸方向のオフセット")]
        [SerializeField, Min(0.001f)] private float _surfaceOffset = 0.03f;

        [Header("ゆらぎと波紋")]
        [Tooltip("出現位置から動く最大距離、ローカル座標の単位")]
        [SerializeField, Min(0f)] private float _movementDistance = 0.5f;
        [Tooltip("出現位置から移動先へ往復する秒数")]
        [SerializeField, Min(0.1f)] private float _movementPeriod = 6f;
        [Tooltip("移動中の波紋を発生させる移動距離、ワールド座標の単位")]
        [SerializeField, Min(0.01f)] private float _rippleDistance = 0.12f;

        [Header("出現タイミング（秒）")]
        [SerializeField] private Vector2 _initialDelay = new(0f, 2f);
        [SerializeField, Min(0f)] private float _fadeInDuration = 0.4f;
        [Tooltip("フェードイン完了からフェードアウト開始までの時間")]
        [SerializeField] private Vector2 _visibleDuration = new(6f, 10f);
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.4f;
        [SerializeField] private bool _respawn = true;
        [SerializeField] private Vector2 _respawnDelay = new(3f, 6f);
        [SerializeField, Tooltip("最初の魚をこのスポットの中央に固定し、獲得するまで消さない")]
        private bool _firstCatchSpot;
        [SerializeField, Tooltip("初回の固定魚を獲得するまで、このスポットの通常出現を待つ")]
        private bool _waitForFirstCatch;
        private TutorialController _tutorial;

        /// <summary>初回釣果の達成状態を受け取り、出現方法を切り替える</summary>
        /// <param name="tutorial">シーンをまたいで共有する進行状態</param>
        /// <example>FishingSpotLifetimeScopeから注入する</example>
        [Inject]
        public void Construct(TutorialController tutorial) => _tutorial = tutorial;

        private FishShadow _shadow;
        private CancellationTokenSource _spawnCancellation;
        private System.IDisposable _movementSubscription;
        private Vector3 _movementStart;
        private Vector3 _movementTarget;
        private float _movementTime;
        private float _distanceSinceRipple;
        private bool _isMoving;

        private IInteractableTarget _currentTarget;
        private FishingTargetProvider _targetProvider;

        // インタラクトできる魚影が出てるかどうか
        public bool CanInteractShadow => _currentTarget != null && _currentTarget.CanInteract;

        /// <summary>出現範囲内でゆっくり往復し、移動距離に応じて波紋を出す</summary>
        /// <example>魚影のルートを動かすため、接近判定とアイコンも同じ位置へ追従する</example>
        private void UpdateMovement()
        {
            if (!_isMoving || _shadow == null || !_shadow.gameObject.activeInHierarchy) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            _movementTime += deltaTime;
            float progress = (1f - Mathf.Cos(_movementTime * Mathf.PI * 2f / Mathf.Max(0.1f, _movementPeriod))) * 0.5f;
            Vector3 next = transform.TransformPoint(Vector3.Lerp(_movementStart, _movementTarget, progress)) + Vector3.up * _surfaceOffset;
            Vector3 displacement = next - _shadow.transform.position;
            _shadow.transform.position = next;

            // 海面上の実変位から速度を求め、停止や高さの変化だけで波紋を増やさない
            Vector2 planarDisplacement = new(displacement.x, displacement.z);
            float distance = planarDisplacement.magnitude;
            if (distance <= 0f) return;
            _distanceSinceRipple += distance;

            // 発生間隔で待つ間も移動距離を保持し、登録後は余った距離だけを次へ引き継ぐ
            float rippleDistance = Mathf.Max(0.01f, _rippleDistance);
            if (_distanceSinceRipple >= rippleDistance &&
                _shadow.EmitRipple(false, planarDisplacement / deltaTime))
            {
                _distanceSinceRipple %= rippleDistance;
            }
        }

        /// <summary>参照を確認して出現サイクルを開始する</summary>
        /// <example>スポットを再有効化した場合も初回待機から再開する</example>
        private void OnEnable()
        {
            _targetProvider = GetComponent<FishingTargetProvider>();

            if (_shadowPrefab == null || _bigSprite == null || _smallSprite == null || _targetProvider == null)
            {
                Debug.LogError("FishingSpotに魚影Prefab、大小のSprite、FishingTargetProviderを設定してください", this);
                return;
            }

            _spawnCancellation = new CancellationTokenSource();
            _movementSubscription = Observable.EveryUpdate().Subscribe(_ => UpdateMovement());
            SpawnCycleAsync(_spawnCancellation.Token).Forget();
        }

        /// <summary>待機、出現、消滅を繰り返し、各スポットの魚影を1体に保つ</summary>
        /// <param name="cancellationToken">有効期間が終了したときに待機を中断するトークン</param>
        /// <returns>ゲーム時間で進行する出現サイクル</returns>
        /// <example>再出現を無効にすると最初の魚影が消えた時点で終了する</example>
        private async UniTask SpawnCycleAsync(CancellationToken cancellationToken)
        {
            // OnEnableより後のDI注入を待ち、最初の一匹は指定スポットだけに出す
            await UniTask.WaitUntil(() => _tutorial != null, cancellationToken: cancellationToken);
            await UniTask.WaitUntil(() => _firstCatchSpot || !_waitForFirstCatch || !_tutorial.IsFirstFishingPending,
                cancellationToken: cancellationToken);
            if (!_firstCatchSpot || !_tutorial.IsFirstFishingPending)
                await WaitAsync(_initialDelay, 0f, cancellationToken);

            do
            {
                bool isFirstCatch = _firstCatchSpot && _tutorial.IsFirstFishingPending;
                PrepareShadow(isFirstCatch);

                // 透明な状態から表示し、十分に見えるようになってから操作を受け付ける
                _shadow.SetOpacity(0f);
                _shadow.SetInteractionAvailable(false);
                _shadow.gameObject.SetActive(true);
                _shadow.EmitRipple(true);
                await _shadow.FadeToAsync(1f, _fadeInDuration, cancellationToken);
                _shadow.SetInteractionAvailable(true);
                _isMoving = !isFirstCatch;

                // ミスやキャンセルでは進行が変わらないので、同じ位置で何度でも挑戦できる
                if (isFirstCatch)
                    await UniTask.WaitUntil(() => !_tutorial.IsFirstFishingPending, cancellationToken: cancellationToken);
                else
                    await WaitAsync(_visibleDuration, 0.01f, cancellationToken);

                // プロンプトを閉じながら魚影を薄くし、完了後にインスタンスを無効化する
                _isMoving = false;
                if (_shadow != null)
                {
                    _shadow.SetInteractionAvailable(false);
                    await _shadow.FadeToAsync(0f, _fadeOutDuration, cancellationToken);
                    _shadow.gameObject.SetActive(false);
                }
                if (!_respawn) break;

                await WaitAsync(_respawnDelay, 0.01f, cancellationToken);
            } while (isActiveAndEnabled);
        }

        /// <summary>再利用する魚影の外観と範囲内の往復経路を準備する</summary>
        /// <param name="fixedPosition">初回の魚をスポット中央へ固定する場合はtrue</param>
        /// <example>各出現サイクルのフェードイン前に呼ぶ</example>
        private void PrepareShadow(bool fixedPosition)
        {
            // 同じインスタンスを再利用し、待機中はプロンプトと判定も無効にする
            if (_shadow == null)
            {
                _shadow = Instantiate(_shadowPrefab, transform);
                _currentTarget = _shadow.GetComponent<IInteractableTarget>();
                _shadow.gameObject.SetActive(false);
            }

            _targetProvider.SelectFish();

            bool isBig = Random.value < _bigProbability;
            Vector3 localPoint = new(
                Random.Range(-Mathf.Abs(_areaSize.x), Mathf.Abs(_areaSize.x)) * 0.5f,
                0f,
                Random.Range(-Mathf.Abs(_areaSize.y), Mathf.Abs(_areaSize.y)) * 0.5f);
            if (fixedPosition) localPoint = Vector3.zero;

            // 出現位置の近くを移動先に選び、岸側の範囲外へ出ないよう制限する
            Vector2 direction = Random.insideUnitCircle.normalized * Mathf.Max(0f, _movementDistance);
            _movementStart = localPoint;
            _movementTarget = new Vector3(
                Mathf.Clamp(localPoint.x + direction.x, -Mathf.Abs(_areaSize.x) * 0.5f, Mathf.Abs(_areaSize.x) * 0.5f),
                0f,
                Mathf.Clamp(localPoint.z + direction.y, -Mathf.Abs(_areaSize.y) * 0.5f, Mathf.Abs(_areaSize.y) * 0.5f));
            _movementTime = 0f;
            _distanceSinceRipple = 0f;
            _shadow.transform.position = transform.TransformPoint(localPoint) + Vector3.up * _surfaceOffset;
            _shadow.Configure(isBig ? _bigSprite : _smallSprite,
                isBig ? _bigWidth : _smallWidth, _aspectCorrection,
                Random.Range(Mathf.Min(_headingRange.x, _headingRange.y), Mathf.Max(_headingRange.x, _headingRange.y)));
        }

        /// <summary>抽選した秒数だけゲーム時間で待機する</summary>
        /// <param name="range">待機秒数の上下限</param>
        /// <param name="minimum">許容する最小秒数</param>
        /// <param name="cancellationToken">無効化時の中断に使用するトークン</param>
        /// <returns>待機完了を表す処理</returns>
        /// <example>await WaitAsync(_respawnDelay, 0.01f, cancellationToken)</example>
        private static UniTask WaitAsync(Vector2 range, float minimum, CancellationToken cancellationToken)
        {
            return UniTask.Delay(System.TimeSpan.FromSeconds(SampleSeconds(range, minimum)),
                cancellationToken: cancellationToken);
        }

        /// <summary>上下限の逆転や負数を補正して待機秒数を抽選する</summary>
        /// <param name="range">最小値と最大値の組</param>
        /// <param name="minimum">許容する最小秒数</param>
        /// <returns>補正後の範囲内の秒数</returns>
        /// <example>SampleSeconds(_visibleDuration, 0.01f)で表示時間を取得する</example>
        private static float SampleSeconds(Vector2 range, float minimum)
        {
            float lower = Mathf.Max(minimum, Mathf.Min(range.x, range.y));
            float upper = Mathf.Max(lower, Mathf.Max(range.x, range.y));
            return Random.Range(lower, upper);
        }

        /// <summary>待機処理を停止し、魚影と子の接近判定を非表示にする</summary>
        /// <example>シーン切り替えやスポットの無効化時に呼ばれる</example>
        private void OnDisable()
        {
            // 再有効化後に以前の待機や購読が動作しないよう終了する
            _movementSubscription?.Dispose();
            _movementSubscription = null;
            _spawnCancellation?.Cancel();
            _spawnCancellation?.Dispose();
            _spawnCancellation = null;

            _isMoving = false;
            if (_shadow != null) _shadow.gameObject.SetActive(false);
        }

        /// <summary>選択中のスポットの出現範囲をSceneビューに表示する</summary>
        /// <example>岸から届く範囲に収まるよう水色の枠を配置する</example>
        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(_areaSize.x), 0.02f, Mathf.Abs(_areaSize.y)));
            Gizmos.matrix = previousMatrix;
        }
    }
}
