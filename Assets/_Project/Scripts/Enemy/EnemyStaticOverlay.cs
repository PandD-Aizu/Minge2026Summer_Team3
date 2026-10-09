using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>発見中の画面全体に、入力を遮らない砂嵐を重ねる</summary>
public sealed class EnemyStaticOverlay : IDisposable
{
    private const int TextureSize = 128;
    private readonly GameObject _root;
    private readonly UnityEngine.UI.RawImage _image;
    private readonly CanvasGroup _opacity;
    private readonly UnityEngine.UI.RawImage[] _glitches = new UnityEngine.UI.RawImage[12];
    private readonly Texture2D _texture;
    private readonly System.Random _random = new(7319);
    private float _nextFrame;
    private float _nextGlitch;
    private float _animationTime;
    private float _intensity;
    private float _glitchStrength;
    private bool _fading;
    private float _fadeStart;
    private float _fadeElapsed;
    private float _fadeDuration;

    /// <summary>指定シーンに砂嵐専用Canvasと使い回すノイズ画像を生成する</summary>
    /// <param name="scene">敵が所属するシーン</param>
    /// <example>敵ControllerのInitializeで一度だけ生成する</example>
    public EnemyStaticOverlay(Scene scene)
    {
        // ゲーム本体の乱数列を変えずに粒状の白黒画像を作る
        _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "Enemy Static Noise",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        var pixels = new Color32[TextureSize * TextureSize];
        for (var i = 0; i < pixels.Length; i++)
        {
            var value = (byte)_random.Next(256);
            pixels[i] = new Color32(value, value, value, 255);
        }
        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);

