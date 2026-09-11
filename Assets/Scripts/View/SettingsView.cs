using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsView : MonoBehaviour
{
    [SerializeField] private Button settingButton;
    [SerializeField] private GameObject settingpanel;

    public void Show()
    {
        settingpanel.SetActive(true);
        
    }
    public void Close()
    {
        settingpanel.SetActive(false);
    }
}
