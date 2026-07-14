using Nagare.Domain.Channels;
using Nagare.Infrastructure.Ffmpeg;

namespace Nagare.UnitTests.Infrastructure.Ffmpeg;

/// <summary>
/// ffmpeg repeats the output URL — stream key included — in its stderr messages. Every
/// process line goes through the scrubber before being emitted, buffered, logged or stored
/// as LastError (ARCHITECTURE.md §6.3, ADR-0005).
/// </summary>
public sealed class StreamKeyScrubberTests
{
    private const string Key = "live_2468_KpH2sAbCdEf";
    private const string Mask = ProtectedStreamKey.Mask;

    private static StreamKeyScrubber ScrubberFor(params string[] secrets) => new(secrets);

    [Fact]
    public void Scrub_LineWithTheFullRtmpUrl_MasksOnlyTheKey()
    {
        var scrubber = ScrubberFor(Key);

        var scrubbed = scrubber.Scrub($"[rtmp @ 0000] rtmp://live.twitch.tv/app/{Key}: Connection refused");

        Assert.Equal($"[rtmp @ 0000] rtmp://live.twitch.tv/app/{Mask}: Connection refused", scrubbed);
    }

    [Fact]
    public void Scrub_LineThatIsOnlyTheKey_ReturnsTheMask()
    {
        var scrubber = ScrubberFor(Key);

        Assert.Equal(Mask, scrubber.Scrub(Key));
    }

    [Fact]
    public void Scrub_KeyInTheMiddleOfAMessage_MasksItAndKeepsTheRest()
    {
        var scrubber = ScrubberFor(Key);

        var scrubbed = scrubber.Scrub($"Failed to publish stream key {Key} to the ingest server.");

        Assert.Equal($"Failed to publish stream key {Mask} to the ingest server.", scrubbed);
    }

    [Fact]
    public void Scrub_KeyAppearingSeveralTimes_MasksEveryOccurrence()
    {
        var scrubber = ScrubberFor(Key);

        var scrubbed = scrubber.Scrub($"{Key} rejected; retrying rtmp://ingest/{Key}");

        Assert.Equal($"{Mask} rejected; retrying rtmp://ingest/{Mask}", scrubbed);
        Assert.DoesNotContain(Key, scrubbed, StringComparison.Ordinal);
    }

    [Fact]
    public void Scrub_SecretContainingAnotherSecret_MasksTheLongestFirst()
    {
        // Longest-first ordering: otherwise "live_2468" would be masked inside the longer key
        // and leave the remaining characters ("_KpH2sAbCdEf") in clear.
        var scrubber = ScrubberFor("live_2468", Key);

        var scrubbed = scrubber.Scrub($"error on {Key}");

        Assert.Equal($"error on {Mask}", scrubbed);
    }

    [Fact]
    public void Scrub_LineWithoutAnySecret_IsLeftUntouched()
    {
        var scrubber = ScrubberFor(Key);
        const string line = "frame=  120 fps= 30 q=28.0 size=1024kB time=00:00:04.00 bitrate=3000.0kbits/s speed=1.0x";

        Assert.Equal(line, scrubber.Scrub(line));
    }

    [Fact]
    public void Scrub_EmptySecrets_AreIgnoredAndTheLineSurvives()
    {
        // Guard against a String.Replace("") which would throw / mangle the whole line.
        var scrubber = ScrubberFor("", Key, "");

        Assert.Equal($"key={Mask}", scrubber.Scrub($"key={Key}"));
    }

    [Fact]
    public void Scrub_NoSecretAtAll_ReturnsTheLineUnchanged()
    {
        var scrubber = ScrubberFor();

        Assert.Equal("rtmp://ingest/whatever", scrubber.Scrub("rtmp://ingest/whatever"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Scrub_BlankLine_ReturnsItUnchanged(string line)
        => Assert.Equal(line, ScrubberFor(Key).Scrub(line));
}
