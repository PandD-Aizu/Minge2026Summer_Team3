using UnityEngine;
using FMODUnity;

/// <summary>PlayerVisualのQuadに移動方向と歩行フレームに応じた画像を表示する</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshRenderer))]
public sealed class PlayerAnimation : MonoBehaviour
{
    private enum Facing
    {
        Front,
        Back,
        Left,
        Right
    }

    private const string BaseMapName = "_BaseMap";
    private static readonly int BaseMapId = Shader.PropertyToID(BaseMapName);

    [SerializeField] private Texture2D _frontIdle;
    [SerializeField] private Texture2D _backIdle;
    [SerializeField] private Texture2D _sideIdle;
    [SerializeField] private Texture2D[] _frontWalk;
    [SerializeField] private Texture2D[] _backWalk;
    [SerializeField] private Texture2D[] _sideWalk;
    [SerializeField, Min(0.01f)] private float _secondsPerFrame = 0.12f;
    [SerializeField, Min(0f)] private float _minimumMoveSpeed = 0.05f;

    private EventReference _walkSound;
    private string _walkSoundPath;
    private CharacterController _characterController;
    private PlayerInputReader _inputReader;
    private Material _material;
    private Transform _cameraTransform;
    private Texture _displayedTexture;
    private Vector2 _baseMapScale;
    private Vector2 _baseMapOffset;
    private Facing _facing = Facing.Front;
    private bool _wasMoving;
    private bool _isMirrored;
    private float _frameElapsed;
    private int _frameIndex;

    /// <summary>Playerが配置されたステージに対応する足音を取得する</summary>
    /// <example>CampStageではWalkSand、FishingStageではWalkConcを使用する</example>
    private void Start()
    {
        // 対象外のシーンでは足音を鳴らさない
        _walkSoundPath = gameObject.scene.name switch
        {
            "CampStage" => "event:/SE/WalkSand",
            "FishingStage" => "event:/SE/WalkConc",
            _ => null
        };
    }

    /// <summary>親の移動コンポーネントとQuad専用のマテリアルを取得する</summary>
    private void Awake()
    {
        _characterController = GetComponentInParent<CharacterController>();
        _inputReader = GetComponentInParent<PlayerInputReader>();
        if (_characterController == null)
        {
            Debug.LogError("PlayerAnimationには親のCharacterControllerが必要です", this);
            enabled = false;
            return;
        }

        _material = GetComponent<MeshRenderer>().material;
        if (!_material.HasProperty(BaseMapId))
        {
            Debug.LogError("PlayerAnimationのマテリアルには_BaseMapが必要です", this);
            enabled = false;
            return;
        }

        _baseMapScale = _material.GetTextureScale(BaseMapName);
        _baseMapOffset = _material.GetTextureOffset(BaseMapName);
    }

    /// <summary>移動が反映された後に向きと歩行画像を更新する</summary>
    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;

