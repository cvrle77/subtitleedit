namespace Nikse.SubtitleEdit.UiLogic.AudioToText
{
    public class ResultText
    {
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Start seconds
        /// </summary>
        public decimal Start { get; set; }

        /// <summary>
        /// End seconds
        /// </summary>
        public decimal End { get; set; }

        public decimal Confidence { get; set; }

        /// <summary>
        /// True when this line's cue boundary was decided by a non-punctuation rule (an "i ..."
        /// clause start, a pause, or the longest-pause/dense-block fallback) - i.e. a judgment call
        /// worth reviewing. Used only to tint the row in the grid.
        /// </summary>
        public bool Risky { get; set; }
    }
}
