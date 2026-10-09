using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>落水地点の飛沫と、沈む視点から浮上する気泡を管理する</summary>
public sealed class OceanCaptureParticles : IDisposable
{
    private readonly GameObject _root;
    private readonly ParticleSystem _splash;
    private readonly ParticleSystem _bubbles;
    private readonly Material _sprayMaterial;
    private readonly Material _bubbleMaterial;
    private readonly System.Random _random = new(731);
    private float _bubbleBudget;

    /// <summary>演出のシーンに専用のパーティクルと材質を用意する</summary>
    /// <param name="shader">ビルドにも含める飛沫と気泡のシェーダー</param>
    /// <param name="scene">所有するシーン</param>
    /// <example>主観カメラの起動時に生成する</example>
    public OceanCaptureParticles(Shader shader, Scene scene)
    {
        _root = new GameObject("Ocean Capture VFX");
        SceneManager.MoveGameObjectToScene(_root, scene);
        _sprayMaterial = new Material(shader) { name = "Capture Spray" };
        _bubbleMaterial = new Material(shader) { name = "Capture Bubbles" };
        _bubbleMaterial.SetFloat("_Bubble", 1f);
        _splash = CreateSystem("Water Splash", _sprayMaterial, 420, 0.85f);
        _bubbles = CreateSystem("Rising Bubbles", _bubbleMaterial, 180, 0f);
    }

    /// <summary>自動放出を止めたワールド座標の粒子を構成する</summary>
    /// <param name="name">識別名</param>
    /// <param name="material">粒子の材質</param>
    /// <param name="limit">最大粒子数</param>
    /// <param name="gravity">重力倍率</param>
    /// <returns>手動放出できる粒子システム</returns>
    private ParticleSystem CreateSystem(string name, Material material, int limit, float gravity)
    {
        var child = new GameObject(name);
        child.transform.SetParent(_root.transform, false);
        var particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startSize3D = gravity > 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true;
        main.maxParticles = limit;
        main.gravityModifier = gravity;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        // 発生直後に馴染ませ、寿命の終わりに消す
        var color = particles.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.08f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.maxParticleSize = 0.22f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
        return particles;
    }

    /// <summary>水面に接触した位置から王冠状に飛沫を飛ばす</summary>
    /// <param name="position">水面上の衝突位置</param>
    /// <param name="burst">タコの出現時は粒子数と高さ、広がりを強める</param>
    /// <example>入水フレームに一度だけ呼ぶ</example>
    public void Splash(Vector3 position, bool burst = false)
    {
        var force = burst ? 1.65f : 1f;
        for (var i = 0; i < (burst ? 280 : 140); i++)
        {
            var angle = Range(0f, Mathf.PI * 2f);
            var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var particle = new ParticleSystem.EmitParams
            {
                position = position + outward * Range(0.15f, burst ? 1.1f : 0.6f),
                velocity = (outward * Range(0.6f, 2.7f) + Vector3.up * Range(1.4f, 4.2f)) * force,
                startLifetime = Range(0.35f, 1.1f),
                startSize3D = new Vector3(Range(0.025f, 0.09f) * force, Range(0.08f, 0.25f) * force, 1f),
                startColor = new Color(0.65f, 0.87f, 0.93f, 0.85f),
                rotation = Range(0f, 360f)
            };
            _splash.Emit(particle, 1);
        }
    }

    /// <summary>カメラ前方に吐き出した気泡を、ワールド上方向へ浮上させる</summary>
    /// <param name="eye">現在の視点位置</param>
    /// <param name="rotation">視点の向き</param>
    /// <param name="elapsed">入水後の秒数</param>
    /// <param name="deltaTime">停止の影響を受けない経過秒数</param>
    /// <example>入水後の各フレームで呼ぶ</example>
    public void Tick(Vector3 eye, Quaternion rotation, float elapsed, float deltaTime)
    {
        _bubbleBudget += deltaTime * (elapsed < 0.6f ? 95f : 24f);
        var count = Mathf.Min(40, Mathf.FloorToInt(_bubbleBudget));
        _bubbleBudget -= count;
        for (var i = 0; i < count; i++)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = eye + rotation * new Vector3(Range(-0.65f, 0.65f), Range(-0.5f, -0.15f), Range(0.45f, 1.5f)),
                velocity = new Vector3(Range(-0.16f, 0.16f), Range(0.65f, 1.3f), Range(-0.16f, 0.16f)),
                startLifetime = Range(0.65f, 1.5f),
                startSize = Range(0.025f, 0.11f),
                startColor = new Color(0.5f, 0.85f, 0.91f, 0.7f)
            };
            _bubbles.Emit(particle, 1);
        }
    }

    /// <summary>ゲーム本体の乱数状態を変えず、指定範囲の値を返す</summary>
    /// <param name="min">最小値</param>
    /// <param name="max">最大値</param>
    /// <returns>範囲内の乱数</returns>
    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)_random.NextDouble());

    /// <summary>帰還や中断時に粒子と専用材質を解放する</summary>
    /// <example>カメラ演出のDeactivateから呼ぶ</example>
    public void Dispose()
    {
        if (_root != null) UnityEngine.Object.Destroy(_root);
        if (_sprayMaterial != null) UnityEngine.Object.Destroy(_sprayMaterial);
        if (_bubbleMaterial != null) UnityEngine.Object.Destroy(_bubbleMaterial);
    }
}
