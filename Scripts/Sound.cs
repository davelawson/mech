using Godot;

namespace Mech;

/// <summary>Every sound the game makes. Call sites name a cue; <see cref="SoundBank"/> maps it to files. See docs/audio.md.</summary>
public enum Cue
{
    UiHover, UiClick, UiConfirm, UiCancel, UiError, HeatWarning,
    FireSmallLaser, FireLargeLaser, FirePpc, FireFlamer, FireMachineGun, FireAutocannon, FireMissiles,
    LaserHit, BulletHit, ImpactGround, ShellExplosion, MissileExplosion, PpcBurst,
    LocationDestroyed, MechDestroyed, AmmoExplosion, CriticalHit,
    Footstep, JumpJets, Landing, Knockdown, StandUp, Ejection, Surrender
}

public enum SoundBus { Ui, Sfx, Music }
public enum MusicContext { None, Menu, Strategy, Combat }

/// <summary>A cue's variant files, its bus and level, random pitch spread, and how many may sound at once.</summary>
internal sealed record CueSpec(string[] Files, SoundBus Bus, float VolumeDb = 0, float Jitter = .06f, int Voices = 4, float Cooldown = .03f);

internal static class SoundBank
{
    public const string Root = "res://Assets/Audio/";
    private static string[] Files(string folder, params string[] names) => names.Select(name => $"{Root}{folder}/{name}").ToArray();
    private static string[] Numbered(string folder, string stem, string format, params int[] numbers) =>
        numbers.Select(number => $"{Root}{folder}/{stem}{number.ToString(format)}.ogg").ToArray();
    private const string Interface = "kenney-interface", SciFi = "kenney-scifi", Sfx = "cc0-sfx";

