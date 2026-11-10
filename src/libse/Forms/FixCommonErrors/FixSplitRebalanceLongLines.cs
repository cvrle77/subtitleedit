using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Interfaces;

namespace Nikse.SubtitleEdit.Core.Forms.FixCommonErrors
{
    /// <summary>
    /// Re-wraps every line the way the "Split/rebalance long lines" tool's rebalance pass does,
    /// using the settings that tool remembers. Only line breaks change here - the subtitle is
    /// never cut into new events, so this is safe to run near the end of the fix chain.
    /// <para>
    /// The line-length and unbreak settings live in the UI project's settings, so the caller
    /// (FixCommonErrorsViewModel.MakeDefaultRules) resolves them - including the fallbacks to the
    /// general settings - and assigns them to the static fields below.
    /// </para>
    /// </summary>
    public class FixSplitRebalanceLongLines : IFixCommonError
    {
        public static class Language
        {
            public static string RebalanceLongLine { get; set; } = "Rebalance long line";
            public static string RebalanceLongLines { get; set; } = "Rebalance long lines";
        }

        /// <summary>Per-line maximum length the rebalance wraps to.</summary>
        public static int SingleLineMaxLength { get; set; } = 43;

        /// <summary>
        /// AutoBreakLine keeps text on one line only when it is strictly shorter than this, so a
        /// threshold at or above <see cref="SingleLineMaxLength"/> means "keep any text that fits
        /// on one line".
        /// </summary>
        public static int MergeLinesShorterThan { get; set; } = 44;

        public void Fix(Subtitle subtitle, IFixCallbacks callbacks)
        {
            var fixAction = Language.RebalanceLongLine;
            var rebalancedCount = 0;
            foreach (var p in subtitle.Paragraphs)
            {
                if (string.IsNullOrEmpty(p.Text) || !callbacks.AllowFix(p, fixAction))
                {
                    continue;
                }

                var oldText = p.Text;
                var newText = Utilities.AutoBreakLine(oldText, SingleLineMaxLength, MergeLinesShorterThan, callbacks.Language);
                if (newText != oldText)
                {
                    p.Text = newText;
                    rebalancedCount++;
                    callbacks.AddFixToListView(p, fixAction, oldText, newText);
                }
            }

            callbacks.UpdateFixStatus(rebalancedCount, Language.RebalanceLongLines);
        }
    }
}
