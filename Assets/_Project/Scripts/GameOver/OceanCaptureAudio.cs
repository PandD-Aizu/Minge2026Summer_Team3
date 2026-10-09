using System;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using UnityEngine;

/// <summary>出現前の音量を抑え、入水音と落水中の高域減衰を制御する</summary>
public sealed class OceanCaptureAudio : IDisposable
{
    private Sound _sound;
    private Channel _channel;
    private ChannelGroup _master;
    private DSP _lowpass;
    private DSP _quietFader;

    /// <summary>専用フィルターで出現前の音量を18dB下げ、ユーザーの音量設定を保持する</summary>
    /// <example>捕獲カメラを有効化した直後にBeginQuiet()を呼ぶ</example>
    public void BeginQuiet()
    {
        if (!RuntimeManager.IsInitialized || _quietFader.hasHandle()) return;

        // 同じフィルターを重ねず、演出だけが所有する減衰処理を追加する
        var system = RuntimeManager.CoreSystem;
        if (system.getMasterChannelGroup(out _master) != RESULT.OK) return;
        if (system.createDSPByType(DSP_TYPE.FADER, out _quietFader) != RESULT.OK) return;

        var result = _quietFader.setParameterFloat((int)DSP_FADER.GAIN, -18f);
        if (result == RESULT.OK)
            result = _master.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, _quietFader);

        // 設定や接続に失敗したフィルターも解放する
        if (result != RESULT.OK)
        {
            _quietFader.release();
            _quietFader.clearHandle();
            UnityEngine.Debug.LogWarning("捕獲前の音量減衰に失敗: " + result);
        }
    }

    /// <summary>出現前の音量フィルターだけを外し、通常の音量へ戻す</summary>
    /// <example>タコが出現する直前や演出中断時にRestoreAmbience()を呼ぶ</example>
    public void RestoreAmbience()
    {
        // 複数回呼ばれた場合やFMOD終了後も安全に片付ける
        if (RuntimeManager.IsInitialized && _quietFader.hasHandle())
        {
            if (_master.hasHandle()) _master.removeDSP(_quietFader);
            _quietFader.release();
        }

        _quietFader.clearHandle();
    }

    /// <summary>波と気泡の音を用意し、まだ再生しない</summary>
    /// <param name="waveData">WAV形式のデータを持つTextAsset</param>
    /// <example>暗転中にnew OceanCaptureAudio(waveData)で準備する</example>
    public OceanCaptureAudio(TextAsset waveData)
    {
        if (waveData == null || !RuntimeManager.IsInitialized) return;
        var bytes = waveData.bytes;
        var info = new CREATESOUNDEXINFO { cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(), length = (uint)bytes.Length };
        var result = RuntimeManager.CoreSystem.createSound(bytes, MODE.OPENMEMORY | MODE.LOOP_OFF | MODE._2D, ref info, out _sound);
        if (result != RESULT.OK) UnityEngine.Debug.LogWarning("落水音の読み込みに失敗: " + result);
    }

    /// <summary>水面を横切った瞬間に音とローパス処理を開始する</summary>
    /// <example>最初の入水フレームに一度だけ呼ぶ</example>
    public void EnterWater()
    {
        if (!RuntimeManager.IsInitialized) return;
        var system = RuntimeManager.CoreSystem;
        if (system.getMasterChannelGroup(out _master) != RESULT.OK) return;
        if (_sound.hasHandle() && system.playSound(_sound, _master, false, out _channel) == RESULT.OK)
            _channel.setVolume(0.7f);

        if (system.createDSPByType(DSP_TYPE.LOWPASS, out _lowpass) == RESULT.OK)
        {
            _lowpass.setParameterFloat((int)DSP_LOWPASS.CUTOFF, 18000f);
            _lowpass.setParameterFloat((int)DSP_LOWPASS.RESONANCE, 1f);
            if (_master.addDSP(CHANNELCONTROL_DSP_INDEX.HEAD, _lowpass) != RESULT.OK)
            {
                _lowpass.release();
                _lowpass.clearHandle();
            }
        }
    }

    /// <summary>水中へ入る深さに合わせて音をくぐもらせる</summary>
    /// <param name="amount">0で通常、1で水中の音</param>
    /// <example>入水後にUpdateMuffling(progress)を呼ぶ</example>
    public void UpdateMuffling(float amount)
    {
        if (RuntimeManager.IsInitialized && _lowpass.hasHandle())
            _lowpass.setParameterFloat((int)DSP_LOWPASS.CUTOFF, Mathf.Lerp(18000f, 850f, Mathf.Clamp01(amount)));
    }

    /// <summary>自分で追加した処理だけを外し、音量設定を変えずに元の音へ戻す</summary>
    /// <example>帰還、中断、シーン破棄のいずれでも呼ぶ</example>
    public void Dispose()
    {
        RestoreAmbience();

        if (RuntimeManager.IsInitialized)
        {
            if (_channel.hasHandle()) _channel.stop();
            if (_lowpass.hasHandle())
            {
                if (_master.hasHandle()) _master.removeDSP(_lowpass);
                _lowpass.release();
            }
            if (_sound.hasHandle()) _sound.release();
        }
        _channel.clearHandle();
        _lowpass.clearHandle();
        _sound.clearHandle();
        _master.clearHandle();
    }
}
