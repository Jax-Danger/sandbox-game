using Prowl.Runtime;
using Prowl.Runtime.Rendering;

namespace Sandbox;

/// <summary>Post-processing presets, following Prowl's samples (FXAA + Bloom + Tonemapper on the camera).</summary>
public static class Graphics
{
    /// <summary>The engine default (threshold 0.6) bloomed plain sunlit skin and paint into a white haze, worst on the
    /// close-up arms in a car; only genuinely hot pixels (lamps, muzzle flashes, emissives) should glow.</summary>
    private static BloomEffect Bloom() => new() { Threshold = 1.15f, Intensity = 0.7f };

    public static void Apply(Camera camera, GraphicsQuality q)
    {
        try
        {
            camera.Effects = q switch
            {
                GraphicsQuality.Low => [],
                GraphicsQuality.Medium => [new FXAAEffect(), Bloom(), new TonemapperEffect()],
                _ => [new FXAAEffect(), new GTAOEffect(), Bloom(), new TonemapperEffect()],
            };
        }
        catch (Exception e)
        {
            Console.WriteLine($"Post-processing unavailable: {e.Message}");
        }
    }
}