    public static readonly IReadOnlyDictionary<Cue, CueSpec> Cues = new Dictionary<Cue, CueSpec>
    {
        [Cue.UiHover] = new(Files(Interface, "tick_002.ogg"), SoundBus.Ui, -10, .04f, 2, .04f),
        [Cue.UiClick] = new(Files(Interface, "click_001.ogg"), SoundBus.Ui, -2, .04f, 3),
        [Cue.UiConfirm] = new(Files(Interface, "confirmation_001.ogg"), SoundBus.Ui, -4, 0, 2),
        [Cue.UiCancel] = new(Files(Interface, "back_002.ogg"), SoundBus.Ui, -2, .03f, 2),
        [Cue.UiError] = new(Files(Interface, "error_004.ogg"), SoundBus.Ui, -4, 0, 2, .2f),
        [Cue.HeatWarning] = new(Files(Interface, "error_007.ogg"), SoundBus.Ui, -8, 0, 1, 1),
        [Cue.FireSmallLaser] = new(Numbered(SciFi, "laserSmall_", "000", 0, 1, 2, 3, 4), SoundBus.Sfx, -6, .08f, 8),
        [Cue.FireLargeLaser] = new(Numbered(SciFi, "laserLarge_", "000", 0, 1, 2), SoundBus.Sfx, -5, .06f, 6),
        [Cue.FirePpc] = new(Numbered(SciFi, "forceField_", "000", 0, 1, 2), SoundBus.Sfx, -3, .05f, 4),
        [Cue.FireFlamer] = new(Files(Sfx, "noise_01.ogg"), SoundBus.Sfx, -6, .08f, 4),
        [Cue.FireMachineGun] = new(Files(Sfx, "shot_02.ogg"), SoundBus.Sfx, -10, .1f, 6, .05f),
        [Cue.FireAutocannon] = new(Files(Sfx, "shot_01.ogg", "shot_02.ogg"), SoundBus.Sfx, -3, .05f, 6),
        [Cue.FireMissiles] = new(Numbered(SciFi, "doorOpen_", "000", 0, 1, 2), SoundBus.Sfx, -5, .08f, 6),
        [Cue.LaserHit] = new(Files(Sfx, "noise_02.ogg"), SoundBus.Sfx, -10, .1f, 6),
        [Cue.BulletHit] = new(Numbered(SciFi, "impactMetal_", "000", 0, 1, 2, 3, 4), SoundBus.Sfx, -12, .1f, 6, .04f),
        [Cue.ImpactGround] = new(Files(Sfx, "hit_01.ogg", "hit_02.ogg", "hit_03.ogg", "hit_04.ogg"), SoundBus.Sfx, -10, .1f, 6, .04f),
        [Cue.ShellExplosion] = new(Numbered(SciFi, "explosionCrunch_", "000", 0, 1, 2), SoundBus.Sfx, -4, .08f, 6),
        [Cue.MissileExplosion] = new(Numbered(SciFi, "explosionCrunch_", "000", 0, 2), SoundBus.Sfx, -9, .12f, 6, .05f),
        [Cue.PpcBurst] = new(Numbered(SciFi, "explosionCrunch_", "000", 0, 1), SoundBus.Sfx, -4, .06f, 4),
        [Cue.LocationDestroyed] = new(Files(SciFi, "lowFrequency_explosion_001.ogg"), SoundBus.Sfx, -2, .06f, 4),
        [Cue.MechDestroyed] = new(Files(SciFi, "lowFrequency_explosion_000.ogg"), SoundBus.Sfx, 0, .04f, 3),
        [Cue.AmmoExplosion] = new(Numbered(SciFi, "explosionCrunch_", "000", 3, 4), SoundBus.Sfx, 0, .05f, 3),
        [Cue.CriticalHit] = new(Files(Sfx, "metal_02.ogg", "metal_03.ogg", "metal_11.ogg"), SoundBus.Sfx, -6, .08f, 4),
        [Cue.Footstep] = new(Files(Sfx, "slam_01.ogg", "slam_03.ogg", "slam_04.ogg", "slam_05.ogg", "slam_06.ogg"), SoundBus.Sfx, -12, .06f, 8, 0),
        [Cue.JumpJets] = new(Files(SciFi, "thrusterFire_000.ogg"), SoundBus.Sfx, -6, .05f, 8, 0),
        [Cue.Landing] = new(Files(Sfx, "slam_02.ogg", "slam_07.ogg"), SoundBus.Sfx, -4, .05f, 4),
        [Cue.Knockdown] = new(Files(Sfx, "metal_12.ogg", "metal_04.ogg"), SoundBus.Sfx, -2, .05f, 4),
        [Cue.StandUp] = new(Files(Sfx, "machine_01.ogg", "machine_02.ogg", "machine_03.ogg"), SoundBus.Sfx, -6, .05f, 4),
        [Cue.Ejection] = new(Files(SciFi, "thrusterFire_000.ogg"), SoundBus.Sfx, -1, .04f, 3),
        [Cue.Surrender] = new(Files(Sfx, "machine_03.ogg"), SoundBus.Sfx, -4, 0, 2),
    };

    public static readonly IReadOnlyDictionary<MusicContext, string> Music = new Dictionary<MusicContext, string>
    {
        [MusicContext.Menu] = $"{Root}music/menu-music.ogg",
        [MusicContext.Strategy] = $"{Root}music/observing-the-star.ogg",
        [MusicContext.Combat] = $"{Root}music/battle-theme-a.mp3",
    };

    public static string BusName(SoundBus bus) => bus switch { SoundBus.Ui => "Ui", SoundBus.Sfx => "Sfx", _ => "Music" };

    /// <summary>Creates the Music, Sfx and Ui buses under Master if the project's bus layout lacks them.</summary>
    public static void EnsureBuses()
    {
        foreach (var bus in Enum.GetValues<SoundBus>())
        {
            var name = BusName(bus);
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus(); var index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name); AudioServer.SetBusSend(index, "Master");
        }
    }
}

