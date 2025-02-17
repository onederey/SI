﻿using SIEngine;
using SImulator.ViewModel.ButtonManagers;
using SImulator.ViewModel.Contracts;
using SImulator.ViewModel.Core;
using SImulator.ViewModel.Model;
using SImulator.ViewModel.PlatformSpecific;
using SImulator.ViewModel.Properties;
using SIPackages;
using SIPackages.Core;
using SIUI.ViewModel;
using SIUI.ViewModel.Core;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Utils;
using Utils.Commands;

namespace SImulator.ViewModel;

/// <summary>
/// Controls a single game run.
/// </summary>
public sealed class GameViewModel : INotifyPropertyChanged, IButtonManagerListener, IAsyncDisposable
{
    #region Fields

    internal event Action<string>? Error;
    internal event Action? RequestStop;

    private readonly Stack<Tuple<PlayerInfo, int, bool>> _answeringHistory = new();

    private readonly EngineBase _engine;

    /// <summary>
    /// Game buttons manager.
    /// </summary>
    private IButtonManager? _buttonManager;

    /// <summary>
    /// Game log writer.
    /// </summary>
    private readonly ILogger _logger;

    private readonly Timer _roundTimer;
    private readonly Timer _questionTimer;
    private readonly Timer _thinkingTimer;

    private bool _timerStopped;
    private bool _mediaStopped;

    private QuestionState _state = QuestionState.Normal;
    private QuestionState _previousState;

