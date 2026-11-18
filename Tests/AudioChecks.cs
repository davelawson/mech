using Godot;

namespace Mech.Tests;

public partial class SmokeTests
{
    private async Task TestAudioUi()
    {
        var sound = _app.Sound;
        Check(SoundBank.Cues.Values.SelectMany(spec => spec.Files).Concat(SoundBank.Music.Values).All(path => ResourceLoader.Exists(path)),
            "every cue and music file is packaged");
        Check(Enum.GetValues<Cue>().All(SoundBank.Cues.ContainsKey), "every cue has files");
        Check(sound.Load("res://Assets/Audio/missing.ogg") is null, "a missing sound file loads as silence");
        await Frames();
        Check(sound.Music == MusicContext.Menu && sound.MusicTrack == SoundBank.Music[MusicContext.Menu], "the main menu plays the menu track");

        sound.ClearRecent();
        await Click(_app.MainMenu.AudioButton);
        Check(sound.Count(Cue.UiHover) == 1 && sound.Count(Cue.UiClick) == 1, "hovering and pressing a menu button tick and click");
        Check(_app.AudioOptions.Visible && _app.Navigation.CurrentScreen == CommandScreen.Audio && _app.MainMenu.Visible,
            "Audio opens over the main menu");
        Check(_app.AudioOptions.MasterSlider.Value == _app.AudioLevels.Master && _app.AudioOptions.MusicSlider.Value == _app.AudioLevels.Music,
            "the sliders show the current levels");

        _app.AudioOptions.MusicSlider.Value = 30; _app.AudioOptions.EffectsSlider.Value = 0;
        var music = AudioServer.GetBusIndex("Music"); var effects = AudioServer.GetBusIndex("Sfx");
        Check(Math.Abs(AudioServer.GetBusVolumeDb(music) - AudioSettings.Decibels(30)) < .01f && AudioServer.IsBusMute(effects),
            "moving a slider sets its bus volume, and zero silences the bus");
        _app.AudioOptions.MuteToggle.ButtonPressed = true;
        Check(AudioServer.IsBusMute(AudioServer.GetBusIndex("Master")), "Mute silences the master bus");
        var saved = AudioSettings.Load(_settingsFile);
        Check(saved.Music == 30 && saved.Effects == 0 && saved.Muted, "levels save as they change");
        _app.AudioOptions.MuteToggle.ButtonPressed = false; _app.AudioOptions.EffectsSlider.Value = AudioSettings.Default;
        Check(!AudioServer.IsBusMute(AudioServer.GetBusIndex("Master")) && !AudioServer.IsBusMute(effects), "unmuting restores sound");

        sound.ClearRecent();
        await RightClick(new Vector2(20, 20));
        Check(!_app.AudioOptions.Visible && _app.Navigation.CurrentScreen == CommandScreen.MainMenu && sound.Count(Cue.UiCancel) == 1,
            "right-click closes Audio with a back sound");
        await Click(_app.MainMenu.AudioButton); await PressKey(Key.Escape);
        Check(!_app.AudioOptions.Visible && _app.Navigation.CurrentScreen == CommandScreen.MainMenu, "Escape closes Audio");
        await Click(_app.MainMenu.AudioButton);
        sound.ClearRecent();
        await Click(_app.AudioOptions.BackButton);
        Check(!_app.AudioOptions.Visible && sound.Count(Cue.UiCancel) == 1 && sound.Count(Cue.UiClick) == 0,
            "Back closes Audio with its own back sound instead of a click");

        // A burst of one cue keeps to its voice cap.
        for (var i = 0; i < 50; i++) sound.Play(Cue.FireAutocannon);
        Check(sound.Active(Cue.FireAutocannon) is >= 1 and <= 6, $"a burst keeps to the voice cap ({sound.Active(Cue.FireAutocannon)})");

        await Click(_app.MainMenu.NewButton); await FinishDraft(); await Frames();
        Check(sound.Music == MusicContext.Strategy && sound.MusicTrack == SoundBank.Music[MusicContext.Strategy],
            "the office plays the strategy track");
        var settled = AudioSettings.Load(_settingsFile);
        Check(settled.Music == 30 && !settled.Muted, "levels persist outside the campaign");
    }

