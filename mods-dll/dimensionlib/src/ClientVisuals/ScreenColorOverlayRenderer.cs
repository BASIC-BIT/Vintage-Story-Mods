using System;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace DimensionLib.ClientVisuals;

internal sealed class ScreenColorOverlayRenderer : IDisposable
{
    private const string VertexShaderCode = @"
#version 330 core
#extension GL_ARB_explicit_attrib_location: enable

layout(location = 0) in vec3 vertex;

void main(void)
{
    gl_Position = vec4(vertex.xy, 0.0, 1.0);
}";

    private const string FragmentShaderCode = @"
#version 330 core

uniform vec4 color;
out vec4 outColor;

void main(void)
{
    outColor = color;
}";

    private readonly ICoreClientAPI _api;
    private MeshRef _quad;
    private IShaderProgram _shader;
    private bool _skyRenderFailed;
    private bool _lightLiftRenderFailed;

    public ScreenColorOverlayRenderer(ICoreClientAPI api)
    {
        _api = api;
    }

    public void Start()
    {
        _quad = _api.Render.UploadMesh(QuadMeshUtil.GetCustomQuadModelData(-1f, -1f, 0f, 2f, 2f));
        _api.Event.ReloadShader += LoadShader;
        LoadShader();
    }

    public void Dispose()
    {
        _api.Event.ReloadShader -= LoadShader;
        if (_quad != null)
        {
            _api.Render.DeleteMesh(_quad);
            _quad = null;
        }

        (_shader as IDisposable)?.Dispose();
        _shader = null;
    }

    public void ResetFailures()
    {
        _skyRenderFailed = false;
        _lightLiftRenderFailed = false;
    }

    public void RenderSkyCover(Vec4f color)
    {
        Render(color, ref _skyRenderFailed, "sky cover");
    }

    public void RenderMinimumLightLift(Vec4f color)
    {
        Render(color, ref _lightLiftRenderFailed, "minimum scene light overlay");
    }

    private bool LoadShader()
    {
        (_shader as IDisposable)?.Dispose();
        _shader = _api.Shader.NewShaderProgram();
        _shader.VertexShader = _api.Shader.NewShader(EnumShaderType.VertexShader);
        _shader.FragmentShader = _api.Shader.NewShader(EnumShaderType.FragmentShader);
        _shader.VertexShader.Code = VertexShaderCode;
        _shader.FragmentShader.Code = FragmentShaderCode;
        _api.Shader.RegisterMemoryShaderProgram("dimensionlib-skycover", _shader);
        ResetFailures();
        return _shader.Compile();
    }

    private void Render(Vec4f color, ref bool failed, string failureSubject)
    {
        if (failed || _shader?.LoadError != false || _quad == null)
        {
            return;
        }

        var currentShader = _api.Render.CurrentActiveShader;
        try
        {
            currentShader?.Stop();
            _shader.Use();
            _shader.Uniform("color", color.X, color.Y, color.Z, color.W);
            _api.Render.GLDisableDepthTest();
            _api.Render.GLDepthMask(false);
            _api.Render.GlToggleBlend(true, EnumBlendMode.Standard);
            _api.Render.RenderMesh(_quad);
        }
        catch (Exception ex)
        {
            failed = true;
            _api.Logger.Warning("[DimensionLib] Disabling {0} after render failure: {1}", failureSubject, ex.Message);
        }
        finally
        {
            _api.Render.GLDepthMask(true);
            _api.Render.GLEnableDepthTest();
            _api.Render.GlToggleBlend(false);
            _shader?.Stop();
            currentShader?.Use();
        }
    }
}
