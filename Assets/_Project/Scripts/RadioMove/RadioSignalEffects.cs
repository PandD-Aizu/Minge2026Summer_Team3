using System;
using FMOD;
using FMODUnity;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>ラジオ専用のノイズと、使い回す音符スプライトを所有する</summary>
public sealed class RadioSignalEffects : IDisposable
{
    private readonly Transform _radio;
    private readonly SpriteRenderer[] _notes = new SpriteRenderer[5];
    private readonly Texture2D _texture;
    private readonly Sprite _sprite;
    private float _noteTime;
    private float _retryAt;
    private DSP _noise;
    private DSP _filter;
    private Channel _channel;
    private FMOD.Studio.VCA _masterVolume;
    private FMOD.Studio.VCA _seVolume;

    /// <summary>音符画像と固定数の表示オブジェクトを生成する</summary>
    /// <param name="radio">音符の発生位置となるラジオ</param>
    /// <example>RadioTalkingAnimationのAwakeから一度だけ生成する</example>
    public RadioSignalEffects(Transform radio)
    {
        _radio = radio;
        _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Radio Note", filterMode = FilterMode.Bilinear };
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = (x - 10f) / 6f;
            float dy = (y - 7f) / 4f;
            bool head = dx * dx + dy * dy < 1f;
            bool stem = x >= 13 && x <= 16 && y >= 7 && y <= 27;
            bool flag = y >= 20 && y <= 27 && x >= 16 && x <= 24 && y <= 30 - (x - 16) / 2;
            pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(head || stem || flag ? 255 : 0));
        }
        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);

        // 毎フレーム生成せず、発話中だけ既存の音符を循環させる
        for (int i = 0; i < _notes.Length; i++)
        {
            var note = new GameObject("Radio Music Note " + i, typeof(SpriteRenderer));
            note.transform.SetParent(radio, false);
            var renderer = note.GetComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.enabled = false;
            _notes[i] = renderer;
        }
    }

    /// <summary>カメラから見て右上に音符を流し、発話終了時には消す</summary>
    /// <param name="speaking">ラジオが発話中か</param>
    /// <param name="deltaTime">ゲーム内の経過秒数</param>
    public void UpdateNotes(bool speaking, float deltaTime)
    {
        Camera camera = Camera.main;
        if (!speaking || camera == null)
        {
            foreach (var note in _notes) note.enabled = false;
            _noteTime = 0f;
            return;
        }
        _noteTime += deltaTime;
        for (int i = 0; i < _notes.Length; i++)
        {
            float age = _noteTime - i * 0.28f;
            var note = _notes[i];
            note.enabled = age >= 0f;
            if (age < 0f) continue;
            float phase = Mathf.Repeat(age, 1.4f) / 1.4f;
            var cameraTransform = camera.transform;
            note.transform.position = _radio.position
                + cameraTransform.right * (0.38f + phase * 0.7f + Mathf.Sin(phase * 7f + i) * 0.08f)
                + cameraTransform.up * (0.3f + phase * 0.85f) - cameraTransform.forward * 0.12f;
            note.transform.rotation = cameraTransform.rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 6f + i) * 18f);
            // 親の拡縮に依存せず、音符の表示サイズを一定に保つ
            Vector3 scale = _radio.lossyScale;
            note.transform.localScale = new Vector3(0.22f / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
                0.22f / Mathf.Max(Mathf.Abs(scale.y), 0.001f), 1f);
            note.color = new Color(1f, 0.87f, 0.38f, Mathf.Sin(phase * Mathf.PI));
        }
    }

    /// <summary>FMODのノイズ生成器を必要な間だけ再生し、MasterとSE音量を反映する</summary>
    /// <param name="volume">距離で減衰済みの音量、0なら停止する</param>
    public void UpdateNoise(float volume)
    {
        if (volume <= 0f) { StopNoise(); return; }
        if (!RuntimeManager.HaveAllBanksLoaded) return;
        if (!_noise.hasHandle())
        {
            if (Time.unscaledTime < _retryAt) return;
            _retryAt = Time.unscaledTime + 1f;
            var system = RuntimeManager.CoreSystem;
            if (system.createDSPByType(DSP_TYPE.OSCILLATOR, out _noise) != RESULT.OK) return;
            // FMOD OSCILLATORのtype=5はホワイトノイズ
            if (_noise.setParameterInt(0, 5) != RESULT.OK
                || system.playDSP(_noise, default, true, out _channel) != RESULT.OK)
            { StopNoise(); return; }
            if (system.createDSPByType(DSP_TYPE.LOWPASS, out _filter) == RESULT.OK)
            {
                _filter.setParameterFloat((int)DSP_LOWPASS.CUTOFF, 2400f);
                _channel.addDSP(0, _filter);
            }
            _masterVolume = RuntimeManager.GetVCA("vca:/Master");
            _seVolume = RuntimeManager.GetVCA("vca:/SE");
        }
        _masterVolume.getVolume(out float master);
        _seVolume.getVolume(out float se);
        _channel.setVolume(volume * master * se);
        _channel.setPaused(false);
    }

    /// <summary>自分が生成したノイズとフィルターを停止して解放する</summary>
    public void StopNoise()
    {
        if (RuntimeManager.IsInitialized)
        {
            if (_channel.hasHandle()) _channel.stop();
            if (_filter.hasHandle()) _filter.release();
            if (_noise.hasHandle()) _noise.release();
        }
        _channel.clearHandle();
        _filter.clearHandle();
        _noise.clearHandle();
    }

    /// <summary>シーン終了時に音声と生成したスプライト・画像を解放する</summary>
    public void Dispose()
    {
        StopNoise();
        foreach (var note in _notes)
            if (note != null) Object.Destroy(note.gameObject);
        Object.Destroy(_sprite);
        Object.Destroy(_texture);
    }
}
