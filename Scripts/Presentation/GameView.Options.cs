using Godot;
using System.IO;

namespace FutureDark.Presentation;

public partial class GameView
{
    private float _masterVolume = .5f, _ambientVolume = .5f, _effectsVolume = .5f, _interfaceVolume = .5f;
    private float _shakeStrength = 1, _effectIntensity = 1, _uiScale = 1, _textScale = 1;
    private bool _muteUnfocused = true, _windowFocused = true, _fastAnimations;
    private int _displayMode, _resolutionIndex = 1;
    private AudioStreamPlayer _ambientAudio = null!, _interfaceAudio = null!;
    private static readonly Vector2I[] Resolutions = [new(1280, 800), new(1920, 1080), new(2560, 1440), new(3440, 1440)];

    private void InitializeAudio()
    {
        _ambientAudio = new AudioStreamPlayer { Stream = CreateAmbientSound() };
        _interfaceAudio = new AudioStreamPlayer { Stream = CreateInterfaceSound() };
        AddChild(_ambientAudio);
        AddChild(_interfaceAudio);
        GetWindow().FocusEntered += () => { _windowFocused = true; ApplyAudio(); };
        GetWindow().FocusExited += () => { _windowFocused = false; ApplyAudio(); };
        ApplyAudio();
    }

    private void ApplyAudio()
    {
        if (_ambientAudio is null) return;
        float master = _muted || _muteUnfocused && !_windowFocused ? 0 : _masterVolume;
        static float Db(float value) => value <= 0 ? -80 : Mathf.LinearToDb(value);
        _audio.VolumeDb = Db(master * _effectsVolume * .5f);
        _ambientAudio.VolumeDb = Db(master * _ambientVolume * .35f);
        _interfaceAudio.VolumeDb = Db(master * _interfaceVolume * .3f);
        if (master * _ambientVolume > 0)
        {
            if (!_ambientAudio.Playing) _ambientAudio.Play();
        }
        else _ambientAudio.Stop();
    }

    private void PlayInterfaceSound()
    {
        if (!_muted && (!_muteUnfocused || _windowFocused)) _interfaceAudio.Play();
    }

    private static AudioStreamWav CreateAmbientSound()
    {
        const int rate = 22050, samples = rate * 4;
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / rate;
            double signal = (.14 * Math.Sin(Math.Tau * 55 * t) + .05 * Math.Sin(Math.Tau * 82.5 * t) +
                .02 * Math.Sin(Math.Tau * 110 * t)) * (.8 + .2 * Math.Sin(Math.Tau * .25 * t));
            short sample = (short)(signal * short.MaxValue);
            data[i * 2] = (byte)(sample & 255);
            data[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate,
            Data = data, LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = samples };
    }

