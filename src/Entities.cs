using Prowl.Runtime;

namespace Sandbox;

/// <summary>Explosive barrel: blows up when shot enough, hit by an explosion, or slammed hard.</summary>
public sealed class ExplosiveBarrel : MonoBehaviour
{
    public SandboxGame? Game;
    public float Health = 20f;
    private float _fuse = -1f;
    public bool Exploded { get; private set; }

    public void Trigger(float delay) { if (_fuse < 0f && !Exploded) _fuse = delay; }
    public void Damage(float amount) { Health -= amount; if (Health <= 0f) Trigger(0.05f); }

    public override void Update()
    {
        if (_fuse < 0f || Exploded || Game is null) return;
        _fuse -= Time.DeltaTime;
        if (_fuse > 0f) return;
        Exploded = true;
        Prop? p = Game.Props.FromBody(GameObject.GetComponent<Rigidbody3D>());
        var at = GameObject.Transform.Position;
        if (p is not null) Game.Props.Remove(p);
        Game.Fx.Explode(at, 7f, 70f);
    }
}
