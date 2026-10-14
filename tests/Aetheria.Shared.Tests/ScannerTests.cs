using System;
using System.Collections.Generic;
using System.Linq;
using CultMath;
using Xunit;
using static CultMath.math;
using float2 = CultMath.float2;
using float3 = CultMath.float3;

// Inspection (rulings quiet-gear-identifiable-hull-layout-known, scan-held-beam, scan-is-an-act, reveal-ends-on-perception,
// sensor-stat-set): a scanner held on its ship's tracked target within Range for ScanTime reveals everything fitted to it,
// as claimed, and is itself an emission the target is warned of and a third party can hear. The fixtures are the
// catalog's: "Scanner" has Range 200 and ScanTime 2, "QuietScanner" the same with intercept factor 4 (heard at 50). A tick
// is 1/8 s, so a scan builds 1/16 a tick and finishes on the sixteenth with no float drift. The scanner's ship keeps
// its track on the target at .5 each tick (above the detection gate, below the one at which gear is resolved, so
// Sense neither loses the track nor perceives a destruction). Every fixture drives the production path, ticking inside
// Zone.Update.
public sealed partial class ContactRevealTests
{
    private const float ScanStep = .125f;

    private static ScannerData ScannerBehavior(float range, float scanTime, float? interceptFactor = null) => new ScannerData
    {
        Range = Const(range), ScanTime = Const(scanTime),
        InterceptFactor = interceptFactor.HasValue ? Const(interceptFactor.Value) : null
    };

    private static Scanner ScannerOf(Entity e) => e.Equipment.SelectMany(i => i.Behaviors).OfType<Scanner>().Single();

    private static EquippedItem ScannerItemOf(Entity e) => e.Equipment.Single(i => i.Behaviors.Any(b => b is Scanner));

    // A scanner ship at the origin, with a passive eye and the named scanner, tracking and targeting a ship `distance`
    // ahead that carries a reactor, a radiator and a suppressor, none of which ever acts.
    private (Ship scanner, Ship target) ScanOn(string scanner = "Scanner", float distance = 100f)
    {
        var target = Add(float3(0, 0, distance), tools: new[] { "Reactor", "Radiator", "SuppressorCold" });
        var s = Add(float3(0, 0, 0), new[] { "Eye" }, new[] { scanner });
        s.EntityInfoGathered[target] = .5f;
        s.SetTarget(target);
        Assert.Same(target, s.Target.Value.Entity); // fixture
        return (s, target);
    }

    // Ticks `ticks` steps, holding the observer's track on each of `tracked` at `info` before each.
    private void Hold(Entity observer, int ticks, float info, params Entity[] tracked)
    {
        for (var i = 0; i < ticks; i++)
        {
            foreach (var t in tracked) observer.EntityInfoGathered[t] = info;
            Tick(ScanStep);
        }
    }

    private void Hold(Entity observer, int ticks, params Entity[] tracked) => Hold(observer, ticks, .5f, tracked);

    private static EquippedItem[] FittedBesidesTheHull(Entity e) => e.Equipment.Where(i => i != e.EquippedHull).ToArray();

    // ---- AScanRevealsQuietGear ----

    [Fact]
    public void AScanRevealsQuietGear()
    {
        var (s, target) = ScanOn();
        var quiet = FittedBesidesTheHull(target);
        Assert.Equal(3, quiet.Length);
        Assert.Empty(s.RevealsOf(target)); // fixture: nothing acts

        Hold(s, 15, target);

        Assert.Empty(s.RevealsOf(target));
        Assert.Equal(15f / 16f, ScannerOf(s).Progress, 4);

        Hold(s, 1, target);

        foreach (var item in quiet)
        {
            var reveal = Assert.Single(s.RevealsOf(target), r => r.Item == item);
            Assert.Null(reveal.Channel);
            AssertBearing(0f, 1f, reveal.Bearing); // normalize((target - scanner).xz)
            Assert.Equal(_zone.Time, reveal.Time);
        }
        Assert.Equal(0f, ScannerOf(s).Progress); // a finished scan starts over
    }

