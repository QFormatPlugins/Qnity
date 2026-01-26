using Xunit;
using QuakeKit;
using System;
using System.IO;
using System.Linq;

namespace QuakeKit.Tests.NativeBindings;

public class NativeQFWadTests : IDisposable
{
    private readonly string _testWadPath;
    private NativeQFWad? _wad;

    public NativeQFWadTests()
    {
        _testWadPath = Path.Combine(Directory.GetCurrentDirectory(), "prototype.wad");
    }

    public void Dispose()
    {
        _wad?.Dispose();
    }

    [Fact]
    public void WadFile_Exists()
    {
        Assert.True(File.Exists(_testWadPath), $"Test WAD file not found at: {_testWadPath}");
    }

    [Fact]
    public void Load_ValidWadFile_SuccessfullyLoads()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);

        Assert.NotNull(_wad);
    }

    [Fact]
    public void GetTextureNames_ReturnsTextureNames()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        Assert.NotEmpty(_wad.Textures);
        Assert.All(_wad.Textures, t => Assert.False(string.IsNullOrEmpty(t.Name)));
    }

    [Fact]
    public void GetTexture_ReturnsValidRGBAData()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        var texture = _wad.GetTexture("128_blue_3");

        Assert.NotNull(texture);
        Assert.Equal(128u, texture.Width);
        Assert.Equal(128u, texture.Height);
        Assert.Equal(128u * 128u * 4u, (uint)texture.Data.Length);
    }

    [Fact]
    public void GetTexture_LoadsSpecificTexture()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        var texture = _wad.GetTexture("128_blue_3");

        Assert.NotNull(texture);
        Assert.Equal("128_blue_3", texture.Name);
    }

    [Fact]
    public void GetTexture_ExistingTexture_ReturnsTexture()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        var textureName = _wad.Textures[0].Name;
        var texture = _wad.GetTexture(textureName);

        Assert.NotNull(texture);
        Assert.Equal(textureName, texture.Name);
    }

    [Fact]
    public void GetTexture_NonExistentTexture_ReturnsNull()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        var texture = _wad.GetTexture("nonexistent");

        Assert.Null(texture);
    }

    [Fact]
    public void GetTextureNames_ReturnsUniqueNames()
    {
        if (!IsLibraryAvailable()) return;

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        var names = _wad.Textures.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    private bool IsLibraryAvailable()
    {
        try
        {
            using var testWad = new NativeQFWad();
            testWad.Load(_testWadPath);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