    private async Task TestCombatAudio()
    {
        // Footfalls: one per planted foot while walking, none standing still.
        var model = MechModel.Create(DraftCatalog.Mech("atlas"), CombatSide.Friendly); AddChild(model);
        for (var i = 0; i < 30; i++) model.AnimateLocomotion(1 / 60f, 0, 15, 25);
        var still = model.Footfalls;
        for (var i = 0; i < 600; i++) model.AnimateLocomotion(1 / 60f, 15, 15, 25);
        var walked = model.Footfalls - still;
        Check(still == 0 && walked >= 4, $"a walking mech plants its feet in turn ({walked} footfalls in 10 s)");
        model.QueueFree();

        var sound = new SoundPlayer { Name = "CombatSound" }; AddChild(sound); await Frames();
        sound.SetMusic(MusicContext.Combat);
        Check(sound.MusicTrack == SoundBank.Music[MusicContext.Combat] && sound.MusicFade == 0, "a new music context starts a crossfade");
        await Delay(.3);
        Check(sound.MusicFade is > 0 and < 1, $"the crossfade advances over time ({sound.MusicFade:0.00})");
        sound.SetMusic(MusicContext.Combat);
        Check(sound.MusicFade > 0, "the playing context does not restart");
        sound.SetMusic(MusicContext.None);
        var roster = ForceRoster.Create("Audio checks", [DraftCatalog.Mech("awesome"), DraftCatalog.Mech("atlas")], DraftCatalog.Warriors.Take(2));
        for (var i = 0; i < 2; i++) roster.Assign(roster.Mechs[i].Id, roster.Warriors[i].Id);
        var field = new CombatViewport { Position = new Vector2(40, 40), Size = new Vector2(900, 650), Sound = sound };
        AddChild(field); await Frames();
        try
        {
            var sim = CombatSimulation.DeployOnTerrain(roster, roster.Mechs.Select(unit => unit.Id), BattlefieldTerrain.Flat);
            var friendly = sim.Units.Where(unit => unit.Side == CombatSide.Friendly).ToArray();
            for (var i = 0; i < friendly.Length; i++) { friendly[i].Position = new Vector2(240 * i, 0); friendly[i].Facing = Vector2.Up; }
            // Sound checks need every weapon type to fire, not just the best-first ones a lower heat limit allows.
            foreach (var unit in friendly) unit.HeatLimit = CombatRules.MaxHeatLimit;
            var spread = 0;
            foreach (var other in sim.Units.Where(unit => unit.Side == CombatSide.Enemy)) { other.Position = new Vector2(200 * spread++, -360); other.Facing = Vector2.Down; }
            field.Display(sim); sim.RefreshDetection(); sim.Paused = false;
            Cue[] fire = [Cue.FirePpc, Cue.FireAutocannon, Cue.FireMissiles];
            Cue[] land = [Cue.ShellExplosion, Cue.MissileExplosion, Cue.PpcBurst];
            for (var step = 0; step < 1200 && !(fire.All(cue => sound.Count(cue) > 0) && land.Any(cue => sound.Count(cue) > 0)); step++)
            {
                sim.Advance(CombatRules.StepSeconds); field._Process(0);
                if (step % 20 == 0) await Frames();
            }
            Check(fire.All(cue => sound.Count(cue) > 0), $"PPCs, autocannon and missiles each sound as they fire ({string.Join(", ", fire.Select(cue => $"{cue} {sound.Count(cue)}"))})");
            Check(sound.Count(Cue.FireSmallLaser) + sound.Count(Cue.FireLargeLaser) > 0, "lasers sound as they fire");
            Check(land.Any(cue => sound.Count(cue) > 0), "shells, missiles or PPC bolts sound where they land");
            Check(field.FindChildren("*", "AudioListener3D", true, false).OfType<AudioListener3D>().Any(listener => listener.IsCurrent()),
                "the battlefield hears through its own listener");
        }
        finally { field.QueueFree(); sound.QueueFree(); await Frames(); }
    }
}
