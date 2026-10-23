namespace Sandbox;

/// <summary>
/// Prop protection: every prop has an owner ("you" for the local player, "world" for map/town entities, a player
/// name in future multiplayer). While enforced (Roleplay, or always when turned on), you can only physgun, tool or
/// remove props you own, or props whose owner has you on their buddy list. Buddy lists are per owner, so the same
/// rules work unchanged once other players exist; the console edits yours (buddy add/remove/list).
/// </summary>
public sealed class PropProtection
{
    public const string LocalPlayer = "you", World = "world";
    /// <summary>off = never, rp = only in Roleplay (default), always = every mode.</summary>
    public string Mode { get; set; } = "rp";
    public Func<bool> RoleplayActive { get; set; } = () => false;
    private readonly Dictionary<string, HashSet<string>> _buddies = new(StringComparer.OrdinalIgnoreCase);
    public int Denied { get; private set; }

    public bool Enforced => Mode == "always" || (Mode == "rp" && RoleplayActive());

    public IReadOnlyCollection<string> BuddiesOf(string owner) => _buddies.TryGetValue(owner, out var b) ? b : [];

    public bool AddBuddy(string owner, string buddy)
    {
        buddy = buddy.Trim();
        if (buddy.Length == 0 || buddy.Length > 32 || string.Equals(owner, buddy, StringComparison.OrdinalIgnoreCase)) return false;
        if (!_buddies.TryGetValue(owner, out var set)) _buddies[owner] = set = new(StringComparer.OrdinalIgnoreCase);
        return set.Count < 64 && set.Add(buddy);
    }

    public bool RemoveBuddy(string owner, string buddy) => _buddies.TryGetValue(owner, out var set) && set.Remove(buddy.Trim());

    /// <summary>Can <paramref name="who"/> manipulate <paramref name="p"/>? <paramref name="why"/> explains a refusal.</summary>
    public bool CanTouch(Prop p, string who, out string? why)
    {
        why = null;
        if (!Enforced || p.Owner is null || string.Equals(p.Owner, who, StringComparison.OrdinalIgnoreCase)) return true;
        if (_buddies.TryGetValue(p.Owner, out var set) && set.Contains(who)) return true;
        Denied++;
        why = p.Owner == World ? "That belongs to the map" : $"That belongs to {p.Owner}";
        return false;
    }

    public bool CanTouch(Prop p, out string? why) => CanTouch(p, LocalPlayer, out why);

    public void Load(IEnumerable<string>? mine)
    {
        _buddies.Clear();
        foreach (string b in mine ?? []) AddBuddy(LocalPlayer, b);
    }
}