    internal QuestionState State
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                _state = value;
                OnStateChanged();
            }
        }
    }

    private PlayerInfo? _selectedPlayer = null;

    private readonly List<PlayerInfo> _selectedPlayers = new();
    private readonly Dictionary<string, PlayerInfo> _playersTable = new();

    #endregion

    #region Commands

    private readonly SimpleCommand _stop;
    private readonly SimpleUICommand _next;
    private readonly SimpleCommand _back;

    private readonly SimpleCommand _addRight;
    private readonly SimpleCommand _addWrong;

    private readonly SimpleCommand _nextRound;
    private readonly SimpleCommand _previousRound;

    public ICommand Stop => _stop;
    public ICommand Next => _next;
    public ICommand Back => _back;

    public ICommand RunRoundTimer { get; private set; }
    public ICommand StopRoundTimer { get; private set; }

    public ICommand RunQuestionTimer { get; private set; }
    public ICommand StopQuestionTimer { get; private set; }

    public ICommand RunMediaTimer { get; private set; }
    public ICommand StopMediaTimer { get; private set; }

    public ICommand AddPlayer { get; private set; }
    public ICommand RemovePlayer { get; private set; }
    public ICommand ClearPlayers { get; private set; }

    public ICommand AddRight => _addRight;
    public ICommand AddWrong => _addWrong;

    public ICommand NextRound => _nextRound;

    public ICommand PreviousRound => _previousRound;

    private ICommand? _activeRoundCommand;

    public ICommand? ActiveRoundCommand
    {
        get => _activeRoundCommand;
        set { _activeRoundCommand = value; OnPropertyChanged(); }
    }

    private ICommand? _activeQuestionCommand;

    public ICommand? ActiveQuestionCommand
    {
        get => _activeQuestionCommand;
        set { _activeQuestionCommand = value; OnPropertyChanged(); }
    }

    private ICommand? _activeMediaCommand;

    public ICommand? ActiveMediaCommand
    {
        get => _activeMediaCommand;
        set { if (_activeMediaCommand != value) { _activeMediaCommand = value; OnPropertyChanged(); } }
    }

    #endregion

    #region Properties

    public AppSettingsViewModel Settings { get; }

    /// <summary>
    /// Presentation link.
    /// </summary>
    public IPresentationController PresentationController { get; }

    /// <summary>
    /// Table info view model.
    /// </summary>
    public TableInfoViewModel LocalInfo { get; set; }

    private bool _showingRoundThemes = false;

    private bool ShowingRoundThemes
    {
        set
        {
            if (_showingRoundThemes != value)
            {
                _showingRoundThemes = value;
                UpdateNextCommand();
            }
        }
    }

    private bool _playingQuestionType = false;

    private int _price;

    public int Price
    {
        get => _price;
        set 
        {
            if (_price != value)
            {
                _price = value;
                OnPropertyChanged();
                UpdateCaption();
            }
        }
    }

    private int _rountTime = 0;

    public int RoundTime
    {
        get => _rountTime;
        set { _rountTime = value; OnPropertyChanged(); }
    }

    private int _questionTime = 0;

    /// <summary>
    /// Current question time value.
    /// </summary>
    public int QuestionTime
    {
        get => _questionTime;
        set { _questionTime = value; OnPropertyChanged(); }
    }

    private int _questionTimeMax = int.MaxValue;

    /// <summary>
    /// Maximum question time value.
    /// </summary>
    public int QuestionTimeMax
    {
        get => _questionTimeMax;
        set { _questionTimeMax = value; OnPropertyChanged(); }
    }

    private int _thinkingTime = 0;

    /// <summary>
    /// Current thinking time value.
    /// </summary>
    public int ThinkingTime
    {
        get => _thinkingTime;
        set { _thinkingTime = value; OnPropertyChanged(); }
    }

    private int _thinkingTimeMax = int.MaxValue;

    /// <summary>
    /// Maximum thinking time value.
    /// </summary>
    public int ThinkingTimeMax
    {
        get => _thinkingTimeMax;
        set { _thinkingTimeMax = value; OnPropertyChanged(); }
    }

    private Round? _activeRound;

    public Round? ActiveRound => _activeRound;

    private Question? _activeQuestion;

    public Question? ActiveQuestion
    {
        get => _activeQuestion;
        set { _activeQuestion = value; OnPropertyChanged(); }
    }

    private Theme? _activeTheme;

    public Theme? ActiveTheme
    {
        get => _activeTheme;
        set { _activeTheme = value; OnPropertyChanged(); }
    }

    private Atom? _activeAtom;

    /// <summary>
    /// Current active question atom.
    /// </summary>
    public Atom? ActiveAtom
    {
        get => _activeAtom;
        set { if (_activeAtom != value) { _activeAtom = value; OnPropertyChanged(); } }
    }

    private IEnumerable<ContentItem>? _contentItems = null;

    /// <summary>
    /// Currently played content items.
    /// </summary>
    public IEnumerable<ContentItem>? ContentItems
    {
        get => _contentItems;
        set { _contentItems = value; OnPropertyChanged(); }
    }

    private ContentItem? _activeContentItem;

    /// <summary>
    /// Currently active content item.
    /// </summary>
    public ContentItem? ActiveContentItem
    {
        get => _activeContentItem;
        set { if (_activeContentItem != value) { _activeContentItem = value; OnPropertyChanged(); } }
    }

    private int _mediaProgress;

    private bool _mediaProgressBlock = false;

    public int MediaProgress
    {
        get => _mediaProgress;
        set
        {
            if (_mediaProgress != value)
            {
                _mediaProgress = value;
                OnPropertyChanged();

                if (!_mediaProgressBlock)
                {
                    PresentationController?.SeekMedia(_mediaProgress);

                    if (_presentationListener.IsMediaEnded)
                    {
                        ActiveMediaCommand = StopMediaTimer;
                        _presentationListener.IsMediaEnded = false;
                    }
                }
            }
        }
    }

    private bool _isMediaControlled;

    public bool IsMediaControlled
    {
        get => _isMediaControlled;
        set
        {
            if (_isMediaControlled != value)
            {
                _isMediaControlled = value;
                OnPropertyChanged();
            }
        }
    }

    public int ButtonBlockTime => (int)(Settings.Model.BlockingTime * 1000);

    private readonly IExtendedListener _presentationListener;

    private readonly string _packageFolder;

    #endregion

    public GameViewModel(
        AppSettingsViewModel settings,
        ISIEngine engine,
        IExtendedListener presentationListener,
        IPresentationController presentationController,
        IList<SimplePlayerInfo> players,
        string packageFolder,
        ILogger logger)
    {
        Settings = settings;
        _engine = (EngineBase)engine;
        _presentationListener = presentationListener;
        _packageFolder = packageFolder;
        _logger = logger;
        PresentationController = presentationController;

        LocalInfo = new TableInfoViewModel(players);

        foreach (var playerInfo in LocalInfo.Players.Cast<PlayerInfo>())
        {
            playerInfo.IsRegistered = false;
            playerInfo.PropertyChanged += PlayerInfo_PropertyChanged;
        }

        LocalInfo.QuestionSelected += QuestionInfo_Selected;
        LocalInfo.ThemeSelected += ThemeInfo_Selected;

        _presentationListener.Next = _next = new SimpleUICommand(Next_Executed) { Name = Resources.Next };
        _presentationListener.Back = _back = new SimpleCommand(Back_Executed) { CanBeExecuted = false };
        _presentationListener.Stop = _stop = new SimpleCommand(Stop_Executed);

        AddPlayer = new SimpleCommand(AddPlayer_Executed);
        RemovePlayer = new SimpleCommand(RemovePlayer_Executed);
        ClearPlayers = new SimpleCommand(ClearPlayers_Executed);
        _addRight = new SimpleCommand(AddRight_Executed) { CanBeExecuted = false };
        _addWrong = new SimpleCommand(AddWrong_Executed) { CanBeExecuted = false };

        RunRoundTimer = new SimpleUICommand(RunRoundTimer_Executed) { Name = Resources.Run };
        StopRoundTimer = new SimpleUICommand(StopRoundTimer_Executed) { Name = Resources.Pause };

        RunQuestionTimer = new SimpleUICommand(RunQuestionTimer_Executed) { Name = Resources.Run };
        StopQuestionTimer = new SimpleUICommand(StopQuestionTimer_Executed) { Name = Resources.Pause };

        RunMediaTimer = new SimpleUICommand(RunMediaTimer_Executed) { Name = Resources.Run };
        StopMediaTimer = new SimpleUICommand(StopMediaTimer_Executed) { Name = Resources.Pause };

        _presentationListener.NextRound = _nextRound = new SimpleCommand(NextRound_Executed) { CanBeExecuted = false };
        _presentationListener.PreviousRound = _previousRound = new SimpleCommand(PreviousRound_Executed) { CanBeExecuted = false };

        UpdateNextCommand();

        _roundTimer = new Timer(RoundTimer_Elapsed, null, Timeout.Infinite, Timeout.Infinite);
        _questionTimer = new Timer(QuestionTimer_Elapsed, null, Timeout.Infinite, Timeout.Infinite);
        _thinkingTimer = new Timer(ThinkingTimer_Elapsed, null, Timeout.Infinite, Timeout.Infinite);

        settings.Model.SIUISettings.PropertyChanged += Default_PropertyChanged;
        settings.SIUISettings.PropertyChanged += Default_PropertyChanged;
        settings.Model.PropertyChanged += Settings_PropertyChanged;

        _engine.Package += Engine_Package;
        _engine.GameThemes += Engine_GameThemes;
        _engine.NextRound += Engine_NextRound;
        _engine.Round += Engine_Round;
        _engine.RoundThemes += Engine_RoundThemes;
        _engine.Theme += Engine_Theme;
        _engine.Question += Engine_Question;
        _engine.QuestionSelected += Engine_QuestionSelected;

        _engine.QuestionAtom += Engine_QuestionAtom;
        _engine.QuestionText += Engine_QuestionText;
        _engine.QuestionOral += Engine_QuestionOral;
        _engine.QuestionImage += Engine_QuestionImage;
        _engine.QuestionSound += Engine_QuestionSound;
        _engine.QuestionVideo += Engine_QuestionVideo;
        _engine.QuestionHtml += Engine_QuestionHtml;
        _engine.QuestionOther += Engine_QuestionOther;
        _engine.QuestionProcessed += Engine_QuestionProcessed;
        _engine.WaitTry += Engine_WaitTry;

        _engine.SimpleAnswer += Engine_SimpleAnswer;
        _engine.RightAnswer += Engine_RightAnswer;
        _engine.ShowScore += Engine_ShowScore;
        _engine.LogScore += LogScore;
        _engine.QuestionPostInfo += Engine_QuestionPostInfo;
        _engine.QuestionFinish += Engine_QuestionFinish;
        _engine.EndQuestion += Engine_EndQuestion;
        _engine.RoundTimeout += Engine_RoundTimeout;
        _engine.NextQuestion += Engine_NextQuestion;
        _engine.RoundEmpty += Engine_RoundEmpty;
        _engine.FinalThemes += Engine_FinalThemes;
        _engine.ThemeSelected += Engine_ThemeSelected;
        _engine.PrepareFinalQuestion += Engine_PrepareFinalQuestion;
        _engine.Error += OnError;
        _engine.EndGame += Engine_EndGame;

        _engine.PropertyChanged += Engine_PropertyChanged;

        _presentationListener.MediaStart += GameHost_MediaStart;
        _presentationListener.MediaProgress += GameHost_MediaProgress;
        _presentationListener.MediaEnd += GameHost_MediaEnd;
        _presentationListener.RoundThemesFinished += GameHost_RoundThemesFinished;
    }

    private void Engine_QuestionFinish() => ClearState();

    private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != null && e.PropertyName == nameof(AppSettings.ShowPlayers))
        {
            PresentationController.UpdateShowPlayers(((AppSettings)sender).ShowPlayers);
        }
    }

    private async void Engine_QuestionPostInfo()
    {
        await Task.Yield();

        try
        {
            _engine.MoveNext();
        }
        catch (Exception exc)
        {
            OnError(exc.ToString());
        }
    }

    private void GameHost_RoundThemesFinished()
    {
        ShowingRoundThemes = false;
        PresentationController.SetStage(TableStage.RoundTable);
    }

    private void GameHost_MediaEnd()
    {
        if (ActiveMediaCommand == StopMediaTimer)
        {
            ActiveMediaCommand = RunMediaTimer;
        }
    }

    private void GameHost_MediaProgress(double progress)
    {
        _mediaProgressBlock = true;

        try
        {
            MediaProgress = (int)(progress * 100);
        }
        finally
        {
            _mediaProgressBlock = false;
        }
    }

    private void GameHost_MediaStart() => IsMediaControlled = true;

    #region Event handlers

    private void Default_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        PresentationController.UpdateSettings(Settings.SIUISettings.Model);

    private void PlayerInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerInfo.IsSelected) || e.PropertyName == nameof(PlayerInfo.IsRegistered))
        {
            return;
        }

        if (sender == null)
        {
            return;
        }

        var player = (PlayerInfo)sender;

        if (e.PropertyName == nameof(PlayerInfo.WaitForRegistration))
        {
            if (player.WaitForRegistration)
            {
                foreach (PlayerInfo item in LocalInfo.Players.Cast<PlayerInfo>())
                {
                    if (item != sender)
                    {
                        item.WaitForRegistration = false;
                    }
                }
            }

            return;
        }

        PresentationController?.UpdatePlayerInfo(LocalInfo.Players.IndexOf(player), player);
    }

    private void QuestionTimer_Elapsed(object? state) =>
        UI.Execute(
            () =>
            {
                QuestionTime++;
                PresentationController.SetLeftTime(1.0 - (double)QuestionTime / QuestionTimeMax);

                if (QuestionTime < QuestionTimeMax)
                {
                    return;
                }

                PresentationController.SetSound(Settings.Model.Sounds.NoAnswer);
                StopQuestionTimer_Executed(null);
                ActiveQuestionCommand = null;

                if (!Settings.Model.SignalsAfterTimer && _buttonManager != null)
                {
                    _buttonManager.Stop();
                }
            },
            exc => OnError(exc.ToString()));

    private void RoundTimer_Elapsed(object? state) => UI.Execute(
        () =>
        {
            RoundTime++;

            if (RoundTime >= Settings.Model.RoundTime)
            {
                _engine.SetTimeout();
                StopRoundTimer_Executed(null);
            }
        },
        exc => OnError(exc.ToString()));

    private void ThinkingTimer_Elapsed(object? state) => UI.Execute(
        () =>
        {
            ThinkingTime++;

            if (ThinkingTime < ThinkingTimeMax)
            {
                return;
            }

            StopThinkingTimer_Executed(null);
        },
        exc => OnError(exc.ToString()));

    private void ThemeInfo_Selected(ThemeInfoViewModel theme)
    {
        int themeIndex;

        for (themeIndex = 0; themeIndex < LocalInfo.RoundInfo.Count; themeIndex++)
        {
            if (LocalInfo.RoundInfo[themeIndex] == theme)
            {
                break;
            }
        }

        _presentationListener.OnThemeSelected(themeIndex);
    }

    private void QuestionInfo_Selected(QuestionInfoViewModel question)
    {
        if (!((TvEngine)_engine).CanSelectQuestion || _showingRoundThemes)
        {
            return;
        }

        int questionIndex = -1;
        int themeIndex;

        for (themeIndex = 0; themeIndex < LocalInfo.RoundInfo.Count; themeIndex++)
        {
            bool found = false;

            for (questionIndex = 0; questionIndex < LocalInfo.RoundInfo[themeIndex].Questions.Count; questionIndex++)
            {
                if (LocalInfo.RoundInfo[themeIndex].Questions[questionIndex] == question)
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                break;
            }
        }

        _presentationListener.OnQuestionSelected(themeIndex, questionIndex);
    }

    #endregion

    #region Command handlers

    private void NextRound_Executed(object? arg)
    {
        StopRoundTimer_Executed(0);
        StopQuestionTimer_Executed(0);
        StopThinkingTimer_Executed(0);
        _engine.MoveNextRound();
        _showingRoundThemes = false;
    }

    private void PreviousRound_Executed(object? arg)
    {
        StopRoundTimer_Executed(0);
        StopQuestionTimer_Executed(0);
        StopThinkingTimer_Executed(0);
        ActiveRoundCommand = null;
        PresentationController.SetStage(TableStage.Sign);

        _engine.MoveBackRound();
    }

    private void RunRoundTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            RoundTime = 0;
        }

        _roundTimer.Change(1000, 1000);

        ActiveRoundCommand = StopRoundTimer;
    }

    private void StopRoundTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            RoundTime = 0;
        }

        _roundTimer.Change(Timeout.Infinite, Timeout.Infinite);
        
        ActiveRoundCommand = RunRoundTimer;
    }

    private void RunQuestionTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            QuestionTime = 0;
            PresentationController.SetLeftTime(1.0);
        }

        _questionTimer.Change(1000, 1000);

        ActiveQuestionCommand = StopQuestionTimer;
    }

    private void StopQuestionTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            QuestionTime = 0;
            PresentationController.SetLeftTime(1.0);
        }

        _questionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        ActiveQuestionCommand = RunQuestionTimer;
    }

    private void RunThinkingTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            ThinkingTime = 0;
        }

        _thinkingTimer.Change(1000, 1000);
    }

    public void StopThinkingTimer_Executed(object? arg)
    {
        if (arg != null)
        {
            ThinkingTime = 0;
        }

        _thinkingTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void RunMediaTimer_Executed(object? arg)
    {
        PresentationController.RunMedia();
        ActiveMediaCommand = StopMediaTimer;
    }

    private void StopMediaTimer_Executed(object? arg)
    {
        PresentationController.StopMedia();
        ActiveMediaCommand = RunMediaTimer;
    }

    private void AddPlayer_Executed(object? arg)
    {
        var playerInfo = new PlayerInfo();
        playerInfo.PropertyChanged += PlayerInfo_PropertyChanged;

        LocalInfo.Players.Add(playerInfo);
        PresentationController.AddPlayer();
    }

    private void RemovePlayer_Executed(object? arg)
    {
        if (arg is not SimplePlayerInfo player)
        {
            return;
        }

        player.PropertyChanged -= PlayerInfo_PropertyChanged;
        LocalInfo.Players.Remove(player);
        PresentationController.RemovePlayer(player.Name);
    }

    private void ClearPlayers_Executed(object? arg)
    {
        LocalInfo.Players.Clear();
        PresentationController.ClearPlayers();
    }

    private void AddRight_Executed(object? arg)
    {
        if (arg is not PlayerInfo player)
        {
            if (_selectedPlayer == null)
            {
                return;
            }

            player = _selectedPlayer;
        }

        player.Right++;
        player.Sum += Price;

        PresentationController.SetSound(Settings.Model.Sounds.AnswerRight);

        _logger.Write("{0} +{1}", player.Name, Price);

        _answeringHistory.Push(Tuple.Create(player, Price, true));

        if (Settings.Model.EndQuestionOnRightAnswer)
        {
            _engine.MoveToAnswer();
            Next_Executed();
        }
        else
        {
            ReturnToQuestion();
        }
    }

    private void AddWrong_Executed(object? arg)
    {
        if (arg is not PlayerInfo player)
        {
            if (_selectedPlayer == null)
            {
                return;
            }

            player = _selectedPlayer;
        }

        player.Wrong++;

        var substract = Settings.Model.SubstractOnWrong ? Price : 0;
        player.Sum -= substract;

        PresentationController.SetSound(Settings.Model.Sounds.AnswerWrong);

        _logger.Write("{0} -{1}", player.Name, substract);

        _answeringHistory.Push(Tuple.Create(player, Price, false));

        ReturnToQuestion();
    }

    internal void Start()
    {
        UpdateNextCommand();

        PresentationController.ClearPlayers();

        for (int i = 0; i < LocalInfo.Players.Count; i++)
        {
            PresentationController.AddPlayer();
            PresentationController.UpdatePlayerInfo(i, (PlayerInfo)LocalInfo.Players[i]);
        }

        PresentationController.Start();

        ShowingRoundThemes = false;

        _buttonManager = PlatformManager.Instance.ButtonManagerFactory.Create(Settings.Model, this);

        _logger.Write("Game started {0}", DateTime.Now);
        _logger.Write("Package: {0}", _engine.PackageName);

        _selectedPlayers.Clear();
        PresentationController.ClearLostButtonPlayers();

        if (Settings.Model.AutomaticGame)
        {
            Next_Executed();
        }
    }

    private void Engine_Question(Question question)
    {
        question.Upgrade();
        ActiveQuestion = question;

        PresentationController.SetText(question.Price.ToString());
        PresentationController.SetStage(TableStage.QuestionPrice);

        CurrentTheme = ActiveTheme?.Name;
        Price = question.Price;

        LocalInfo.Text = question.Price.ToString();
        LocalInfo.TStage = TableStage.QuestionPrice;

        _playingQuestionType = true;
    }

    private void SetCaption(string caption) => PresentationController.SetCaption(Settings.Model.ShowTableCaption ? caption : "");

    private void Engine_Theme(Theme theme)
    {
        PresentationController.SetText($"{Resources.Theme}: {theme.Name}");
        PresentationController.SetStage(TableStage.Theme);

        LocalInfo.Text = $"{Resources.Theme}: {theme.Name}";
        LocalInfo.TStage = TableStage.Theme;

        ActiveTheme = theme;
    }

    private void Engine_EndGame() => PresentationController?.SetStage(TableStage.Sign);

    private void Stop_Executed(object? arg = null) => RequestStop?.Invoke();

    private void Engine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EngineBase.CanMoveNext):
                UpdateNextCommand();
                break;

            case nameof(EngineBase.CanMoveBack):
                _back.CanBeExecuted = _engine.CanMoveBack;
                break;

            case nameof(EngineBase.CanMoveNextRound):
                _nextRound.CanBeExecuted = _engine.CanMoveNextRound;
                break;

            case nameof(EngineBase.CanMoveBackRound):
                _previousRound.CanBeExecuted = _engine.CanMoveBackRound;
                break;

            case nameof(EngineBase.Stage):
                _addRight.CanBeExecuted = _addWrong.CanBeExecuted = _engine.CanChangeSum();
                break;
        }
    }

    /// <summary>
    /// Moves the game to the next stage.
    /// </summary>
    private void Next_Executed(object? arg = null)
    {
        try
        {
            if (_showingRoundThemes)
            {
                PresentationController.SetStage(TableStage.RoundTable);
                ShowingRoundThemes = false;
                return;
            }

            if (_playingQuestionType)
            {
                _playingQuestionType = false;

                if (PlayQuestionType())
                {
                    return;
                }
            }

            _engine.MoveNext();
        }
        catch (Exception exc)
        {
            PlatformManager.Instance.ShowMessage($"{Resources.Error}: {exc.Message}");
        }
    }

    private bool PlayQuestionType()
    {
        if (_activeQuestion == null)
        {
            return false;
        }

        var typeName = _activeQuestion.TypeName ?? _activeQuestion.Type.Name;

        if (typeName == QuestionTypes.Simple)
        {
            return false;
        }

        switch (typeName)
        {
            case QuestionTypes.Cat:
            case QuestionTypes.BagCat:
            case QuestionTypes.Secret:
            case QuestionTypes.SecretOpenerPrice:
            case QuestionTypes.SecretNoQuestion:
                PresentationController.SetSound(Settings.Model.Sounds.SecretQuestion);
                PrintQuestionType(Resources.SecretQuestion.ToUpper(), Settings.Model.SpecialsAliases.SecretQuestionAlias);
                break;

            case QuestionTypes.Auction:
            case QuestionTypes.Stake:
                PresentationController.SetSound(Settings.Model.Sounds.StakeQuestion);
                PrintQuestionType(Resources.StakeQuestion.ToUpper(), Settings.Model.SpecialsAliases.StakeQuestionAlias);
                break;

            case QuestionTypes.Sponsored:
            case QuestionTypes.NoRisk:
                PresentationController.SetSound(Settings.Model.Sounds.NoRiskQuestion);
                PrintQuestionType(Resources.NoRiskQuestion.ToUpper(), Settings.Model.SpecialsAliases.NoRiskQuestionAlias);
                break;

            case QuestionTypes.Choice:
                break;

            default:
                PresentationController.SetText(typeName);
                break;
        }

        LocalInfo.TStage = TableStage.Special;
        return true;
    }

    private void Engine_Package(Package package, IMedia packageLogo)
    {
        if (PresentationController == null)
        {
            return;
        }

        var videoUrl = Settings.Model.VideoUrl;

        if (!string.IsNullOrWhiteSpace(videoUrl))
        {
            if (SetMedia(new Media(videoUrl), SIDocument.VideoStorageName))
            {
                PresentationController.SetStage(TableStage.Question);
                PresentationController.SetQuestionContentType(QuestionContentType.Video);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(packageLogo?.Uri))
            {
                if (SetMedia(packageLogo, SIDocument.AudioStorageName))
                {
                    PresentationController.SetStage(TableStage.Question);
                    PresentationController.SetQuestionSound(false);
                    PresentationController.SetQuestionContentType(QuestionContentType.Image);
                }
            }

            PresentationController.SetSound(Settings.Model.Sounds.BeginGame);
        }

        LocalInfo.TStage = TableStage.Sign;
    }

    private void Engine_GameThemes(string[] themes)
    {
        PresentationController.SetGameThemes(themes);
        LocalInfo.TStage = TableStage.GameThemes;

        PresentationController.SetSound(Settings.Model.Sounds.GameThemes);
    }

    private void Engine_Round(Round round)
    {
        _activeRound = round ?? throw new ArgumentNullException(nameof(round));

        if (PresentationController == null)
        {
            return;
        }

        PresentationController.SetText(round.Name);
        PresentationController.SetStage(TableStage.Round);
        PresentationController.SetSound(Settings.Model.Sounds.RoundBegin);
        LocalInfo.TStage = TableStage.Round;

        _logger.Write("\r\n{0} {1}", Resources.Round, round.Name);

        if (round.Type == RoundTypes.Standart)
        {
            if (Settings.Model.RoundTime > 0)
            {
                RunRoundTimer_Executed(0);
            }
        }
    }

    private void Engine_RoundThemes(Theme[] roundThemes)
    {
        LocalInfo.RoundInfo.Clear();

        _logger.Write($"{Resources.RoundThemes}:");

        int maxQuestion = roundThemes.Max(theme => theme.Questions.Count);
        foreach (var theme in roundThemes)
        {
            var themeInfo = new ThemeInfoViewModel { Name = theme.Name };
            LocalInfo.RoundInfo.Add(themeInfo);

            _logger.Write(theme.Name);

            for (int i = 0; i < maxQuestion; i++)
            {
                var questionInfo = new QuestionInfoViewModel { Price = i < theme.Questions.Count ? theme.Questions[i].Price : -1 };
                themeInfo.Questions.Add(questionInfo);
            }
        }

        PresentationController.SetRoundThemes(LocalInfo.RoundInfo.ToArray(), false);
        PresentationController.SetSound(Settings.Model.Sounds.RoundThemes);
        ShowingRoundThemes = true;
        LocalInfo.TStage = TableStage.RoundTable;
    }

    private void Engine_QuestionProcessed(Question question, bool finished, bool pressMode)
    {
        if (question.Type.Name == QuestionTypes.Simple && _buttonManager != null)
        {
            _buttonManager.Start(); // Buttons are activated in advance for false starts to work
        }

        if (finished)
        {
            if (_activeRound.Type == RoundTypes.Standart && question.Type.Name == QuestionTypes.Simple)
            {
                if (!pressMode && Settings.Model.ThinkingTime > 0 && ActiveMediaCommand == null)
                {
                    // Запуск таймера при игре:
                    // 1) без фальстартов - на фальстартах он активируется доп. нажатием (см. WaitTry)
                    // 2) не мультимедиа-элемент - таймер будет запущен только по его завершении
                    QuestionTimeMax = Settings.Model.ThinkingTime + question.Scenario.ToString().Length / 20;
                    RunQuestionTimer_Executed(0);
                }
            }
            else if (_activeRound.Type == RoundTypes.Standart)
            {
                var time = Settings.Model.SpecialQuestionThinkingTime;

                if (time > 0)
                {
                    ThinkingTimeMax = time;
                    RunThinkingTimer_Executed(0);
                }
            }
        }
    }

    private void Engine_WaitTry(Question question, bool final)
    {
        if (final)
        {
            PresentationController.SetSound(Settings.Model.Sounds.FinalThink);

            var time = Settings.Model.FinalQuestionThinkingTime;

            if (time > 0)
            {
                ThinkingTimeMax = time;
                RunThinkingTimer_Executed(0);
            }

            return;
        }

        if (ActiveMediaCommand == StopMediaTimer)
        {
            StopMediaTimer_Executed(null);
            MediaProgress = 100;
        }

        if (question.Type.Name == QuestionTypes.Simple)
        {
            PresentationController.SetQuestionStyle(QuestionStyle.WaitingForPress);

            if (Settings.Model.ThinkingTime > 0)
            {
                // Runs timer in game with false starts
                QuestionTimeMax = Settings.Model.ThinkingTime;
                RunQuestionTimer_Executed(0);
            }
        }
    }

    public void StartQuestionTimer()
    {
        if (Settings.Model.ThinkingTime <= 0)
        {
            return;
        }

        // Runs timer in game with false starts
        QuestionTimeMax = Settings.Model.ThinkingTime;
        RunQuestionTimer_Executed(0);
    }

    public void AskAnswerDirect()
    {
        if (ActiveRound?.Type == RoundTypes.Final)
        {
            PresentationController.SetSound(Settings.Model.Sounds.FinalThink);

            var time = Settings.Model.FinalQuestionThinkingTime;

            if (time > 0)
            {
                ThinkingTimeMax = time;
                RunThinkingTimer_Executed(0);
            }

            return;
        }
        else
        {
            var time = Settings.Model.SpecialQuestionThinkingTime;

            if (time > 0)
            {
                ThinkingTimeMax = time;
                RunThinkingTimer_Executed(0);
            }
        }
    }

    private void Engine_RightAnswer()
    {
        StopQuestionTimer.Execute(0);

        _buttonManager?.Stop();

        State = QuestionState.Normal;
        PresentationController.SetSound();
    }

    public void OnRightAnswer()
    {
        StopQuestionTimer.Execute(0);
        StopThinkingTimer_Executed(0);
        _buttonManager?.Stop();
        ActiveMediaCommand = null;
    }

    private void Engine_RoundEmpty() => StopRoundTimer_Executed(0);

    private void Engine_NextQuestion()
    {
        if (Settings.Model.GameMode == GameModes.Tv)
        {
            PresentationController.SetStage(TableStage.RoundTable);
            LocalInfo.TStage = TableStage.RoundTable;
        }
        else
        {
            Next_Executed();
        }
    }

    private void Engine_RoundTimeout()
    {
        PresentationController?.SetSound(Settings.Model.Sounds.RoundTimeout);
        _logger?.Write(Resources.RoundTimeout);
    }

    private void Engine_EndQuestion(int themeIndex, int questionIndex)
    {
        if (themeIndex > -1 && themeIndex < LocalInfo.RoundInfo.Count)
        {
            var themeInfo = LocalInfo.RoundInfo[themeIndex];

            if (questionIndex > -1 && questionIndex < themeInfo.Questions.Count)
            {
                themeInfo.Questions[questionIndex].Price = -1;
            }
        }
    }

    private void ClearState()
    {
        StopQuestionTimer_Executed(0);
        StopThinkingTimer_Executed(0);

        _buttonManager?.Stop();

        UnselectPlayer();
        _selectedPlayers.Clear();

        foreach (var player in LocalInfo.Players)
        {
            ((PlayerInfo)player).BlockedTime = null;
        }

        ActiveQuestionCommand = null;
        ActiveMediaCommand = null;

        PresentationController.SetText();
        PresentationController.SetActivePlayerIndex(-1);

        CurrentTheme = null;
        Price = 0;

        _playingQuestionType = false;
    }

    private void Engine_FinalThemes(Theme[] finalThemes)
    {
        LocalInfo.RoundInfo.Clear();

        foreach (var theme in finalThemes)
        {
            if (theme.Questions.Count == 0)
            {
                continue;
            }

            var themeInfo = new ThemeInfoViewModel { Name = theme.Name };
            LocalInfo.RoundInfo.Add(themeInfo);
        }

        PresentationController.SetRoundThemes(LocalInfo.RoundInfo.ToArray(), true);
        PresentationController.SetSound("");
        LocalInfo.TStage = TableStage.Final;
    }

    private void Engine_SimpleAnswer(string answer)
    {
        PresentationController.SetText(answer);
        PresentationController.SetStage(TableStage.Answer);
        PresentationController.SetSound();
    }

    /// <summary>
    /// Moves back.
    /// </summary>
    private void Back_Executed(object? arg = null)
    {
        var data = _engine.MoveBack();

        if (Settings.Model.GameMode == GameModes.Tv)
        {
            LocalInfo.RoundInfo[data.Item1].Questions[data.Item2].Price = data.Item3;

            PresentationController.RestoreQuestion(data.Item1, data.Item2, data.Item3);
            PresentationController.SetStage(TableStage.RoundTable);
            LocalInfo.TStage = TableStage.RoundTable;
        }
        else
        {
            PresentationController.SetText(data.Item3.ToString());
            PresentationController.SetStage(TableStage.QuestionPrice);
        }

        StopQuestionTimer_Executed(0);
        StopThinkingTimer_Executed(0);

        _buttonManager?.Stop();
        State = QuestionState.Normal;
        _previousState = QuestionState.Normal;

        UnselectPlayer();
        _selectedPlayers.Clear();

        foreach (var player in LocalInfo.Players)
        {
            ((PlayerInfo)player).BlockedTime = null;
        }

        ActiveQuestionCommand = null;
        ActiveMediaCommand = null;

        _engine.UpdateCanNext();

        if (Settings.Model.DropStatsOnBack)
        {
            while (_answeringHistory.Count > 0)
            {
                var item = _answeringHistory.Pop();
                if (item == null)
                {
                    break;
                }

                if (item.Item3)
                {
                    item.Item1.Right--;
                    item.Item1.Sum -= item.Item2;
                }
                else
                {
                    item.Item1.Wrong--;
                    item.Item1.Sum += item.Item2;
                }
            }
        }
    }

    #endregion

    private void Engine_ThemeSelected(int themeIndex)
    {
        PresentationController.PlaySelection(themeIndex);
        PresentationController.SetSound(Settings.Model.Sounds.FinalDelete);
    }

    private void UpdateNextCommand()
    {
        _next.CanBeExecuted = _engine != null && _engine.CanMoveNext || _showingRoundThemes;
    }

    private void Engine_ShowScore()
    {
        PresentationController.SetStage(TableStage.Score);
        LocalInfo.TStage = TableStage.Score;
    }

    private void ReturnToQuestion()
    {
        State = _previousState;

        if (_timerStopped)
        {
            RunQuestionTimer_Executed(null);
        }

        UnselectPlayer();

        StopThinkingTimer_Executed(0);

        if (_mediaStopped)
        {
            RunMediaTimer_Executed(null);
        }
    }

    /// <summary>
    /// Ends the game.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            StopRoundTimer_Executed(null);
            _roundTimer.Dispose();

            StopQuestionTimer_Executed(0);
            _questionTimer.Dispose();

            StopThinkingTimer_Executed(0);
            _thinkingTimer.Dispose();

            Settings.Model.SIUISettings.PropertyChanged -= Default_PropertyChanged;
            Settings.SIUISettings.PropertyChanged -= Default_PropertyChanged;

            if (_buttonManager != null)
            {
                _buttonManager.Stop();
                await _buttonManager.DisposeAsync();
                _buttonManager = null;
            }

            if (Directory.Exists(_packageFolder))
            {
                try
                {
                    Directory.Delete(_packageFolder, true);
                }
                catch (IOException exc)
                {
                    _logger.Write($"Temp folder delete error: {exc}");
                }
            }

            lock (_engine.SyncRoot)
            {
                _engine.PropertyChanged -= Engine_PropertyChanged;
                _engine.Dispose();

                _logger.Dispose();

                PresentationController.SetSound("");

                PlatformManager.Instance.ClearMedia();
            }
        }
        catch (Exception exc)
        {
            PlatformManager.Instance.ShowMessage(string.Format(Resources.GameEndingError, exc.Message));
        }
    }

    private void Engine_PrepareFinalQuestion(Theme theme, Question question)
    {
        ActiveTheme = theme;
        ActiveQuestion = question;

        question.Upgrade(true);

        PresentationController.SetSound("");
        SetCaption(theme.Name);
    }

    /// <summary>
    /// Writes players scores to log.
    /// </summary>
    private void LogScore()
    {
        if (!Settings.Model.SaveLogs || LocalInfo.Players.Count <= 0)
        {
            return;
        }

        var sb = new StringBuilder("\r\n").Append(Resources.Score).Append(": ");
        var first = true;

        foreach (var player in LocalInfo.Players)
        {
            if (!first)
            {
                sb.Append(", ");
            }

            first = false;
            sb.AppendFormat("{0}:{1}", player.Name, player.Sum);
        }

        _logger?.Write(sb.ToString());
    }

    private void Engine_NextRound(bool showSign)
    {
        ActiveRoundCommand = null;
        PresentationController.SetSound("");

        if (showSign)
        {
            PresentationController.SetStage(TableStage.Sign);
        }
    }

    private void Engine_QuestionOther(Atom atom)
    {
        PresentationController.SetText("");
        PresentationController.SetQuestionSound(false);

        ActiveMediaCommand = null;
    }

    internal void InitMedia()
    {
        ActiveMediaCommand = StopMediaTimer;
        IsMediaControlled = false;
        MediaProgress = 0;
    }

    private bool SetMedia(IMedia media, string category, bool background = false)
    {
        if (media.GetStream == null)
        {
            PresentationController.SetMedia(new MediaSource(media.Uri), background);
            return true;
        }

        var localFile = Path.Combine(_packageFolder, category, media.Uri);

        if (!File.Exists(localFile))
        {
            return false;
        }

        PresentationController.SetMedia(new MediaSource(localFile), background);
        return true;
    }

    private void Engine_QuestionSound(IMedia sound)
    {
        PresentationController.SetQuestionSound(true);

        var result = SetMedia(sound, SIDocument.AudioStorageName, true);

        PresentationController.SetSound();
        PresentationController.SetQuestionContentType(QuestionContentType.Void);

        if (result)
        {
            InitMedia();
        }
    }

    private void Engine_QuestionVideo(IMedia video)
    {
        PresentationController.SetQuestionSound(false);

        var result = SetMedia(video, SIDocument.VideoStorageName);

        if (result)
        {
            PresentationController.SetQuestionContentType(QuestionContentType.Video);
            PresentationController.SetSound();
            InitMedia();
        }
        else
        {
            PresentationController.SetQuestionContentType(QuestionContentType.Void);
        }
    }

    private void Engine_QuestionHtml(IMedia html)
    {
        PresentationController.SetMedia(new MediaSource(html.Uri), false);
        PresentationController.SetQuestionSound(false);
        PresentationController.SetSound();
        PresentationController.SetQuestionContentType(QuestionContentType.Html);
    }

    private void Engine_QuestionImage(IMedia image, IMedia sound)
    {
        PresentationController.SetQuestionSound(sound != null);

        var resultImage = SetMedia(image, SIDocument.ImagesStorageName);

        if (sound != null)
        {
            if (SetMedia(sound, SIDocument.AudioStorageName, true))
            {
                PresentationController.SetSound();
                InitMedia();
            }
        }
        else
        {
            ActiveMediaCommand = null;
        }

        if (resultImage)
        {
            PresentationController.SetQuestionContentType(QuestionContentType.Image);
        }
        else
        {
            PresentationController.SetQuestionContentType(QuestionContentType.Void);
        }
    }

    private void Engine_QuestionText(string text, IMedia sound)
    {
        // Если без фальстартов, то выведем тему и стоимость
        var displayedText = 
            Settings.Model.FalseStart || Settings.Model.ShowTextNoFalstart || _activeRound?.Type == RoundTypes.Final
            ? text
            : $"{CurrentTheme}\n{Price}";

        PresentationController.SetText(displayedText);
        PresentationController.SetQuestionContentType(QuestionContentType.Text);

        var useSound = sound != null && SetMedia(sound, SIDocument.AudioStorageName, true);

        PresentationController.SetQuestionSound(useSound);

        if (useSound)
        {
            PresentationController.SetSound();
            InitMedia();
        }
        else
        {
            ActiveMediaCommand = null;
        }
    }

    private void Engine_QuestionOral(string oralText)
    {
        // Show nothing. The text should be read by the showman
        PresentationController.SetQuestionSound(false);
        ActiveMediaCommand = null;
    }

    private void Engine_QuestionAtom(Atom atom)
    {
        LocalInfo.TStage = TableStage.Question;
        PresentationController.SetStage(TableStage.Question);
        ActiveAtom = atom;
    }

    private string? _currentTheme;

    public string? CurrentTheme
    {
        get => _currentTheme;
        set
        {
            _currentTheme = value;
            UpdateCaption();
        }
    }

    private void UpdateCaption()
    {
        if (_currentTheme == null)
        {
            return;
        }

        var caption = _price > 0 ? $"{_currentTheme}, {_price}" : _currentTheme;
        SetCaption(caption);
    }

    private async void Engine_QuestionSelected(int themeIndex, int questionIndex, Theme theme, Question question)
    {
        _answeringHistory.Push(null);

        ActiveTheme = theme;
        ActiveQuestion = question;

        question.Upgrade();

        CurrentTheme = theme.Name;
        Price = question.Price;

        LogScore();
        _logger?.Write("\r\n{0}, {1}", theme.Name, question.Price);

        var typeName = question.TypeName ?? question.Type.Name;

        if (typeName == QuestionTypes.Simple)
        {
            PresentationController.SetSound(Settings.Model.Sounds.QuestionSelected);
            PresentationController.PlaySimpleSelection(themeIndex, questionIndex);

            try
            {
                await Task.Delay(700);
                _engine.MoveNext();
            }
            catch (Exception exc)
            {
                Trace.TraceError("QuestionSelected error: " + exc.Message);
            }
        }
        else
        {
            var setActive = true;

            switch (typeName)
            {
                case QuestionTypes.Cat:
                case QuestionTypes.BagCat:
                case QuestionTypes.Secret:
                case QuestionTypes.SecretOpenerPrice:
                case QuestionTypes.SecretNoQuestion:
                    PresentationController.SetSound(Settings.Model.Sounds.SecretQuestion);
                    PrintQuestionType(Resources.SecretQuestion.ToUpper(), Settings.Model.SpecialsAliases.SecretQuestionAlias);
                    setActive = false;
                    break;

                case QuestionTypes.Auction:
                case QuestionTypes.Stake:
                    PresentationController.SetSound(Settings.Model.Sounds.StakeQuestion);
                    PrintQuestionType(Resources.StakeQuestion.ToUpper(), Settings.Model.SpecialsAliases.StakeQuestionAlias);
                    break;

                case QuestionTypes.Sponsored:
                case QuestionTypes.NoRisk:
                    PresentationController.SetSound(Settings.Model.Sounds.NoRiskQuestion);
                    PrintQuestionType(Resources.NoRiskQuestion.ToUpper(), Settings.Model.SpecialsAliases.NoRiskQuestionAlias);
                    setActive = false;
                    break;

                case QuestionTypes.Choice:
                    break;

                default:
                    PresentationController.SetText(typeName);
                    break;
            }

            LocalInfo.TStage = TableStage.Special;
            PresentationController.PlayComplexSelection(themeIndex, questionIndex, setActive);
        }

        _logger.Write(question.Scenario.ToString());
    }

    private void PrintQuestionType(string originalTypeName, string? aliasName)
    {
        var actualName = string.IsNullOrWhiteSpace(aliasName) ? originalTypeName : aliasName;

        PresentationController.SetText(actualName);
        _logger.Write(actualName);
    }

    private void OnError(string error) => Error?.Invoke(error);

    public PlayerInfo? GetPlayerById(string playerId, bool strict)
    {
        if (Settings.Model.UsePlayersKeys != PlayerKeysModes.Web)
        {
            return null;
        }

        lock (_playersTable)
        {
            if (_playersTable.TryGetValue(playerId, out var player))
            {
                return player;
            }

            if (!strict)
            {
                foreach (PlayerInfo playerInfo in LocalInfo.Players.Cast<PlayerInfo>())
                {
                    if (playerInfo.WaitForRegistration)
                    {
                        playerInfo.WaitForRegistration = false;
                        playerInfo.IsRegistered = true;

                        _playersTable[playerId] = playerInfo;

                        return playerInfo;
                    }
                }
            }
        }

        return null;
    }

    public bool OnKeyPressed(GameKey key)
    {
        var index = Settings.Model.PlayerKeys2.IndexOf(key);

        if (index == -1 || index >= LocalInfo.Players.Count)
        {
            return false;
        }

        var player = (PlayerInfo)LocalInfo.Players[index];

        return ProcessPlayerPress(index, player);
    }

    public bool OnPlayerPressed(PlayerInfo player)
    {
        var index = LocalInfo.Players.IndexOf(player);

        if (index == -1)
        {
            return false;
        }

        return ProcessPlayerPress(index, player);
    }

    private bool ProcessPlayerPress(int index, PlayerInfo player)
    {
        // The player has pressed already
        if (_selectedPlayers.Contains(player))
        {
            return false;
        }

        // It is no pressing time
        if (_state != QuestionState.Pressing)
        {
            player.BlockedTime = DateTime.Now;

            // Somebody is answering already
            if (_selectedPlayer != null)
            {
                if (Settings.Model.ShowLostButtonPlayers && _selectedPlayer != player && !_selectedPlayers.Contains(player))
                {
                    PresentationController.AddLostButtonPlayer(player.Name);
                }
            }

            return false;
        }

        // Заблокирован
        if (player.BlockedTime.HasValue && DateTime.Now.Subtract(player.BlockedTime.Value).TotalSeconds < Settings.Model.BlockingTime)
        {
            return false;
        }

        // Все проверки пройдены, фиксируем нажатие
        player.IsSelected = true;
        _selectedPlayer = player;
        _selectedPlayers.Add(_selectedPlayer);

        PresentationController.SetSound(Settings.Model.Sounds.PlayerPressed);
        PresentationController.SetActivePlayerIndex(index);

        _previousState = State;
        State = QuestionState.Pressed;

        _timerStopped = ActiveQuestionCommand == StopQuestionTimer;

        if (_timerStopped)
        {
            StopQuestionTimer.Execute(null);
        }

        _mediaStopped = ActiveMediaCommand == StopMediaTimer;

        if (_mediaStopped)
        {
            StopMediaTimer_Executed(null);
        }

        ThinkingTimeMax = Settings.Model.ThinkingTime2;
        RunThinkingTimer_Executed(0);

        return true;
    }

    private void UnselectPlayer()
    {
        if (_selectedPlayer != null)
        {
            _selectedPlayer.IsSelected = false;
            _selectedPlayer = null;
        }

        if (Settings.Model.ShowLostButtonPlayers)
        {
            PresentationController.ClearLostButtonPlayers();
        }        
    }

    private void OnStateChanged()
    {
        switch (_state)
        {
            case QuestionState.Normal:
                PresentationController.SetQuestionStyle(QuestionStyle.Normal);
                StopThinkingTimer_Executed(0);
                break;

            case QuestionState.Pressing:
                PresentationController.SetQuestionStyle(QuestionStyle.WaitingForPress);
                break;

            case QuestionState.Pressed:
                PresentationController.SetQuestionStyle(Settings.Model.ShowPlayers ? QuestionStyle.Normal : QuestionStyle.Pressed);
                break;

            case QuestionState.Thinking:
                break;

            default:
                throw new InvalidOperationException($"_state has an invalid value of {_state}");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void CloseMainView() => PresentationController.StopGame();

    internal void StartButtons() => _buttonManager?.Start();

    internal void AskAnswerButton() => State = QuestionState.Pressing;

    internal void OnQuestionStart()
    {
        State = QuestionState.Normal;
        _previousState = QuestionState.Normal;

        LocalInfo.TStage = TableStage.Question;
    }
}
