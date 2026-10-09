using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>落水中のゲームカメラに水膜、屈折、気泡を合成する</summary>
public sealed class OceanCaptureRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader _shader;
    private Material _material;
    private WaterPass _pass;

    /// <summary>描画用マテリアルとパスを用意する</summary>
    /// <example>Rendererの生成時にURPが呼ぶ</example>
    public override void Create()
    {
        CoreUtils.Destroy(_material);
        _material = _shader != null ? CoreUtils.CreateEngineMaterial(_shader) : null;
        _pass = new WaterPass(_material);
    }

    /// <summary>落水演出中のカメラにだけ描画を登録する</summary>
    /// <param name="renderer">現在のRenderer</param>
    /// <param name="renderingData">カメラを含む描画情報</param>
    /// <example>通常プレイではパスを追加せず負荷を避ける</example>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || renderingData.cameraData.cameraType != CameraType.Game) return;
        if (renderingData.cameraData.camera.TryGetComponent<OceanCaptureScreenView>(out var water)
            && water.isActiveAndEnabled && water.IsVisible) renderer.EnqueuePass(_pass);
    }

    /// <summary>描画資源を解放する</summary>
    /// <param name="disposing">明示的な破棄かどうか</param>
    /// <example>Rendererの再作成時に呼ばれる</example>
    protected override void Dispose(bool disposing) => CoreUtils.Destroy(_material);

    private sealed class WaterPass : ScriptableRenderPass
    {
        private static readonly int WaterId = Shader.PropertyToID("_WaterCapture");
        private readonly Material _material;

        /// <summary>水中の濁りに必要な深度とカラー入力を指定する</summary>
        /// <param name="material">水中表示のマテリアル</param>
        /// <example>FeatureのCreateから呼ぶ</example>
        public WaterPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        /// <summary>現在のカラーを独立した出力へ加工し、後続処理へ渡す</summary>
        /// <param name="renderGraph">フレームのRender Graph</param>
        /// <param name="frameData">カメラと描画資源</param>
        /// <example>入水時だけURPが実行する</example>
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>().camera;
            if (resources.isActiveTargetBackBuffer || !camera.TryGetComponent<OceanCaptureScreenView>(out var water)) return;
            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "Ocean Capture Color";
            descriptor.clearBuffer = false;
            descriptor.msaaSamples = MSAASamples.None;
            var destination = renderGraph.CreateTexture(descriptor);
            var properties = new MaterialPropertyBlock();
            properties.SetVector(WaterId, new Vector4(water.Immersion, water.SubmergedTime, water.Impact, camera.aspect));
            var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0) { propertyBlock = properties };
            using (var builder = renderGraph.AddBlitPass(parameters, passName: "Ocean Capture", returnBuilder: true))
            {
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
            }
            resources.cameraColor = destination;
        }
    }
}
