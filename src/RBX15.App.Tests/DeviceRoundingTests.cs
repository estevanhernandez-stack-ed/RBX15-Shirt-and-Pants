namespace RBX15.App.Tests;

/// <summary>The helpers the display-scale tests lean on are themselves pinned, so a fix to one cannot hide a wrong expectation.</summary>
public class DeviceRoundingTests
{
    [Theory] // 125% and 150% put a device-pixel length on the DIP grid; Snap(dip, scale) is whole device pixels back in DIPs
    [InlineData(1.0, 1.25, 0.8)]        // 1 px
    [InlineData(1.0, 1.5, 1.3333333333333333)]   // 1.5 px rounds to 2
    [InlineData(761.0, 1.25, 760.8)]    // 951.25 px -> 951
    [InlineData(761.0, 1.0, 761.0)]
    public void Snap_puts_a_length_on_the_device_grid(double dip, double scale, double expected) =>
        Assert.Equal(expected, DeviceRounding.Snap(dip, scale), 9);

    [Fact] // the bridge injects with Math.Round: 62.5 goes to 62 and 112.5 to 112, and so do the expectations
    public void Snap_rounds_halves_to_even_as_the_bridge_does()
    {
        Assert.Equal(62, DeviceRounding.Snap(62.5));
        Assert.Equal(112, DeviceRounding.Snap(112.5));
        Assert.Equal(-38, DeviceRounding.Snap(-37.5));
    }

    [Theory] // 40.4 + 3.2 - 40.4 is 3.200000000000003, over the 3.2 DIP drag distance of a 125% display; AtMost never goes over
    [InlineData(40.4, 3.2)]
    [InlineData(40.5, 3.2)]
    [InlineData(100.8, 3.2)]
    [InlineData(10.0, 4.0)]
    public void AtMost_stays_on_the_boundary_without_crossing_it(double from, double distance)
    {
        double to = DeviceRounding.AtMost(from, distance);
        Assert.True(to - from <= distance, $"{to} - {from} = {to - from} is past {distance}");
        Assert.True(Math.BitIncrement(to) - from > distance, "and the next double is past it, so the probe is on the boundary");
    }

    [Fact] // the case AtMost exists for: the plain sum crosses the boundary
    public void The_plain_sum_crosses_the_boundary_at_a_fractional_distance() =>
        Assert.True(40.4 + 3.2 - 40.4 > 3.2);
}
