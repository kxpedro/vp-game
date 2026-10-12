using Godot;
using System;
using System.Text.Json;

public sealed class GlobalSettingsData
{
    public int Version { get; set; } = 3;
    public string Language { get; set; } = "pt-BR";
    // Campos legados (versão 1/2): ResolutionIndex 0..2 = 1280x720/1600x900/1920x1080. Lidos só para migrar e mantidos coerentes ao gravar.
    public int ResolutionIndex { get; set; } = 1;
    public bool Fullscreen { get; set; }
    public float MusicVolume { get; set; } = 70.0f;
    public float EffectsVolume { get; set; } = 85.0f;

    // Vídeo. Todo padrão = aparência de antes das opções gráficas (arquivos antigos caem aqui).
    public int DisplayMode { get; set; } = -1;     // -1 = migrar do legado; 0 janela, 1 sem bordas, 2 tela cheia exclusiva
    public int ResolutionWidth { get; set; }       // 0 = migrar do legado
    public int ResolutionHeight { get; set; }
    public int Monitor { get; set; } = -1;         // -1 = o monitor atual da janela
    public int VsyncMode { get; set; } = 1;        // 0 desligado, 1 ligado, 2 adaptativo
    public int MaxFps { get; set; }                // 0 = ilimitado
    public int RenderScalePercent { get; set; } = 100;
    public int Upscaler { get; set; }              // 0 bilinear, 1 FSR 1.0
    public int AntiAliasing { get; set; }          // 0 desligado, 1 FXAA, 2 MSAA 2x, 3 MSAA 4x
    public int ShadowQuality { get; set; } = 3;    // 0 desligadas, 1 baixa, 2 média, 3 alta
    public int SsaoQuality { get; set; } = 2;      // 0 desligado, 1 baixa, 2 média, 3 alta
    public bool Glow { get; set; } = true;
    public int BrightnessPercent { get; set; } = 100;

    // Acessibilidade / jogo.
    public int CameraShakeReduction { get; set; }  // 0, 50 ou 100 (%)
    public int DamageFlashPercent { get; set; } = 100;
}

public partial class GlobalSettings : Node
{
    private const string DefaultSettingsPath = "user://global_settings.json";
    private const float SaveDelaySeconds = 1.0f;
    private const float ApplyDelaySeconds = 0.12f;

    private static string _settingsPath = DefaultSettingsPath;
    public static GlobalSettings Instance { get; private set; }
    public GlobalSettingsData Data { get; private set; } = new();

    private Timer _saveTimer;
    private Timer _applyTimer;
    private bool _savePending;
    private bool _loadedFromFile;
    private DisplaySnapshot? _confirmedDisplay;

