using Xunit;
using Qnity;
using System;
using System.IO;

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
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();

        // Should not throw
        var exception = Record.Exception(() => _wad.Load(_testWadPath));
        Assert.Null(exception);
    }

    [Fact]
    public void ExportData_AfterLoad_ReturnsData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);

        // Should not throw
        var exception = Record.Exception(() => _wad.ExportData());
        Assert.Null(exception);
    }

    [Fact]
    public void ExportData_ContainsTextures()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        // Prototype WAD should contain textures
        Assert.NotEmpty(_wad.Textures);
    }

    [Fact]
    public void ExportData_TexturesHaveValidNames()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        foreach (var texture in _wad.Textures)
        {
            // Each texture should have a non-empty name
            Assert.False(string.IsNullOrWhiteSpace(texture.Name));
        }
    }

    [Fact]
    public void ExportData_TexturesHaveValidDimensions()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        foreach (var texture in _wad.Textures)
        {
            // Width and height should be positive
            Assert.True(texture.Width > 0, $"Texture {texture.Name} has invalid width");
            Assert.True(texture.Height > 0, $"Texture {texture.Name} has invalid height");

            // Dimensions should be reasonable (not absurdly large)
            Assert.True(texture.Width <= 4096, $"Texture {texture.Name} width seems too large");
            Assert.True(texture.Height <= 4096, $"Texture {texture.Name} height seems too large");
        }
    }

    [Fact]
    public void ExportData_TexturesHaveRGBAData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        foreach (var texture in _wad.Textures)
        {
            // RGBA data should match dimensions
            uint expectedSize = texture.Width * texture.Height * 4;
            Assert.Equal(expectedSize, (uint)texture.Data.Length);
        }
    }

    [Fact]
    public void ExportData_Contains128Blue3Texture()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        // Our test map uses this texture
        var blueTexture = _wad.Textures.Find(t => t.Name == "128_blue_3");
        Assert.NotNull(blueTexture);
        Assert.True(blueTexture.Width > 0);
        Assert.True(blueTexture.Height > 0);
    }

    [Fact]
    public void GetTexture_ExistingTexture_ReturnsTexture()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        // Get the first texture by name
        var firstTextureName = _wad.Textures[0].Name;
        var texture = _wad.GetTexture(firstTextureName);

        Assert.NotNull(texture);
        Assert.Equal(firstTextureName, texture.Name);
    }

    [Fact]
    public void GetTexture_NonExistentTexture_ReturnsNull()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);

        var texture = _wad.GetTexture("nonexistent_texture_name_12345");

        Assert.Null(texture);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        // Should not throw
        var exception = Record.Exception(() => _wad.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_WithDefaultPalette_SuccessfullyLoads()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();

        // Load with default palette (IntPtr.Zero)
        var exception = Record.Exception(() => _wad.Load(_testWadPath, IntPtr.Zero));
        Assert.Null(exception);
    }

    [Fact]
    public void MultipleTextures_AllHaveUniqueData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.ExportData();

        if (_wad.Textures.Count < 2)
        {
            // Need at least 2 textures to test
            return;
        }

        // Check that textures have different data
        var firstData = _wad.Textures[0].Data;
        var secondData = _wad.Textures[1].Data;

        // At least some bytes should be different (unless they're identical textures)
        bool hasDifference = false;
        int compareLength = Math.Min(firstData.Length, secondData.Length);
        for (int i = 0; i < compareLength && !hasDifference; i++)
        {
            if (firstData[i] != secondData[i])
            {
                hasDifference = true;
            }
        }

        // Either they have different lengths or different data
        Assert.True(firstData.Length != secondData.Length || hasDifference,
            "Different textures should have different data");
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