    private static AudioStreamWav CreateInterfaceSound()
    {
        const int rate = 22050;
        var data = new byte[2206];
        for (int i = 0; i < data.Length / 2; i++)
        {
            double t = (double)i / rate;
            short sample = (short)(Math.Sin(Math.Tau * 420 * t) * Math.Exp(-t * 95) * Math.Min(1, t * 1000) * short.MaxValue * .16);
            data[i * 2] = (byte)(sample & 255);
            data[i * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data };
    }

    private void ApplyDisplay()
    {
        var window = GetWindow();
        window.Mode = Window.ModeEnum.Windowed;
        window.Borderless = _displayMode == 1;
        if (_displayMode == 1)
        {
            window.Position = DisplayServer.ScreenGetPosition(window.CurrentScreen);
            window.Size = DisplayServer.ScreenGetSize(window.CurrentScreen);
        }
        else if (_displayMode == 2) window.Mode = Window.ModeEnum.Fullscreen;
        else window.Size = Resolutions[_resolutionIndex];
    }

    private void DrawOptions()
    {
        Box(new Rect2(140, 88, 1160, 737), Ink, Line);
        Text("Options", 179, 140, 34, Paper, _bold);
        Text("Audio", 182, 190, 23, Paper, _bold);
        Text("Display & feedback", 744, 190, 23, Paper, _bold);
        VolumeOption("master", "Master", 225, _masterVolume, v => _masterVolume = v);
        VolumeOption("ambient", "Music / ambience", 286, _ambientVolume, v => _ambientVolume = v);
        VolumeOption("effects", "Combat effects", 347, _effectsVolume, v => _effectsVolume = v);
        VolumeOption("interface", "Interface sounds", 408, _interfaceVolume, v => _interfaceVolume = v);
        Button("option-mute", _muted ? "MUTED" : "SOUND ON", new Rect2(183, 493, 470, 37), ToggleMute);
        Button("option-focus", _muteUnfocused ? "MUTE WHEN UNFOCUSED: ON" : "MUTE WHEN UNFOCUSED: OFF",
            new Rect2(183, 544, 470, 37), () => { _muteUnfocused = !_muteUnfocused; SaveSettings(); ApplyAudio(); });
        Paragraph("A quiet ambient bed and interface clicks sit below combat feedback. Successful combinations carry the strongest sound.", 183, 627, 463, 18, Muted, 4);

        Button("option-display", new[] { "WINDOWED", "BORDERLESS DESKTOP", "FULLSCREEN" }[_displayMode],
            new Rect2(745, 214, 506, 35), () => { _displayMode = (_displayMode + 1) % 3; ApplyDisplay(); SaveSettings(); });
        var resolution = Resolutions[_resolutionIndex];
        Button("option-resolution", $"{resolution.X} x {resolution.Y}", new Rect2(745, 260, 506, 35),
            () => { _resolutionIndex = (_resolutionIndex + 1) % Resolutions.Length; ApplyDisplay(); SaveSettings(); }, _displayMode == 0);
        Button("option-ui", $"UI FIT: {_uiScale * 100:0}%", new Rect2(745, 306, 243, 35),
            () => { _uiScale = Mathf.RoundToInt(_uiScale * 100) >= 100 ? .85f : (Mathf.RoundToInt(_uiScale * 100) + 5) / 100f; SaveSettings(); });
        Button("option-text", $"TEXT: {_textScale * 100:0}%", new Rect2(1000, 306, 251, 35),
            () => { _textScale = _textScale > 1.05f ? .9f : _textScale + .1f; SaveSettings(); });
        Button("option-shake", $"SHAKE: {_shakeStrength * 100:0}%", new Rect2(745, 352, 506, 35),
            () => { _shakeStrength = _shakeStrength >= 1 ? 0 : _shakeStrength + .25f; SaveSettings(); });
        Button("option-intensity", $"COMBO INTENSITY: {_effectIntensity * 100:0}%", new Rect2(745, 398, 506, 35),
            () => { _effectIntensity = _effectIntensity >= 1 ? 0 : _effectIntensity + .25f; SaveSettings(); });
        Button("option-reduced", _reduced ? "REDUCED MOTION: ON" : "REDUCED MOTION: OFF", new Rect2(745, 444, 506, 35), ToggleMotion);
        Button("option-fast", _fastAnimations ? "ANIMATIONS: FAST (2X)" : "ANIMATIONS: NORMAL", new Rect2(745, 490, 506, 35),
            () => { _fastAnimations = !_fastAnimations; SaveSettings(); });
        Button("option-haze", _haze ? "THREAT OVERLAY: ON" : "THREAT OVERLAY: OFF", new Rect2(745, 536, 506, 35), ToggleHaze);
        Paragraph("UI fit preserves the whole tactical canvas. Text has its own scale. Reduced motion removes movement; fast mode shortens animation timing without changing the rules.", 745, 627, 506, 18, Muted, 4);
        Button("options-close", "BACK", new Rect2(1038, 763, 214, 38), () => _modal = _title ? "" : "restart", primary: true);
    }

    private void VolumeOption(string id, string label, float y, float value, Action<float> set)
    {
        Text(label, 184, y + 24, 19, Paper);
        Button("option-" + id + "-down", "-", new Rect2(426, y, 42, 35), () => { set(Math.Max(0, Mathf.RoundToInt(value * 100) - 10) / 100f); SaveSettings(); ApplyAudio(); }, value > 0);
        Text($"{value * 100:0}%", 490, y + 24, 18, Muted, _mono);
        Button("option-" + id + "-up", "+", new Rect2(594, y, 42, 35), () => { set(Math.Min(100, Mathf.RoundToInt(value * 100) + 10) / 100f); SaveSettings(); ApplyAudio(); }, value < 1);
    }

    private void ReadOptions(ConfigFile config)
    {
        float Read(string key, float fallback, float min = 0, float max = 1)
        {
            var setting = config.GetValue("options", key, fallback);
            if (setting.VariantType is not (Variant.Type.Float or Variant.Type.Int)) throw new InvalidDataException($"Invalid setting: {key}");
            float value = (float)setting.AsDouble();
            if (!float.IsFinite(value) || value < min || value > max) throw new InvalidDataException($"Invalid setting: {key}");
            return value;
        }
        int ReadIndex(string key, int fallback, int max)
        {
            float value = Read(key, fallback, 0, max);
            if (value != MathF.Truncate(value)) throw new InvalidDataException($"Invalid setting: {key}");
            return (int)value;
        }
        _masterVolume = Read("master", .5f); _ambientVolume = Read("ambient", .5f);
        _effectsVolume = Read("effects", .5f); _interfaceVolume = Read("interface", .5f);
        _shakeStrength = Read("shake", 1); _effectIntensity = Read("intensity", 1);
        _uiScale = Read("ui_scale", 1, .8f, 1.01f); _textScale = Read("text_scale", 1, .89f, 1.11f);
        _displayMode = ReadIndex("display", 0, 2);
        _resolutionIndex = ReadIndex("resolution", 1, Resolutions.Length - 1);
        _fastAnimations = config.GetValue("options", "fast", false).AsBool();
        _muteUnfocused = config.GetValue("options", "mute_unfocused", true).AsBool();
        if (!_smoke) ApplyDisplay();
    }

    private void WriteOptions(ConfigFile config)
    {
        config.SetValue("options", "master", _masterVolume); config.SetValue("options", "ambient", _ambientVolume);
        config.SetValue("options", "effects", _effectsVolume); config.SetValue("options", "interface", _interfaceVolume);
        config.SetValue("options", "shake", _shakeStrength); config.SetValue("options", "intensity", _effectIntensity);
        config.SetValue("options", "ui_scale", _uiScale); config.SetValue("options", "text_scale", _textScale);
        config.SetValue("options", "display", _displayMode); config.SetValue("options", "resolution", _resolutionIndex);
        config.SetValue("options", "fast", _fastAnimations); config.SetValue("options", "mute_unfocused", _muteUnfocused);
    }
}
