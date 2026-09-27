using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RotationMiniGameView : MonoBehaviour
{
    [SerializeField] private Image _greatZone;
    [SerializeField] private Image _goodZone;
    [SerializeField] private GameObject _pin;
    [SerializeField] private RectTransform _circles;
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TextMeshProUGUI _text;

    private RotationMiniGameSettings _settings;
    private bool _isRotating;

    void Start()
    {
        _canvas.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isRotating) return;
        if (!_settings) return;
        float rotate = _settings.RotationSpeed * Time.deltaTime;
        _pin.transform.Rotate(0, 0, rotate, Space.Self);
    }

    /// <summary>
    /// RotationMiniGameの設定を渡す
    /// </summary>
    public void ApplySettings(RotationMiniGameSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// 回転ゲームのCanvasを表示
    /// </summary>
    public void ShowMiniGameCanvas()
    {
        StopRotatingPin();

        _greatZone.fillAmount = _settings.GreatAngle / 360;
        _goodZone.fillAmount = _settings.GoodAngle / 360;

        SetBaseRotation(RandomizeBaseRotation());

        _canvas.enabled = true;
    }

    /// <summary>
    /// 回転ゲームのCanvasを非表示
    /// </summary>
    public void HideMiniGameCanvas()
    {
        StopRotatingPin();
        _canvas.enabled = false;
    }

    /// <summary>
    /// J:STOP　を非表示にする
    /// </summary>
    public void HideText()
    {
        _text.enabled = false;
    }

    /// <summary>
    /// ピンを回転させる
    /// </summary>
    public void StartRotatingPin()
    {
        _isRotating = true;

    }

    /// <summary>
    /// ピンの回転を止める
    /// </summary>
    public void StopRotatingPin()
    {
        _isRotating = false;
    }


    // ランダムに回転し、設定した角度を返す
    private float RandomizeBaseRotation()
    {
        float angle = Random.Range(0f, 360f);
        SetBaseRotation(angle);

        return angle;
    }

    // 指定した角度に回転する
    private void SetBaseRotation(float angle)
    {
        _circles.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public float GetPinAngle()
    {
            // 土台の上方向から、ピンの上方向までの角度
            float angle = Vector3.SignedAngle(
                _circles.up,
                _pin.transform.up,
                _circles.forward);

            // -180～180度を、0～360度未満に変換
            return Mathf.Repeat(angle, 360f);
    }
}
