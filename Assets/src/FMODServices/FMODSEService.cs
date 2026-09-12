using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace FMODServices
{
    /// <summary>
    /// FMODのSE（サウンドエフェクト）を管理するサービスクラス
    /// </summary>
    public class FMODSEService : IDisposable
    {
        private readonly Dictionary<string, EventInstance> _seInstances = new Dictionary<string, EventInstance>();

        public void PlaySE(EventReference eventReference, string key = null)
        {
            if (eventReference.IsNull)
            {
                UnityEngine.Debug.LogError("[FMODSEService] Invalid EventReference.");
                return;
            }

            key ??= eventReference.Guid.ToString();

            if (_seInstances.TryGetValue(key, out var existingInstance))
            {
                if (!existingInstance.isValid())
                {
                    _seInstances.Remove(key);
                }
                else if (existingInstance.getPlaybackState(out var state) == FMOD.RESULT.OK &&
                         state == PLAYBACK_STATE.STOPPED)
                {
                    existingInstance.release();
                    _seInstances.Remove(key);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[FMODSEService] Already playing SE with key: {key}");
                    return;
                }
            }

            try
            {
                var instance = RuntimeManager.CreateInstance(eventReference);
                instance.start();
                _seInstances[key] = instance;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[FMODSEService] Failed to play SE: {eventReference}");
                UnityEngine.Debug.LogError(e);
            }
        }

        public void PlayOneShot(EventReference eventReference)
        {
            if (eventReference.IsNull)
            {
                UnityEngine.Debug.LogError("[FMODSEService] Invalid EventReference.");
                return;
            }

            try
            {
                RuntimeManager.PlayOneShot(eventReference);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[FMODSEService] Failed PlayOneShot SE: {eventReference}");
                UnityEngine.Debug.LogError(e);
            }
        }

        public void StopSE(string key, bool allowFadeOut = true)
        {
            if (string.IsNullOrEmpty(key) || !_seInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODSEService] SE with key not found: {key}");
                return;
            }

            var stopMode = allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE;
            instance.stop(stopMode);
            instance.release();
            instance.clearHandle();
            _seInstances.Remove(key);
        }

        public void StopAllSE(bool allowFadeOut = true)
        {
            var keys = new List<string>(_seInstances.Keys);
            foreach (var k in keys)
            {
                StopSE(k, allowFadeOut);
            }
        }

        public void PauseSE(string key)
        {
            if (string.IsNullOrEmpty(key) || !_seInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODSEService] SE with key not found: {key}");
                return;
            }

            instance.setPaused(true);
        }

        public void ResumeSE(string key)
        {
            if (string.IsNullOrEmpty(key) || !_seInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODSEService] SE with key not found: {key}");
                return;
            }

            instance.setPaused(false);
        }

        public void SetSEParameter(string key, string parameterName, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                UnityEngine.Debug.LogError($"[FMODSEService] Invalid key: {key}");
                return;
            }

            if (string.IsNullOrEmpty(parameterName))
            {
                UnityEngine.Debug.LogError($"[FMODSEService] Invalid parameter name: {parameterName}");
                return;
            }

            if (_seInstances.TryGetValue(key, out var instance))
            {
                instance.setParameterByName(parameterName, value);
            }
            else
            {
                UnityEngine.Debug.LogError($"[FMODSEService] SE with key not found: {key}");
            }
        }

        public void SetSEVolume(string key, float volume)
        {
            if (string.IsNullOrEmpty(key) || !_seInstances.TryGetValue(key, out var instance))
            {
                UnityEngine.Debug.LogWarning($"[FMODSEService] SE with key not found: {key}");
                return;
            }

            instance.setVolume(Mathf.Clamp01(volume));
        }

        public bool IsSEPlaying(string key)
        {
            if (string.IsNullOrEmpty(key) || !_seInstances.TryGetValue(key, out var instance))
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
            StopAllSE(false);
        }
    }
}
