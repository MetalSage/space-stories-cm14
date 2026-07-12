using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._Stories.Hunter.Marking.Components;
using Content.Shared._Stories.SCCVars;
using Content.Shared._Stories.TTS;
using Robust.Shared.Configuration;

namespace Content.Server._Stories.TTS;

public sealed class TtsAudioProcessingSystem : EntitySystem
{
    private const string StandardRadioEffectName = "standard radio";
    private const string XenoHivemindEffectName = "xeno hivemind";
    private const string HunterEffectName = "hunter";
    private const string MegaphoneEffectName = "megaphone";
    private const string AresEffectName = "ARES";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    private readonly HashSet<string> _disabledEffects = new();
    private readonly object _disabledEffectsLock = new();

    private string _ffmpegArgs = "";
    private string _ffmpegPath = "ffmpeg";
    private string _hunterFfmpegArgs = "";
    private string _megaphoneFfmpegArgs = "";
    private string _aresFfmpegArgs = "";
    private bool _radioEffectEnabled;

    private ISawmill _sawmill = default!;
    private string _xenoFfmpegArgs = "";

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("tts.processing");

        _cfg.OnValueChanged(SCCVars.TTSRadioEffect, v =>
        {
            _radioEffectEnabled = v;
            if (v)
                EnableEffect(StandardRadioEffectName);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSFfmpegPath, v =>
        {
            _ffmpegPath = v;
            EnableAllEffects();
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSFfmpegArguments, v =>
        {
            _ffmpegArgs = v;
            EnableEffect(StandardRadioEffectName);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSXenoFfmpegArguments, v =>
        {
            _xenoFfmpegArgs = v;
            EnableEffect(XenoHivemindEffectName);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSHunterFfmpegArguments, v =>
        {
            _hunterFfmpegArgs = v;
            EnableEffect(HunterEffectName);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSMegaphoneFfmpegArguments, v =>
        {
            _megaphoneFfmpegArgs = v;
            EnableEffect(MegaphoneEffectName);
        }, true);
        _cfg.OnValueChanged(SCCVars.TTSAresFfmpegArguments, v =>
        {
            _aresFfmpegArgs = v;
            EnableEffect(AresEffectName);
        }, true);
    }

    public async Task<byte[]> ProcessRadioAudio(EntityUid uid, byte[] audioData)
    {
        if (_entityManager.HasComponent<HunterComponent>(uid))
            return await ApplyHunterEffect(audioData);

        if (_entityManager.HasComponent<XenoComponent>(uid))
            return await ApplyXenoHivemindEffect(audioData);

        return await ApplyStandardRadioEffect(audioData);
    }

    public async Task<byte[]> ApplyStandardRadioEffect(byte[] oggData)
    {
        return await ApplyEffect(oggData, _ffmpegArgs, StandardRadioEffectName);
    }

    public async Task<byte[]> ApplyXenoHivemindEffect(byte[] oggData)
    {
        return await ApplyEffect(oggData, _xenoFfmpegArgs, XenoHivemindEffectName);
    }

    public async Task<byte[]> ApplyHunterEffect(byte[] oggData)
    {
        return await ApplyEffect(oggData, _hunterFfmpegArgs, HunterEffectName);
    }

    public async Task<byte[]> ApplyMegaphoneEffect(byte[] oggData)
    {
        return await ApplyEffect(oggData, _megaphoneFfmpegArgs, MegaphoneEffectName);
    }

    public async Task<byte[]> ApplyPlaybackEffects(byte[] oggData, TTSAudioEffect effects)
    {
        if (effects == TTSAudioEffect.None)
            return oggData;

        var processed = oggData;

        if (effects.HasFlag(TTSAudioEffect.Hunter))
            processed = await ApplyHunterEffect(processed);

        if (effects.HasFlag(TTSAudioEffect.XenoHivemind))
            processed = await ApplyXenoHivemindEffect(processed);

        if (effects.HasFlag(TTSAudioEffect.Ares))
            processed = await ApplyAresEffect(processed);

        if (effects.HasFlag(TTSAudioEffect.StandardRadio))
            processed = await ApplyStandardRadioEffect(processed);

        if (effects.HasFlag(TTSAudioEffect.Megaphone))
            processed = await ApplyMegaphoneEffect(processed);

        return processed;
    }

    public async Task<byte[]> ApplyAresEffect(byte[] oggData)
    {
        return await ApplyEffect(oggData, _aresFfmpegArgs, AresEffectName);
    }

    private async Task<byte[]> ApplyEffect(byte[] oggData, string ffmpegArgs, string effectName)
    {
        if (!_radioEffectEnabled && effectName == StandardRadioEffectName)
            return oggData;

        if (IsEffectDisabled(effectName))
            return oggData;

        if (string.IsNullOrWhiteSpace(_ffmpegPath))
        {
            DisableEffect(effectName, "ffmpeg path is not configured.", false);
            return oggData;
        }

        if (string.IsNullOrWhiteSpace(ffmpegArgs))
        {
            DisableEffect(effectName, $"ffmpeg arguments for {effectName} are not configured.", false);
            return oggData;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = ffmpegArgs,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = new Process { StartInfo = processStartInfo };

            if (!process.Start())
            {
                DisableEffect(effectName, $"Failed to start ffmpeg process for {effectName} effect.", true);
                return oggData;
            }

            using var memoryStream = new MemoryStream();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.StandardInput.BaseStream.WriteAsync(oggData, 0, oggData.Length);

            process.StandardInput.Close();

            await Task.WhenAll(outputTask, errorTask);
            await process.WaitForExitAsync();

            var errorOutput = await errorTask;
            if (process.ExitCode != 0)
            {
                DisableEffect(
                    effectName,
                    $"ffmpeg for {effectName} effect exited with code {process.ExitCode}. Stderr: {errorOutput}",
                    true);
                return oggData;
            }

            var processedData = memoryStream.ToArray();
            if (processedData.Length == 0)
            {
                DisableEffect(
                    effectName,
                    $"ffmpeg for {effectName} effect produced an empty output. Stderr: {errorOutput}.",
                    false);
                return oggData;
            }

            return processedData;
        }
        catch (Win32Exception)
        {
            DisableEffect(effectName, $"ffmpeg not found at path '{_ffmpegPath}'.", true);
            return oggData;
        }
        catch (Exception e)
        {
            DisableEffect(effectName, $"An exception occurred while running ffmpeg for {effectName} effect: {e}", true);
            return oggData;
        }
    }

    private bool IsEffectDisabled(string effectName)
    {
        lock (_disabledEffectsLock)
        {
            return _disabledEffects.Contains(effectName);
        }
    }

    private void DisableEffect(string effectName, string reason, bool error)
    {
        lock (_disabledEffectsLock)
        {
            if (!_disabledEffects.Add(effectName))
                return;
        }

        var message = $"{reason} Disabling {effectName} TTS effect until its audio processing configuration changes.";
        if (error)
            _sawmill.Error(message);
        else
            _sawmill.Warning(message);
    }

    private void EnableEffect(string effectName)
    {
        lock (_disabledEffectsLock)
        {
            _disabledEffects.Remove(effectName);
        }
    }

    private void EnableAllEffects()
    {
        lock (_disabledEffectsLock)
        {
            _disabledEffects.Clear();
        }
    }
}