/// <summary>
/// Plays cues for one <see cref="CommandApp"/>: flat sounds for the interface, positional sounds in a 3D world, and music.
/// Each cue keeps to its voice cap and cooldown, so a burst of shots never stacks dozens of copies.
/// </summary>
public partial class SoundPlayer : Node
{
    private sealed class Voice(Node player)
    {
        public Node Player { get; } = player;
        public Cue Cue { get; set; }
        public double BusyUntil { get; set; }
    }
    private const int FlatVoices = 24, WorldVoices = 48, RecentLimit = 256;
    private const double MusicFadeSeconds = 1.5;
    private readonly List<Voice> _flat = [];
    private readonly Dictionary<Node3D, List<Voice>> _world = [];
    private readonly Dictionary<Cue, double> _lastPlayed = [];
    private readonly Dictionary<string, AudioStream?> _streams = [];
    private readonly Random _random = new();
    private readonly List<(Cue Cue, double At)> _recent = [];
    private AudioStreamPlayer _musicA = null!, _musicB = null!;
    private AudioStreamPlayer _musicCurrent = null!;
    private double _musicFade = 1;
    /// <summary>Cues actually played, oldest first, so tests can check routing without listening.</summary>
    public IReadOnlyList<(Cue Cue, double At)> Recent => _recent;
    public MusicContext Music { get; private set; }
    /// <summary>The playing track's path, or null for silence.</summary>
    public string? MusicTrack { get; private set; }
    /// <summary>Linear 0–1 progress of the latest music crossfade.</summary>
    public double MusicFade => _musicFade;
    private static double Now => Time.GetTicksMsec() / 1000d;

    /// <summary>The sound player of the app that holds this node, or null outside an app (as in standalone test views).</summary>
    public static SoundPlayer? For(Node node)
    {
        for (Node? current = node; current is not null; current = current.GetParent())
            if (current is CommandApp app) return app.Sound;
        return null;
    }

    public override void _Ready()
    {
        SoundBank.EnsureBuses();
        _musicA = new AudioStreamPlayer { Bus = SoundBank.BusName(SoundBus.Music), Name = "MusicA" };
        _musicB = new AudioStreamPlayer { Bus = SoundBank.BusName(SoundBus.Music), Name = "MusicB" };
        AddChild(_musicA); AddChild(_musicB); _musicCurrent = _musicA;
    }

    public int Count(Cue cue) => _recent.Count(item => item.Cue == cue);
    public int Active(Cue cue) => _flat.Concat(_world.Values.SelectMany(list => list)).Count(voice => voice.Cue == cue && voice.BusyUntil > Now);
    public void ClearRecent() => _recent.Clear();

    /// <summary>Plays a cue without position, on the interface bus or flat in the mix.</summary>
    public bool Play(Cue cue, float pitch = 1, float volumeDb = 0)
    {
        if (!Admit(cue, out var spec, out var stream)) return false;
        var voice = Claim(_flat, FlatVoices, () => new AudioStreamPlayer());
        if (voice is null) return false;
        var player = (AudioStreamPlayer)voice.Player;
        player.Bus = SoundBank.BusName(spec.Bus); player.Stream = stream;
        player.PitchScale = Pitch(spec, pitch); player.VolumeDb = spec.VolumeDb + volumeDb;
        player.Play();
        Start(voice, cue, stream, player.PitchScale);
        return true;
    }

    /// <summary>Plays a cue at a point in a 3D world; the world's current listener hears it.</summary>
    public bool PlayAt(Cue cue, Node3D world, Vector3 at, float pitch = 1, float volumeDb = 0)
    {
        if (!IsInstanceValid(world) || !world.IsInsideTree()) return false;
        if (!Admit(cue, out var spec, out var stream)) return false;
        if (!_world.TryGetValue(world, out var pool))
        {
            foreach (var stale in _world.Keys.Where(key => !IsInstanceValid(key)).ToArray()) _world.Remove(stale);
            _world[world] = pool = [];
        }
        var voice = Claim(pool, WorldVoices, () =>
        {
            var created = Positional();
            world.AddChild(created);
            return created;
        }, add: false);
        if (voice is null) return false;
        var player = (AudioStreamPlayer3D)voice.Player;
        player.Bus = SoundBank.BusName(spec.Bus); player.Stream = stream; player.GlobalPosition = at;
        player.PitchScale = Pitch(spec, pitch); player.VolumeDb = spec.VolumeDb + volumeDb;
        player.Play();
        Start(voice, cue, stream, player.PitchScale);
        return true;
    }

