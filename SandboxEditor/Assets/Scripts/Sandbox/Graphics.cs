using Prowl.Runtime;
using Prowl.Runtime.Rendering;

namespace Sandbox;

/// <summary>Post-processing presets, following Prowl's samples (FXAA + Bloom + Tonemapper on the camera).</summary>
public static class Graphics
{
    /// <summary>Only genuinely hot pixels (lamps, muzzle flashes, emissives) should glow; sunlit skin and paint stay clean.</summary>
    public static float BloomThreshold = 1.8f, BloomIntensity = 0.45f;

    /// <summary>ACES over the engine's AgX default: AgX rolled sunlit paints and skin toward desaturated white.</summary>
    public static TonemappingMode Tonemapper()
        => Enum.TryParse(Environment.GetEnvironmentVariable("SANDBOX_TONEMAP"), out TonemappingMode ty) ? ty : TonemappingMode.ACES;

    public static void Apply(Camera camera, GraphicsQuality q)
    {
        try
        {
            camera.HDR = q != GraphicsQuality.Low && Environment.GetEnvironmentVariable("SANDBOX_HDR") != "0";
            camera.Tonemapping = Tonemapper();
            camera.PostProcessing = q != GraphicsQuality.Low;
            camera.AntiAliasing = q == GraphicsQuality.Low ? AntiAliasingMode.None : AntiAliasingMode.FXAA;
            var p = new VolumeProfile();
            Bloom b = p.Add<Bloom>(overrideAll: true); b.Threshold.Value = BloomThreshold; b.Intensity.Value = BloomIntensity;
            ColorGrading cg = p.Add<ColorGrading>(overrideAll: true); cg.Saturation.Value = 0.05f;
            if (q == GraphicsQuality.High && Environment.GetEnvironmentVariable("SANDBOX_NOAO") != "1")
            { AmbientOcclusion ao = p.Add<AmbientOcclusion>(overrideAll: true); ao.Intensity.Value = 1f; }
            camera.Profile = p;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Post-processing unavailable: {e.Message}");
        }
    }
}
