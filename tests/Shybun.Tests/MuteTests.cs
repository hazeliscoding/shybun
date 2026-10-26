using Shybun.Core;

namespace Shybun.Tests;

public class MuteTests
{
    private static Mute Start(params (string Id, bool? Muted)[] mics) =>
        new(mics.Select(m => KeyValuePair.Create(m.Id, m.Muted)));

    [Fact]
    public void Starts_muted_only_when_Windows_already_has_every_mic_muted()
    {
        Assert.True(Start(("a", true), ("b", true)).On);
        Assert.False(Start(("a", true), ("b", false)).On);
        Assert.False(Start().On);
    }

    [Fact]
    public void The_key_mutes_every_mic_when_any_is_live()
    {
        var mute = Start(("a", true), ("b", false));

        Assert.True(mute.Press());
        Assert.True(mute.On);
    }

    [Fact]
    public void The_key_unmutes_when_every_mic_reads_muted()
    {
        var mute = Start(("a", true), ("b", true));

        Assert.False(mute.Press());
        Assert.False(mute.On);
    }

    [Fact]
    public void The_badge_shows_what_was_read_not_what_was_asked()
    {
        var mute = Start(("a", false));
        mute.Press();

        Assert.Equal(BadgeState.Live, mute.Badge);
        mute.Read("a", true);
        Assert.Equal(BadgeState.Muted, mute.Badge);
    }

    [Fact]
    public void A_mic_that_appears_while_muted_is_muted_on_arrival()
    {
        var mute = Start(("a", true));

        Assert.True(mute.Added("headset", false));
        Assert.Equal(BadgeState.Live, mute.Badge);
        mute.Read("headset", true);
        Assert.Equal(BadgeState.Muted, mute.Badge);
    }

    [Fact]
    public void A_mic_that_appears_already_muted_needs_nothing()
    {
        var mute = Start(("a", true));

        Assert.False(mute.Added("headset", true));
    }

    [Fact]
    public void A_mic_that_appears_while_live_is_left_alone()
    {
        var mute = Start(("a", false));

        Assert.False(mute.Added("headset", false));
        Assert.Equal(BadgeState.Hidden, mute.Badge);
    }

    [Fact]
    public void A_mic_unmuted_behind_Shybuns_back_shows_live_and_the_key_mutes_it_again()
    {
        var mute = Start(("a", true), ("b", true));

        mute.Read("b", false);

        Assert.True(mute.On);
        Assert.Equal(BadgeState.Live, mute.Badge);
        Assert.True(mute.Press());
    }

    [Fact]
    public void A_mic_that_cant_be_read_is_unconfirmed_never_muted()
    {
        var mute = Start(("a", true), ("b", true));

        mute.Read("b", null);

        Assert.Equal(BadgeState.Unconfirmed, mute.Badge);
        Assert.True(mute.Press());
    }

    [Fact]
    public void An_unconfirmed_mic_shows_even_while_live()
    {
        Assert.Equal(BadgeState.Unconfirmed, Start(("a", false), ("b", null)).Badge);
    }

    [Fact]
    public void With_no_mics_the_badge_follows_the_key()
    {
        var mute = Start();
        Assert.Equal(BadgeState.Hidden, mute.Badge);

        Assert.True(mute.Press());
        Assert.Equal(BadgeState.Muted, mute.Badge);
    }

    [Fact]
    public void A_removed_mic_no_longer_counts()
    {
        var mute = Start(("a", true), ("b", false));
        mute.Press();

        mute.Removed("b");

        Assert.Equal(BadgeState.Muted, mute.Badge);
    }

    [Fact]
    public void A_reading_for_an_unknown_mic_is_ignored()
    {
        var mute = Start(("a", true));

        mute.Read("gone", false);

        Assert.Equal(BadgeState.Muted, mute.Badge);
        Assert.Single(mute.Mics);
    }

    [Fact]
    public void Muted_covers_the_Shybun_camera_only_once_it_reads_off()
    {
        var mute = Start(("a", true));
        mute.ReadCamera(false);

        Assert.Equal(BadgeState.Live, mute.Badge);
        Assert.Equal(BadgeDevices.Camera, mute.BadgeDevices);
        mute.ReadCamera(true);
        Assert.Equal(BadgeState.Muted, mute.Badge);
        Assert.Equal(BadgeDevices.Mic | BadgeDevices.Camera, mute.BadgeDevices);
    }

    [Fact]
    public void A_Shybun_camera_that_cant_be_read_is_unconfirmed_never_muted()
    {
        var mute = Start(("a", true));
        mute.ReadCamera(null);

        Assert.Equal(BadgeState.Unconfirmed, mute.Badge);
        Assert.Equal(BadgeDevices.Camera, mute.BadgeDevices);
    }

    [Fact]
    public void Without_a_Shybun_camera_the_badge_is_about_the_mics_alone()
    {
        var mute = Start(("a", true));
        mute.ReadCamera(false);
        mute.RemoveCamera();

        Assert.Equal(BadgeState.Muted, mute.Badge);
        Assert.Equal(BadgeDevices.Mic, mute.BadgeDevices);
    }

    [Fact]
    public void The_badge_never_says_muted_while_the_mics_are_live_and_only_the_camera_is_off()
    {
        var mute = Start(("a", false));
        mute.ReadCamera(true);

        Assert.Equal(BadgeState.Hidden, mute.Badge);
    }

    [Fact]
    public void The_key_mutes_when_only_the_camera_is_live_and_unmutes_when_both_read_muted()
    {
        var mute = Start(("a", true));
        mute.ReadCamera(false);

        Assert.True(mute.Press());
        mute.ReadCamera(true);
        Assert.False(mute.Press());
    }

    [Fact]
    public void A_log_row_holds_the_time_the_cause_and_the_counts_only()
    {
        var mute = Start(("a", true), ("b", false), ("c", null));
        var time = new DateTimeOffset(2026, 10, 25, 9, 30, 0, 125, TimeSpan.FromHours(-5));

        Assert.Equal("time,cause,mute,mics,muted,live,unconfirmed", MuteLog.Header);
        Assert.Equal("2026-10-25T09:30:00.125-05:00,key,off,3,1,1,1", MuteLog.Row(time, MuteCause.Key, mute));
    }
}
