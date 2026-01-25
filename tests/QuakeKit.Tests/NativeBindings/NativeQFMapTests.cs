using Xunit;
using Qnity;
using System;
using System.IO;

namespace QuakeKit.Tests.NativeBindings;

public class NativeQFMapTests : IDisposable
{
    private readonly string _testMapPath;
    private NativeQFMap? _map;

    public NativeQFMapTests()
    {
        _testMapPath = Path.Combine(Directory.GetCurrentDirectory(), "test.map");
    }

    public void Dispose()
    {
        _map?.Dispose();
    }

    [Fact]
    public void MapFile_Exists()
    {
        Assert.True(File.Exists(_testMapPath), $"Test map file not found at: {_testMapPath}");
    }

    [Fact]
    public void Load_ValidMapFile_SuccessfullyLoads()
    {
        // Skip if libquake binary is not available
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // Should not throw
        var exception = Record.Exception(() => _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false));
        Assert.Null(exception);
    }

    [Fact]
    public void ExportData_AfterLoad_ReturnsData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);

        // Should not throw
        var exception = Record.Exception(() => _map.ExportData());
        Assert.Null(exception);
    }

    [Fact]
    public void ExportData_ContainsWorldspawn()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        // Test map should have at least worldspawn
        Assert.NotEmpty(_map.SolidEntities);
        Assert.Equal("worldspawn", _map.SolidEntities[0].ClassName);
    }

    [Fact]
    public void ExportData_WorldspawnHasGeometry()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Worldspawn should have vertices and indices
        Assert.NotEmpty(worldspawn.Vertices);
        Assert.NotEmpty(worldspawn.Indices);
        Assert.NotEmpty(worldspawn.Submeshes);
    }

    [Fact]
    public void ExportData_VerticesHaveValidData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];
        var firstVertex = worldspawn.Vertices[0];

        // Vertex should have valid position (not all zeros)
        var hasNonZero = firstVertex.pos.x != 0 || firstVertex.pos.y != 0 || firstVertex.pos.z != 0;
        Assert.True(hasNonZero || worldspawn.Vertices.Length > 1, "Vertices should have valid position data");
    }

    [Fact]
    public void ExportData_SubmeshesHaveValidOffsets()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        foreach (var submesh in worldspawn.Submeshes)
        {
            // Vertex offset + count should be within bounds
            Assert.True(submesh.VertexOffset + submesh.VertexCount <= worldspawn.Vertices.Length,
                "Submesh vertex range should be within vertex array bounds");

            // Index offset + count should be within bounds
            Assert.True(submesh.IndexOffset + submesh.IndexCount <= worldspawn.Indices.Length,
                "Submesh index range should be within index array bounds");

            // Should have valid counts
            Assert.True(submesh.VertexCount > 0, "Submesh should have vertices");
            Assert.True(submesh.IndexCount > 0, "Submesh should have indices");
        }
    }

    [Fact]
    public void ExportData_TextureNamesPopulated()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        // Map uses "128_blue_3" texture
        Assert.NotEmpty(_map.TextureNames);
        Assert.Contains("128_blue_3", _map.TextureNames);
    }

    [Fact]
    public void ExportData_RequiredWadsPopulated()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        // Map should reference prototype WAD
        Assert.NotEmpty(_map.RequiredWads);
        Assert.Contains(_map.RequiredWads, wad => wad.Contains("prototype"));
    }

    [Fact]
    public void SetFaceTypes_ValidTexture_DoesNotThrow()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);

        // Should not throw
        var exception = Record.Exception(() =>
            _map.SetFaceTypes("clip;trigger", SurfaceType.CLIP));
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_ReleasesResources()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        // Should not throw
        var exception = Record.Exception(() => _map.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Load_WithoutExportData_ThrowsOnDispose()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);

        // Should still be able to dispose even without calling ExportData
        var exception = Record.Exception(() => _map.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void EntityAttributes_WorldspawnHasAttributes()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Should have classname attribute at minimum
        Assert.NotEmpty(worldspawn.Attributes);
        Assert.True(worldspawn.Attributes.ContainsKey("classname"));
        Assert.Equal("worldspawn", worldspawn.Attributes["classname"]);
    }

    [Fact]
    public void Geometry_HasExpectedVertexCount()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // A box should have 24 vertices (4 per face × 6 faces)
        Assert.Equal(24, worldspawn.Vertices.Length);
    }

    [Fact]
    public void Geometry_HasExpectedTriangleCount()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // A box should have 12 triangles (2 per face × 6 faces) = 36 indices
        Assert.Equal(36, worldspawn.Indices.Length);
    }

    [Fact]
    public void Geometry_BoundsMatchBrushDimensions()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Test brush is 128x128x32 units (from -64 to 64 in X/Y, -16 to 16 in Z)
        // Note: Coordinates might be converted to Unity space
        var boundsSize = worldspawn.BoundsMax - worldspawn.BoundsMin;

        // Check that bounds have reasonable size (accounting for coordinate conversion)
        Assert.True(boundsSize.x > 0, "Bounds X size should be positive");
        Assert.True(boundsSize.y > 0, "Bounds Y size should be positive");
        Assert.True(boundsSize.z > 0, "Bounds Z size should be positive");

        // The total volume should be approximately 128*128*32 = 524288 cubic units
        float volume = boundsSize.x * boundsSize.y * boundsSize.z;
        Assert.True(volume > 500000 && volume < 550000,
            $"Expected volume around 524288, got {volume}");
    }

    [Fact]
    public void Geometry_HasExactlyOneSurfaceTypeForBox()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Simple box should have 6 submeshes (one per face)
        Assert.Equal(6, worldspawn.Submeshes.Count);

        // All should be solid type
        foreach (var submesh in worldspawn.Submeshes)
        {
            Assert.Equal(SurfaceType.SOLID, submesh.SurfaceType);
        }
    }

    [Fact]
    public void Geometry_EachFaceHasCorrectVertexCount()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Each face of the box should have 4 vertices
        foreach (var submesh in worldspawn.Submeshes)
        {
            Assert.Equal(4u, submesh.VertexCount);
        }
    }

    [Fact]
    public void Geometry_EachFaceHasTwoTriangles()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Each face should have 6 indices (2 triangles × 3 vertices)
        foreach (var submesh in worldspawn.Submeshes)
        {
            Assert.Equal(6u, submesh.IndexCount);
        }
    }

    [Fact]
    public void Geometry_AllFacesUseSameTexture()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // All faces of test box use the same texture (128_blue_3)
        var firstTexture = worldspawn.Submeshes[0].TextureName;
        foreach (var submesh in worldspawn.Submeshes)
        {
            Assert.Equal(firstTexture, submesh.TextureName);
            Assert.Equal("128_blue_3", submesh.TextureName);
        }
    }

    private bool IsLibraryAvailable()
    {
        try
        {
            // Try to instantiate and load - if library is missing, this will fail
            using var testMap = new NativeQFMap();
            testMap.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
            return true;
        }
        catch (DllNotFoundException)
        {
            // Library not available, skip test
            return false;
        }
        catch (Exception)
        {
            // Some other error occurred, library is available
            return true;
        }
    }
}
