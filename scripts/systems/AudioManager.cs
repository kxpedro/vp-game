using Godot;

public partial class AudioManager : Node
{
    private AudioStreamPlayer _music;
    private AudioStreamPlayer _effects;
    private float _effectsVolumeDb = -3.0f;
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
        var paths = effectName switch
        {
            "treasure" or "blessing_select" or "blessing_upgrade" => new[] { "res://audio/effects/Treasure.mp3" },
            "death" => new[] { "res://audio/effects/Death.mp3" },
            "boss_defeated" => new[] { "res://audio/effects/Boss Defeated.mp3" },
            "attack_basic_1" => new[]
            {
                "res://audio/effects/combat/Sword Swing Light 1.wav",
                "res://audio/effects/combat/Sword Swing Light 2.wav"
            },
            "attack_basic_2" => new[]
            {
                "res://audio/effects/combat/Sword Swing Light 3.wav",
                "res://audio/effects/combat/Greatsword Sweep 2.wav"
            },
            "attack_thrust" => new[] { "res://audio/effects/combat/Greatsword Thrust.wav" },
            "attack_quick" => new[] { "res://audio/effects/combat/Greatsword Spin.wav" },
            "attack_special" => new[]
            {
                "res://audio/effects/combat/Sword Swing Heavy 1.wav",
                "res://audio/effects/combat/Sword Swing Heavy 2.wav"
            },
            "special_ground" => new[] { "res://audio/effects/combat/Sword Hit Heavy.ogg" },
            "enemy_attack" => new[] { "res://audio/effects/combat/Sword Swing Light 2.wav" },
            "hit_slash" => new[]
            {
                "res://audio/effects/combat/flesh/Flesh Slash 1.wav",
                "res://audio/effects/combat/flesh/Flesh Slash 2.wav"
            },
            "hit_pierce" or "hit_backstab" => new[]
            {
                "res://audio/effects/combat/flesh/Flesh Pierce 1.wav",
                "res://audio/effects/combat/flesh/Flesh Pierce 2.wav"
            },
            "hit_spin" => new[]
            {
                "res://audio/effects/combat/flesh/Flesh Slash 1.wav",
                "res://audio/effects/combat/flesh/Flesh Slam 1.wav"
            },
            "hit_slam" => new[]
            {
                "res://audio/effects/combat/flesh/Flesh Slam 1.wav",
                "res://audio/effects/combat/flesh/Flesh Slam 2.wav"
            },
            "armor_block" or "wall_impact" => new[]
            {
                "res://audio/effects/combat/flesh/Armor Impact 1.wav",
                "res://audio/effects/combat/flesh/Armor Impact 2.wav"
            },
            "dodge" => new[] { "res://audio/effects/combat/Dodge Air.wav" },
            _ => System.Array.Empty<string>()
        };
        if (paths.Length == 0) return;
        PlayOneShot(paths[(int)GD.RandRange(0, paths.Length - 1)], effectName);
    }

    public void SetMusicVolume(float percent) => _music.VolumeDb = PercentToDb(percent);
    public void SetEffectsVolume(float percent)
    {
        _effectsVolumeDb = PercentToDb(percent);
        _effects.VolumeDb = _effectsVolumeDb;
    }
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

    private void PlayOneShot(string path, string effectName)
    {
        var stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null) return;
        var player = new AudioStreamPlayer
        {
            Name = $"Sfx_{effectName}",
            Stream = stream,
            VolumeDb = _effectsVolumeDb,
            PitchScale = (float)GD.RandRange(0.96, 1.04),
            ProcessMode = ProcessModeEnum.Always
        };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
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
