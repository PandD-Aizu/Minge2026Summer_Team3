using MiniGame;
using UnityEngine;
using UnityEngine.UI;

public class RotationMiniGameView : MonoBehaviour
{
    [SerializeField] private RotationMiniGameSettings _settings;
    [SerializeField] private Image _greatZone;
    [SerializeField] private Image _goodZone;
    [SerializeField] private GameObject _pin;
    private bool _isRotating;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _greatZone.fillAmount = _settings.GreatAngle;
        _goodZone.fillAmount = _settings.GoodAngle;


    }

    // Update is called once per frame
    void Update()
    {
        //if (!_isRotating) return;
        float rotate = _settings.RotationSpeed * Time.deltaTime;
        _pin.transform.Rotate(0, 0, rotate, 0);

    }

    public void StartRotatingPin()
    {
        _isRotating = true;

    }

    public void StopRotatingPin()
    {
        _isRotating = false;
    }
}
