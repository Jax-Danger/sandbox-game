using Prowl.Runtime;
using Prowl.Runtime.Rendering;

namespace Sandbox;

/// <summary>Post-processing presets, following Prowl's samples (FXAA + Bloom + Tonemapper on the camera).</summary>
public static class Graphics
{
    public static void Apply(Camera camera, GraphicsQuality q)
    {
        try
        {
            camera.Effects = q switch
            {
                GraphicsQuality.Low => [],
                GraphicsQuality.Medium => [new FXAAEffect(), new TonemapperEffect()],
                _ => [new FXAAEffect(), new BloomEffect(), new TonemapperEffect()],
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"Post-processing unavailable: {e.Message}");
        }
    }
}
