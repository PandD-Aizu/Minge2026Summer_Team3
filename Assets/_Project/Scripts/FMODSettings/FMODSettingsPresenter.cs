using System;
using FMODServices;
using R3;
using SaveSettings;
using VContainer.Unity;

namespace FMODSettings
{
    /// <summary>設定画面の寿命に合わせて音量表示と購読を管理する</summary>
    public sealed class FMODSettingsPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly FMODVCAService _vcaService;
        private readonly FMODAudioInitializer _initializer;
        private readonly FMODVCASettingsView _view;
        private readonly SaveService _saveService;
        private readonly CompositeDisposable _subscriptions = new();
        private bool _isBound;
        private bool _isDisposed;

        /// <summary>Rootの音声サービスと、この画面のViewを受け取る</summary>
        /// <param name="vcaService">共有の音量サービス</param>
        /// <param name="initializer">音量の初期化状態</param>
        /// <param name="view">このScopeに登録された設定画面</param>
        /// <param name="saveService">音量変更の保存先</param>
        /// <example>FMODSettingsLifetimeScopeから自動注入される</example>
        public FMODSettingsPresenter(FMODVCAService vcaService, FMODAudioInitializer initializer,
            FMODVCASettingsView view, SaveService saveService)
        {
            _vcaService = vcaService;
            _initializer = initializer;
            _view = view;
            _saveService = saveService;
        }

        /// <summary>音量の準備完了までスライダー操作を無効にする</summary>
        /// <example>設定画面のScope構築時に呼ばれる</example>
        public void Initialize()
        {
            _view.SetInteractable(false);
            Tick();
        }

        /// <summary>初期化完了後に現在の音量を表示し、変更通知を一度だけ購読する</summary>
        /// <example>Bankロードが遅れてもPlayerLoopで接続を完了する</example>
        public void Tick()
        {
            if (_isDisposed || _isBound || !_initializer.IsReady)
                return;

            // 画面を開き直した場合も保存値ではなく現在の音量を表示する
            _view.InitUIValue(_vcaService.GetMasterVolume(), _vcaService.GetBgmVolume(),
                _vcaService.GetSeVolume(), _vcaService.GetEnvironmentVolume());

            // 操作のたびに音量を反映してから、全音量の最新値を保存する
            _view.MasterVCA.Subscribe(volume =>
            {
                _vcaService.SetMasterVolume(volume);
                SaveAudioSettings();
            }).AddTo(_subscriptions);
            _view.BgmVCA.Subscribe(volume =>
            {
                _vcaService.SetBgmVolume(volume);
                SaveAudioSettings();
            }).AddTo(_subscriptions);
            _view.SeVCA.Subscribe(volume =>
            {
                _vcaService.SetSeVolume(volume);
                SaveAudioSettings();
            }).AddTo(_subscriptions);
            _view.EnvironmentVCA.Subscribe(volume =>
            {
                _vcaService.SetEnvironmentVolume(volume);
                SaveAudioSettings();
            }).AddTo(_subscriptions);

            _isBound = true;
            _view.SetInteractable(true);
        }

        /// <summary>既存のセーブデータの音量設定を現在のVCA音量で更新する</summary>
        /// <example>スライダーの変更値をVCAへ反映した直後に呼ぶ</example>
        private void SaveAudioSettings()
        {
            // ほかの保存項目を維持し、初回保存ではデータを新規作成する
            var data = _saveService.LoadSaveData() ?? new GameData();
            data.audioSettings = new AudioSettingsData
            {
                masterVolume = _vcaService.GetMasterVolume(),
                bgmVolume = _vcaService.GetBgmVolume(),
                seVolume = _vcaService.GetSeVolume(),
                environmentVolume = _vcaService.GetEnvironmentVolume()
            };

            _saveService.WriteSaveData(data);
        }

        /// <summary>画面が所有する購読を解除する</summary>
        /// <example>設定シーンのアンロード時にVContainerから呼ばれる</example>
        public void Dispose()
        {
            _isDisposed = true;
            _subscriptions.Dispose();
        }
    }
}
