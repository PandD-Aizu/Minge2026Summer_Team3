using InventoryData;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>手元の炎と明かり、歩行画像を使った光学迷彩を共有状態に合わせる</summary>
[DefaultExecutionOrder(100)]
public sealed class PlayerItemEffects : MonoBehaviour, IHeldPlaceable
{
    [SerializeField] private GameObject _torch;
    [SerializeField] private GameObject _placedTorchPrefab;
    [SerializeField] private Light _firelight;
    [SerializeField] private MeshRenderer _body;
    [SerializeField] private VisualEffect _camouflage;
    private static readonly int BodyFrame = Shader.PropertyToID("BodyFrame");
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private ConsumableEffects _effects;
    private Texture _lastFrame;
    private bool _previousForceOff;
    public bool Ready => isActiveAndEnabled && _effects != null;

    /// <summary>ステージのプレイヤーを共通のアイテム効果に接続する</summary>
    /// <param name="effects">Rootが所有する効果と使用期限</param>
    /// <example>ステージのLifetimeScopeが注入する</example>
    public void Construct(ConsumableEffects effects) => _effects = effects;

    public GameObject PlacementPrefab => Ready && _effects.TorchLit ? _placedTorchPrefab : null;

    /// <summary>接地に成功した松明を手元から外す</summary>
    /// <example>共有の設置処理が地面に松明を生成した直後に呼ぶ</example>
    public void OnPlaced()
    {
        _effects.ReleaseTorch();
        _torch.SetActive(false);
    }

    /// <summary>歩行アニメーションの更新後に点灯と迷彩の表示を同期する</summary>
    /// <example>画像や左右の向きが変わったフレームも追従する</example>
    private void LateUpdate()
    {
        if (_effects == null) return;
        bool lit = _effects.TorchLit;
        if (_torch.activeSelf != lit) _torch.SetActive(lit);
        if (lit) _firelight.intensity = 2.4f + 0.8f * Mathf.PerlinNoise(Time.time * 9f, 0.5f);

        // 元のQuadと同じ位置・向きで、歩行中の画像を走査線状の粒子に描く
        if (!_effects.Cloaked)
        {
            EndCamouflage();
            return;
        }

        var material = _body.sharedMaterial;
        var frame = material.GetTexture(BaseMap);
        if (frame == null) return;
        if (!_camouflage.gameObject.activeSelf)
        {
            _previousForceOff = _body.forceRenderingOff;
            _body.forceRenderingOff = true;
            _camouflage.gameObject.SetActive(true);
        }

        if (frame != _lastFrame)
        {
            _camouflage.SetTexture(BodyFrame, frame);
            _lastFrame = frame;
        }
        _camouflage.transform.localScale = material.GetTextureScale(BaseMap).x < 0f
            ? new Vector3(-1, 1, 1) : Vector3.one;
    }

    /// <summary>迷彩の粒子を止め、元のQuadの表示設定を戻す</summary>
    /// <example>効果の期限到達時に呼ぶ</example>
    private void EndCamouflage()
    {
        if (!_camouflage.gameObject.activeSelf) return;
        _camouflage.gameObject.SetActive(false);
        _body.forceRenderingOff = _previousForceOff;
        _lastFrame = null;
    }

    /// <summary>プレイヤーの無効化時に表示を戻す</summary>
    /// <example>ステージの切り替え時にUnityが呼ぶ</example>
    private void OnDisable()
    {
        if (_torch != null) _torch.SetActive(false);
        if (_camouflage != null) EndCamouflage();
    }
}
