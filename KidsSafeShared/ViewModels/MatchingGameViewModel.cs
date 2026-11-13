using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 拼图配对（记忆翻牌）：翻开两张牌，相同则配对成功，全部配对即获胜。
/// </summary>
public class MemoryCard : BaseViewModel
{
    public string Emoji { get; }
    public int PairId { get; }
    private bool _faceUp;
    private bool _matched;

    public bool FaceUp => _faceUp || _matched;
    public bool Matched
    {
        get => _matched;
        set
        {
            if (SetProperty(ref _matched, value))
            {
                OnPropertyChanged(nameof(FaceUp));
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    public string Display => FaceUp ? Emoji : "❓";

    public MemoryCard(string emoji, int pairId)
    {
        Emoji = emoji;
        PairId = pairId;
    }

    public void SetFaceUp(bool value)
    {
        if (_faceUp == value) return;
        _faceUp = value;
        OnPropertyChanged(nameof(FaceUp));
        OnPropertyChanged(nameof(Display));
    }
}

public class MatchingGameViewModel : BaseViewModel
{
    private readonly System.Random _rnd = new();
    private MemoryCard? _first;
    private bool _busy;
    private int _matchedPairs;
    private int _totalPairs;
    private GameDifficulty _difficulty = GameDifficulty.Medium;
    private bool _finished;
    private string _progressText = string.Empty;
    private string _difficultyLabel = "中等";
    private string _resultText = string.Empty;

    public ObservableCollection<MemoryCard> Cards { get; } = new();

    public string DifficultyLabel
    {
        get => _difficultyLabel;
        set => SetProperty(ref _difficultyLabel, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public bool Finished
    {
        get => _finished;
        set
        {
            if (SetProperty(ref _finished, value))
                OnPropertyChanged(nameof(NotFinished));
        }
    }

    public bool NotFinished => !_finished;

    public string ResultText
    {
        get => _resultText;
        set => SetProperty(ref _resultText, value);
    }

    public void Start()
    {
        _difficulty = ServiceProvider.Get<ISettingsService>().Current.Difficulty;
        DifficultyLabel = _difficulty.Label();

        var pairs = _difficulty.PairCount();
        var pool = GamesData.MatchingEmojis.OrderBy(_ => _rnd.Next()).Take(pairs).ToList();
        var board = new List<MemoryCard>();
        foreach (var e in pool)
        {
            var id = e.GetHashCode();
            board.Add(new MemoryCard(e, id));
            board.Add(new MemoryCard(e, id));
        }
        var shuffled = board.OrderBy(_ => _rnd.Next()).ToList();

        Cards.Clear();
        foreach (var c in shuffled)
            Cards.Add(c);

        _first = null;
        _busy = false;
        _matchedPairs = 0;
        _totalPairs = pairs;
        Finished = false;
        UpdateProgress();
    }

    public async Task OnCardTapped(MemoryCard card)
    {
        if (_busy || card.FaceUp || Finished) return;

        var sound = ServiceProvider.Get<ISoundService>();
        sound.PlayClick();
        card.SetFaceUp(true);

        if (_first is null)
        {
            _first = card;
            return;
        }

        if (_first.PairId == card.PairId)
        {
            _first.Matched = true;
            card.Matched = true;
            _first = null;
            _matchedPairs++;
            sound.PlayCorrect();
            UpdateProgress();

            if (_matchedPairs >= _totalPairs)
            {
                Finished = true;
                var isBest = ServiceProvider.Get<ILeaderboardService>().Record(new ScoreEntry
                {
                    Game = GameType.Matching,
                    Difficulty = _difficulty,
                    Score = _totalPairs,
                    MaxScore = _totalPairs,
                    Date = System.DateTime.Now,
                    Player = ServiceProvider.Get<ISettingsService>().Current.Nickname
                });
                ResultText = isBest
                    ? "🎉 新纪录！全部配对成功，你赢了！"
                    : "全部配对成功，你赢了！🎉";
                sound.PlayWin();
            }
        }
        else
        {
            _busy = true;
            sound.PlayWrong();
            var a = _first;
            await Task.Delay(800);
            a.SetFaceUp(false);
            card.SetFaceUp(false);
            _first = null;
            _busy = false;
        }
    }

    public void Replay() => Start();

    private void UpdateProgress() => ProgressText = $"已配对 {_matchedPairs} / {_totalPairs}";
}
