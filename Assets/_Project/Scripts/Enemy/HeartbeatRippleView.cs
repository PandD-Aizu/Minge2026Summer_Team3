using System;
using FMOD;
using FMOD.Studio;
using UnityEngine;

/// <summary>心音の実際の音量から拍動を検出し、カメラへ円形波の状態を渡す</summary>
[DisallowMultipleComponent]
public sealed class HeartbeatRippleView : MonoBehaviour
{
    private DSP _meter;
    private bool _armed = true;
    private float _cooldown;
    private Vector4 _waves = new(1f, 0f, 1f, 0f);

    public Vector4 Waves => _waves;
    public bool HasWaves => _waves.x < 1f || _waves.z < 1f;

    /// <summary>心音の音量の立ち上がりで波を追加し、既存の波を進める</summary>
    /// <param name="heartbeat">現在再生している心音イベント</param>
    /// <param name="levels">xが再生ピッチ、yがイベント音量</param>
    /// <param name="strength">画面の高さに対する最大歪み量</param>
    /// <param name="threshold">イベント音量で正規化した拍動検出のRMSしきい値</param>
    /// <example>追跡中のUpdateHeartbeatから毎フレーム呼ぶ</example>
    public void UpdateHeartbeat(EventInstance heartbeat, Vector2 levels, float strength, float threshold)
    {
        // 音を止めるポーズ中は波も同じ位置で停止する
        if (Time.deltaTime <= 0f) return;
        var step = Time.deltaTime * Mathf.Max(0.01f, levels.x) / 0.65f;
        _waves.x = Mathf.Min(1f, _waves.x + step);
        _waves.z = Mathf.Min(1f, _waves.z + step);
        _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);

        // FMODの非同期生成完了まで待ち、心音専用の出力だけを測定する
        if (!_meter.hasHandle())
        {
            if (heartbeat.getChannelGroup(out var group) != RESULT.OK
                || group.getDSP(CHANNELCONTROL_DSP_INDEX.HEAD, out var meter) != RESULT.OK
                || meter.setMeteringEnabled(false, true) != RESULT.OK) return;
            _meter = meter;
        }

        if (_meter.getMeteringInfo(IntPtr.Zero, out var info) != RESULT.OK)
        {
            _meter.clearHandle();
            return;
        }

        var rms = 0f;
        for (var i = 0; i < info.numchannels; i++) rms = Mathf.Max(rms, info.rmslevel[i]);
        rms /= Mathf.Max(0.001f, levels.y);
        threshold = Mathf.Max(0.001f, threshold);
        if (rms < threshold * 0.5f) _armed = true;
        if (!_armed || _cooldown > 0f || rms < threshold) return;

        // 二つの波を残すことで、ドクンという二段の拍動も重ねて表現する
        _waves.z = _waves.x;
        _waves.w = _waves.y;
        _waves.x = 0f;
        _waves.y = Mathf.Clamp(strength, 0f, 0.05f) * Mathf.Clamp01(levels.y);
        _armed = false;
        _cooldown = 0.12f / Mathf.Max(0.01f, levels.x);
    }

    /// <summary>波と計測を解除し、次の心音を新しく検出できる状態に戻す</summary>
    /// <example>心音イベントを停止・解放する直前に呼ぶ</example>
    public void Clear()
    {
        // DSPはFMODの所有物なのでreleaseせず計測だけを停止する
        if (FMODUnity.RuntimeManager.IsInitialized && _meter.hasHandle())
            _meter.setMeteringEnabled(false, false);
        _meter.clearHandle();
        _waves = new Vector4(1f, 0f, 1f, 0f);
        _armed = true;
        _cooldown = 0f;
    }

    /// <summary>カメラの無効化時に残っている波と計測を終了する</summary>
    /// <example>シーン切り替えやカメラ切り替えでUnityが呼ぶ</example>
    private void OnDisable() => Clear();
}
