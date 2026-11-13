using System.Collections.Generic;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 欢乐打气球：彩色气球从底部不断升起，点中即爆破得分，限时 60 秒。
/// 游戏循环由页面用 DispatcherTimer 每帧调用 Update(dt) 驱动。
/// </summary>
public class BalloonGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<Color> _palette = new()
    {
        Colors.Red, Colors.Orange, Colors.Gold, Colors.Green,
        Colors.DodgerBlue, Colors.Purple, Colors.DeepPink, Colors.Teal
    };

    public List<Balloon> Balloons { get; } = new();

    private float _w;
    private float _h;
    private int _score;
    private float _timeLeft = 60f;
    private int _displaySec = 60;
    private bool _playing = true;
    private bool _gameOver;
    private float _spawnTimer;
    private float _spawnInterval = 0.85f;
    private float _riseMin = 70f;
    private float _riseMax = 120f;
    private string _resultText = string.Empty;

    public float TimeLeft => _timeLeft;

    public string TimeText => $"⏱ {_displaySec}s";

    public string ScoreText
    {
        get => $"🎈 {_score}";
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

    public void SetSize(float w, float h)
    {
        if (w <= 0 || h <= 0) return;
        _w = w;
        _h = h;
    }

    public void Start()
    {
        var diff = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        (_spawnInterval, _riseMin, _riseMax) = diff switch
        {
            GameDifficulty.Easy => (1.05f, 55f, 95f),
            GameDifficulty.Hard => (0.55f, 95f, 160f),
            _ => (0.8f, 75f, 130f)
        };

        Balloons.Clear();
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

        // 移动气球；处理被打中后的爆炸余晖
        for (var i = Balloons.Count - 1; i >= 0; i--)
        {
            var b = Balloons[i];
            if (b.Popped)
            {
                b.PopTimer -= dt;
                if (b.PopTimer <= 0) Balloons.RemoveAt(i);
                continue;
            }
            b.Y += b.Vy * dt; // Vy 为负，向上
            if (b.Y < -b.R * 2) Balloons.RemoveAt(i);
        }

        // 生成新气球
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0;
            SpawnBalloon();
        }
    }

    private void SpawnBalloon()
    {
        if (_w <= 0 || _h <= 0) return;
        var r = 26f + _rnd.Next(0, 22);
        var x = r + (float)_rnd.NextDouble() * (_w - 2 * r);
        var speed = _riseMin + (float)_rnd.NextDouble() * (_riseMax - _riseMin);
        Balloons.Add(new Balloon
        {
            X = x,
            Y = _h + r,
            R = r,
            Vy = -speed,
            Color = _palette[_rnd.Next(_palette.Count)]
        });
    }

    public void OnTap(Microsoft.Maui.Graphics.PointF p)
    {
        if (!_playing || _gameOver) return;
        for (var i = Balloons.Count - 1; i >= 0; i--)
        {
            var b = Balloons[i];
            if (b.Popped) continue;
            var dx = b.X - p.X;
            var dy = b.Y - p.Y;
            var rr = b.R * 1.05f;
            if (dx * dx + dy * dy <= rr * rr)
            {
                b.Popped = true;
                b.PopTimer = 0.22f;
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
        var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
        {
            Game = GameType.Balloon,
            Difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty,
            Score = _score,
            MaxScore = _score,
            Date = System.DateTime.Now,
            Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
        });
        ResultText = isBest
            ? $"🎉 新纪录！你戳破了 {_score} 个气球！"
            : $"时间到！你一共戳破了 {_score} 个气球，真开心！";
        ServiceProvider.Get<ISoundService>().PlayWin();
    }

    public void Replay() => Start();
}
