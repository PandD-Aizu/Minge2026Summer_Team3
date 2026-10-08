using R3;
using UnityEngine;

public class TitleUIView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button _startButton;
    [SerializeField] private UnityEngine.UI.Button _settingButton;
    [SerializeField] private UnityEngine.UI.Button _endButton;

    public UnityEngine.UI.Button StartButton => _startButton;
    public UnityEngine.UI.Button SettingButton => _settingButton;
    public UnityEngine.UI.Button EndButton => _endButton;

    public Observable<Unit> OnStartButtonClick => _startButton.OnClickAsObservable();
    public Observable<Unit> OnSettingButtonClick => _settingButton.OnClickAsObservable();
    public Observable<Unit> OnEndButtonClick => _endButton.OnClickAsObservable();

    /// <summary>タイトルの3ボタンの操作可否を切り替える</summary>
    /// <param name="interactable">操作を受け付ける場合はtrue</param>
    /// <example>シーン遷移を始める前にSetInteractable(false)を呼ぶ</example>
    public void SetInteractable(bool interactable)
    {
        _startButton.interactable = interactable;
        _settingButton.interactable = interactable;
        _endButton.interactable = interactable;
    }

}
