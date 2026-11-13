using System.Collections.Generic;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 大鱼吃小鱼：玩家控制一条小鱼，吃掉比自己小的鱼会变大，被比自己大的鱼吃掉则游戏结束。
/// 游戏循环由页面用 DispatcherTimer 每帧调用 Update(dt) 驱动。
/// </summary>
public class FishGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private readonly List<Color> _palette = new()
    {
        Colors.Orange, Colors.DeepPink, Colors.Gold, Colors.LimeGreen,
        Colors.MediumPurple, Colors.Tomato, Colors.Teal, Colors.HotPink, Colors.DodgerBlue
    };

    public Fish Player { get; } = new() { IsPlayer = true, Color = Colors.OrangeRed };
    public List<Fish> Others { get; } = new();

    private float _width = 360;
    private float _height = 640;
    private int _score;
    private bool _playing = true;
    private bool _gameOver;
    private float _playerSpeed = 215;
    private float _aiFactor = 1f;
    private int _targetCount = 10;
    private float _spawnInterval = 1.1f;
    private float _spawnTimer;
    private PointF _target;

    public float Width
    {
        get => _width;
        set => _width = value > 0 ? value : _width;
    }

    public float Height
    {
        get => _height;
        set => _height = value > 0 ? value : _height;
    }

    public int Score
    {
        get => _score;
        set => SetProperty(ref _score, value);
    }

    public bool Playing
    {
        get => _playing;
        set => SetProperty(ref _playing, value);
    }

    public bool GameOver
    {
        get => _gameOver;
        set => SetProperty(ref _gameOver, value);
    }

    public string ScoreText => $"🐟 吃了 {_score} 条";
    public string SizeText => $"大小 {Player.R:0}";

    public void Start()
    {
        var diff = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        (_playerSpeed, _aiFactor, _targetCount, _spawnInterval, Player.R) = diff switch
        {
            GameDifficulty.Easy => (240f, 0.8f, 8, 1.4f, 28f),
            GameDifficulty.Hard => (190f, 1.25f, 12, 0.9f, 20f),
            _ => (215f, 1.0f, 10, 1.1f, 24f)
        };

        Player.X = _width / 2f;
        Player.Y = _height / 2f;
        Player.Vx = 0;
        Player.Vy = 0;
        Player.Angle = 0;
        _target = new PointF(Player.X, Player.Y);

        Others.Clear();
        for (var i = 0; i < _targetCount; i++)
            SpawnFish();

        Score = 0;
        _spawnTimer = 0;
        Playing = true;
        GameOver = false;
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(SizeText));
    }

    public void OnPointer(PointF p)
    {
        _target = p;
        Playing = true; // 触摸即开始游动
    }

    /// <summary>推进一帧（dt 为秒）。</summary>
    public void Update(float dt)
    {
        if (!Playing || GameOver) return;

        // 玩家朝目标移动
        var dx = _target.X - Player.X;
        var dy = _target.Y - Player.Y;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > 1f)
        {
            var step = MathF.Min(_playerSpeed * dt, dist);
            Player.X += dx / dist * step;
            Player.Y += dy / dist * step;
            Player.Angle = Math.Atan2(dy, dx);
        }
        Clamp(Player);

        // AI 鱼移动 + 撞墙反弹
        foreach (var f in Others)
        {
            f.X += f.Vx * dt;
            f.Y += f.Vy * dt;
            if (f.X < f.R) { f.X = f.R; f.Vx = Math.Abs(f.Vx); }
            else if (f.X > _width - f.R) { f.X = _width - f.R; f.Vx = -Math.Abs(f.Vx); }
            if (f.Y < f.R) { f.Y = f.R; f.Vy = Math.Abs(f.Vy); }
            else if (f.Y > _height - f.R) { f.Y = _height - f.R; f.Vy = -Math.Abs(f.Vy); }
            f.Angle = Math.Atan2(f.Vy, f.Vx);
        }

        // 碰撞：吃鱼 / 被吃
        for (var i = Others.Count - 1; i >= 0; i--)
        {
            var o = Others[i];
            var ox = o.X - Player.X;
            var oy = o.Y - Player.Y;
            var od = MathF.Sqrt(ox * ox + oy * oy);
            if (od > (Player.R + o.R) * 0.7f) continue;

            if (Player.R > o.R * 1.06f)
            {
                Others.RemoveAt(i);
                Player.R = MathF.Min(Player.R + 0.7f, 66f);
                Score++;
                ServiceProvider.Get<ISoundService>().PlayCorrect();
                OnPropertyChanged(nameof(ScoreText));
                OnPropertyChanged(nameof(SizeText));
            }
            else if (o.R > Player.R * 1.06f)
            {
                EndGame();
                return;
            }
        }

        // 补充鱼群
        _spawnTimer += dt;
        if (_spawnTimer >= _spawnInterval && Others.Count < _targetCount)
        {
            _spawnTimer = 0;
            SpawnFish();
        }
    }

    private void EndGame()
    {
        Playing = false;
        GameOver = true;
        ServiceProvider.Get<ISoundService>().PlayWrong();
        var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
        {
            Game = GameType.FishEat,
            Difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty,
            Score = _score,
            MaxScore = _score, // 无尽模式，以吃鱼数为得分
            Date = System.DateTime.Now,
            Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
        });
        ResultText = isBest
            ? $"🎉 新纪录！你一共吃了 {_score} 条小鱼！"
            : $"你被大鲨鱼吃掉啦～这次吃了 {_score} 条小鱼，再来一次？";
    }

    public string ResultText { get; private set; } = string.Empty;

    private void SpawnFish()
    {
        var angle = _rnd.NextDouble() * Math.PI * 2;
        var speed = (50 + _rnd.Next(0, 70)) * _aiFactor;
        var edge = _rnd.Next(4);
        var r = 12f + _rnd.Next(0, (int)(Player.R * 2f));
        r = Math.Clamp(r, 10f, Player.R * 2.2f);
        var (x, y) = edge switch
        {
            0 => (r, _height / 2f),
            1 => (_width - r, _height / 2f),
            2 => (_width / 2f, r),
            _ => (_width / 2f, _height - r)
        };
        var f = new Fish
        {
            X = x,
            Y = y,
            R = r,
            Vx = (float)Math.Cos(angle) * speed,
            Vy = (float)Math.Sin(angle) * speed,
            Color = _palette[_rnd.Next(_palette.Count)]
        };
        Others.Add(f);
    }

    private void Clamp(Fish f)
    {
        f.X = Math.Clamp(f.X, f.R, _width - f.R);
        f.Y = Math.Clamp(f.Y, f.R, _height - f.R);
    }

    public void Replay() => Start();
}
