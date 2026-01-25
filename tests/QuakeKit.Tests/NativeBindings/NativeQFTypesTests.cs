using Xunit;
using Qnity;
using UnityEngine;

namespace QuakeKit.Tests.NativeBindings;

public class NativeQFTypesTests
{
    [Fact]
    public void TextureBounds_DefaultConstructor_SetsZeroValues()
    {
        var bounds = new TextureBounds();

        Assert.Equal(0, bounds.Width);
        Assert.Equal(0, bounds.Height);
    }

    [Fact]
    public void TextureBounds_WithDimensions_StoresCorrectValues()
    {
        var bounds = new TextureBounds { Width = 64, Height = 128 };

        Assert.Equal(64, bounds.Width);
        Assert.Equal(128, bounds.Height);
    }
}
