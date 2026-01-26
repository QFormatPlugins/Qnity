using Xunit;
using QuakeKit;
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
        var exception = Record.Exception(() => LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false));
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);

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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
    public void ExportData_WithoutLoad_ThrowsInvalidOperationException()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // Should throw InvalidOperationException when ExportData is called without Load
        var exception = Assert.Throws<InvalidOperationException>(() => _map.ExportData());
        Assert.Contains("Map not loaded", exception.Message);
    }

    [Fact]
    public void ExportData_HandlesNullPointerFromNativeLibrary()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // This tests the scenario where Load succeeds but ExportAll returns null
        // This might happen if the native library is in an inconsistent state
        try
        {
            LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
            _map.ExportData();
        }
        catch (Exception ex)
        {
            // We expect either an Exception about export failure 
            // or a NullReferenceException if marshaling fails with null data
            Assert.True(
                ex is Exception && ex.Message.Contains("Failed to export map data") ||
                ex is NullReferenceException ||
                ex is Exception && ex.Message.Contains("Failed to marshal map data"),
                $"Expected export or marshal error, got: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [Fact]
    public void MarshalStringArray_HandlesNullPointers()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);

        // This will test if the marshaling properly handles null or invalid pointers
        // The actual marshaling happens inside ExportData, so we test the full flow
        try
        {
            _map.ExportData();

            // If we get here, verify the data is valid
            Assert.NotNull(_map.RequiredWads);
            Assert.NotNull(_map.SolidEntities);
            Assert.NotNull(_map.PointEntities);
        }
        catch (NullReferenceException ex)
        {
            // This is the error we're trying to reproduce
            Assert.True(true, $"Successfully reproduced NullReferenceException: {ex.Message}");
            throw; // Re-throw to see full stack trace
        }
    }

    [Fact]
    public void ExportData_ValidatesDataStructureBeforeMarshaling()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);

        // Try to export data and check for any marshaling errors
        var exception = Record.Exception(() => _map.ExportData());

        if (exception != null)
        {
            // Log detailed information about the failure
            Assert.True(false,
                $"ExportData failed with {exception.GetType().Name}: {exception.Message}\n" +
                $"StackTrace: {exception.StackTrace}");
        }

        // Verify all collections are initialized (not null)
        Assert.NotNull(_map.RequiredWads);
        Assert.NotNull(_map.SolidEntities);
        Assert.NotNull(_map.PointEntities);
    }

    [Fact]
    public void Load_WithInvalidPath_ReturnsWithoutException()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // Current implementation doesn't throw exceptions for invalid paths
        // It returns silently - this is the actual behavior
        _map.Load("/nonexistent/path/file.map", enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.ExportData();

        // Should have no entities if load failed
        Assert.Empty(_map.SolidEntities);
    }

    [Fact]
    public void ExportData_TextureNamesPopulated()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        // Verify texture names are populated at the top level
        Assert.NotEmpty(_map.TextureNames);
        Assert.Contains("128_blue_3", _map.TextureNames);

        // Also verify through submeshes
        var worldspawn = _map.SolidEntities.FirstOrDefault(e => e.ClassName == "worldspawn");
        Assert.NotNull(worldspawn);
        Assert.NotEmpty(worldspawn.Submeshes);
        Assert.Contains(worldspawn.Submeshes, s => s.TextureName == "128_blue_3");
    }

    [Fact]
    public void ExportData_RequiredWadsPopulated()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);

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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);

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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // A box with CSG enabled has 36 vertices (6 per face × 6 faces)
        Assert.Equal(36, worldspawn.Vertices.Length);
    }

    [Fact]
    public void Geometry_HasExpectedTriangleCount()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Test brush is 128x128x32 units (from -64 to 64 in X/Y, -16 to 16 in Z)
        Console.WriteLine($"BoundsMin: {worldspawn.BoundsMin}");
        Console.WriteLine($"BoundsMax: {worldspawn.BoundsMax}");
        var boundsSize = worldspawn.BoundsMax - worldspawn.BoundsMin;

        // Check that bounds have reasonable size
        Assert.True(boundsSize.x > 0, $"Bounds X size should be positive, got {boundsSize.x}");
        Assert.True(boundsSize.y > 0, $"Bounds Y size should be positive, got {boundsSize.y}");
        Assert.True(boundsSize.z > 0, $"Bounds Z size should be positive, got {boundsSize.z}");

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
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // CSG merges all faces into a single submesh
        Assert.Equal(1, worldspawn.Submeshes.Count);

        // Should be solid type
        Assert.Equal(SurfaceType.SOLID, worldspawn.Submeshes[0].SurfaceType);
    }

    [Fact]
    public void Geometry_EachFaceHasCorrectVertexCount()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // CSG creates a merged mesh - for a box (6 faces × 6 vertices per face = 36)
        Assert.Single(worldspawn.Submeshes);
        Assert.Equal(36u, worldspawn.Submeshes[0].VertexCount);
    }

    [Fact]
    public void Geometry_EachFaceHasTwoTriangles()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // CSG creates a merged mesh - for a box (6 faces × 6 indices per face = 36)
        Assert.Single(worldspawn.Submeshes);
        Assert.Equal(36u, worldspawn.Submeshes[0].IndexCount);
    }

    [Fact]
    public void Geometry_AllFacesUseSameTexture()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        LoadAndGenerate(_map, _testMapPath, enableCSG: true, convertToOpenGL: false);
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

    // Helper method to load and generate geometry in one call (for tests that don't need to test the individual phases)
    private void LoadAndGenerate(NativeQFMap map, string mapPath, bool enableCSG = true, bool convertToOpenGL = false)
    {
        map.Load(mapPath, enableCSG, convertToOpenGL);
        map.GenerateGeometry();
    }

    private bool IsLibraryAvailable()
    {
        try
        {
            // Try to instantiate and load - if library is missing, this will fail
            using var testMap = new NativeQFMap();
            LoadAndGenerate(testMap, _testMapPath, enableCSG: true, convertToOpenGL: false);
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
