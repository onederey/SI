﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIPackages;
using SIQuester.Model;
using SIQuester.ViewModel.Contracts;
using SIQuester.ViewModel.Helpers;
using SIQuester.ViewModel.PlatformSpecific;
using SIQuester.ViewModel.Properties;
using SIQuester.ViewModel.Serializers;
using SIQuester.ViewModel.Services;
using SIStorageService.Client;
using SIStorageService.ViewModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Utils;
using Utils.Commands;

namespace SIQuester.ViewModel;

/// <summary>
/// Represents a main application model.
/// </summary>
public sealed class MainViewModel : ModelViewBase, INotifyPropertyChanged
{
    private const string DonateUrl = "https://yoomoney.ru/embed/shop.xml?account=410012283941753&quickpay=shop&payment-type-choice=on&writer=seller&targets=%D0%9F%D0%BE%D0%B4%D0%B4%D0%B5%D1%80%D0%B6%D0%BA%D0%B0+%D0%B0%D0%B2%D1%82%D0%BE%D1%80%D0%B0&targets-hint=&default-sum=100&button-text=03&comment=on&hint=%D0%92%D0%B0%D1%88+%D0%BA%D0%BE%D0%BC%D0%BC%D0%B5%D0%BD%D1%82%D0%B0%D1%80%D0%B8%D0%B9";
    private const int MaxMessageLength = 1000;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;

    #region Commands

    /// <summary>
    /// Opens a file.
    /// </summary>
    public ICommand Open { get; private set; }

    /// <summary>
    /// Opens one of the recently opened files.
    /// </summary>
    public ICommand OpenRecent { get; private set; }

    /// <summary>
    /// Removes one of the recentlty opened files.
    /// </summary>
    public ICommand RemoveRecent { get; private set; }

    /// <summary>
    /// Imports text file.
    /// </summary>
    public ICommand ImportTxt { get; private set; }

    /// <summary>
    /// Imports XML file.
    /// </summary>
    public ICommand ImportXml { get; private set; }

    /// <summary>
    /// Imports YAML file.
    /// </summary>
    public ICommand ImportYaml { get; private set; }

    /// <summary>
    /// Imports quizz DB file.
    /// </summary>
    public ICommand ImportBase { get; private set; }

    /// <summary>
    /// Imports SIStorage file.
    /// </summary>
    public ICommand ImportFromSIStore { get; private set; }

    /// <summary>
    /// Saves all changed workspaces.
    /// </summary>
    public SimpleCommand SaveAll { get; private set; }

    public ICommand About { get; private set; }

    public ICommand Feedback { get; private set; }

    public ICommand Donate { get; private set; }

    public ICommand SetSettings { get; private set; }

    public ICommand SearchFolder { get; private set; }

    #endregion

    /// <summary>
    /// Opened workspaces.
    /// </summary>
    public ObservableCollection<WorkspaceViewModel> DocList { get; } = new();

    private QDocument? _activeDocument = null;

    /// <summary>
    /// Currently opened document.
    /// </summary>
    public QDocument? ActiveDocument
    {
        get => _activeDocument;
        set
        {
            if (_activeDocument != value)
            {
                _activeDocument = value;
                OnPropertyChanged();
            }
        }
    }

    public AppSettings Settings => AppSettings.Default;

    private readonly string[] _args = null;

    private readonly StorageContextViewModel _storageContextViewModel;
    
