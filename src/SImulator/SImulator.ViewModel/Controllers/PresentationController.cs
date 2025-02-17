﻿using SImulator.ViewModel.Contracts;
using SImulator.ViewModel.Core;
using SImulator.ViewModel.Model;
using SImulator.ViewModel.PlatformSpecific;
using SIUI.ViewModel;
using SIUI.ViewModel.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Utils;
using Utils.Commands;

namespace SImulator.ViewModel.Controllers;

/// <inheritdoc cref="IPresentationController" />
public sealed class PresentationController : IPresentationController, INotifyPropertyChanged
{
    private int _previousCode = -1;

    private IPresentationListener? _listener;

    internal IPresentationListener? Listener
    {
        get => _listener;
        set
        {
            _listener = value;
        }
    }

    public TableInfoViewModel TInfo { get; private set; }

    /// <summary>
    /// Game players.
    /// </summary>
    public IList<SimplePlayerInfo> Players { get; private set; } = new ObservableCollection<SimplePlayerInfo>();

    public ICommand Next { get; private set; }

    public ICommand Back { get; private set; }

    public ICommand NextRound { get; private set; }

    public ICommand BackRound { get; private set; }

    public ICommand Stop { get; private set; }

    private bool _stageCallbackBlock = false;

    public int ScreenIndex { get; set; }

    public event Action<Exception>? Error;

    private bool _showPlayers = false;

    /// <summary>
    /// Show players and scores.
    /// </summary>
    [DefaultValue(false)]
    public bool ShowPlayers
    {
        get => _showPlayers;
        set { if (_showPlayers != value) { _showPlayers = value; OnPropertyChanged(); } }
    }

    public PresentationController()
    {
        TInfo = new TableInfoViewModel
        {
            Enabled = true,
            TStage = TableStage.Sign
        };

        TInfo.PropertyChanged += TInfo_PropertyChanged;
        TInfo.QuestionSelected += QuestionInfo_Selected;
        TInfo.ThemeSelected += ThemeInfo_Selected;

        TInfo.MediaStart += () =>
        {
            _listener?.OnMediaStart();
        };

        TInfo.MediaEnd += () =>
        {
            try
            {
                _listener?.OnMediaEnd();
            }
            catch (ObjectDisposedException)
            {

            }
        };

        TInfo.MediaProgress += progress =>
        {
            _listener?.OnMediaProgress(progress);
        };

        Next = new SimpleCommand(arg => _listener?.AskNext());
        Back = new SimpleCommand(arg => _listener?.AskBack());
        NextRound = new SimpleCommand(arg => _listener?.AskNextRound());
        BackRound = new SimpleCommand(arg => _listener?.AskBackRound());

        Stop = new SimpleCommand(arg =>
        {
            if (_listener != null)
            {
                UI.Execute(() => _listener.AskStop(), exc => Error?.Invoke(exc));
            }
        });
    }

