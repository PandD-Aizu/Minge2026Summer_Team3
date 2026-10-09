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

        /// <summary>単発の主観効果音をリスナーの減衰基準位置で再生する</summary>
        /// <param name="eventReference">再生するFMODイベント</param>
        /// <example>主人公をAttenuation Objectに設定して釣竿やUIの効果音を再生する</example>
        public void PlayOneShot(EventReference eventReference)
        {
            if (eventReference.IsNull)
            {
                UnityEngine.Debug.LogError("[FMODSEService] Invalid EventReference.");
                return;
            }

            try
            {
                // カメラの位置ではなく減衰基準位置を使い、見下ろし視点の高さによる減衰を防ぐ
                var result = RuntimeManager.StudioSystem.getListenerAttributes(0, out _, out var attenuationPosition);
                if (result != FMOD.RESULT.OK)
                {
                    Debug.LogError($"[FMODSEService] Failed to get listener attenuation position: {result}");
                    return;
                }

                var position = new Vector3(attenuationPosition.x, attenuationPosition.y, attenuationPosition.z);
                RuntimeManager.PlayOneShot(eventReference, position);
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

        /// <summary>Root破棄時に所有するSEを停止して解放する</summary>
        /// <example>GameLifetimeScopeの破棄時にVContainerから呼ばれる</example>
        public void Dispose()
        {
            // FMODが先に終了している場合はネイティブAPIにアクセスしない
            if (!RuntimeManager.IsInitialized)
            {
                _seInstances.Clear();
                return;
            }

            StopAllSE(false);
        }
    }
}