        // 全画面へ伸ばし、Raycasterを付けず操作をそのまま通す
        _root = new GameObject("Enemy Static Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(_root, scene);
        var canvas = _root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        _opacity = _root.GetComponent<CanvasGroup>();
        _opacity.blocksRaycasts = false;
        _opacity.interactable = false;
        var imageObject = new GameObject("Static", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
        imageObject.transform.SetParent(_root.transform, false);
        _image = imageObject.GetComponent<UnityEngine.UI.RawImage>();
        _image.texture = _texture;
        _image.raycastTarget = false;
        var rect = _image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 矩形ノイズは先に確保し、点滅のたびに生成・破棄しない
        for (var i = 0; i < _glitches.Length; i++)
        {
            var block = new GameObject("Glitch " + i, typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            block.transform.SetParent(_root.transform, false);
            var graphic = block.GetComponent<UnityEngine.UI.RawImage>();
            graphic.raycastTarget = false;
            graphic.enabled = false;
            _glitches[i] = graphic;
        }
        Clear();
    }

    /// <summary>発見中の濃さを反映し、フェード中なら追跡表示へ戻す</summary>
    /// <param name="intensity">0で非表示、1で完全に画面を覆う濃さ</param>
    /// <param name="glitchStrength">矩形ノイズの強さ、0で砂嵐だけを表示する</param>
    /// <example>追跡中にSetIntensity(0.3f)を呼ぶ</example>
    public void SetIntensity(float intensity, float glitchStrength = 0.6f)
    {
        if (_root == null) return;
        intensity = Mathf.Clamp01(intensity);
        if (intensity <= 0f)
        {
            Clear();
            return;
        }

        _fading = false;
        _intensity = intensity;
        _glitchStrength = Mathf.Clamp01(glitchStrength);
        _root.SetActive(true);
        _opacity.alpha = _intensity;
        if (_glitchStrength <= 0f)
            foreach (var glitch in _glitches) glitch.enabled = false;
    }

    /// <summary>現在の濃さからフェードアウトを始め、重複要求では開始時刻を変えない</summary>
    /// <param name="duration">消え切るまでのゲーム内秒数、0なら即座に非表示</param>
    /// <example>敵のデスポーン時にBeginFadeOut(1.5f)を呼ぶ</example>
    public void BeginFadeOut(float duration)
    {
        if (_fading || _intensity <= 0f) return;
        if (duration <= 0f)
        {
            Clear();
            return;
        }

        _fading = true;
        _fadeStart = _intensity;
        _fadeElapsed = 0f;
        _fadeDuration = duration;
    }

    /// <summary>砂嵐と矩形の模様を更新し、消失後もフェードを進める</summary>
    /// <param name="deltaTime">ゲーム内の経過秒数、ポーズ中は0</param>
    /// <example>敵ControllerのTick先頭で毎フレーム呼ぶ</example>
    public void Tick(float deltaTime)
    {
        if (_root == null || _intensity <= 0f || deltaTime <= 0f) return;
        _animationTime += deltaTime;
        if (_fading)
        {
            _fadeElapsed += deltaTime;
            if (_fadeElapsed >= _fadeDuration)
            {
                Clear();
                return;
            }

            _intensity = _fadeStart * (1f - Mathf.SmoothStep(0f, 1f, _fadeElapsed / _fadeDuration));
            _opacity.alpha = _intensity;
        }

        if (_animationTime >= _nextGlitch)
        {
            UpdateGlitches();
            _nextGlitch = _animationTime + Mathf.Lerp(0.06f, 0.16f, (float)_random.NextDouble());
        }

        // テクスチャ再生成を避け、毎秒20回だけ参照位置を切り替える
        if (_animationTime < _nextFrame) return;
        _nextFrame = _animationTime + 0.05f;
        _image.uvRect = new Rect((float)_random.NextDouble(), (float)_random.NextDouble(),
            Mathf.Max(1f, Screen.width / (TextureSize * 2f)),
            Mathf.Max(1f, Screen.height / (TextureSize * 2f)));
    }

    /// <summary>画面内のランダムな位置に色付きの帯とブロックを短く表示する</summary>
    /// <example>Tickから秒間6〜16回呼び、同じ画像部品を再利用する</example>
    private void UpdateGlitches()
    {
        foreach (var glitch in _glitches)
        {
            // 遠距離とフェード終盤では出現頻度も下げる
            glitch.enabled = _glitchStrength > 0f
                && _random.NextDouble() < Mathf.Lerp(0.08f, 0.65f, _intensity);
            if (!glitch.enabled) continue;

            var isBand = _random.NextDouble() < 0.65;
            var width = Mathf.Lerp(isBand ? 0.18f : 0.03f, isBand ? 0.65f : 0.18f, (float)_random.NextDouble());
            var height = Mathf.Lerp(0.004f, isBand ? 0.022f : 0.09f, (float)_random.NextDouble());
            var position = new Vector2((float)_random.NextDouble() * (1f - width),
                (float)_random.NextDouble() * (1f - height));
            var rect = glitch.rectTransform;
            rect.anchorMin = position;
            rect.anchorMax = position + new Vector2(width, height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // ざらつく矩形と単色の欠損ブロックを混ぜる
            glitch.texture = _random.NextDouble() < 0.6 ? _texture : Texture2D.whiteTexture;
            glitch.uvRect = new Rect((float)_random.NextDouble(), (float)_random.NextDouble(),
                width * Screen.width / TextureSize, height * Screen.height / TextureSize);
            var tint = _random.Next(4) switch
            {
                0 => new Color(0.25f, 0.85f, 1f),
                1 => new Color(1f, 0.3f, 0.55f),
                2 => new Color(0.04f, 0.04f, 0.06f),
                _ => Color.white
            };
            tint.a = _glitchStrength * Mathf.Lerp(0.45f, 0.95f, (float)_random.NextDouble());
            glitch.color = tint;
        }
    }

    /// <summary>砂嵐の描画を即座に停止する</summary>
    /// <example>捕獲演出やシーン破棄で画面をすぐ戻す場合に呼ぶ</example>
    public void Clear()
    {
        if (_root != null) _root.SetActive(false);
        _intensity = 0f;
        _fading = false;
        if (_opacity != null) _opacity.alpha = 0f;
        foreach (var glitch in _glitches)
            if (glitch != null) glitch.enabled = false;
        _animationTime = 0f;
        _nextFrame = 0f;
        _nextGlitch = 0f;
    }

    /// <summary>生成したCanvasとノイズ画像を解放する</summary>
    /// <example>敵ControllerのDisposeから呼ぶ</example>
    public void Dispose()
    {
        Clear();
        if (_root != null) UnityEngine.Object.Destroy(_root);
        if (_texture != null) UnityEngine.Object.Destroy(_texture);
    }
}
