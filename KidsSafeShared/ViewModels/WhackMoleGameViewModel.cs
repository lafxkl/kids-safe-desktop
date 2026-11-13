using System.Collections.Generic;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 打地鼠：9 个地洞随机冒出地鼠，点中得分，限时 60 秒。
/// 游戏循环由页面用 DispatcherTimer 每帧调用 Update(dt) 驱动。
/// </summary>
public class WhackMoleGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    public List<Mole> Moles { get; } = new();

    private float _w;
    private float _h;
    private int _score;
    private float _timeLeft = 60f;
    private int _displaySec = 60;
    private bool _playing = true;
    private bool _gameOver;
    private float _spawnTimer;
    private float _upTime = 1.1f;
    private float _spawnInterval = 0.5f;
    private int _maxUp = 3;
    private string _resultText = string.Empty;

    public float TimeLeft => _timeLeft;

    public string TimeText => $"⏱ {_displaySec}s";

    public string ScoreText
    {
        get => $"💥 {_score}";
        private set { }
    }

    public bool GameOver
    {
        get => _gameOver;
        private set => SetProperty(ref _gameOver, value);
    }

    public string ResultText
    {
        get => _resultText;
        private set => SetProperty(ref _resultText, value);
    }

    /// <summary>页面测得画布尺寸后调用，仅在尺寸变化时重新布局地洞。</summary>
    public void SetSize(float w, float h)
    {
        if (w <= 0 || h <= 0) return;
        var changed = !_w.Equals(w) || !_h.Equals(h);
        _w = w;
        _h = h;
        if (changed) Layout();
    }

    private void Layout()
    {
        Moles.Clear();
        var pad = 26f;
        var fieldTop = 36f;
        var fieldBottom = _h - 16f;
        var colW = (_w - 2 * pad) / 3f;
        var rowH = (fieldBottom - fieldTop) / 3f;
        var r = System.MathF.Min(colW, rowH) * 0.34f;
        for (var ri = 0; ri < 3; ri++)
            for (var ci = 0; ci < 3; ci++)
            {
                var x = pad + (ci + 0.5f) * colW;
                var y = fieldTop + (ri + 0.5f) * rowH;
                Moles.Add(new Mole { X = x, Y = y, R = r, State = 0, Timer = 0 });
            }
    }

    public void Start()
    {
        var diff = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        (_upTime, _spawnInterval, _maxUp) = diff switch
        {
            GameDifficulty.Easy => (1.45f, 0.72f, 2),
            GameDifficulty.Hard => (0.8f, 0.34f, 4),
            _ => (1.1f, 0.5f, 3)
        };

        foreach (var m in Moles) { m.State = 0; m.Timer = 0; }
        _score = 0;
        _timeLeft = 60f;
        _displaySec = 60;
        _spawnTimer = 0;
        _playing = true;
        GameOver = false;
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(TimeText));
    }

    public void Update(float dt)
    {
        if (!_playing || _gameOver) return;

        _timeLeft -= dt;
        if (_timeLeft <= 0)
        {
            _timeLeft = 0;
            EndGame();
            return;
        }

        var sec = (int)System.Math.Ceiling(_timeLeft);
        if (sec != _displaySec)
        {
            _displaySec = sec;
            OnPropertyChanged(nameof(TimeText));
        }

        // 地鼠状态倒计时
        foreach (var m in Moles)
        {
            if (m.State == 1 || m.State == 2)
            {
                m.Timer -= dt;
                if (m.Timer <= 0) m.State = 0;
            }
        }

        // 生成新地鼠
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0;
            var upCount = 0;
            foreach (var m in Moles) if (m.State != 0) upCount++;
            if (upCount < _maxUp)
            {
                var hidden = new List<Mole>();
                foreach (var m in Moles) if (m.State == 0) hidden.Add(m);
                if (hidden.Count > 0)
                {
                    var m = hidden[_rnd.Next(hidden.Count)];
                    m.State = 1;
                    m.Timer = _upTime;
                }
            }
        }
    }

    public void OnTap(Microsoft.Maui.Graphics.PointF p)
    {
        if (!_playing || _gameOver) return;
        foreach (var m in Moles)
        {
            if (m.State != 1) continue;
            var dx = m.X - p.X;
            var dy = m.Y - p.Y;
            var rr = m.R * 0.95f;
            if (dx * dx + dy * dy <= rr * rr)
            {
                m.State = 2;
                m.Timer = 0.22f;
                _score++;
                ServiceProvider.Get<ISoundService>().PlayCorrect();
                OnPropertyChanged(nameof(ScoreText));
                return;
            }
        }
    }

    private void EndGame()
    {
        _playing = false;
        GameOver = true;
        var sound = ServiceProvider.Get<ISoundService>();
        var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
        {
            Game = GameType.WhackMole,
            Difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty,
            Score = _score,
            MaxScore = _score,
            Date = System.DateTime.Now,
            Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
        });
        ResultText = isBest
            ? $"🎉 新纪录！你打了 {_score} 只地鼠！"
            : $"时间到！你一共打了 {_score} 只地鼠，真厉害！";
        sound.PlayWin();
    }

    public void Replay() => Start();
}
