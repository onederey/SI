﻿using SIEngine;
using SImulator.ViewModel.Core;
using SIUI.ViewModel.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace SImulator.ViewModel.Model
{
    /// <summary>
    /// Defines application settings.
    /// </summary>
    public sealed class AppSettings : INotifyPropertyChanged
    {
        #region Settings

        private const int RoundTimeDefaultValue = 600;

        private int _roundTime = RoundTimeDefaultValue;

        /// <summary>
        /// Maximum round time.
        /// </summary>
        [DefaultValue(RoundTimeDefaultValue)]
        public int RoundTime
        {
            get => _roundTime;
            set
            {
                if (_roundTime != value && value > 0)
                {
                    _roundTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private const int ThinkingTimeDefaultValue = 5;

        private int _thinkingTime = ThinkingTimeDefaultValue;

        /// <summary>
        /// Time for thinking on question.
        /// </summary>
        [DefaultValue(ThinkingTimeDefaultValue)]
        public int ThinkingTime
        {
            get => _thinkingTime;
            set
            {
                if (_thinkingTime != value && value > 0)
                {
                    _thinkingTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _screenNumber = 0;

        [DefaultValue(0)]
        public int ScreenNumber
        {
            get => _screenNumber;
            set
            {
                if (_screenNumber != value)
                {
                    _screenNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        private Settings _siUISettings = new Settings();

        public Settings SIUISettings
        {
            get => _siUISettings;
            set
            {
                if (_siUISettings != value && value != null)
                {
                    _siUISettings = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _dropStatsOnBack = true;

        /// <summary>
        /// Откатывать статистику при возврате
        /// </summary>
        [DefaultValue(true)]
        public bool DropStatsOnBack
        {
            get => _dropStatsOnBack;
            set
            {
                if (_dropStatsOnBack != value)
                {
                    _dropStatsOnBack = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showRight = false;

        /// <summary>
        /// Show right answers on the screen.
        /// </summary>
        [DefaultValue(false)]
        public bool ShowRight
        {
            get => _showRight;
            set
            {
                if (_showRight != value)
                {
                    _showRight = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _substractOnWrong = true;

        /// <summary>
        /// Subtract points for wrong answer.
        /// </summary>
        [DefaultValue(true)]
        public bool SubstractOnWrong
        {
            get => _substractOnWrong;
            set
            {
                if (_substractOnWrong != value)
                {
                    _substractOnWrong = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _playSpecials = true;

        /// <summary>
        /// Play special questions in classic game mode.
        /// </summary>
        [DefaultValue(true)]
        public bool PlaySpecials
        {
            get => _playSpecials;
            set
            {
                if (_playSpecials != value)
                {
                    _playSpecials = value;
                    OnPropertyChanged();
                }
            }
        }

        private GameModes _gameMode = GameModes.Tv;

        /// <summary>
        /// Default game mode.
        /// </summary>
        [DefaultValue(GameModes.Tv)]
        public GameModes GameMode
        {
            get => _gameMode;
            set
            {
                if (_gameMode != value)
                {
                    _gameMode = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _videoUrl = "";

        /// <summary>
        /// Адрес заставочного видеофайла
        /// </summary>
        [DefaultValue("")]
        public string VideoUrl
        {
            get { return _videoUrl; }
            set
            {
                if (_videoUrl != value)
                {
                    _videoUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _restriction = "12+";

        /// <summary>
        /// Default package age restriction.
        /// </summary>
        [DefaultValue("12+")]
        public string Restriction
        {
            get => _restriction;
            set
            {
                if (_restriction != value)
                {
                    _restriction = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<string> _recent = new();

        /// <summary>
        /// Played package files history.
        /// </summary>
        public ObservableCollection<string> Recent
        {
            get => _recent;
            set
            {
                if (_recent != value)
                {
                    _recent = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _falseStart = true;

        /// <summary>
        /// Игра с фальстартами
        /// </summary>
        [DefaultValue(true)]
        public bool FalseStart
        {
            get => _falseStart;
            set
            {
                if (_falseStart != value)
                {
                    _falseStart = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _falseStartMultimedia = true;

        /// <summary>
        /// Мультимедиа с фальстартами
        /// </summary>
        [DefaultValue(true)]
        public bool FalseStartMultimedia
        {
            get => _falseStartMultimedia;
            set
            {
                if (_falseStartMultimedia != value)
                {
                    _falseStartMultimedia = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showTextNoFalstart = false;

        /// <summary>
        /// Показывать текст вопросов
        /// </summary>
        [DefaultValue(false)]
        public bool ShowTextNoFalstart
        {
            get => _showTextNoFalstart;
            set
            {
                if (_showTextNoFalstart != value)
                {
                    _showTextNoFalstart = value;
                    OnPropertyChanged();
                }
            }
        }

        private PlayerKeysModes _usePlayersKeys = PlayerKeysModes.None;

        [DefaultValue(PlayerKeysModes.None)]
        public PlayerKeysModes UsePlayersKeys
        {
            get => _usePlayersKeys;
            set
            {
                if (_usePlayersKeys != value)
                {
                    _usePlayersKeys = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _signalsAfterTimer = false;

        [DefaultValue(false)]
        public bool SignalsAfterTimer
        {
            get => _signalsAfterTimer;
            set
            {
                if (_signalsAfterTimer != value)
                {
                    _signalsAfterTimer = value;
                    OnPropertyChanged();
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private double _blockingTime = 3.0;

        [DefaultValue(3.0)]
        public double BlockingTime
        {
            get => _blockingTime;
            set
            {
                if (_blockingTime != value)
                {
                    _blockingTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _comPort = "";

        /// <summary>
        /// Используемый COM-порт
        /// </summary>
        [DefaultValue("")]
        public string ComPort
        {
            get => _comPort;
            set
            {
                if (_comPort != value)
                {
                    _comPort = value;
                    OnPropertyChanged();
                }
            }
        }

        private ErrorInfoList _delayedErrors = new();

        public ErrorInfoList DelayedErrors
        {
            get => _delayedErrors;
            set
            {
                if (_delayedErrors != value)
                {
                    _delayedErrors = value;
                    OnPropertyChanged();
                }
            }
        }

        private KeyCollection2 _playerKeys2 = new();

        [XmlIgnore]
        public KeyCollection2 PlayerKeys2
        {
            get => _playerKeys2;
            set
            {
                if (_playerKeys2 != value)
                {
                    _playerKeys2 = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<int> _playerKeysPublic = new();
        
        public List<int> PlayerKeysPublic
        {
            get => _playerKeysPublic;
            set { _playerKeysPublic = value; OnPropertyChanged(); }
        }

        private PlayersViewMode _playersView = PlayersViewMode.Hidden;

        [DefaultValue(PlayersViewMode.Hidden)]
        public PlayersViewMode PlayersView
        {
            get { return _playersView; }
            set
            {
                if (_playersView != value)
                {
                    _playersView = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _saveLogs = false;

        [DefaultValue(false)]
        public bool SaveLogs
        {
            get { return _saveLogs; }
            set
            {
                if (_saveLogs != value)
                { 
                    _saveLogs = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _logsFolder;

        public string LogsFolder
        {
            get { return _logsFolder; }
            set
            {
                if (_logsFolder != value)
                {
                    _logsFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _endQuestionOnRightAnswer = true;

        [DefaultValue(true)]
        public bool EndQuestionOnRightAnswer
        {
            get { return _endQuestionOnRightAnswer; }
            set
            {
                if (_endQuestionOnRightAnswer != value)
                {
                    _endQuestionOnRightAnswer = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _automaticGame = false;

        [DefaultValue(false)]
        public bool AutomaticGame
        {
            get { return _automaticGame; }
            set
            {
                if (_automaticGame != value)
                {
                    _automaticGame = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _webPort = 80;

        /// <summary>
        /// Имя порта для веб-доступа
        /// </summary>
        [DefaultValue(80)]
        public int WebPort
        {
            get { return _webPort; }
            set
            {
                if (_webPort != value)
                {
                    _webPort = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _showLostButtonPlayers = false;

        /// <summary>
        /// Показывать игроков, проигравших кнопку
        /// </summary>
        [DefaultValue(false)]
        public bool ShowLostButtonPlayers
        {
            get { return _showLostButtonPlayers; }
            set
            {
                if (_showLostButtonPlayers != value)
                {
                    _showLostButtonPlayers = value;
                    OnPropertyChanged();
                }
            }
        }

        public SoundsSettings Sounds { get; set; } = new SoundsSettings();

        public SpecialsAliases SpecialsAliases { get; set; } = new SpecialsAliases();

        #endregion

        public void Save(Stream stream, XmlSerializer serializer = null)
        {
            _playerKeysPublic = new List<int>(_playerKeys2.Cast<int>());
            if (serializer == null)
                serializer = new XmlSerializer(typeof(AppSettings));

            serializer.Serialize(stream, this);
        }

        /// <summary>
        /// Загрузить пользовательские настройки
        /// </summary>
        public static AppSettings Load(Stream stream, XmlSerializer serializer = null)
        {
            if (serializer == null)
                serializer = new XmlSerializer(typeof(AppSettings));

            var settings = (AppSettings)serializer.Deserialize(stream);
            settings._playerKeys2 = new KeyCollection2(settings._playerKeysPublic.Cast<GameKey>());

            return settings;
        }

        public static AppSettings Create()
        {
            var newSettings = new AppSettings();
            newSettings.Initialize();
            return newSettings;
        }

        internal void Initialize()
        {
            
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
