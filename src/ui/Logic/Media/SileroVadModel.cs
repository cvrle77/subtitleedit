using Nikse.SubtitleEdit.Logic.Config;
using System.IO;

namespace Nikse.SubtitleEdit.Logic.Media;

/// <summary>
/// Where the Silero VAD model lives. It is installed from the app (Settings), not shipped, so this
/// only answers where it is and whether it is there.
/// </summary>
public static class SileroVadModel
{
    public static string GetModelPath()
    {
        return Path.Combine(Se.DataFolder, SileroVad.ModelFileName);
    }

    public static bool IsInstalled()
    {
        return File.Exists(GetModelPath());
    }
}