    public override void _Ready()
    {
        Instance = this; ProcessMode = ProcessModeEnum.Always;
        var previewRun = ParseDebugArguments();
        Load();
        Data.Language = Localization.NormalizeLanguage(Data.Language);
        Localization.Instance?.SetLanguage(Data.Language);
        _saveTimer = MakeTimer(SaveDelaySeconds, FlushPendingSave);
        _applyTimer = MakeTimer(ApplyDelaySeconds, () => GraphicsSettings.ApplyAll(Data, false));
        GraphicsSettings.Attach(GetTree());
        // Sem arquivo (primeiro boot) a janela fica como o Godot a criou; nos previews de UI ela também não é tocada.
        GraphicsSettings.ApplyAll(Data, _loadedFromFile && !previewRun);
        // A cena inicial entra depois dos autoloads e pode não disparar SceneChanged.
        Callable.From(() => GraphicsSettings.ApplyCurrentScene(Data)).CallDeferred();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete) FlushPendingSave();
    }

    public override void _ExitTree() => FlushPendingSave();

    // ---------- Idioma e áudio ----------

    public void SetMusicVolume(float value) { Data.MusicVolume = Mathf.Clamp(value, 0, 100); ApplyAudio(); RequestSave(); }
    public void SetEffectsVolume(float value) { Data.EffectsVolume = Mathf.Clamp(value, 0, 100); ApplyAudio(); RequestSave(); }
    public void SetLanguage(string language) { Data.Language = Localization.NormalizeLanguage(language); Localization.Instance?.SetLanguage(Data.Language); RequestSave(); }
    public void ApplyAudio() { GetTree().CallGroup("audio_manager", "SetMusicVolume", Data.MusicVolume); GetTree().CallGroup("audio_manager", "SetEffectsVolume", Data.EffectsVolume); }

    // ---------- Janela (aplica na hora, mas só grava em CommitDisplay: a UI pede confirmação antes) ----------

    // Captura com a tela já resolvida (Monitor -1 = a tela atual da janela), para Reverter desfazer a troca de monitor.
    public DisplaySnapshot CaptureDisplay() => new(Data.DisplayMode, Data.ResolutionWidth, Data.ResolutionHeight, Data.Monitor, GraphicsSettings.ResolveScreen(Data));

    // Início de uma tentativa de mudança de janela: até Commit/Restore/Cancel o arquivo recebe o estado confirmado, não o de teste.
    public DisplaySnapshot BeginDisplayTrial()
    {
        _confirmedDisplay = CaptureDisplay();
        return _confirmedDisplay.Value;
    }

    public void CancelDisplayTrial() => _confirmedDisplay = null;

    public void SetDisplayMode(int mode) { Data.DisplayMode = Math.Clamp(mode, 0, 2); GraphicsSettings.ApplyWindow(Data); }
    public void SetResolution(Vector2I size) { Data.ResolutionWidth = size.X; Data.ResolutionHeight = size.Y; GraphicsSettings.ApplyWindow(Data); }
    public void SetMonitor(int monitor) { Data.Monitor = Math.Max(-1, monitor); GraphicsSettings.ApplyWindow(Data); }

    public void RestoreDisplay(DisplaySnapshot snapshot)
    {
        Data.DisplayMode = snapshot.Mode;
        Data.ResolutionWidth = snapshot.Width;
        Data.ResolutionHeight = snapshot.Height;
        Data.Monitor = snapshot.Screen; // volta para a tela de verdade (mesmo se o original era "automático")
        GraphicsSettings.ApplyWindow(Data);
        Data.Monitor = snapshot.Monitor;
        _confirmedDisplay = null;
        RequestSave();
    }

    public void CommitDisplay()
    {
        _confirmedDisplay = null;
        RequestSave();
    }

    // ---------- Gráficos (sliders aplicam com um pequeno atraso para agrupar os passos) ----------

    public void SetVsync(int mode) { Data.VsyncMode = Math.Clamp(mode, 0, 2); ApplyGraphics(); }
    public void SetMaxFps(int fps) { Data.MaxFps = Math.Max(0, fps); ApplyGraphics(); }
    public void SetRenderScale(int percent) { Data.RenderScalePercent = Math.Clamp(percent, 50, 100); ApplyGraphicsDeferred(); }
    public void SetUpscaler(int upscaler) { Data.Upscaler = Math.Clamp(upscaler, 0, 1); ApplyGraphics(); }
    public void SetAntiAliasing(int mode) { Data.AntiAliasing = Math.Clamp(mode, 0, 3); ApplyGraphics(); }
    public void SetShadowQuality(int quality) { Data.ShadowQuality = Math.Clamp(quality, 0, 3); ApplyGraphics(); }
    public void SetSsaoQuality(int quality) { Data.SsaoQuality = Math.Clamp(quality, 0, 3); ApplyGraphics(); }
    public void SetGlow(bool enabled) { Data.Glow = enabled; ApplyGraphics(); }
    public void SetBrightness(int percent) { Data.BrightnessPercent = Math.Clamp(percent, 50, 150); ApplyGraphicsDeferred(); }
    public void SetCameraShakeReduction(int percent) { Data.CameraShakeReduction = Math.Clamp(percent, 0, 100); ApplyGraphics(); }
    public void SetDamageFlash(int percent) { Data.DamageFlashPercent = Math.Clamp(percent, 0, 100); ApplyGraphics(); }
    public void ApplyPreset(int preset) { GraphicsSettings.ApplyPreset(Data, preset); ApplyGraphics(); }

    private void ApplyGraphics()
    {
        _applyTimer.Stop();
        GraphicsSettings.ApplyAll(Data, false);
        RequestSave();
    }

    private void ApplyGraphicsDeferred()
    {
        _applyTimer.Start();
        RequestSave();
    }

    // ---------- Arquivo ----------

    private Timer MakeTimer(float seconds, Action onTimeout)
    {
        var timer = new Timer { WaitTime = seconds, OneShot = true, IgnoreTimeScale = true, ProcessMode = ProcessModeEnum.Always };
        timer.Timeout += onTimeout;
        AddChild(timer);
        return timer;
    }

    // Só debug: --settings-file=<caminho> usa outro arquivo (testes); previews de UI não mexem na janela.
    private static bool ParseDebugArguments()
    {
        _settingsPath = DefaultSettingsPath;
        if (!OS.IsDebugBuild()) return false;
        var preview = false;
        foreach (var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith("--settings-file=")) _settingsPath = argument.Substring("--settings-file=".Length).Trim();
            else if (argument == "--ui-batch" || argument.StartsWith("--ui-batch=") || argument.StartsWith("--ui-preview=")) preview = true;
        }
        return preview;
    }

    private void Load()
    {
        _loadedFromFile = false;
        // Se o principal sumiu no meio de um rename, o .tmp (gravação completa) serve.
        var path = FileAccess.FileExists(_settingsPath) ? _settingsPath : FileAccess.FileExists(_settingsPath + ".tmp") ? _settingsPath + ".tmp" : null;
        if (path != null)
        {
            try
            {
                Data = JsonSerializer.Deserialize<GlobalSettingsData>(FileAccess.GetFileAsString(path)) ?? new();
                _loadedFromFile = true;
            }
            catch (Exception error)
            {
                GD.PushWarning($"Configuracoes globais invalidas: {error.Message}");
                // Guarda o arquivo ilegível (sobrescreve o anterior) antes de cair nos padrões e ele ser regravado.
                var bad = ProjectSettings.GlobalizePath(_settingsPath.GetBaseName() + ".bad.json");
                var copyError = DirAccess.CopyAbsolute(ProjectSettings.GlobalizePath(path), bad);
                if (copyError != Error.Ok) GD.PushWarning($"Nao foi possivel copiar o arquivo invalido: {copyError}");
                Data = new();
            }
        }
        // Migração só em memória: o arquivo antigo é reescrito no primeiro ajuste do jogador.
        GraphicsSettings.Normalize(Data);
    }

    private void RequestSave()
    {
        _savePending = true;
        _saveTimer?.Start();
    }

    public void FlushPendingSave()
    {
        if (!_savePending) return;
        _savePending = false;
        _saveTimer?.Stop();
        Save();
    }

    private void Save()
    {
        try
        {
            // Janela em teste (confirmação pendente): grava o estado confirmado, nunca o não confirmado.
            var live = new DisplaySnapshot(Data.DisplayMode, Data.ResolutionWidth, Data.ResolutionHeight, Data.Monitor, 0);
            if (_confirmedDisplay is { } confirmed)
            {
                Data.DisplayMode = confirmed.Mode;
                Data.ResolutionWidth = confirmed.Width;
                Data.ResolutionHeight = confirmed.Height;
                Data.Monitor = confirmed.Monitor;
            }
            GraphicsSettings.SyncLegacyFields(Data);
            var json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
            Data.DisplayMode = live.Mode;
            Data.ResolutionWidth = live.Width;
            Data.ResolutionHeight = live.Height;
            Data.Monitor = live.Monitor;
            // Arquivo temporário + rename: um crash no meio da escrita não corrompe as configurações.
            var tempPath = _settingsPath + ".tmp";
            using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    GD.PushError($"Nao foi possivel gravar as configuracoes: {FileAccess.GetOpenError()}");
                    return;
                }
                file.StoreString(json);
            }
            var error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tempPath), ProjectSettings.GlobalizePath(_settingsPath));
            if (error != Error.Ok) GD.PushError($"Nao foi possivel concluir a gravacao das configuracoes: {error}");
        }
        catch (Exception error) { GD.PushError($"Falha ao gravar as configuracoes: {error.Message}"); }
    }
}
