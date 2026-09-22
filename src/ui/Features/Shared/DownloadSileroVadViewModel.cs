using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;

namespace Nikse.SubtitleEdit.Features.Shared;

public partial class DownloadSileroVadViewModel : ObservableObject, IClosingCleanup
{
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private string _error;

    public Window? Window { get; set; }

    public bool OkPressed { get; private set; }

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _downloadTask;
    private readonly Timer _timer;
    private bool _done;

    public DownloadSileroVadViewModel()
    {
        StatusText = Se.Language.General.StartingDotDotDot;
        Error = string.Empty;

        _timer = new Timer(250);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_done)
        {
            return;
        }

        if (_downloadTask is { IsCompletedSuccessfully: true })
        {
            _timer.Stop();
            _done = true;
            OkPressed = true;
            StatusText = Se.Language.General.DownloadComplete;
            Close();
        }
        else if (_downloadTask is { IsFaulted: true })
        {
            _timer.Stop();
            _done = true;
            var exception = _downloadTask.Exception?.InnerException ?? _downloadTask.Exception;
            if (exception is OperationCanceledException)
            {
                StatusText = Se.Language.General.DownloadCanceled;
            }
            else
            {
                StatusText = Se.Language.General.DownloadFailed;
                Error = exception?.Message ?? Se.Language.General.UnknownError;
            }
        }
    }

    public void StartDownload()
    {
        var progress = new Progress<float>(number =>
        {
            var percentage = (int)Math.Round(number * 100.0, MidpointRounding.AwayFromZero);
            Progress = percentage;
            StatusText = string.Format(Se.Language.General.DownloadingXPercent, percentage.ToString(CultureInfo.InvariantCulture));
        });

        _downloadTask = DownloadAsync(progress, _cancellationTokenSource.Token);
    }

    private static async Task DownloadAsync(IProgress<float> progress, CancellationToken cancellationToken)
    {
        var folder = Se.DataFolder;
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var targetPath = SileroVadModel.GetModelPath();
        var tempPath = targetPath + ".tmp";

        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(SileroVad.ModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? -1L;

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, true))
        {
            var buffer = new byte[1024 * 1024];
            long readTotal = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                readTotal += read;
                if (total > 0)
                {
                    progress.Report((float)((double)readTotal / total));
                }
            }
        }

        File.Move(tempPath, targetPath, true);
    }

    [RelayCommand]
    private void CommandCancel()
    {
        _cancellationTokenSource.Cancel();
        _done = true;
        Close();
    }

    public void OnClosingCleanup()
    {
        _timer.StopAndDispose(OnTimerElapsed);
        _cancellationTokenSource.Cancel();
    }

    private void Close()
    {
        Dispatcher.UIThread.Post(() => Window?.Close());
    }

    internal void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CommandCancel();
        }
    }
}