    // ---- AScanSkipsTheHull ----

    [Fact]
    public void AScanSkipsTheHull()
    {
        var (s, target) = ScanOn();
        Assert.Contains(target.EquippedHull, target.Equipment); // fixture: the hull is an item of Equipment

        Hold(s, 16, target);

        Assert.Equal(3, s.RevealsOf(target).Count()); // fixture: the scan finished
        Assert.DoesNotContain(s.RevealsOf(target), r => r.Item == target.EquippedHull);
    }

    // ---- AScanSkipsDestroyedGear ----

    [Fact]
    public void AScanSkipsDestroyedGear()
    {
        var (s, target) = ScanOn();
        var items = FittedBesidesTheHull(target);
        items[0].EquippableItem.Durability = 0f;
        Assert.True(items[0].EquippableItem.IsDestroyed);

        Hold(s, 16, target);

        Assert.DoesNotContain(s.RevealsOf(target), r => r.Item == items[0]);
        Assert.Equal(2, s.RevealsOf(target).Count());
    }

    // ---- AScanRecordsTheClaim ----

    [Fact]
    public void AScanRecordsTheClaim()
    {
        var (s, target) = ScanOn();
        var reactor = target.Equipment.Single(i => i.Behaviors.Any(b => b is Reactor));
        var radiator = DataOf("Radiator");
        target.Claim(reactor, radiator);

        Hold(s, 16, target);

        var reveal = Assert.Single(s.RevealsOf(target), r => r.Item == reactor);
        Assert.Same(radiator, reveal.Claimed);
        Assert.NotSame(reactor.Data, reveal.Claimed);
    }

    // ---- AScanNeedsATrackAndRange ----

    [Theory]
    [InlineData(199f, true, true)]
    [InlineData(201f, true, false)]
    [InlineData(100f, false, false)]
    public void AScanNeedsATrackAndRange(float distance, bool tracked, bool builds)
    {
        var (s, target) = ScanOn(distance: distance);
        var third = Add(float3(40, 0, 0), new[] { "Eye" }); // within 200 of the scanner: it hears an emission
        var info = tracked ? .5f : 0f;
        s.EntityInfoGathered[target] = info;
        Assert.Equal(tracked, s.VisibleEntities.Contains(target)); // fixture

        Hold(s, 4, info, target);

        Assert.Equal(builds, ScannerOf(s).Progress > 0f);
        Assert.Equal(builds, target.IncomingPaints.Count > 0);
        Assert.Equal(builds, target.RevealsOf(s).Any());
        Assert.Equal(builds, third.RevealsOf(s).Any());
    }

    // ---- ABrokenScanDecays ----

    // A scan at .5 broken for half of ScanTime (one second) falls to 0, a quarter in half of that: by range (Execute
    // runs and does not hold) and by the item switched off (Execute does not run at all).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ABrokenScanDecays(bool byRange)
    {
        var (s, target) = ScanOn();
        Hold(s, 8, target);
        Assert.Equal(.5f, ScannerOf(s).Progress, 4);
        if (byRange) target.Position = float3(0, 0, 400);
        else ScannerItemOf(s).Enabled.Value = false;

        Hold(s, 4, target);
        Assert.Equal(.25f, ScannerOf(s).Progress, 4);

        Hold(s, 4, target);
        Assert.Equal(0f, ScannerOf(s).Progress, 4);
        Hold(s, 2, target);
        Assert.Equal(0f, ScannerOf(s).Progress, 4); // never below 0
    }

    // ---- ATargetChangeResetsTheScan ----

    [Fact]
    public void ATargetChangeResetsTheScan()
    {
        var (s, first) = ScanOn();
        var second = Add(float3(100, 0, 0));
        Hold(s, 11, first, second);
        Assert.Equal(11f / 16f, ScannerOf(s).Progress, 4);

        s.SetTarget(second);
        Hold(s, 1, first, second);

        Assert.Equal(1f / 16f, ScannerOf(s).Progress, 4); // reset, then one tick on the new target
        Assert.Empty(s.RevealsOf(first));
    }

