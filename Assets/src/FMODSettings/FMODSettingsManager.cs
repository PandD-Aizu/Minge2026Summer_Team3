using FMODServices;
using JetBrains.Annotations;
using R3;
using SaveSettings;
using UnityEngine;

namespace FMODSettings
{
    /// <summary>
    /// FMODの設定を管理するMonoBehaviourクラス
    /// </summary>
    public class FMODSettingsManager : MonoBehaviour
    {
        [CanBeNull] private FMODVCAService _vcaService;
        [CanBeNull] private FMODVCASettingsView _vcaSettingsView;
        [CanBeNull] private SaveService _saveService;
    
        private void Start()
        {
            _vcaService ??= new FMODVCAService();
            _saveService ??= new SaveService();
        
            _vcaService.LinkVCAs();
        
            var vcaData = _saveService.LoadSaveData()?.audioSettings;
            if (vcaData != null)
            {
                _vcaService.InitVCAs(vcaData.masterVolume, vcaData.bgmVolume, vcaData.seVolume, vcaData.environmentVolume);
            }
        
            if (_vcaSettingsView == null)
            {
                _vcaSettingsView = FindAnyObjectByType<FMODVCASettingsView>();
            }

            if (_vcaSettingsView != null)
            {
                _vcaSettingsView.InitUIValue(_vcaService.GetMasterVolume(), _vcaService.GetBgmVolume(), _vcaService.GetSeVolume(), _vcaService.GetEnvironmentVolume());
                _vcaSettingsView.MasterVCA.Subscribe(volume => _vcaService.SetMasterVolume(volume)).AddTo(this);
                _vcaSettingsView.BgmVCA.Subscribe(volume => _vcaService.SetBgmVolume(volume)).AddTo(this);
                _vcaSettingsView.SeVCA.Subscribe(volume => _vcaService.SetSeVolume(volume)).AddTo(this);
                _vcaSettingsView.EnvironmentVCA.Subscribe(volume => _vcaService.SetEnvironmentVolume(volume)).AddTo(this);
            }
        }
    }
}
