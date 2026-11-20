using Nikse.SubtitleEdit.Logic.Media;

namespace UITests.Logic;

public class SmartBreakTests
{
    private static SmartBreakWord W(string w, double start, double end) => new(w, start, end);

    // Reported on a real transcript, line 24: the cue ran through the interjection
    // ("... od 9. | E, da proverimo." on two lines of one cue) instead of ending the cue at
    // "9." and starting the next one with "E, da proverimo.". The interjection opens the
    // sentence, so it must never be the last word of a cue or of a line.
    [Fact]
    public void InterjectionThatOpensASentence_StartsItsOwnCue()
    {
        var words = new List<SmartBreakWord>
        {
            W("Na", 45.71, 45.95), W("indukciji", 45.95, 46.40), W("staklo-keramici", 46.40, 47.00),
            W("kuvate", 47.00, 47.40), W("na", 47.40, 47.55), W("6", 47.55, 47.70),
            W("ili", 47.90, 48.05), W("7", 48.05, 48.20), W("od", 48.20, 48.35), W("9.", 48.35, 48.60),
            W("E,", 48.90, 49.10),
            W("da", 51.00, 51.10), W("proverimo.", 51.10, 52.10),
        };

        var cues = SmartBreak.Build(words, Array.Empty<(double, double)>());

        Assert.Equal(2, cues.Count);
        Assert.Equal("Na indukciji staklo-keramici kuvate na 6 ili 7 od 9.", string.Join(" ", cues[0].Lines));
        Assert.Equal("E, da proverimo.", string.Join(" ", cues[1].Lines));
    }

    // A block that has no punctuation and no soft clause, and would be cut only to fit the length,
    // must be cut at the next conjunction (the first part still fits one line) and that boundary
    // gets the comma. Generic words on purpose: the rule knows word classes (conjunctions), not any
    // particular transcript.
    [Fact]
    public void LengthOnlyBlock_SplitsAtTheConjunctionAndGetsAComma()
    {
        var words = new List<SmartBreakWord>
        {
            W("Prvo", 0.0, 0.3), W("uzmemo", 0.3, 0.7), W("ovu", 0.7, 0.9), W("dublju", 0.9, 1.2),
            W("plasticnu", 1.2, 1.7), W("posudu", 1.7, 2.1),
            W("i", 2.4, 2.5), W("sve", 2.5, 2.7), W("to", 2.7, 2.8), W("dobro", 2.8, 3.1),
            W("promesamo", 3.1, 3.7), W("dok", 3.7, 3.8), W("se", 3.8, 3.9), W("ne", 3.9, 4.0),
            W("bi", 4.0, 4.1), W("mnogo", 4.1, 4.4), W("zalepilo", 4.4, 4.9), W("za", 4.9, 5.0),
            W("ruku.", 5.0, 5.4),
        };

        var cues = SmartBreak.Build(words, Array.Empty<(double, double)>());

        Assert.Equal(2, cues.Count);
        Assert.Equal("Prvo uzmemo ovu dublju plasticnu posudu,", string.Join(" ", cues[0].Lines).Trim());
        Assert.Equal("i sve to dobro promesamo, dok se ne bi mnogo zalepilo za ruku.",
            string.Join(" ", cues[1].Lines).Trim());
    }

    // Same rule inside one cue: a line must not end on the interjection either.
    [Fact]
    public void LineDoesNotEndOnAnInterjection()
    {
        var words = new List<SmartBreakWord>
        {
            W("Kada", 0.0, 0.3), W("se", 0.3, 0.4), W("sve", 0.4, 0.6), W("otopilo", 0.6, 1.0),
            W("ostavite", 1.0, 1.4), W("da", 1.4, 1.5), W("vri", 1.5, 1.7), W("i", 1.7, 1.8),
            W("sada", 1.8, 2.0), W("sipamo", 2.0, 2.4), W("mleko", 2.4, 2.7), W("u", 2.7, 2.8),
            W("prahu", 2.8, 3.1), W("pa", 3.1, 3.2), W("mešamo", 3.2, 3.6), W("polako", 3.6, 4.0),
        };

        var cues = SmartBreak.Build(words, Array.Empty<(double, double)>());

        foreach (var line in cues.SelectMany(c => c.Lines))
        {
            Assert.False(line.TrimEnd().EndsWith("E,"), "line ends on E,: " + line);
        }
    }
}
