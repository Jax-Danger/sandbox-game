using Prowl.PaperUI;

namespace Sandbox;

/// <summary>Vehicle manager (filled in by the vehicles feature).</summary>
public sealed partial class VehicleSystem
{
    public bool InVehicle => Driving is not null;
    public partial void DrawHud(Paper paper, float w, float h, float m);
}

public sealed partial class VehicleSystem(SandboxGame game)
{
    public object? Driving { get; private set; }
    public partial void DrawHud(Paper paper, float w, float h, float m) { }
    public void Tick(float dt, bool allowInput) { }
    public bool TryUse() => false;
}
