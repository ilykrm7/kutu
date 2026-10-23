using Kutu.Core.Geometry;

namespace Kutu.Core.Tests;

public class SelectionShadeTests
{
    [Fact]
    public void Cover_dims_the_whole_window_before_a_selection()
    {
        var bands = SelectionShade.Cover(1920, 1080);
        Assert.Equal(new SelectionShade.Band(0, 0, 1920, 1080), bands[0]);
        Assert.Equal(0, bands[1].Width);
        Assert.Equal(0, bands[2].Width);
        Assert.Equal(0, bands[3].Height);
    }

    [Fact]
    public void Around_leaves_the_selection_undimmed_on_every_side()
    {
        var bands = SelectionShade.Around(2000, 1000, 100, 80, 400, 300);
        Assert.Equal(new SelectionShade.Band(0, 0, 2000, 80), bands[0]);
        Assert.Equal(new SelectionShade.Band(0, 80, 100, 300), bands[1]);
        Assert.Equal(new SelectionShade.Band(500, 80, 1500, 300), bands[2]);
        Assert.Equal(new SelectionShade.Band(0, 380, 2000, 620), bands[3]);
        Assert.False(Covers(bands, 100, 80));
        Assert.False(Covers(bands, 499, 379));
        Assert.True(Covers(bands, 50, 40));
        Assert.True(Covers(bands, 500, 200));
        Assert.True(Covers(bands, 10, 900));
    }

    private static bool Covers(SelectionShade.Band[] bands, double x, double y)
    {
        foreach (var band in bands)
        {
            if (x >= band.X && y >= band.Y && x < band.X + band.Width && y < band.Y + band.Height)
                return true;
        }

        return false;
    }
}