    public MainViewModel(string[] args, ISIStorageServiceClient siStorageServiceClient, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        DocList.CollectionChanged += DocList_CollectionChanged;
        CollectionViewSource.GetDefaultView(DocList).CurrentChanged += MainViewModel_CurrentChanged;

        Open = new SimpleCommand(Open_Executed);
        OpenRecent = new SimpleCommand(OpenRecent_Executed);
        RemoveRecent = new SimpleCommand(RemoveRecent_Executed);

        ImportTxt = new SimpleCommand(ImportTxt_Executed);
        ImportXml = new SimpleCommand(ImportXml_Executed);
        ImportYaml = new SimpleCommand(ImportYaml_Executed);
        ImportBase = new SimpleCommand(ImportBase_Executed);
        ImportFromSIStore = new SimpleCommand(ImportFromSIStore_Executed);

        SaveAll = new SimpleCommand(SaveAll_Executed) { CanBeExecuted = false };

        About = new SimpleCommand(About_Executed);
        Feedback = new SimpleCommand(Feedback_Executed);
        Donate = new SimpleCommand(Donate_Executed);

        SetSettings = new SimpleCommand(SetSettings_Executed);
        SearchFolder = new SimpleCommand(SearchFolder_Executed);

        _storageContextViewModel = new StorageContextViewModel(siStorageServiceClient);
        _storageContextViewModel.Load();

        AddCommandBinding(ApplicationCommands.New, New_Executed);
        AddCommandBinding(ApplicationCommands.Open, (sender, e) => Open_Executed(e.Parameter));
        AddCommandBinding(ApplicationCommands.Help, Help_Executed);
        AddCommandBinding(ApplicationCommands.Close, Close_Executed);

        AddCommandBinding(ApplicationCommands.SaveAs, (s, e) => ActiveDocument?.SaveAs_Executed(), CanExecuteDocumentCommand);

        AddCommandBinding(ApplicationCommands.Copy, (s, e) => ActiveDocument?.Copy_Executed(), CanExecuteDocumentCommand);
        AddCommandBinding(ApplicationCommands.Paste, (s, e) => ActiveDocument?.Paste_Executed(), CanExecuteDocumentCommand);

        _args = args;

        UI.Initialize();
    }

