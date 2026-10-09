using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using VContainer.Unity;

namespace FMODServices
{
    /// <summary>
    /// FMODのBGM（バックグラウンドミュージック）を管理するサービスクラス
    /// </summary>
    public class FMODBGMService : IDisposable, ILateTickable
    {
        private readonly Dictionary<string, EventInstance> _bgmInstances = new Dictionary<string, EventInstance>();
        private readonly Dictionary<string, float> _fadeRemaining = new();
        private readonly List<string> _fadeKeys = new();

        /// <summary>未再生のBGMを開始し、同じキーで再生中なら再生位置を維持する</summary>
        /// <param name="eventReference">再生するFMODイベント</param>
        /// <param name="key">管理キー、省略時はイベントのGUID</param>
        /// <example>シーン到着時にPlayBGM(eventReference)を呼んで同じ曲を継続する</example>
        public void PlayBGM(EventReference eventReference, string key = null)
        {
            if (eventReference.IsNull)
            {
                UnityEngine.Debug.LogError($"[FMOD] PlayBGM: eventReference is null");
                return;
            }

            key ??= eventReference.Guid.ToString();

            // 昼に戻った場合は進行中のフェードを取り消し、同じ再生位置から音量を戻す
            if (_fadeRemaining.Remove(key) && _bgmInstances.TryGetValue(key, out var fadingInstance) &&
                fadingInstance.isValid())
            {
                fadingInstance.setVolume(1f);
            }

            if (_bgmInstances.TryGetValue(key, out var existingInstance))
            {
                if (!existingInstance.isValid())
                {
                    _bgmInstances.Remove(key);
                }
                else if (existingInstance.getPlaybackState(out var state) == FMOD.RESULT.OK &&
                         state == PLAYBACK_STATE.STOPPED)
                {
                    existingInstance.release();
                    _bgmInstances.Remove(key);
                }
                else
                {
                    // シーン遷移後の同じ曲の要求ではstartを呼ばず、既存の再生を続ける
                    return;
                }
            }

            try
            {
                var instance = RuntimeManager.CreateInstance(eventReference);
                // シーンを跨ぐBGMは現在のリスナー位置で鳴らし、3Dイベントも開始前に初期化する
                if (!TryGetBGMAttributes(out var attributes))
                {
                    instance.release();
                    UnityEngine.Debug.LogError("[FMOD] PlayBGM: Failed to get listener attributes");
                    return;
                }

                var spatialResult = instance.set3DAttributes(attributes);
                if (spatialResult != FMOD.RESULT.OK)
                {
                    instance.release();
                    UnityEngine.Debug.LogError($"[FMOD] PlayBGM: Failed to initialize BGM attributes: {spatialResult}");
                    return;
                }

                instance.start();
                _bgmInstances[key] = instance;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FMOD] PlayBGM: Failed to play BGM '{eventReference}': {ex.Message}");
            }
        }

        /// <summary>継続中のBGMを現在のリスナーへ追従させ、移動やシーン切り替えによる距離減衰を防ぐ</summary>
        /// <example>RootのVContainerから毎フレーム呼ばれる</example>
        public void LateTick()
        {
            if (!RuntimeManager.IsInitialized) return;

            // Rootで更新し、シーン遷移中やポーズ中もフェードを完了させる
            UpdateFadeOuts();

            if (_bgmInstances.Count == 0 || !RuntimeManager.IsInitialized ||
                !TryGetBGMAttributes(out var attributes))
            {
                return;
            }

            // シーンのTransformを保持せず、FMODが管理する最新のリスナー位置を使う
            foreach (var instance in _bgmInstances.Values)
            {
                if (instance.isValid())
                {
                    instance.set3DAttributes(attributes);
                }
            }
        }

        /// <summary>主リスナーの距離減衰位置と向きからBGMの3D属性を取得する</summary>
        /// <param name="attributes">BGMに設定する位置、速度、向き</param>
        /// <returns>リスナーの属性を取得できた場合はtrue</returns>
        private static bool TryGetBGMAttributes(out FMOD.ATTRIBUTES_3D attributes)
        {
            var result = RuntimeManager.StudioSystem.getListenerAttributes(0, out attributes, out var attenuationPosition);
            attributes.position = attenuationPosition;
            return result == FMOD.RESULT.OK;
        }

