using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>心音の波があるゲームカメラにだけ円形の画面歪みを描く</summary>
public sealed class HeartbeatRippleRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader _shader;
    private Material _material;
    private RipplePass _pass;

    /// <summary>シリアライズされたシェーダーから描画資源を生成する</summary>
    /// <example>Rendererの構築時や設定変更時にURPが呼ぶ</example>
    public override void Create()
    {
        CoreUtils.Destroy(_material);
        _material = _shader != null ? CoreUtils.CreateEngineMaterial(_shader) : null;
        _pass = new RipplePass(_material);
    }

    /// <summary>波が残っているゲームカメラに描画パスを登録する</summary>
    /// <param name="renderer">現在のRenderer</param>
    /// <param name="renderingData">カメラを含むフレームの描画情報</param>
    /// <example>URPが各カメラの描画前に呼ぶ</example>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || renderingData.cameraData.cameraType != CameraType.Game) return;
        if (renderingData.cameraData.camera.TryGetComponent<HeartbeatRippleView>(out var ripple)
            && ripple.isActiveAndEnabled && ripple.HasWaves)
            renderer.EnqueuePass(_pass);
    }

    /// <summary>Rendererが所有する一時マテリアルを解放する</summary>
    /// <param name="disposing">URPからの明示的な破棄かどうか</param>
    /// <example>Rendererの再構築時にURPが呼ぶ</example>
    protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

    private sealed class RipplePass : ScriptableRenderPass
    {
        private static readonly int WavesId = Shader.PropertyToID("_HeartbeatWaves");
        private static readonly int AspectId = Shader.PropertyToID("_HeartbeatAspect");
        private readonly Material _material;

        /// <summary>透過物を含む画面全体をポスト処理前に歪めるパスを作る</summary>
        /// <param name="material">歪みを描くマテリアル</param>
        /// <example>FeatureのCreateから一度だけ生成する</example>
        public RipplePass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        /// <summary>現在のカラーを別のテクスチャへ歪め、後続処理の入力にする</summary>
        /// <param name="renderGraph">フレームの描画グラフ</param>
        /// <param name="frameData">現在のカメラとテクスチャ情報</param>
        /// <example>URPのRender Graph記録時に呼ばれる</example>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer
                || !cameraData.camera.TryGetComponent<HeartbeatRippleView>(out var ripple)) return;

            // 入力と出力を分け、既存カラーのサイズやHDR形式を引き継ぐ
            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "Heartbeat Ripple Color";
            descriptor.clearBuffer = false;
            descriptor.msaaSamples = MSAASamples.None;
            var destination = renderGraph.CreateTexture(descriptor);

            // 遅延実行時にもこのカメラの値を使えるよう、パス専用の値を渡す
            var properties = new MaterialPropertyBlock();
            properties.SetVector(WavesId, ripple.Waves);
            properties.SetFloat(AspectId, cameraData.camera.aspect);
            var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0)
            {
                propertyBlock = properties
            };
            renderGraph.AddBlitPass(parameters, passName: "Heartbeat Ripple");
            resources.cameraColor = destination;
        }
    }
}
