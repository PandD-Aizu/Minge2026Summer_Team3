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

        public void StopBGM(string key, bool allowFadeOut = true)
        {
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
                _bgmInstances.Clear();
                return;
            }

            StopAllBGM(false);
        }
    }
}