        /// <summary>指定BGMの音量を徐々に下げ、無音になったら停止して解放する</summary>
        /// <param name="key">PlayBGMで使用した管理キー</param>
        /// <param name="duration">フェードにかける実時間の秒数、0以下なら即停止</param>
        /// <example>夜への切り替え時にFadeOutBGM(eventReference.Guid.ToString())を呼ぶ</example>
        public void FadeOutBGM(string key, float duration = 2f)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.ContainsKey(key)) return;

            if (duration <= 0f)
            {
                StopBGM(key, false);
                return;
            }

            // 夜のシーンへ移動して再要求されてもフェード時間を延長しない
            _fadeRemaining.TryAdd(key, duration);
        }

        /// <summary>BGMごとの残り時間に応じて音量を下げ、完了したインスタンスを解放する</summary>
        /// <example>LateTickから毎フレーム呼ぶ</example>
        private void UpdateFadeOuts()
        {
            // 停止時の辞書変更に備えてキーを再利用可能なリストへ退避する
            _fadeKeys.Clear();
            _fadeKeys.AddRange(_fadeRemaining.Keys);
            foreach (var key in _fadeKeys)
            {
                var remaining = _fadeRemaining[key];
                var next = UnityEngine.Mathf.Max(0f, remaining - UnityEngine.Time.unscaledDeltaTime);
                var instance = _bgmInstances[key];
                if (!instance.isValid() || next <= 0f)
                {
                    StopBGM(key, false);
                    continue;
                }

                // イベント単位の音量だけを変更し、環境音・効果音・ユーザー設定は維持する
                instance.getVolume(out var volume);
                instance.setVolume(volume * next / remaining);
                _fadeRemaining[key] = next;
            }
        }

        public void StopBGM(string key, bool allowFadeOut = true)
        {
            _fadeRemaining.Remove(key);
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            var stopMode = allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE;
            instance.stop(stopMode);
            instance.release();
            instance.clearHandle();
            _bgmInstances.Remove(key);
        }

        public void StopAllBGM(bool allowFadeOut = true)
        {
            var keys = new List<string>(_bgmInstances.Keys);
            foreach (var k in keys)
            {
                StopBGM(k, allowFadeOut);
            }
        }

        public void PauseBGM(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            instance.setPaused(true);
        }

        public void ResumeBGM(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODBGMService] BGM with key not found: {key}");
                return;
            }

            instance.setPaused(false);
        }

        public void SwitchBGM(string oldKey, EventReference newEventReference, bool allowFadeOut = true)
        {
            StopBGM(oldKey, allowFadeOut);
            PlayBGM(newEventReference);
        }

        public void SetBGMParameter(string key, string parameterName, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] Invalid key: {key}");
                return;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] Invalid parameter name: {parameterName}");
                return;
            }

            if (_bgmInstances.TryGetValue(key, out var instance))
            {
                instance.setParameterByName(parameterName, value);
            }
            else
            {
                UnityEngine.Debug.LogError($"[FMODBGMService] BGM with key not found: {key}");
            }
        }

        public bool IsBGMPlaying(string key)
        {
            if (string.IsNullOrEmpty(key) || !_bgmInstances.TryGetValue(key, out var instance))
            {
                return false;
            }

            if (!instance.isValid())
            {
                return false;
            }

            instance.getPlaybackState(out PLAYBACK_STATE playbackState);
            return playbackState != PLAYBACK_STATE.STOPPED;
        }

        /// <summary>Root破棄時に所有するBGMを停止して解放する</summary>
        /// <example>GameLifetimeScopeの破棄時にVContainerから呼ばれる</example>
        public void Dispose()
        {
            // FMODが先に終了している場合はネイティブAPIにアクセスしない
            if (!RuntimeManager.IsInitialized)
            {
                _fadeRemaining.Clear();
                _bgmInstances.Clear();
                return;
            }

            StopAllBGM(false);
        }
    }
}
