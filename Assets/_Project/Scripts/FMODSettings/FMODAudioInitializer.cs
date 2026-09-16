using System;
using FMODServices;
using FMODUnity;
using SaveSettings;
using UnityEngine;
using VContainer.Unity;

namespace FMODSettings
{
    /// <summary>Bankのロード後に保存済み音量を一度だけ適用する</summary>
    public sealed class FMODAudioInitializer : IInitializable, ITickable
    {
        private const float BankLoadTimeoutSeconds = 30f;
        private readonly FMODVCAService _vcaService;
        private readonly SaveService _saveService;
        private float _loadStartedAt;

        public bool IsReady { get; private set; }
        public bool HasFailed { get; private set; }

        /// <summary>Rootが所有するサービスを受け取る</summary>
        /// <param name="vcaService">音量の取得と適用を担当するサービス</param>
        /// <param name="saveService">保存済み音量の読み込み元</param>
        /// <example>GameLifetimeScopeのEntryPoint登録で自動注入される</example>
        public FMODAudioInitializer(FMODVCAService vcaService, SaveService saveService)
        {
            _vcaService = vcaService;
            _saveService = saveService;
        }

        /// <summary>Bankの待機を開始し、準備済みならその場で音量を適用する</summary>
        /// <example>Rootコンテナ構築時にVContainerから呼ばれる</example>
        public void Initialize()
        {
            _loadStartedAt = Time.realtimeSinceStartup;
            Tick();
        }

        /// <summary>Bankの準備を確認し、成功または失敗まで初期化を進める</summary>
        /// <example>VContainerのPlayerLoopから呼ばれる</example>
        public void Tick()
        {
            if (IsReady || HasFailed)
                return;

            try
            {
                // このプロパティへのアクセスがRuntimeManagerと自動Bankロードを起動する
                if (!RuntimeManager.HaveAllBanksLoaded)
                {
                    if (Time.realtimeSinceStartup - _loadStartedAt >= BankLoadTimeoutSeconds)
                        throw new TimeoutException("FMOD Bank loading timed out");

                    return;
                }

                // VCA取得に失敗した場合は未初期化のままUIを操作させない
                _vcaService.LinkVCAs();
                var audioSettings = _saveService.LoadSaveData()?.audioSettings;
                if (audioSettings != null)
                {
                    _vcaService.InitVCAs(audioSettings.masterVolume, audioSettings.bgmVolume,
                        audioSettings.seVolume, audioSettings.environmentVolume);
                }

                // セーブがない場合はFMOD側の既定音量を維持する
                IsReady = true;
            }
            catch (Exception exception)
            {
                HasFailed = true;
                Debug.LogError($"[FMODAudioInitializer] 音量の初期化に失敗: {exception.Message}");
            }
        }
    }
}