    /// <summary>A positional player the caller owns and loops, such as jump jets that follow a mech; null when the cue has no file.</summary>
    public AudioStreamPlayer3D? Loop(Cue cue, float pitch = 1, float volumeDb = 0)
    {
        if (!Stream(cue, out var spec, out var stream)) return null;
        var player = Positional();
        player.Bus = SoundBank.BusName(spec.Bus); player.Stream = stream;
        player.PitchScale = Pitch(spec, pitch); player.VolumeDb = spec.VolumeDb + volumeDb;
        player.Finished += () => player.Play();
        Record(cue);
        return player;
    }

    /// <summary>Positional players share one falloff: the battlefield reads at about a kilometre, softened beyond.</summary>
    private static AudioStreamPlayer3D Positional() => new()
    {
        AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, UnitSize = 450, MaxDistance = 9000,
        MaxPolyphony = 1, PanningStrength = .7f
    };

    public void SetMusic(MusicContext context)
    {
        if (context == Music || _musicA is null) return;
        Music = context;
        var path = SoundBank.Music.GetValueOrDefault(context);
        var stream = path is null ? null : Load(path);
        if (stream is AudioStreamOggVorbis ogg) ogg.Loop = true;
        if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
        MusicTrack = stream is null ? null : path;
        var next = _musicCurrent == _musicA ? _musicB : _musicA;
        next.Stop(); next.Stream = stream; next.VolumeDb = -80;
        if (stream is not null) next.Play();
        _musicCurrent = next; _musicFade = 0;
    }

    public override void _Process(double delta)
    {
        if (_musicFade >= 1) return;
        _musicFade = Math.Min(1, _musicFade + delta / MusicFadeSeconds);
        var other = _musicCurrent == _musicA ? _musicB : _musicA;
        _musicCurrent.VolumeDb = Mathf.LinearToDb((float)Math.Max(1e-4, _musicFade));
        other.VolumeDb = Mathf.LinearToDb((float)Math.Max(1e-4, 1 - _musicFade));
        if (_musicFade >= 1) other.Stop();
    }

    private bool Admit(Cue cue, out CueSpec spec, out AudioStream stream)
    {
        if (!Stream(cue, out spec, out stream)) return false;
        var now = Now;
        if (_lastPlayed.TryGetValue(cue, out var last) && now - last < spec.Cooldown) return false;
        if (Active(cue) >= spec.Voices) return false;
        _lastPlayed[cue] = now;
        return true;
    }

    private bool Stream(Cue cue, out CueSpec spec, out AudioStream stream)
    {
        spec = SoundBank.Cues[cue]; stream = null!;
        var path = spec.Files[spec.Files.Length == 1 ? 0 : _random.Next(spec.Files.Length)];
        if (Load(path) is not { } loaded) return false;
        stream = loaded;
        return true;
    }

    /// <summary>A missing file is silent: it warns once and the cue simply does not sound.</summary>
    internal AudioStream? Load(string path)
    {
        if (_streams.TryGetValue(path, out var cached)) return cached;
        var stream = ResourceLoader.Exists(path) ? ResourceLoader.Load<AudioStream>(path) : null;
        if (stream is null) GD.PushWarning($"Sound file missing: {path}");
        return _streams[path] = stream;
    }

    private float Pitch(CueSpec spec, float pitch) => Math.Max(.05f, pitch * (1 + spec.Jitter * ((float)_random.NextDouble() * 2 - 1)));

    private Voice? Claim(List<Voice> pool, int limit, Func<Node> create, bool add = true)
    {
        var now = Now;
        var free = pool.FirstOrDefault(voice => voice.BusyUntil <= now && IsInstanceValid(voice.Player));
        if (free is not null) return free;
        pool.RemoveAll(voice => !IsInstanceValid(voice.Player));
        if (pool.Count >= limit) return null;
        var player = create();
        if (add) AddChild(player);
        var voice = new Voice(player);
        pool.Add(voice);
        return voice;
    }

    private void Start(Voice voice, Cue cue, AudioStream stream, float pitch)
    {
        voice.Cue = cue;
        voice.BusyUntil = Now + Math.Max(.05, stream.GetLength() / pitch);
        Record(cue);
    }

    private void Record(Cue cue)
    {
        _recent.Add((cue, Now));
        if (_recent.Count > RecentLimit) _recent.RemoveRange(0, _recent.Count - RecentLimit);
    }
}
