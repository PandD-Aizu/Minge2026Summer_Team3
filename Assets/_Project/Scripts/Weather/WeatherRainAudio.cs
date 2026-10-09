using System;
using System.Collections.Generic;
using System.Text;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace Minge2026.Weather
{
    /// <summary>天候に合わせてEnvironmentの雨音をフェードし、FMODインスタンスを管理する</summary>
    [DisallowMultipleComponent]
    public sealed class WeatherRainAudio : MonoBehaviour
    {
        private const string DefaultEventPath = "event:/Environment/Rain";
        private const float BankLoadTimeout = 30f;
        private const float SilentThreshold = 0.001f;

        [SerializeField] private string _eventPath = DefaultEventPath;
        [SerializeField, Range(0f, 1f)] private float _maximumVolume = 0.65f;
        [SerializeField, Min(0.01f)] private float _fadeDuration = 1.5f;
        [SerializeField, Range(1f, 5000f)] private float _thunderDistance = 170f;
        [SerializeField, Range(0f, 6f)] private float _thunderVolume = 1.2f;

        private EventInstance _rainEvent;
        private FMOD.DSP _thunderDsp;
        private FMOD.Channel _thunderChannel;
        private readonly HashSet<IntPtr> _visitedDsps = new();
        private PARAMETER_DESCRIPTION _intensityParameter;
        private float _targetIntensity;
        private float _currentIntensity;
        private float _bankWaitStartedAt = -1f;
        private bool _hasIntensityParameter;
        private bool _is3D;
        private bool _hasFailed;
        private bool _applicationPaused;
        private bool _isQuitting;
        private bool _thunderWarningShown;
        private bool _ownsThunderDsp;
        private float _nextDspSearchTime;
        private int _autoThunderIndex = -1;
        private int _thunderTriggerIndex = -1;
        private int _thunderDistanceIndex = -1;
        private int _thunderVolumeIndex = -1;
        private int _pluginIntensityIndex = -1;
        private int _pluginRainVolumeIndex = -1;

        /// <summary>雨音イベントを指定し、以前のイベントを停止して次の降雨に備える</summary>
        /// <param name="eventPath">FMODイベントパス、空ならevent:/Environment/Rainを使用する</param>
        /// <example>weatherAudio.Initialize("event:/Environment/Rain")</example>
        public void Initialize(string eventPath)
        {
            string nextPath = string.IsNullOrWhiteSpace(eventPath) ? DefaultEventPath : eventPath;
            if (string.Equals(_eventPath, nextPath, StringComparison.Ordinal) && !_hasFailed)
                return;

            // パスを変更したときだけ所有中のイベントを入れ替える
            StopAndRelease();
            _eventPath = nextPath;
            _hasFailed = false;
            _bankWaitStartedAt = -1f;
        }

        /// <summary>雨音の目標強度を設定し、毎フレームの更新で滑らかに音量を変える</summary>
        /// <param name="intensity">雨の強さ、0で無音、1で最大、範囲外は0〜1に制限する</param>
        /// <example>雨が上がったらweatherAudio.SetRainIntensity(0f)を呼ぶ</example>
        public void SetRainIntensity(float intensity)
        {
            _targetIntensity = float.IsNaN(intensity) ? 0f : Mathf.Clamp01(intensity);
        }

        /// <summary>現在の雨音に含まれる雷鳴を一度だけ鳴らす</summary>
        /// <example>稲妻を表示するフレームに呼び、設定距離に応じて少し遅れて雷鳴を鳴らす</example>
        public void TriggerThunder()
        {
            if (!isActiveAndEnabled || _applicationPaused || Time.timeScale <= 0f ||
                !RuntimeManager.IsInitialized || !_rainEvent.isValid())
                return;

            try
            {
                // ゲーム側の落雷抽選だけで鳴らし、プラグイン側の自動抽選と重複させない
                UpdateThunderPlugin();
                if (!_thunderDsp.hasHandle())
                    TryCreateThunderDsp();

                if (_thunderDsp.hasHandle() && _thunderTriggerIndex >= 0 &&
                    _thunderDsp.setParameterBool(_thunderTriggerIndex, true) == FMOD.RESULT.OK)
                    return;
            }
            catch (Exception exception)
            {
                // 雷鳴の取得失敗で、正常に再生できているRainまで停止させない
                if (_ownsThunderDsp)
                    ReleaseThunderDsp();
                WarnThunderUnavailable(exception.Message);
                return;
            }

            WarnThunderUnavailable("ThunderStormプラグインを取得できなかった");
        }

        /// <summary>再有効化後に雨音を作り直せるよう、失敗状態とフェードを初期化する</summary>
        /// <example>天候オブジェクトを再表示したときにUnityから呼ばれる</example>
        private void OnEnable()
        {
            _hasFailed = false;
            _currentIntensity = 0f;
            _bankWaitStartedAt = -1f;
        }

        /// <summary>雨音のフェード、リスナー追従、ゲームのポーズ状態を更新する</summary>
        /// <example>Unityのフレーム更新から呼ばれる</example>
        private void Update()
        {
            if (_hasFailed || _isQuitting)
                return;

            // ポーズ中は再生位置とフェードを保持し、復帰時に同じ雨音を続ける
            bool paused = _applicationPaused || Time.timeScale <= 0f;
            if (!paused)
                _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity,
                    Time.deltaTime / Mathf.Max(0.01f, _fadeDuration));

            if (_currentIntensity <= SilentThreshold && _targetIntensity <= SilentThreshold)
            {
                StopAndRelease();
                return;
            }

            try
            {
                // Bankの読み込み完了前にはイベントを解決しない
                if (!_rainEvent.isValid())
                {
                    if (paused || !TryStartRain())
                        return;
                }

                _rainEvent.setPaused(paused);
                if (_ownsThunderDsp && _thunderChannel.hasHandle())
                    _thunderChannel.setPaused(paused);
                _rainEvent.setVolume(_currentIntensity * _maximumVolume);
                UpdateIntensityParameter();
                UpdateSpatialAttributes();
                UpdateThunderPlugin();
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
            }
        }

        /// <summary>Bankを待って雨音を一度だけ開始し、利用可能なイベントパラメータを取得する</summary>
        /// <returns>雨音のインスタンスを正常に開始できた場合はtrue</returns>
        /// <example>降雨が始まったUpdateから呼ぶ</example>
        private bool TryStartRain()
        {
            if (_bankWaitStartedAt < 0f)
                _bankWaitStartedAt = Time.realtimeSinceStartup;

            // HaveAllBanksLoadedへのアクセスでRuntimeManagerの自動初期化も実行する
            if (!RuntimeManager.HaveAllBanksLoaded)
            {
                if (Time.realtimeSinceStartup - _bankWaitStartedAt >= BankLoadTimeout)
                    Fail("FMOD Bankの読み込みが30秒以内に完了しなかった");
                return false;
            }

            _rainEvent = RuntimeManager.CreateInstance(_eventPath);
            if (!_rainEvent.isValid())
            {
                Fail("雨音イベントのインスタンスを作成できなかった");
                return false;
            }

            if (_rainEvent.getDescription(out var description) == FMOD.RESULT.OK)
            {
                description.is3D(out _is3D);
                FindIntensityParameter(description);
            }

            // 開始前に音量と位置を設定し、初フレームの大音量や距離減衰を防ぐ
            _rainEvent.setVolume(_currentIntensity * _maximumVolume);
            UpdateIntensityParameter();
            UpdateSpatialAttributes();
            var result = _rainEvent.start();
            if (result != FMOD.RESULT.OK)
            {
                Fail($"雨音の再生を開始できなかった: {result}");
                return false;
            }

            return true;
        }

        /// <summary>雨の強さを表す、書き込み可能なローカル連続パラメータだけを探す</summary>
        /// <param name="description">雨音イベントの定義</param>
        /// <example>イベント生成時に一度呼び、プラグイン内部パラメータは対象にしない</example>
        private void FindIntensityParameter(EventDescription description)
        {
            _hasIntensityParameter = false;
            if (description.getParameterDescriptionCount(out int count) != FMOD.RESULT.OK)
                return;

            for (int index = 0; index < count; index++)
            {
                if (description.getParameterDescriptionByIndex(index, out var parameter) != FMOD.RESULT.OK)
                    continue;

                string name = parameter.name;
                bool matches = string.Equals(name, "RainIntensity", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(name, "Rain Intensity", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(name, "Intensity", StringComparison.OrdinalIgnoreCase);
                if (!matches || parameter.type != PARAMETER_TYPE.GAME_CONTROLLED ||
                    parameter.flags != 0 || parameter.maximum <= parameter.minimum)
                    continue;

                _intensityParameter = parameter;
                _hasIntensityParameter = true;
                return;
            }
        }

        /// <summary>雨の強度をイベント側の範囲へ変換し、失敗時は音量制御だけに戻す</summary>
        /// <example>パラメータ未設定のEnvironment/Rainでは何もせず音量フェードを使う</example>
        private void UpdateIntensityParameter()
        {
            if (!_hasIntensityParameter)
                return;

            float value = Mathf.Lerp(_intensityParameter.minimum, _intensityParameter.maximum, _currentIntensity);
            if (_rainEvent.setParameterByID(_intensityParameter.id, value) != FMOD.RESULT.OK)
                _hasIntensityParameter = false;
        }

        /// <summary>3Dの雨音イベントをリスナーの減衰基準位置に追従させる</summary>
        /// <example>イベントを3Dへ変更した場合も移動で雨音が消えないようUpdateから呼ぶ</example>
        private void UpdateSpatialAttributes()
        {
            if (!_is3D || !RuntimeManager.IsInitialized)
                return;

            if (RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes,
                    out var attenuationPosition) != FMOD.RESULT.OK)
                return;

            attributes.position = attenuationPosition;
            _rainEvent.set3DAttributes(attributes);
        }

        /// <summary>この雨音が持つDSPを探し、自動落雷を止めて雨量と雷鳴の設定を反映する</summary>
        /// <example>再生開始後に非同期で作られるDSPや、ループで再生成されたDSPをUpdateから取得する</example>
        private void UpdateThunderPlugin()
        {
            // イベント側のループや停止で専用チャンネルが終了した場合は次の落雷で作り直す
            if (_ownsThunderDsp && _thunderChannel.hasHandle() &&
                (_thunderChannel.isPlaying(out bool playing) != FMOD.RESULT.OK || !playing))
                ReleaseThunderDsp();

            // 32秒のループ境界でDSPが作り直された場合はハンドルを再取得する
            if (_thunderDsp.hasHandle() &&
                _thunderDsp.setParameterFloat(_autoThunderIndex, 0f) != FMOD.RESULT.OK)
                ReleaseThunderDsp();

            if (!_thunderDsp.hasHandle())
            {
                if (Time.unscaledTime < _nextDspSearchTime)
                    return;

                _nextDspSearchTime = Time.unscaledTime + 0.5f;
                if (_rainEvent.getChannelGroup(out var group) != FMOD.RESULT.OK ||
                    group.getDSP(0, out var output) != FMOD.RESULT.OK)
                    return;

                // イベント出力より上流だけを検索し、他の環境音やMaster busには触れない
                _visitedDsps.Clear();
                FindThunderDsp(output, 0);
                if (!_thunderDsp.hasHandle())
                    return;
            }

            _thunderDsp.setParameterFloat(_autoThunderIndex, 0f);
            if (_pluginIntensityIndex >= 0)
                _thunderDsp.setParameterFloat(_pluginIntensityIndex, _ownsThunderDsp ? 0f : _currentIntensity);
            if (_thunderDistanceIndex >= 0)
                _thunderDsp.setParameterFloat(_thunderDistanceIndex, _thunderDistance);
            if (_thunderVolumeIndex >= 0)
                _thunderDsp.setParameterFloat(_thunderVolumeIndex, _thunderVolume);
        }

        /// <summary>イベント内の音声入力を遡り、ThunderStormのDSPを一つ取得する</summary>
        /// <param name="dsp">検索を始めるイベント配下のDSP</param>
        /// <param name="depth">現在の探索深さ、16階層まで検索する</param>
        /// <example>UpdateThunderPluginからイベント出力のDSPを渡す</example>
        private void FindThunderDsp(FMOD.DSP dsp, int depth)
        {
            if (_thunderDsp.hasHandle() || depth > 16 || _visitedDsps.Count >= 64 ||
                !dsp.hasHandle() || !_visitedDsps.Add(dsp.handle))
                return;

            if (dsp.getInfo(out string name, out _, out _, out _, out _) == FMOD.RESULT.OK &&
                string.Equals(name, "ThunderStorm", StringComparison.OrdinalIgnoreCase))
            {
                CacheThunderParameters(dsp);
                return;
            }

            if (dsp.getNumInputs(out int count) != FMOD.RESULT.OK)
                return;

            for (int index = 0; index < count && !_thunderDsp.hasHandle(); index++)
            {
                if (dsp.getInput(index, out var input, out _) == FMOD.RESULT.OK)
                    FindThunderDsp(input, depth + 1);
            }
        }

        /// <summary>プラグインが公開する名前と型を確認し、制御対象のパラメータ番号を保存する</summary>
        /// <param name="dsp">この雨音が所有するThunderStormプラグイン</param>
        /// <example>DSPを発見したときに呼び、プラグイン更新による番号変更に備える</example>
        private void CacheThunderParameters(FMOD.DSP dsp)
        {
            _autoThunderIndex = _thunderTriggerIndex = _thunderDistanceIndex = -1;
            _thunderVolumeIndex = _pluginIntensityIndex = -1;
            _pluginRainVolumeIndex = -1;
            if (dsp.getNumParameters(out int count) != FMOD.RESULT.OK)
                return;

            for (int index = 0; index < count; index++)
            {
                if (dsp.getParameterInfo(index, out var parameter) != FMOD.RESULT.OK)
                    continue;

                string name = Encoding.UTF8.GetString(parameter.name).TrimEnd('\0');
                if (name == "Thunder" && parameter.type == FMOD.DSP_PARAMETER_TYPE.BOOL)
                    _thunderTriggerIndex = index;
                if (parameter.type != FMOD.DSP_PARAMETER_TYPE.FLOAT)
                    continue;

                switch (name)
                {
                    case "Auto Thunder": _autoThunderIndex = index; break;
                    case "Distance": _thunderDistanceIndex = index; break;
                    case "Thunder Volume": _thunderVolumeIndex = index; break;
                    case "Intensity": _pluginIntensityIndex = index; break;
                    case "Rain Volume": _pluginRainVolumeIndex = index; break;
                }
            }

            // 自動落雷を確実に停止できる版のプラグインだけを操作する
            if (_autoThunderIndex >= 0 && _thunderTriggerIndex >= 0 &&
                dsp.setParameterFloat(_autoThunderIndex, 0f) == FMOD.RESULT.OK)
                _thunderDsp = dsp;
        }

        /// <summary>収録済み音声のRainにDSPがない場合、同じプラグインから雷鳴専用の音源を作る</summary>
        /// <example>最初のTriggerThunderから呼び、雨音は元のFMODイベントだけを使う</example>
        private void TryCreateThunderDsp()
        {
            if (_rainEvent.getChannelGroup(out var rainGroup) != FMOD.RESULT.OK)
                return;

            var system = RuntimeManager.CoreSystem;
            if (system.getNumPlugins(FMOD.PLUGINTYPE.DSP, out int count) != FMOD.RESULT.OK)
                return;

            for (int index = 0; index < count; index++)
            {
                if (system.getPluginHandle(FMOD.PLUGINTYPE.DSP, index, out uint plugin) != FMOD.RESULT.OK ||
                    system.getPluginInfo(plugin, out _, out string name, 64, out _) != FMOD.RESULT.OK ||
                    !string.Equals(name, "ThunderStorm", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (system.createDSPByPlugin(plugin, out var dsp) != FMOD.RESULT.OK)
                    return;

                // 音源の所有権を明確にし、途中の失敗時も作成したDSPを解放する
                _ownsThunderDsp = true;
                _thunderDsp = dsp;
                CacheThunderParameters(dsp);
                if (_autoThunderIndex < 0 || _thunderTriggerIndex < 0 || _pluginRainVolumeIndex < 0 ||
                    dsp.setParameterFloat(_pluginRainVolumeIndex, 0f) != FMOD.RESULT.OK ||
                    dsp.setParameterFloat(_autoThunderIndex, 0f) != FMOD.RESULT.OK)
                {
                    ReleaseThunderDsp();
                    return;
                }

                // 元イベントの配下へ接続してEnvironment音量設定とイベントのポーズを引き継ぐ
                UpdateThunderPlugin();
                if (system.playDSP(dsp, rainGroup, true, out _thunderChannel) != FMOD.RESULT.OK)
                {
                    ReleaseThunderDsp();
                    return;
                }

                _thunderChannel.setPaused(false);
                return;
            }
        }

        /// <summary>自作した雷鳴DSPだけを停止して解放し、イベント由来のDSPは参照だけを捨てる</summary>
        /// <example>雨音の停止と、雷鳴DSPの初期化失敗から呼ぶ</example>
        private void ReleaseThunderDsp()
        {
            if (_ownsThunderDsp && RuntimeManager.IsInitialized)
            {
                if (_thunderChannel.hasHandle())
                    _thunderChannel.stop();
                if (_thunderDsp.hasHandle())
                    _thunderDsp.release();
            }

            _thunderChannel.clearHandle();
            _thunderDsp.clearHandle();
            _ownsThunderDsp = false;
        }

        /// <summary>雷鳴を再生できなかった原因を一度だけ表示する</summary>
        /// <param name="reason">プラグインの取得や操作に失敗した原因</param>
        /// <example>雨音は継続できるが雷鳴だけ再生できない場合に呼ぶ</example>
        private void WarnThunderUnavailable(string reason)
        {
            if (_thunderWarningShown)
                return;

            _thunderWarningShown = true;
            Debug.LogWarning($"[WeatherRainAudio] 雷鳴を再生できなかった: {reason}", this);
        }

        /// <summary>アプリの中断状態を記録し、中断中の雨音を一時停止する</summary>
        /// <param name="paused">アプリが中断された場合はtrue</param>
        /// <example>バックグラウンドへの移行時にUnityから呼ばれる</example>
        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (RuntimeManager.IsInitialized && _rainEvent.isValid())
                _rainEvent.setPaused(paused || Time.timeScale <= 0f);
            if (RuntimeManager.IsInitialized && _ownsThunderDsp && _thunderChannel.hasHandle())
                _thunderChannel.setPaused(paused || Time.timeScale <= 0f);
        }

        /// <summary>雨音を停止して解放し、同じ警告を毎フレーム出さないよう再試行を止める</summary>
        /// <param name="reason">再生できなかった原因</param>
        /// <example>Bankやイベントの準備に失敗した場合に呼ぶ</example>
        private void Fail(string reason)
        {
            StopAndRelease();
            _hasFailed = true;
            Debug.LogWarning($"[WeatherRainAudio] {_eventPath}: {reason}", this);
        }

        /// <summary>所有する雨音を即座に停止して解放し、次の降雨に備える</summary>
        /// <example>無音到達、シーン終了、無効化で呼ぶ</example>
        private void StopAndRelease()
        {
            ReleaseThunderDsp();

            // FMODが先に破棄されている終了順序でもシステムを作り直さない
            if (RuntimeManager.IsInitialized && _rainEvent.isValid())
            {
                _rainEvent.stop(STOP_MODE.IMMEDIATE);
                _rainEvent.release();
            }

            _rainEvent.clearHandle();
            _visitedDsps.Clear();
            _nextDspSearchTime = 0f;
            _hasIntensityParameter = false;
            _is3D = false;
            _bankWaitStartedAt = -1f;
        }

        /// <summary>無効化と同時に雨音を解放し、多重再生やシーン外への音残りを防ぐ</summary>
        /// <example>天候オブジェクトの非表示時にUnityから呼ばれる</example>
        private void OnDisable()
        {
            StopAndRelease();
        }

        /// <summary>オブジェクト破棄時に残っているFMODインスタンスを解放する</summary>
        /// <example>シーンのアンロード時にUnityから呼ばれる</example>
        private void OnDestroy()
        {
            StopAndRelease();
        }

        /// <summary>アプリ終了時に雨音を止め、その後の再初期化を防ぐ</summary>
        /// <example>アプリ終了時にUnityから呼ばれる</example>
        private void OnApplicationQuit()
        {
            _isQuitting = true;
            StopAndRelease();
        }
    }
}
