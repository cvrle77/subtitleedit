using Nikse.SubtitleEdit.Logic.Media;

namespace UITests.Logic;

public class SmartBreakTests
{
    private static SmartBreakWord W(string w, double start, double end) => new(w, start, end);

    // Reported on a real transcript: a cue ended with the interjection "E," ("... od 9. E,") and
    // the rest of the sentence ("da proverimo.") went to the next cue. The interjection opens the
    // sentence, so it has to travel with the words that follow it.
    [Fact]
    public void CueEndingOnAnInterjection_MovesItToTheNextCue()
    {
        var words = new List<SmartBreakWord>
        {
            W("Na", 0.0, 0.2), W("indukciji", 0.2, 0.6), W("kuvate", 0.6, 1.0),
            W("na", 1.0, 1.1), W("7", 1.1, 1.2), W("od", 1.2, 1.3), W("9.", 1.3, 1.5),
            W("E,", 1.5, 1.6),
            W("da", 4.2, 4.3), W("proverimo.", 4.3, 4.8),
        };

        var cues = SmartBreak.Build(words, Array.Empty<(double, double)>());

        Assert.DoesNotContain(cues, c => c.Lines.Count > 0 && c.Lines[^1].TrimEnd().EndsWith("E,"));
        Assert.Contains(cues, c =>
        {
            var text = string.Join(" ", c.Lines);
            return text.Contains("E,") && text.Contains("proverimo.");
        });
    }

    // Same rule inside one cue: a line must not end on the interjection either.
    [Fact]
    public void LineDoesNotEndOnAnInterjection()
    {
        var words = new List<SmartBreakWord>
        {
            W("Kada", 0.0, 0.3), W("se", 0.3, 0.4), W("sve", 0.4, 0.6), W("otopilo", 0.6, 1.0),
            W("ostavite", 1.0, 1.4), W("da", 1.4, 1.5), W("vri", 1.5, 1.7), W("E,", 1.7, 1.8),
            W("sada", 1.8, 2.0), W("sipamo", 2.0, 2.4), W("mleko", 2.4, 2.7), W("u", 2.7, 2.8),
            W("prahu", 2.8, 3.1), W("i", 3.1, 3.2), W("mešamo", 3.2, 3.6), W("polako", 3.6, 4.0),
        };

        var cues = SmartBreak.Build(words, Array.Empty<(double, double)>());

        foreach (var cue in cues)
        {
            foreach (var line in cue.Lines)
            {
                Assert.False(line.TrimEnd().EndsWith("E,"), "line ends on E,: " + line);
            }
        }
    }
}
