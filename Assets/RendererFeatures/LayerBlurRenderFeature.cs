using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class LayerBlurRenderFeature : ScriptableRendererFeature
{
    [SerializeField] Material _material;
    [SerializeField] LayerMask _layerMask = 0;
    [SerializeField, Range(1, 4)] int _downsample = 2;
    [SerializeField, Range(1, 8)] int _blurIterations = 1;
    [SerializeField] RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

    LayerBlurPass _pass;
    Mesh _fullscreenQuad;

    public override void Create()
    {
        _fullscreenQuad = CreateFullscreenQuad();
        _pass ??= new LayerBlurPass();
        _pass.renderPassEvent = _renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _fullscreenQuad == null || _layerMask == 0)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        _pass.Setup(_layerMask, _downsample, _blurIterations, _material, _fullscreenQuad);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_fullscreenQuad);
        _fullscreenQuad = null;
    }

    static Mesh CreateFullscreenQuad()
    {
        Mesh mesh = new()
        {
            name = "LayerBlurFullscreenQuad",
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.SetVertices(new[]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3(-1f, 1f, 0f),
            new Vector3(1f, 1f, 0f),
            new Vector3(1f, -1f, 0f)
        });

        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        });

        mesh.SetIndices(new[] { 0, 1, 2, 0, 2, 3 }, MeshTopology.Triangles, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 0f));
        mesh.UploadMeshData(false);
        return mesh;
    }

    sealed class LayerBlurPass : ScriptableRenderPass
    {
        const int FinalOverlayPass = 2;

        static readonly int BlurTexId = Shader.PropertyToID("_BlurTex");

        static readonly ShaderTagId[] ForwardShaderTags =
        {
            new("UniversalForwardOnly"),
            new("UniversalForward"),
            new("SRPDefaultUnlit"),
            new("LightweightForward")
        };

        readonly List<ShaderTagId> _shaderTags = new();

        LayerMask _layerMask;
        int _downsample;
        int _blurIterations;
        Material _material;
        Mesh _fullscreenQuad;

        public LayerBlurPass()
        {
            requiresIntermediateTexture = true;
        }

        public void Setup(
            LayerMask layerMask,
            int downsample,
            int blurIterations,
            Material material,
            Mesh fullscreenQuad)
        {
            _layerMask = layerMask;
            _downsample = Mathf.Max(1, downsample);
            _blurIterations = Mathf.Max(1, blurIterations);
            _material = material;
            _fullscreenQuad = fullscreenQuad;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle layerTexture = CreateLayerTexture(renderGraph, resourceData.activeColorTexture, "Layer Blur Capture", 1, true);

            AddCapturePass(renderGraph, frameData, resourceData, layerTexture);

            TextureHandle blurA = CreateLayerTexture(renderGraph, resourceData.activeColorTexture, "Layer Blur Horizontal", _downsample, false);
            TextureHandle blurB = CreateLayerTexture(renderGraph, resourceData.activeColorTexture, "Layer Blur Vertical", _downsample, false);

            TextureHandle blurSource = layerTexture;
            for (int iteration = 0; iteration < _blurIterations; iteration++)
            {
                string iterationLabel = _blurIterations > 1 ? $" {iteration + 1}" : string.Empty;

                RenderGraphUtils.BlitMaterialParameters horizontalBlur = new(blurSource, blurA, _material, 0);
                renderGraph.AddBlitPass(horizontalBlur, $"Layer Blur Horizontal{iterationLabel}");

                RenderGraphUtils.BlitMaterialParameters verticalBlur = new(blurA, blurB, _material, 1);
                renderGraph.AddBlitPass(verticalBlur, $"Layer Blur Vertical{iterationLabel}");

                blurSource = blurB;
            }

            AddOverlayPass(renderGraph, resourceData, blurB);
        }

        TextureHandle CreateLayerTexture(RenderGraph renderGraph, TextureHandle source, string name, int downsample, bool clear)
        {
            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = name;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = clear;
            desc.clearColor = Color.clear;
            desc.filterMode = FilterMode.Bilinear;

            if (downsample > 1)
            {
                desc.sizeMode = TextureSizeMode.Explicit;
                desc.width = Mathf.Max(1, desc.width / downsample);
                desc.height = Mathf.Max(1, desc.height / downsample);
            }

            return renderGraph.CreateTexture(desc);
        }

        void AddCapturePass(RenderGraph renderGraph, ContextContainer frameData, UniversalResourceData resourceData, TextureHandle layerTexture)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<CapturePassData>("Layer Blur Capture", out CapturePassData passData);

            passData.rendererList = CreateRendererList(renderGraph, frameData);
            if (!passData.rendererList.IsValid())
                return;

            builder.UseRendererList(passData.rendererList);
            builder.SetRenderAttachment(layerTexture, 0, AccessFlags.Write);
            //builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.SetRenderFunc(static (CapturePassData data, RasterGraphContext context) =>
            {
                context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                context.cmd.DrawRendererList(data.rendererList);
            });
        }

        RendererListHandle CreateRendererList(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            CullContextData cullContextData = frameData.Get<CullContextData>();

            cameraData.camera.TryGetCullingParameters(false, out ScriptableCullingParameters cullingParameters);
            cullingParameters.cullingMask = (uint)_layerMask.value;
            CullingResults cullingResults = cullContextData.Cull(ref cullingParameters);

            _shaderTags.Clear();
            _shaderTags.AddRange(ForwardShaderTags);

            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                _shaderTags,
                renderingData,
                cameraData,
                lightData,
                SortingCriteria.CommonTransparent);

            FilteringSettings filteringSettings = new(RenderQueueRange.all, _layerMask);
            RendererListParams rendererListParams = new(cullingResults, drawingSettings, filteringSettings);
            return renderGraph.CreateRendererList(rendererListParams);
        }

        void AddOverlayPass(RenderGraph renderGraph, UniversalResourceData resourceData, TextureHandle blurredTexture)
        {
            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<OverlayPassData>("Layer Blur Overlay", out OverlayPassData passData);

            passData.blur = blurredTexture;
            passData.material = _material;
            passData.fullscreenQuad = _fullscreenQuad;

            builder.UseTexture(blurredTexture, AccessFlags.Read);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
            builder.AllowGlobalStateModification(true);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (OverlayPassData data, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalTexture(BlurTexId, data.blur);
                context.cmd.DrawMesh(data.fullscreenQuad, Matrix4x4.identity, data.material, 0, FinalOverlayPass);
            });
        }

        sealed class CapturePassData
        {
            public RendererListHandle rendererList;
        }

        sealed class OverlayPassData
        {
            public TextureHandle blur;
            public Material material;
            public Mesh fullscreenQuad;
        }
    }
}
