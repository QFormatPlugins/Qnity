using Xunit;
using QuakeKit;
using System;
using System.IO;
using System.Linq;

namespace QuakeKit.Tests.NativeBindings;

/// <summary>
/// Integration tests that verify MAP and WAD files work together
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _testMapPath;
    private readonly string _testWadPath;
    private NativeQFMap? _map;
    private NativeQFWad? _wad;

    public IntegrationTests()
    {
        _testMapPath = Path.Combine(Directory.GetCurrentDirectory(), "test.map");
        _testWadPath = Path.Combine(Directory.GetCurrentDirectory(), "prototype.wad");
    }

    public void Dispose()
    {
        _map?.Dispose();
        _wad?.Dispose();
    }

    [Fact]
    public void MapAndWad_BothFilesExist()
    {
        Assert.True(File.Exists(_testMapPath), "Test MAP file should exist");
        Assert.True(File.Exists(_testWadPath), "Test WAD file should exist");
    }

    [Fact]
    public void MapTextures_CanBeLoadedFromWad()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        // Load MAP file
        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        // Load WAD file
        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        // Verify that textures used in the map submeshes can be loaded from WAD
        var worldspawn = _map.SolidEntities[0];
        foreach (var submesh in worldspawn.Submeshes)
        {
            // Use GetTexture to load actual texture data
            var texture = _wad.GetTexture(submesh.TextureName);

            Assert.NotNull(texture);
            Assert.True(texture.Width > 0, $"Texture {submesh.TextureName} should have valid width");
            Assert.True(texture.Height > 0, $"Texture {submesh.TextureName} should have valid height");
        }
    }

    [Fact]
    public void RequiredWads_MatchesProvidedWad()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        // Map should list prototype WAD as required
        Assert.NotEmpty(_map.RequiredWads);
        Assert.Contains(_map.RequiredWads, wad =>
            wad.Contains("prototype", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SubmeshTextures_HaveValidTextureNames()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        foreach (var submesh in worldspawn.Submeshes)
        {
            // Texture name should exist and not be empty
            Assert.NotNull(submesh.TextureName);
            Assert.NotEmpty(submesh.TextureName);

            // For test.map, we know the texture is "128_blue_3"
            Assert.Equal("128_blue_3", submesh.TextureName);
        }
    }

    [Fact]
    public void TextureDimensions_CanBeRetrievedFromWad()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        // Get texture names from submeshes
        var worldspawn = _map.SolidEntities[0];
        foreach (var submesh in worldspawn.Submeshes)
        {
            var texture = _wad.GetTexture(submesh.TextureName);

            Assert.NotNull(texture);
            Assert.True(texture.Width > 0, $"Texture {submesh.TextureName} width should be positive");
            Assert.True(texture.Height > 0, $"Texture {submesh.TextureName} height should be positive");

            // Verify RGBA data size matches dimensions
            uint expectedSize = texture.Width * texture.Height * 4;
            Assert.Equal(expectedSize, (uint)texture.Data.Length);
        }
    }

    [Fact]
    public void GeometryAndTextures_CompleteWorkflow()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        // This test simulates the complete import workflow

        // Step 1: Load MAP file
        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        // Step 2: Set special face types
        _map.SetFaceTypes("clip;trigger", SurfaceType.CLIP);

        // Step 3: Export geometry
        _map.ExportData();

        Assert.NotEmpty(_map.SolidEntities);

        // Step 4: Load required WAD
        _wad = new NativeQFWad();
        _wad.Load(_testWadPath);
        _wad.GetTextureNames();

        Assert.NotEmpty(_wad.Textures);

        // Step 5: Verify we can create mesh data
        var worldspawn = _map.SolidEntities[0];

        Assert.NotEmpty(worldspawn.Vertices);
        Assert.NotEmpty(worldspawn.Indices);
        Assert.NotEmpty(worldspawn.Submeshes);

        // Step 6: Verify submesh data is valid
        foreach (var submesh in worldspawn.Submeshes)
        {
            // Verify vertex range
            Assert.True(submesh.VertexOffset + submesh.VertexCount <= worldspawn.Vertices.Length);

            // Verify index range
            Assert.True(submesh.IndexOffset + submesh.IndexCount <= worldspawn.Indices.Length);

            // Verify we can get texture for this submesh
            var texture = _wad.GetTexture(submesh.TextureName);
            Assert.NotNull(texture);
        }
    }

    [Fact]
    public void UVCoordinates_AreWithinValidRange()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        foreach (var vertex in worldspawn.Vertices)
        {
            // UV coordinates can be outside 0-1 range (for tiling),
            // but should be reasonable values
            Assert.True(Math.Abs(vertex.uv.x) < 1000, "UV X coordinate should be reasonable");
            Assert.True(Math.Abs(vertex.uv.y) < 1000, "UV Y coordinate should be reasonable");
        }
    }

    [Fact]
    public void Normals_AreNormalized()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        foreach (var vertex in worldspawn.Vertices)
        {
            // Calculate normal length
            float length = (float)Math.Sqrt(
                vertex.normal.x * vertex.normal.x +
                vertex.normal.y * vertex.normal.y +
                vertex.normal.z * vertex.normal.z);

            // Should be approximately 1.0 (normalized)
            // Allow some tolerance for floating point precision
            Assert.True(Math.Abs(length - 1.0f) < 0.01f,
                $"Normal should be normalized, got length {length}");
        }
    }

    [Fact]
    public void Indices_FormValidTriangles()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Indices should be in groups of 3 (triangles)
        Assert.True(worldspawn.Indices.Length % 3 == 0,
            "Index count should be divisible by 3 for triangles");

        // All indices should be valid (within vertex array bounds)
        foreach (var index in worldspawn.Indices)
        {
            Assert.True(index < worldspawn.Vertices.Length,
                $"Index {index} should be within vertex array bounds");
        }
    }

    private bool IsLibraryAvailable()
    {
        try
        {
            using var testMap = new NativeQFMap();
            testMap.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
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