        if (_cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null) _cameraTransform = mainCamera.transform;
        }

        Vector3 movement = _characterController.velocity;
        movement.y = 0f;
        bool moving = movement.sqrMagnitude > _minimumMoveSpeed * _minimumMoveSpeed;

        if (!moving)
        {
            _wasMoving = false;
            _frameElapsed = 0f;
            _frameIndex = 0;
            ShowTexture(GetIdleTexture(_facing), _facing == Facing.Left);
            return;
        }

        Facing nextFacing = GetFacing(movement);
        bool startedMoving = !_wasMoving;
        if (startedMoving)
        {
            _frameElapsed = 0f;
            _frameIndex = 0;
        }

        _wasMoving = true;
        _facing = nextFacing;

        Texture2D[] frames = GetWalkTextures(_facing);
        if (frames == null || frames.Length == 0)
        {
            ShowTexture(GetIdleTexture(_facing), _facing == Facing.Left);
            return;
        }

        // 向きが変わっても歩行の位相を維持し、足音の連打を防ぐ
        _frameIndex %= frames.Length;
        bool playStep = startedMoving;
        _frameElapsed += Time.deltaTime;
        while (_frameElapsed >= _secondsPerFrame)
        {
            _frameElapsed -= _secondsPerFrame;
            _frameIndex = (_frameIndex + 1) % frames.Length;
            if (_frameIndex == 0) playStep = true;
        }

        Texture2D frame = frames[_frameIndex] != null ? frames[_frameIndex] : GetIdleTexture(_facing);
        ShowTexture(frame, _facing == Facing.Left);

        // フレーム落ちで複数周しても同時に鳴らす足音は一つだけにする
        if (playStep && _characterController.isGrounded) PlayWalkSound();
    }

    /// <summary>Bankのロード完了後、歩行フレームに同期して足音を一度再生する</summary>
    /// <example>歩き始めと歩行アニメーションが一周したタイミングで呼ぶ</example>
    private void PlayWalkSound()
    {
        if (_walkSoundPath == null || !RuntimeManager.HaveAllBanksLoaded) return;

        if (_walkSound.IsNull)
            _walkSound = RuntimeManager.PathToEventReference(_walkSoundPath);

        if (!_walkSound.IsNull)
            RuntimeManager.PlayOneShot(_walkSound, _characterController.transform.position);
    }

    /// <summary>再有効化時に歩行と足音を先頭フレームから再開できるようリセットする</summary>
    private void OnDisable()
    {
        _wasMoving = false;
        _frameElapsed = 0f;
        _frameIndex = 0;
    }

    /// <summary>Quad用に生成されたマテリアルを破棄する</summary>
    private void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    /// <summary>横入力を優先し、それ以外は移動方向をカメラから見た前後左右へ変換する</summary>
    /// <param name="movement">地面に沿ったプレイヤーの移動方向</param>
    /// <returns>表示するキャラクターの向き</returns>
    /// <example>W+DやS+Dでは右向き、W+AやS+Aでは左向きになる</example>
    private Facing GetFacing(Vector3 movement)
    {
        // 斜め移動時の速度の揺れで前後と横の歩行画像が切り替わらないようにする
        if (_inputReader != null)
        {
            float horizontalInput = _inputReader.MoveInput.x;
            if (Mathf.Abs(horizontalInput) > 0.001f)
                return horizontalInput > 0f ? Facing.Right : Facing.Left;
        }

        Vector3 right = _cameraTransform != null ? _cameraTransform.right : Vector3.right;
        Vector3 forward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
        right.y = 0f;
        forward.y = 0f;
        right.Normalize();
        forward.Normalize();

        float sideways = Vector3.Dot(movement, right);
        float depth = Vector3.Dot(movement, forward);
        if (Mathf.Abs(sideways) > Mathf.Abs(depth))
            return sideways > 0f ? Facing.Right : Facing.Left;

        return depth > 0f ? Facing.Back : Facing.Front;
    }

    /// <summary>向きに対応した待機画像を選ぶ</summary>
    /// <param name="facing">最後に移動した向き</param>
    /// <returns>待機画像、横向き未設定なら横歩行の先頭画像</returns>
    private Texture2D GetIdleTexture(Facing facing)
    {
        switch (facing)
        {
            case Facing.Back:
                return _backIdle != null ? _backIdle : _frontIdle;
            case Facing.Left:
            case Facing.Right:
                return _sideIdle != null ? _sideIdle
                    : _sideWalk != null && _sideWalk.Length > 0 ? _sideWalk[0] : _frontIdle;
            default:
                return _frontIdle;
        }
    }

    /// <summary>向きに対応した歩行画像の並びを返す</summary>
    /// <param name="facing">現在移動している向き</param>
    /// <returns>登録された歩行画像、未登録なら空の並び</returns>
    private Texture2D[] GetWalkTextures(Facing facing)
    {
        switch (facing)
        {
            case Facing.Back:
                return _backWalk;
            case Facing.Left:
            case Facing.Right:
                return _sideWalk;
            default:
                return _frontWalk;
        }
    }

    /// <summary>必要なときだけBase Mapと左右反転を更新する</summary>
    /// <param name="texture">Quadに表示する画像</param>
    /// <param name="mirror">左向きとして画像を左右反転する場合はtrue</param>
    private void ShowTexture(Texture texture, bool mirror)
    {
        if (texture != null && texture != _displayedTexture)
        {
            _material.SetTexture(BaseMapId, texture);
            _displayedTexture = texture;
        }

        if (mirror == _isMirrored) return;

        Vector2 scale = _baseMapScale;
        Vector2 offset = _baseMapOffset;
        if (mirror)
        {
            scale.x = -scale.x;
            offset.x += _baseMapScale.x;
        }

        _material.SetTextureScale(BaseMapName, scale);
        _material.SetTextureOffset(BaseMapName, offset);
        _isMirrored = mirror;
    }
}
