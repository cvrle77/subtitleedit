using Nikse.SubtitleEdit.Features.Video.TextToSpeech.ReviewSpeech;

namespace UITests.Features.Video.TextToSpeech.ReviewSpeech;

/// <summary>
/// Review -> Export names the per-line wavs after the line when the descriptive option is on:
/// "002-idi-u-persun-00.wav" - line number, first words of the spoken text, take index.
/// </summary>
public class DescriptiveExportNameTests
{
    [Fact]
    public void BuildsLineNumberTextAndTake()
    {
        Assert.Equal("002-Idi-u-peršun-00.wav", ReviewSpeechViewModel.DescriptiveExportName(2, "Idi u peršun", 0, ".wav"));
    }

    [Fact]
    public void TakeIndexIsPaddedToTwoDigits()
    {
        Assert.Equal("007-Neka-duga-linija-teksta-03.wav", ReviewSpeechViewModel.DescriptiveExportName(7, "Neka duga linija teksta", 3, ".wav"));
    }

    [Fact]
    public void KeepsOnlyTheFirstFourWords()
    {
        Assert.Equal("001-one-two-three-four-00.wav", ReviewSpeechViewModel.DescriptiveExportName(1, "one two three four five six", 0, ".wav"));
    }

    [Fact]
    public void LineNumberPadsToThreeDigits()
    {
        Assert.Equal("001-x-00.wav", ReviewSpeechViewModel.DescriptiveExportName(1, "x", 0, ".wav"));
        Assert.Equal("012-x-00.wav", ReviewSpeechViewModel.DescriptiveExportName(12, "x", 0, ".wav"));
        Assert.Equal("123-x-00.wav", ReviewSpeechViewModel.DescriptiveExportName(123, "x", 0, ".wav"));
    }

    [Fact]
    public void LineNumberAbove999LengthensNaturally()
    {
        Assert.Equal("1234-x-00.wav", ReviewSpeechViewModel.DescriptiveExportName(1234, "x", 0, ".wav"));
    }

    [Fact]
    public void FallsBackToLineNumberWhenThereIsNoText()
    {
        Assert.Equal("005-00.wav", ReviewSpeechViewModel.DescriptiveExportName(5, "", 0, ".wav"));
        Assert.Equal("005-00.wav", ReviewSpeechViewModel.DescriptiveExportName(5, "   ", 0, ".wav"));
    }

    [Fact]
    public void StripsTagsAndForbiddenCharacters()
    {
        var name = ReviewSpeechViewModel.DescriptiveExportName(3, "<i>Idi</i> u peršun?", 0, ".wav");
        Assert.Equal("003-Idi-u-peršun-00.wav", name);
    }

    [Fact]
    public void KeepsDiacritics()
    {
        var name = ReviewSpeechViewModel.DescriptiveExportName(4, "Čokolada žuta đ", 0, ".wav");
        Assert.Equal("004-Čokolada-žuta-đ-00.wav", name);
    }

    [Fact]
    public void CapsVeryLongWords()
    {
        var longWord = new string('a', 200);
        var name = ReviewSpeechViewModel.DescriptiveExportName(1, longWord, 0, ".wav");
        // 60-char cap on the text part, plus "001-", "-00" and the extension.
        Assert.True(name.Length <= "001-".Length + 60 + "-00.wav".Length, name);
    }
}
