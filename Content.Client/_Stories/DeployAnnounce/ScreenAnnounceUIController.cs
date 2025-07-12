using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Content.Client.Gameplay;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Marines.ScreenAnnounce;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._RMC14.Marines.ScreenAnnounce;

public sealed class ScreenAnnounceUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [UISystemDependency] private readonly ScreenAnnounceSystem? _screenAnnounce = default!;

    private ScreenAnnounceControl? _screenAnnounceControl;

    public void OnStateEntered(GameplayState state)
    {
        _screenAnnounceControl = new ScreenAnnounceControl();
        UIManager.RootControl.AddChild(_screenAnnounceControl);
    }

    public void OnStateExited(GameplayState state)
    {
        if (_screenAnnounceControl != null)
        {
            UIManager.RootControl.RemoveChild(_screenAnnounceControl);
            _screenAnnounceControl = null;
        }
    }

    public void UpdateAnnouncement(string[] announceText)
    {
        _screenAnnounceControl?.UpdateAnnouncement(announceText);
    }
}

public sealed class ScreenAnnounceControl : Control
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Font _font;
    private FormattedMessage[] _announceText = Array.Empty<FormattedMessage>();
    private ScreenAnnounceTarget _type;
    private EntityUid? _squad;

    private float _printSpeed = 0.03f;
    private float _shakeIntensity = 0.8f;
    private float _flickerChance = 0.02f;
    private float _glitchChance = 0.01f;
    private float _holdDuration = 3f;
    private float _fadeDuration = 1.5f;
    private float _lineHeightUnscaled = 40f;
    private float _maxTextWidthFraction = 0.9f;

    private float _timer;
    private float _globalTime;
    private float _holdStartTime;
    private int _currentLine;
    private int _currentChar;
    private bool _finished;
    private bool _fadingOut;

    public ScreenAnnounceControl()
    {
        IoCManager.InjectDependencies(this);
        _font = _resCache.GetFont("/Fonts/Noto/NotoSans-Bold.ttf", 15);
    }

    public void UpdateAnnouncement(FormattedMessage[] announceText)
    {
        _announceText = announceText;

        _timer = 0;
        _globalTime = 0;
        _currentLine = 0;
        _currentChar = 0;
        _finished = false;
        _fadingOut = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var player = _playerManager.LocalSession?.AttachedEntity;
        if (player == null || !_entMan.HasComponent<MarineComponent>(player))
            return;

        var screenSize = PixelSize;

        float scale = MathF.Max(1f, _configManager.GetCVar(CVars.DisplayUIScale)) * 1.5f;
        _globalTime += (float)_timing.FrameTime.TotalSeconds;

        float alpha = GetAlpha();

        DrawFirstDeployAnnounce(handle, screenSize, scale, alpha);
        UpdateState((float)_timing.FrameTime.TotalSeconds);
    }

    private void DrawFirstDeployAnnounce(DrawingHandleScreen handle, Vector2i screenSize, float scale, float alpha)
    {
        var lineHeight = _lineHeightUnscaled * scale;
        var totalHeight = _announceText.Length * lineHeight;
        var padding = 20f * scale;
        var baseX = padding;
        var baseY = screenSize.Y - totalHeight - padding;

        for (int i = 0; i <= _currentLine && i < _announceText.Length; i++)
        {
            var offset = _random.NextVector2(-_shakeIntensity, _shakeIntensity) * alpha;
            var position = new Vector2(baseX, baseY + i * lineHeight) + offset;

            handle.DrawString(_font, position, GetVisibleLine(_announceText[i], i), scale, Color.White.WithAlpha(alpha));
        }
    }

    private float GetAlpha()
    {
        if (!_finished)
            return MathF.Min(1f, _globalTime / _fadeDuration);

        if (_fadingOut)
            return MathF.Max(0f, 1f - (_globalTime - _holdStartTime - _holdDuration) / _fadeDuration);

        return 1f;
    }

    private string GetVisibleLine(FormattedMessage message, int index)
    {
        if (index < _currentLine)
            return message.ToText(); // Full line visible
        if (index > _currentLine)
            return string.Empty;

        return message.ToText().Substring(0, Math.Min(_currentChar, message.ToText().Length));
    }

    private void UpdateState(float delta)
    {
        if (_finished)
        {
            if (!_fadingOut && _globalTime - _holdStartTime > _holdDuration)
                _fadingOut = true;
            return;
        }

        _timer += delta;
        while (_timer >= _printSpeed)
        {
            _timer -= _printSpeed;
            _currentChar++;

            if (_currentLine < _announceText.Length)
            {
                var line = _announceText[_currentLine].ToText();
                if (_currentChar >= line.Length)
                {
                    _currentLine++;
                    _currentChar = 0;

                    if (_currentLine >= _announceText.Length)
                    {
                        _finished = true;
                        _holdStartTime = _globalTime;
                    }
                }
            }
        }
    }
}