    private void CanExecuteDocumentCommand(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = ActiveDocument != null;

    public async Task InitializeAsync()
    {
        if (_args.Length > 0)
        {
            await OpenFileAsync(_args[0]);
        }

        if (!AppSettings.Default.AutoSave)
        {
            return;
        }

        var autoSaveFolder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), AppSettings.ProductName, AppSettings.AutoSaveSimpleFolderName));

        if (!autoSaveFolder.Exists)
        {
            return;
        }

        var folders = autoSaveFolder.EnumerateDirectories();

        if (!folders.Any())
        {
            return;
        }

        try
        {
            _logger.LogInformation("Unsaved files found");

            if (PlatformManager.Instance.Confirm(Resources.RestoreConfirmation))
            {
                foreach (var folder in folders)
                {
                    var originFileName = PathHelper.DecodePath(folder.Name);
                    _logger.LogInformation("Restoring file {file}...", originFileName);

                    var document = await OpenFileAsync(originFileName);
                    document?.RestoreFromFolder(folder);
                }
            }
            else
            {
                foreach (var folder in folders)
                {
                    folder.Delete(true);
                }
            }
        }
        catch (Exception exc)
        {
            ShowError(exc);
        }
    }

    private void SearchFolder_Executed(object? arg) => DocList.Add(new SearchFolderViewModel(this));

    private void SetSettings_Executed(object? arg) => DocList.Add(new SettingsViewModel());

    private void Help_Executed(object? sender, ExecutedRoutedEventArgs e) => DocList.Add(new HowToViewModel());

    private async void Close_Executed(object? sender, ExecutedRoutedEventArgs e)
    {
        if (await DisposeRequestAsync())
        {
            PlatformManager.Instance.Exit();
        }
    }

    public async Task<bool> DisposeRequestAsync()
    {
        foreach (var doc in DocList.ToArray())
        {
            await doc.Close.ExecuteAsync(null);

            if (DocList.Contains(doc)) // Closing has been cancelled
            {
                return false;
            }
        }

        return true;
    }

    private void Feedback_Executed(object? arg) => OpenUri(Resources.AuthorSiteUrl);

    private void Donate_Executed(object? arg) => OpenUri(DonateUrl);

    private static void OpenUri(string uri)
    {
        try
        {
            Browser.Open(uri);
        }
        catch (Exception exc)
        {
            ShowError(exc);
        }
    }

    private void About_Executed(object? arg) => DocList.Add(new AboutViewModel());

    private void MainViewModel_CurrentChanged(object? sender, EventArgs e) =>
        ActiveDocument = CollectionViewSource.GetDefaultView(DocList).CurrentItem as QDocument;

    private void DocList_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (WorkspaceViewModel item in e.NewItems)
                {
                    item.Error += ShowError;
                    item.NewItem += Item_NewDoc;
                    item.Closed += Item_Closed;
                }

                CollectionViewSource.GetDefaultView(DocList).MoveCurrentToLast();
                CheckSaveAllCanBeExecuted(this, EventArgs.Empty);
                break;

            case NotifyCollectionChangedAction.Move:
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (WorkspaceViewModel item in e.OldItems)
                {
                    item.Error -= ShowError;
                    item.NewItem -= Item_NewDoc;
                    item.Closed -= Item_Closed;
                }

                CheckSaveAllCanBeExecuted(this, EventArgs.Empty);
                break;

            default:
                break;
        }
    }

    private void Item_NewDoc(WorkspaceViewModel doc)
    {
        DocList.Add(doc);
    }

    private void Item_Closed(WorkspaceViewModel doc)
    {
        doc.Dispose();
        DocList.Remove(doc);
    }

    private void CheckSaveAllCanBeExecuted(object sender, EventArgs e)
    {
        SaveAll.CanBeExecuted = DocList.Count > 0;
    }

    /// <summary>
    /// Новый
    /// </summary>
    private void New_Executed(object? sender, ExecutedRoutedEventArgs e)
    {
        DocList.Add(new NewViewModel(_storageContextViewModel, _loggerFactory));
    }

    /// <summary>
    /// Открыть существующий пакет
    /// </summary>
    private async void Open_Executed(object? arg)
    {
        if (arg is string filename)
        {
            await OpenFileAsync(filename);
            return;
        }

        var files = PlatformManager.Instance.ShowOpenUI();

        if (files != null)
        {
            foreach (var file in files)
            {
                await OpenFileAsync(file);
            }
        }
    }

    /// <summary>
    /// Открыть недавний файл
    /// </summary>
    /// <param name="arg">Путь к файлу</param>
    private async void OpenRecent_Executed(object? arg) => await OpenFileAsync(arg.ToString());

    /// <summary>
    /// Removes recent file.
    /// </summary>
    /// <param name="arg">Recent file path.</param>
    private void RemoveRecent_Executed(object? arg) => AppSettings.Default.History.Remove(arg.ToString());

    /// <summary>
    /// Opens the existing file.
    /// </summary>
    /// <param name="path">File path.</param>
    internal async Task<QDocument?> OpenFileAsync(string path)
    {
        Task<QDocument> loader(CancellationToken cancellationToken) => Task.Run(() =>
        {
            FileStream? stream = null;

            try
            {
                stream = File.OpenRead(path);

                // Loads in read only mode to keep file LastUpdate time unmodified
                var doc = SIDocument.Load(stream);

                _logger.LogInformation("Document has been successfully opened. Path: {path}", path);

                var docViewModel = new QDocument(doc, _storageContextViewModel, _loggerFactory)
                {
                    Path = path,
                    FileName = Path.GetFileNameWithoutExtension(path)
                };

                docViewModel.CheckFileSize();

                return docViewModel;
            }
            catch (Exception exc)
            {
                stream?.Dispose();

                if (exc is UnauthorizedAccessException && (new FileInfo(path).Attributes & FileAttributes.ReadOnly) > 0)
                {
                    throw new Exception(Resources.FileIsReadOnly, exc);
                }

                throw;
            }
        });

        var loaderViewModel = new DocumentLoaderViewModel(path);
        DocList.Add(loaderViewModel);

        try
        {
            var document = await loaderViewModel.LoadAsync(loader);
            AppSettings.Default.History.Add(path);
            return document;
        }
        catch (Exception exc)
        {
            if (exc is FileNotFoundException)
            {
                AppSettings.Default.History.Remove(path);
            }

            _logger.LogError(exc, "File open error: {error}", exc.Message);
            ShowError(exc);
            return null;
        }
    }

    /// <summary>
    /// Imports text from file.
    /// </summary>
    private void ImportTxt_Executed(object? arg)
    {
        try
        {
            ITextSource? textSource = arg switch
            {
                string filePath => new FileTextSource(filePath),
                Stream stream => new StreamTextSource(stream),
                null => null,
                _ => throw new InvalidOperationException($"Incorrect text source: {arg}"),
            };

            var model = new ImportTextViewModel(_storageContextViewModel, _loggerFactory);
            DocList.Add(model);

            if (textSource != null)
            {
                model.Import(textSource);
            }
        }
        catch (Exception exc)
        {
            ShowError(exc);
        }
    }

    private void ImportXml_Executed(object? arg)
    {
        var file = PlatformManager.Instance.ShowImportUI("xml", Resources.XmlFilesFilter);

        if (file == null)
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(file);
            var doc = SIDocument.LoadXml(stream);

            var docViewModel = new QDocument(doc, _storageContextViewModel, _loggerFactory)
            {
                Path = "",
                Changed = true,
                FileName = Path.GetFileNameWithoutExtension(file)
            };

            var mediaFolder = Path.GetDirectoryName(file);

            if (mediaFolder != null)
            {
                docViewModel.LoadMediaFromFolder(mediaFolder);
            }

            DocList.Add(docViewModel);
        }
        catch (Exception exc)
        {
            ShowError(exc);
        }
    }

    private void ImportYaml_Executed(object? arg)
    {
        var file = PlatformManager.Instance.ShowImportUI("yaml", Resources.YamlFilesFilter);

        if (file == null)
        {
            return;
        }

        try
        {
            Package package;

            using (var reader = new StreamReader(file))
            {
                package = YamlSerializer.DeserializePackage(reader);
            }

            var doc = SIDocument.Create(package);

            var docViewModel = new QDocument(doc, _storageContextViewModel, _loggerFactory)
            {
                Path = "",
                Changed = true,
                FileName = Path.GetFileNameWithoutExtension(file)
            };

            var mediaFolder = Path.GetDirectoryName(file);

            if (mediaFolder != null)
            {
                docViewModel.LoadMediaFromFolder(mediaFolder);
            }

            DocList.Add(docViewModel);
        }
        catch (Exception exc)
        {
            ShowError(exc);
        }
    }

    /// <summary>
    /// Imports package from Packages Database.
    /// </summary>
    private void ImportBase_Executed(object? arg) => DocList.Add(new ImportDBStorageViewModel(_storageContextViewModel, _loggerFactory));

    /// <summary>
    /// Imports package from SI Storage.
    /// </summary>
    private async void ImportFromSIStore_Executed(object? arg)
    {
        var importViewModel = new ImportSIStorageViewModel(
            _storageContextViewModel,
            PlatformManager.Instance.ServiceProvider.GetRequiredService<SIStorage>(),
            _loggerFactory);

        DocList.Add(importViewModel);

        await importViewModel.OpenAsync();
    }

    public async void AutoSave(CancellationToken cancellationToken = default)
    {
        foreach (var item in DocList.ToArray())
        {
            try
            {
                await item.SaveToTempAsync(cancellationToken);
            }
            catch (Exception exc)
            {
                ShowError(exc);
            }
        }
    }

    private async void SaveAll_Executed(object? arg)
    {
        foreach (var item in DocList.ToArray())
        {
            try
            {
                await item.SaveIfNeededAsync();
            }
            catch (Exception exc)
            {
                ShowError(exc);
            }
        }
    }        

    public static void ShowError(Exception exc, string? message = null)
    {
        var fullMessage = message != null ? $"{message}: {exc.Message}" : exc.Message;

        if (fullMessage.Length > MaxMessageLength)
        {
            fullMessage = string.Concat(fullMessage.AsSpan(0, MaxMessageLength), "…");
        }

        PlatformManager.Instance.ShowExclamationMessage(fullMessage);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var item in DocList)
        {
            item.Dispose();
        }

        base.Dispose(disposing);
    }
}
