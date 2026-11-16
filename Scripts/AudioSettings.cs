using Godot;

namespace Mech;

/// <summary>Per-machine volume levels (0–100) and mute, kept outside saves and applied to the audio buses.</summary>
public sealed class AudioSettings
{
    public const int Default = 80;
    private const string Section = "audio";
    public int Master { get; set; } = Default;
    public int Music { get; set; } = 60;
    public int Effects { get; set; } = Default;
    public int Interface { get; set; } = Default;
    public bool Muted { get; set; }

    public static AudioSettings Load(string path)
    {
        var settings = new AudioSettings();
        var file = new ConfigFile();
        if (file.Load(path) != Error.Ok) return settings;
        int Level(string key, int fallback) => Math.Clamp(file.GetValue(Section, key, fallback).AsInt32(), 0, 100);
        settings.Master = Level("master", settings.Master); settings.Music = Level("music", settings.Music);
        settings.Effects = Level("effects", settings.Effects); settings.Interface = Level("interface", settings.Interface);
        settings.Muted = file.GetValue(Section, "muted", false).AsBool();
        return settings;
    }

    public Error Save(string path)
    {
        var file = new ConfigFile();
        file.SetValue(Section, "master", Master); file.SetValue(Section, "music", Music);
        file.SetValue(Section, "effects", Effects); file.SetValue(Section, "interface", Interface);
        file.SetValue(Section, "muted", Muted);
        return file.Save(path);
    }

    public static float Decibels(int level) => level <= 0 ? -80 : Mathf.LinearToDb(level / 100f);

    public void Apply()
    {
        SoundBank.EnsureBuses();
        void Set(string bus, int level)
        {
            var index = AudioServer.GetBusIndex(bus);
            AudioServer.SetBusVolumeDb(index, Decibels(level));
            AudioServer.SetBusMute(index, level <= 0);
        }
        Set("Master", Master);
        Set(SoundBank.BusName(SoundBus.Music), Music);
        Set(SoundBank.BusName(SoundBus.Sfx), Effects);
        Set(SoundBank.BusName(SoundBus.Ui), Interface);
        AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), Muted || Master <= 0);
    }
}
