using Godot;

namespace Mech;

/// <summary>
/// The battlefield's sounds, played where the effects are drawn and when they land. The viewport reports each moment
/// every frame it holds; each impact sounds once.
/// </summary>
internal sealed class CombatAudio(Func<SoundPlayer?> sound, Node3D world)
{
    private readonly HashSet<long> _sounded = [];
    private readonly Dictionary<string, AudioStreamPlayer3D> _jets = [];
    private readonly Dictionary<string, int> _footfalls = [];
    private readonly HashSet<string> _rising = [];
    private AudioListener3D? _listener;

    public void Clear()
    {
        _sounded.Clear(); _footfalls.Clear(); _rising.Clear();
        foreach (var jet in _jets.Values) if (GodotObject.IsInstanceValid(jet)) jet.QueueFree();
        _jets.Clear();
    }

    /// <summary>The ear hangs above the camera's ground focus, higher as the view zooms out, facing as the camera does.</summary>
    public void Listen(Camera3D camera, Vector3 focus)
    {
        if (_listener is null)
        {
            _listener = new AudioListener3D(); world.AddChild(_listener);
            _listener.MakeCurrent();
        }
        _listener.GlobalTransform = new Transform3D(camera.GlobalBasis, focus + Vector3.Up * camera.Size * .4f);
    }

    public static Cue FireCue(WeaponKind kind) => kind switch
    {
        WeaponKind.SmallLaser or WeaponKind.MediumLaser => Cue.FireSmallLaser,
        WeaponKind.LargeLaser or WeaponKind.ERLargeLaser => Cue.FireLargeLaser,
        WeaponKind.PPC => Cue.FirePpc,
        WeaponKind.Flamer => Cue.FireFlamer,
        WeaponKind.MachineGun => Cue.FireMachineGun,
        WeaponKind.AC2 or WeaponKind.AC5 or WeaponKind.AC10 or WeaponKind.AC20 => Cue.FireAutocannon,
        _ => Cue.FireMissiles
    };

    /// <summary>Heavier guns and racks sound lower.</summary>
    public static float Caliber(WeaponKind kind) => kind switch
    {
        WeaponKind.MediumLaser or WeaponKind.ERLargeLaser => .85f,
        WeaponKind.AC2 => 1.3f, WeaponKind.AC5 => 1.1f, WeaponKind.AC10 => .9f, WeaponKind.AC20 => .72f,
        WeaponKind.SRM2 => 1.2f, WeaponKind.SRM4 => 1.1f, WeaponKind.SRM6 => 1f,
        WeaponKind.LRM5 => 1.05f, WeaponKind.LRM10 => .95f, WeaponKind.LRM15 => .88f, WeaponKind.LRM20 => .8f,
        _ => 1
    };

    public void Play(Cue cue, Vector3 at, float pitch = 1, float volumeDb = 0) => sound()?.PlayAt(cue, world, at, pitch, volumeDb);

    /// <summary>A discharge leaving its muzzle. A machine gun sounds per round instead.</summary>
    public void Fired(WeaponKind kind, Vector3 at)
    {
        if (kind != WeaponKind.MachineGun) Play(FireCue(kind), at, Caliber(kind));
    }

    /// <summary>Plays an impact the first time its key is reported.</summary>
    public void Landed(long key, Cue cue, Vector3 at, float pitch = 1, float volumeDb = 0)
    {
        if (_sounded.Add(key)) Play(cue, at, pitch, volumeDb);
    }

    public void Forget(long key) => _sounded.Remove(key);

    /// <summary>A foot planted since the last frame thuds deeper and louder on a heavier mech.</summary>
    public void Steps(string unitId, MechModel model, int tonnage)
    {
        var previous = _footfalls.GetValueOrDefault(unitId, model.Footfalls);
        _footfalls[unitId] = model.Footfalls;
        if (model.Footfalls == previous || !model.Visible) return;
        var weight = Math.Clamp((tonnage - 20) / 80f, 0, 1);
        Play(Cue.Footstep, model.GlobalPosition, 1.25f - .5f * weight, -4 + 6 * weight);
    }

    /// <summary>Jets roar from the mech for as long as it is airborne, then it lands with a thud.</summary>
    public void Jets(string unitId, MechModel model, bool jetting)
    {
        var running = _jets.TryGetValue(unitId, out var jet) && GodotObject.IsInstanceValid(jet);
        if (jetting && !running && model.Visible && sound()?.Loop(Cue.JumpJets) is { } created)
        {
            model.AddChild(created); created.Play(); _jets[unitId] = created;
        }
        else if (!jetting && running)
        {
            jet!.QueueFree(); _jets.Remove(unitId);
            if (model.Visible) Play(Cue.Landing, model.GlobalPosition, .8f);
        }
    }

    /// <summary>A knocked-down mech crashes to the ground, and its servos whine as it starts to rise.</summary>
    public void Prone(string unitId, MechModel model, bool landed, bool rising)
    {
        if (landed && model.Visible) Play(Cue.Knockdown, model.GlobalPosition, .7f);
        if (rising == _rising.Contains(unitId)) return;
        if (!rising) { _rising.Remove(unitId); return; }
        _rising.Add(unitId);
        if (model.Visible) Play(Cue.StandUp, model.GlobalPosition, .8f);
    }
}