    private void TInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TableInfoViewModel.TStage))
        {
            if (TInfo.TStage == TableStage.RoundTable)
            {
                SetSound();

                if (Listener != null && !_stageCallbackBlock)
                {
                    Listener.OnRoundThemesFinished();
                }
            }
        }
    }

    public void SetSound(string sound = "") => UI.Execute(() => PlatformManager.Instance.PlaySound(sound, SoundFinished), exc => Error?.Invoke(exc));

    private void SoundFinished()
    {
        if (TInfo.TStage == TableStage.Sign)
        {
            _listener?.OnIntroFinished();
        }
    }

    public async void Start()
    {
        await PlatformManager.Instance.CreateMainViewAsync(this, ScreenIndex);
        TInfo.TStage = TableStage.Sign;
    }

    private void RemoteGameUI_Closed(object sender, EventArgs e) => UI.Execute(StopGame, exc => Error?.Invoke(exc));

    public async void StopGame()
    {
        await PlatformManager.Instance.CloseMainViewAsync();

        lock (TInfo.TStageLock)
        {
            TInfo.TStage = TableStage.Void;
        }

        SetSound();
        Listener = null;
    }

    public void SetMedia(MediaSource media, bool background)
    {
        if (background)
        {
            TInfo.SoundSource = media;
        }
        else
        {
            TInfo.MediaSource = media;
        }
    }

    public void SetGameThemes(string[] themes)
    {
        TInfo.GameThemes.Clear();
        TInfo.GameThemes.AddRange(themes);

        lock (TInfo.TStageLock)
        {
            TInfo.TStage = TableStage.GameThemes;
        }
    }

    public void SetStage(TableStage stage)
    {
        if (stage == TableStage.RoundTable)
        {
            _stageCallbackBlock = true;
        }

        lock (TInfo.TStageLock)
        {
            TInfo.TStage = stage;
        }

        if (stage == TableStage.RoundTable)
        {
            _stageCallbackBlock = false;
            _previousCode = -1;
            TInfo.QuestionStyle = QuestionStyle.Normal;
        }
    }

    public void SetText(string text) => TInfo.Text = text;

    public void SetQuestionContentType(QuestionContentType questionContentType)
    {
        try
        {
            TInfo.QuestionContentType = questionContentType;
        }
        catch (NotImplementedException exc) when (exc.Message.Contains("The Source property cannot be set to null"))
        {
            // https://github.com/MicrosoftEdge/WebView2Feedback/issues/1136
            return;
        }
    }

    public void SetQuestionStyle(QuestionStyle questionStyle) => TInfo.QuestionStyle = questionStyle;

    public void SetQuestionSound(bool sound)
    {
        TInfo.Sound = sound;

        TInfo.QuestionContentType = TInfo.QuestionContentType == QuestionContentType.Void
            ? QuestionContentType.Clef
            : TInfo.QuestionContentType;
    }

    public void AddPlayer()
    {
        TInfo.Players.Add(new SimplePlayerInfo()); // deprecated
        Players.Add(new SimplePlayerInfo());
    }

    public void RemovePlayer(string playerName)
    {
        var player = TInfo.Players.FirstOrDefault(info => info.Name == playerName);

        if (player != null)
        {
            TInfo.Players.Remove(player);
        }

        var player2 = Players.FirstOrDefault(p => p.Name == playerName);

        if (player2 != null)
        {
            Players.Remove(player2);
        }
    }

    public void ClearPlayers()
    {
        TInfo.Players.Clear();
        Players.Clear();
    }

    public void UpdatePlayerInfo(int index, PlayerInfo player)
    {
        if (index > -1 && index < TInfo.Players.Count)
        {
            var p = TInfo.Players[index];
            p.Sum = player.Sum;
            p.Name = player.Name;
        }

        if (index > -1 && index < Players.Count)
        {
            var p = Players[index];
            p.Sum = player.Sum;
            p.Name = player.Name;
            p.State = player.State;
        }
    }

    public void SetRoundThemes(ThemeInfoViewModel[] themes, bool isFinal)
    {
        TInfo.RoundInfo.Clear();

        foreach (var theme in themes)
        {
            TInfo.RoundInfo.Add(theme);
        }

        lock (TInfo.TStageLock)
        {
            TInfo.TStage = isFinal ? TableStage.Final : TableStage.RoundThemes;
        }
    }

    private void ThemeInfo_Selected(ThemeInfoViewModel theme)
    {
        int themeIndex;

        for (themeIndex = 0; themeIndex < TInfo.RoundInfo.Count; themeIndex++)
        {
            if (TInfo.RoundInfo[themeIndex] == theme)
            {
                break;
            }
        }

        Listener?.OnThemeSelected(themeIndex);
    }

    private void QuestionInfo_Selected(QuestionInfoViewModel question)
    {
        lock (TInfo.TStageLock)
        {
            if (TInfo.TStage != TableStage.RoundTable)
            {
                return;
            }
        }

        int questionIndex = -1;
        int themeIndex;

        for (themeIndex = 0; themeIndex < TInfo.RoundInfo.Count; themeIndex++)
        {
            var found = false;
            var theme = TInfo.RoundInfo[themeIndex];

            for (questionIndex = 0; questionIndex < theme.Questions.Count; questionIndex++)
            {
                if (theme.Questions[questionIndex] == question)
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

        Listener?.OnQuestionSelected(themeIndex, questionIndex);
    }

    public void PlaySimpleSelection(int theme, int quest) => TInfo.PlaySimpleSelectionAsync(theme, quest);

    public void PlayComplexSelection(int theme, int quest, bool setActive) => TInfo.PlayComplexSelectionAsync(theme, quest, setActive);

    public void PlaySelection(int theme) => TInfo.PlaySelection(theme);

    public void UpdateSettings(Settings settings) => TInfo.Settings.Initialize(settings);

    public void UpdateShowPlayers(bool showPlayers) => ShowPlayers = showPlayers;

    public bool OnKeyPressed(GameKey key)
    {
        lock (TInfo.TStageLock)
        {
            switch (TInfo.TStage)
            {
                case TableStage.RoundTable:
                    if (TInfo.Settings.Model.KeyboardControl)
                    {
                        if (_previousCode > -1 && PlatformManager.Instance.IsEscapeKey(key))
                        {
                            _previousCode = -1;
                            return true;
                        }

                        var code = PlatformManager.Instance.GetKeyNumber(key);

                        if (code == -1)
                        {
                            _previousCode = -1;
                            return false;
                        }

                        if (_previousCode == -1)
                        {
                            _previousCode = code;
                            return true;
                        }
                        else
                        {
                            if (_previousCode < TInfo.RoundInfo.Count
                                && code < TInfo.RoundInfo[_previousCode].Questions.Count
                                && TInfo.RoundInfo[_previousCode].Questions[code].Price > -1)
                            {
                                Listener?.OnQuestionSelected(_previousCode, code);
                                _previousCode = -1;
                                return true;
                            }

                            _previousCode = -1;
                        }

                    }

                    break;

                case TableStage.Final:
                    if (TInfo.Settings.Model.KeyboardControl)
                    {
                        var code = PlatformManager.Instance.GetKeyNumber(key);
                        if (code == -1)
                        {
                            return false;
                        }

                        if (code < TInfo.RoundInfo.Count && TInfo.RoundInfo[code].Name != null)
                        {
                            Listener?.OnThemeSelected(code);
                            return true;
                        }
                    }

                    break;
            }
        }
        return false;
    }

    public void SetActivePlayerIndex(int playerIndex)
    {
        if (!ShowPlayers)
        {
            TInfo.PlayerIndex = playerIndex;
        }

        for (var i = 0; i < Players.Count; i++)
        {
            Players[i].State = i == playerIndex ? PlayerState.Active : PlayerState.None;
        }
    }

    public void AddLostButtonPlayer(string name)
    {
        lock (TInfo.LostButtonPlayers)
        {
            if (!TInfo.LostButtonPlayers.Contains(name))
            {
                TInfo.LostButtonPlayers.Add(name);
            }
        }
    }

    public void ClearLostButtonPlayers()
    {
        lock (TInfo.LostButtonPlayers)
        {
            TInfo.LostButtonPlayers.Clear();
        }
    }

    public void SeekMedia(int position) => TInfo.OnMediaSeek(position);

    public void RunMedia() => TInfo.OnMediaResume();

    public void StopMedia() => TInfo.OnMediaPause();

    public void RestoreQuestion(int themeIndex, int questionIndex, int price) => TInfo.RoundInfo[themeIndex].Questions[questionIndex].Price = price;

    public void SetCaption(string caption) => TInfo.Caption = caption;

    public void SetLeftTime(double leftTime) => TInfo.TimeLeft = leftTime;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event PropertyChangedEventHandler? PropertyChanged;
}