    // ---- TheScannedShipIsWarned ----

    // A paint made in a tick is published the next one, so after four ticks the target holds the third tick's paint.
    [Fact]
    public void TheScannedShipIsWarned()
    {
        var (s, target) = ScanOn();

        Hold(s, 4, target);

        var paint = Assert.Single(target.IncomingPaints);
        Assert.Equal(PaintMode.Scan, paint.Mode);
        Assert.Equal(3f / 16f, paint.Strength, 4);
        Assert.Same(DataOf("Scanner"), paint.EmitterClass);
        AssertBearing(0f, -1f, paint.Bearing); // normalize((scanner - target).xz)
        var warned = Assert.Single(target.RevealsOf(s));
        Assert.Same(ScannerItemOf(s), warned.Item);
        Assert.Equal(SignatureChannel.Emission, warned.Channel);
    }

    // ---- AThirdPartyHearsTheScanner ----

    [Theory]
    [InlineData("QuietScanner", 49f, true)]
    [InlineData("QuietScanner", 51f, false)]
    [InlineData("Scanner", 199f, true)]
    [InlineData("Scanner", 201f, false)]
    public void AThirdPartyHearsTheScanner(string scanner, float distance, bool heard)
    {
        var (s, target) = ScanOn(scanner);
        var third = Add(float3(-distance, 0, 0), new[] { "Eye" });

        Hold(s, 3, target);

        Assert.Empty(third.IncomingPaints); // fixture: only the target is painted
        if (!heard)
        {
            Assert.Empty(third.RevealsOf(s));
            return;
        }
        var reveal = Assert.Single(third.RevealsOf(s));
        Assert.Same(ScannerItemOf(s), reveal.Item);
        Assert.Equal(SignatureChannel.Emission, reveal.Channel);
        AssertBearing(1f, 0f, reveal.Bearing); // normalize((scanner - third).xz)
    }

    // ---- AFinishedScanMakesTheReactorSelectable ----

    [Fact]
    public void AFinishedScanMakesTheReactorSelectable()
    {
        var (s, target) = ScanOn();
        var reactor = target.Equipment.Single(i => i.Behaviors.Any(b => b is Reactor));
        Hold(s, 15, target);
        Assert.False(FireControl.IsRevealed(s, reactor));
        Assert.False(s.TrySelectTargetItem(reactor));

        Hold(s, 1, target);

        Assert.True(FireControl.IsRevealed(s, reactor));
        Assert.True(s.TrySelectTargetItem(reactor));
    }

    // ---- AScanEndsNothing ----

    // A gun revealed by firing and then destroyed, while the observer's track stays between the detection and gear
    // thresholds (so Sense perceives no destruction): a scan neither ends its reveal nor refreshes it.
    [Fact]
    public void AScanEndsNothing()
    {
        var target = Add(float3(0, 0, 100), new[] { "Gun" });
        var s = Add(float3(0, 0, 0), new[] { "Eye" }, new[] { "Scanner" });
        var gun = Guns(target)[0];
        var firedAt = Fire(target, gun);
        var before = Assert.Single(s.RevealsOf(target), r => r.Item == gun);
        Assert.Equal(firedAt, before.Time); // fixture: the gun is in the book
        gun.EquippableItem.Durability = 0f;
        s.EntityInfoGathered[target] = .5f;
        s.SetTarget(target);

        Hold(s, 16, target);

        var after = Assert.Single(s.RevealsOf(target), r => r.Item == gun);
        Assert.Equal(firedAt, after.Time);
        Assert.Equal(SignatureChannel.LaunchFlash, after.Channel);
        Assert.Contains(s.RevealsOf(target), r => r.Item != gun && r.Channel == null); // fixture: the scan finished
    }
}
