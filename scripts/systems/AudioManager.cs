using Godot;

public partial class AudioManager : Node
{
    private AudioStreamPlayer _music;
    private AudioStreamPlayer _effects;
    private string _currentTrack = "";
    private ulong _suppressedSceneId;

    public override void _Ready()
    {
        AddToGroup("audio_manager");
        ProcessMode = ProcessModeEnum.Always;
        _music = new AudioStreamPlayer { Name = "Music", VolumeDb = -10 };
        _effects = new AudioStreamPlayer { Name = "Effects", VolumeDb = -3, MaxPolyphony = 8 };
        AddChild(_music); AddChild(_effects);
        GlobalSettings.Instance?.ApplyAudio();
    }

    public override void _Process(double delta)
    {
        var sceneName = GetTree().CurrentScene?.Name.ToString() ?? "";
        var sceneId = GetTree().CurrentScene?.GetInstanceId() ?? 0;
        if (_suppressedSceneId != 0 && _suppressedSceneId == sceneId) return;
        if (_suppressedSceneId != sceneId) _suppressedSceneId = 0;
        var desired = sceneName == "MainMenu"
            ? "res://audio/ambience/Main Menu Theme.mp3"
            : sceneName == "CrowsNest"
                ? "res://audio/ambience/Hub.mp3"
            : RunState.IsBossRoom
                ? RunState.CurrentMapIndex == 1 ? "res://audio/boss/Floor 2 - Boss Theme.mp3" : "res://audio/boss/Floor 1 - Boss Theme.mp3"
                : RunState.CurrentMapIndex == 1 ? "res://audio/ambience/Floor 2.mp3" : "res://audio/ambience/Floor 1.mp3";
        PlayMusic(desired);
    }

    public void PlayEffect(string effectName)
    {
        var path = effectName switch
        {
            "treasure" => "res://audio/effects/Treasure.mp3",
            "death" => "res://audio/effects/Death.mp3",
            "boss_defeated" => "res://audio/effects/Boss Defeated.mp3",
            _ => ""
        };
        if (path == "") return;
        _effects.Stream = ResourceLoader.Load<AudioStream>(path);
        _effects.Play();
    }

    public void SetMusicVolume(float percent) => _music.VolumeDb = PercentToDb(percent);
    public void SetEffectsVolume(float percent) => _effects.VolumeDb = PercentToDb(percent);
    public void SetPaused(bool paused) => _music.StreamPaused = paused;
    public void StopBossMusic()
    {
        _suppressedSceneId = GetTree().CurrentScene?.GetInstanceId() ?? 0;
        _music.Stop();
    }
    public void PlayGameOverMusic()
    {
        _suppressedSceneId = GetTree().CurrentScene?.GetInstanceId() ?? 0;
        _currentTrack = "";
        PlayMusic("res://audio/ambience/Game Over.mp3");
    }

    private static float PercentToDb(float percent)
    {
        if (percent <= 0.001f) return -80.0f;
        return Mathf.LinearToDb(Mathf.Clamp(percent / 100.0f, 0.001f, 1.0f));
    }

    private void PlayMusic(string path)
    {
        if (_currentTrack == path && _music.Stream != null) return;
        var stream = ResourceLoader.Load<AudioStreamMP3>(path);
        if (stream == null) return;
        stream.Loop = true;
        _currentTrack = path;
        _music.Stream = stream;
        _music.Play();
    }
}
