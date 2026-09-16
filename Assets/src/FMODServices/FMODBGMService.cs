using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;

namespace FMODServices
{
    /// <summary>
    /// FMODのBGM（バックグラウンドミュージック）を管理するサービスクラス
    /// </summary>
    public class FMODBGMService : IDisposable
    {
        private readonly Dictionary<string, EventInstance> _bgmInstances = new Dictionary<string, EventInstance>();

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
                    UnityEngine.Debug.LogWarning($"[FMOD] PlayBGM: BGM with key '{key}' is already playing.");
                    return;
                }
            }

            try
            {
                var instance = RuntimeManager.CreateInstance(eventReference);
                instance.start();
                _bgmInstances[key] = instance;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[FMOD] PlayBGM: Failed to play BGM '{eventReference}': {ex.Message}");
            }
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

        public void Dispose()
        {
            StopAllBGM(false);
        }
    }
}
