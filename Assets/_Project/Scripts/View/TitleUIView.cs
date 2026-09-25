using R3;
using UnityEngine;
using UnityEngine.UI;

public class TitleUIView : MonoBehaviour
{
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _settingButton;
    [SerializeField] private Button _endButton;
    [SerializeField] private GameObject _settingUI;

    public Button StartButton => _startButton;
    public Button SettingButton => _settingButton;
    public Button EndButton => _endButton;

    public Observable<Unit> OnStartButtonClick => _startButton.OnClickAsObservable();
    public Observable<Unit> OnSettingButtonClick => _settingButton.OnClickAsObservable();
    public Observable<Unit> OnEndButtonClick => _endButton.OnClickAsObservable();

    public void SetInteractable(bool interactable)
    {
        StartButton.interactable = interactable;
        SettingButton.interactable = interactable;
        EndButton.interactable = interactable;
    }
}

