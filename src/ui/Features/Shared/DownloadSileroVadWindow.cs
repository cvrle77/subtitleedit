using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;

namespace Nikse.SubtitleEdit.Features.Shared;

public class DownloadSileroVadWindow : Window
{
    public DownloadSileroVadWindow(DownloadSileroVadViewModel vm)
    {
        vm.Window = this;
        UiUtil.InitializeWindow(this, GetType().Name);
        Title = string.Format(Se.Language.General.DownloadingX, "Silero VAD");
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        DataContext = vm;

        var titleText = new TextBlock
        {
            Text = string.Format(Se.Language.General.DownloadingX, "Silero VAD"),
            FontSize = UiUtil.ScaledFontSize(20),
            FontWeight = FontWeight.Bold,
        };

        var progressBar = UiUtil.MakeProgressBar();
        progressBar.MinWidth = 400;
        progressBar.Bind(ProgressBar.ValueProperty, new Binding(nameof(DownloadSileroVadViewModel.Progress)));

        var statusText = new TextBlock();
        statusText.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadSileroVadViewModel.StatusText)));

        var buttonCancel = UiUtil.MakeButtonCancel(vm.CommandCancelCommand);
        var buttonBar = UiUtil.MakeButtonBar(buttonCancel);

        Content = new StackPanel
        {
            Spacing = 8,
            Margin = UiUtil.MakeWindowMargin(),
            Children =
            {
                titleText,
                progressBar,
                statusText,
                buttonBar,
            }
        };

        Loaded += delegate
        {
            buttonCancel.Focus();
            vm.StartDownload();
        };
        KeyDown += (s, e) => vm.OnKeyDown(e);
    }
}
