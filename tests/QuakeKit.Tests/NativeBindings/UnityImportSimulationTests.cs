using Xunit;
using QuakeKit;
using System;
using System.IO;
using System.Threading;

namespace QuakeKit.Tests.NativeBindings;

/// <summary>
/// Tests that simulate Unity's actual asset import pipeline
/// These tests should FAIL if there's a crash condition in Unity
/// </summary>
public class UnityImportSimulationTests : IDisposable
{
    private readonly string _testMapPath;
    private NativeQFMap? _map;

    public UnityImportSimulationTests()
    {
        _testMapPath = Path.Combine(Directory.GetCurrentDirectory(), "test.map");
    }

    public void Dispose()
    {
        _map?.Dispose();
    }

    private bool IsLibraryAvailable()
    {
        try
        {
            var testMap = new NativeQFMap();
            testMap.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
            testMap.Dispose();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            return true; // Library exists but might have issues
        }
    }

    /// <summary>
    /// Simulates EXACT Unity import sequence: Load -> SetFaceTypes -> ExportData -> Access entities
    /// This is what Unity's MapAssetImporter does
    /// </summary>
    [Fact]
    public void UnityImportPipeline_ExactSequence_ShouldNotCrash()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // Step 1: Load (like Unity does)
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        // Step 2: SetFaceTypes (like Unity does) - THIS MIGHT BE THE ISSUE
        _map.SetFaceTypes("clip", SurfaceType.CLIP);
        _map.SetFaceTypes("skip", SurfaceType.SKIP);
        _map.SetFaceTypes("sky", SurfaceType.NODRAW);

        // Step 3: ExportData (where crash likely happens)
        _map.ExportData();

        // Step 4: Access SolidEntities (Unity does this immediately)
        Assert.NotNull(_map.SolidEntities);
        var solidCount = _map.SolidEntities.Count;
        Assert.True(solidCount > 0, "Should have at least worldspawn");

        // Step 5: Access first entity (worldspawn) - Unity does this
        var worldspawn = _map.SolidEntities[0];
        Assert.NotNull(worldspawn);
        Assert.Equal("worldspawn", worldspawn.ClassName);

        // Step 6: Access geometry (Unity needs this for mesh generation)
        Assert.NotNull(worldspawn.Vertices);
        Assert.NotNull(worldspawn.Indices);
        Assert.NotNull(worldspawn.Submeshes);

        Assert.True(worldspawn.Vertices.Length > 0, "Worldspawn should have vertices");
        Assert.True(worldspawn.Indices.Length > 0, "Worldspawn should have indices");
        Assert.True(worldspawn.Submeshes.Count > 0, "Worldspawn should have submeshes");

        // Step 7: Access vertex data (Unity reads this for mesh)
        var firstVertex = worldspawn.Vertices[0];
        // These should not throw or crash
        var pos = firstVertex.pos;
        var normal = firstVertex.normal;
        var uv = firstVertex.uv;

        // Step 8: Access index data (Unity reads this for mesh)
        var firstIndex = worldspawn.Indices[0];
        Assert.True(firstIndex < worldspawn.Vertices.Length, "Index should be valid");

        // Step 9: Access submesh data (Unity uses this for materials)
        var firstSubmesh = worldspawn.Submeshes[0];
        Assert.True(firstSubmesh.VertexOffset + firstSubmesh.VertexCount <= worldspawn.Vertices.Length);
        Assert.True(firstSubmesh.IndexOffset + firstSubmesh.IndexCount <= worldspawn.Indices.Length);
    }

    /// <summary>
    /// Test accessing entity data multiple times (Unity might access it in loops)
    /// </summary>
    [Fact]
    public void UnityImportPipeline_MultipleAccess_ShouldNotCrash()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.SetFaceTypes("clip", SurfaceType.CLIP);
        _map.SetFaceTypes("skip", SurfaceType.SKIP);
        _map.SetFaceTypes("sky", SurfaceType.NODRAW);
        _map.ExportData();

        // Unity accesses entities in loops - this might trigger memory issues
        for (int iteration = 0; iteration < 5; iteration++)
        {
            foreach (var entity in _map.SolidEntities)
            {
                // Access all properties (Unity does this)
                var className = entity.ClassName;
                var center = entity.Center;
                var bounds = entity.BoundsMin;
                var vertices = entity.Vertices;
                var indices = entity.Indices;
                var submeshes = entity.Submeshes;

                // Verify data is still valid
                Assert.NotNull(vertices);
                Assert.NotNull(indices);
                Assert.NotNull(submeshes);
            }
        }
    }

    /// <summary>
    /// Test the exact disposal pattern Unity uses
    /// </summary>
    [Fact]
    public void UnityImportPipeline_WithDisposal_ShouldNotCrash()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        try
        {
            _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
            _map.GenerateGeometry();
            _map.SetFaceTypes("clip", SurfaceType.CLIP);
            _map.SetFaceTypes("skip", SurfaceType.SKIP);
            _map.SetFaceTypes("sky", SurfaceType.NODRAW);
            _map.ExportData();

            // Access data
            var count = _map.SolidEntities.Count;
            Assert.True(count > 0);
        }
        finally
        {
            // Unity's finally block disposes
            _map?.Dispose();
            _map = null; // Make sure we don't double-dispose
        }
    }

    /// <summary>
    /// Test without SetFaceTypes to see if that's causing the issue
    /// </summary>
    [Fact]
    public void UnityImportPipeline_WithoutSetFaceTypes_ShouldWork()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        // Skip SetFaceTypes - maybe this is causing the crash?
        // _map.SetFaceTypes("clip", SurfaceType.CLIP);

        _map.ExportData();

        Assert.NotNull(_map.SolidEntities);
        Assert.True(_map.SolidEntities.Count > 0);
    }

    /// <summary>
    /// Test with only one SetFaceType call
    /// </summary>
    [Fact]
    public void UnityImportPipeline_SingleSetFaceType_ShouldWork()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        // Try just one
        _map.SetFaceTypes("clip", SurfaceType.CLIP);

        _map.ExportData();

        Assert.NotNull(_map.SolidEntities);
        Assert.True(_map.SolidEntities.Count > 0);
    }

    /// <summary>
    /// Test calling ExportData twice (maybe Unity does this?)
    /// </summary>
    [Fact]
    public void UnityImportPipeline_DoubleExport_ShouldHandle()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.SetFaceTypes("clip", SurfaceType.CLIP);

        // First export
        _map.ExportData();
        var count1 = _map.SolidEntities.Count;

        // Second export - does this crash?
        _map.ExportData();
        var count2 = _map.SolidEntities.Count;

        Assert.Equal(count1, count2);
    }

    /// <summary>
    /// Test aggressive memory access patterns
    /// </summary>
    [Fact]
    public void UnityImportPipeline_AggressiveMemoryAccess_ShouldNotCrash()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.SetFaceTypes("clip", SurfaceType.CLIP);
        _map.SetFaceTypes("skip", SurfaceType.SKIP);
        _map.SetFaceTypes("sky", SurfaceType.NODRAW);
        _map.ExportData();

        var worldspawn = _map.SolidEntities[0];

        // Access every single vertex
        for (int i = 0; i < worldspawn.Vertices.Length; i++)
        {
            var v = worldspawn.Vertices[i];
            // Force reading all fields
            var sum = v.pos.x + v.pos.y + v.pos.z + v.normal.x + v.uv.x;
        }

        // Access every single index
        for (int i = 0; i < worldspawn.Indices.Length; i++)
        {
            var idx = worldspawn.Indices[i];
            Assert.True(idx < worldspawn.Vertices.Length, $"Index {idx} at position {i} is out of bounds");
        }

        // Access every submesh thoroughly
        foreach (var submesh in worldspawn.Submeshes)
        {
            // This is what Unity does when building meshes
            for (uint i = 0; i < submesh.IndexCount; i++)
            {
                var globalIdx = submesh.IndexOffset + i;
                Assert.True(globalIdx < worldspawn.Indices.Length, "Submesh index out of bounds");

                var vertexIdx = worldspawn.Indices[globalIdx];
                Assert.True(vertexIdx < worldspawn.Vertices.Length, "Vertex index out of bounds");
            }
        }
    }

    /// <summary>
    /// Test with file path exactly as Unity would use it
    /// </summary>
    [Fact]
    public void UnityImportPipeline_AbsolutePath_ShouldWork()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        // Unity uses absolute paths
        var absolutePath = Path.GetFullPath(_testMapPath);

        _map = new NativeQFMap();
        _map.Load(absolutePath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();
        _map.SetFaceTypes("clip", SurfaceType.CLIP);
        _map.SetFaceTypes("skip", SurfaceType.SKIP);
        _map.SetFaceTypes("sky", SurfaceType.NODRAW);
        _map.ExportData();

        Assert.NotNull(_map.SolidEntities);
        Assert.True(_map.SolidEntities.Count > 0);
    }

    /// <summary>
    /// Force test to fail with detailed error if anything throws
    /// </summary>
    [Fact]
    public void UnityImportPipeline_CatchAnyException_ShowDetails()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        try
        {
            _map = new NativeQFMap();

            // Log each step so we know where it fails
            Console.WriteLine("[TEST] Step 1: Load");
            _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
            _map.GenerateGeometry();

            Console.WriteLine("[TEST] Step 2: SetFaceTypes CLIP");
            _map.SetFaceTypes("clip", SurfaceType.CLIP);

            Console.WriteLine("[TEST] Step 3: SetFaceTypes SKIP");
            _map.SetFaceTypes("skip", SurfaceType.SKIP);

            Console.WriteLine("[TEST] Step 4: SetFaceTypes SKY");
            _map.SetFaceTypes("sky", SurfaceType.NODRAW);

            Console.WriteLine("[TEST] Step 5: ExportData");
            _map.ExportData();

            Console.WriteLine("[TEST] Step 6: Access SolidEntities");
            var count = _map.SolidEntities.Count;
            Console.WriteLine($"[TEST] SolidEntities count: {count}");

            Console.WriteLine("[TEST] Step 7: Access worldspawn");
            var worldspawn = _map.SolidEntities[0];
            Console.WriteLine($"[TEST] Worldspawn class: {worldspawn.ClassName}");

            Console.WriteLine("[TEST] Step 8: Access vertices");
            var vertCount = worldspawn.Vertices.Length;
            Console.WriteLine($"[TEST] Vertex count: {vertCount}");

            Console.WriteLine("[TEST] Step 9: Access indices");
            var idxCount = worldspawn.Indices.Length;
            Console.WriteLine($"[TEST] Index count: {idxCount}");

            Console.WriteLine("[TEST] Step 10: Access submeshes");
            var submeshCount = worldspawn.Submeshes.Count;
            Console.WriteLine($"[TEST] Submesh count: {submeshCount}");

            Console.WriteLine("[TEST] Step 11: Read first vertex");
            var v = worldspawn.Vertices[0];
            Console.WriteLine($"[TEST] First vertex: ({v.pos.x}, {v.pos.y}, {v.pos.z})");

            Console.WriteLine("[TEST] Step 12: Read first index");
            var idx = worldspawn.Indices[0];
            Console.WriteLine($"[TEST] First index: {idx}");

            Console.WriteLine("[TEST] All steps completed successfully!");
        }
        catch (Exception ex)
        {
            // Force test to fail with full details
            var errorMsg = $"CRASH REPRODUCTION:\n" +
                          $"Exception Type: {ex.GetType().Name}\n" +
                          $"Message: {ex.Message}\n" +
                          $"Stack Trace:\n{ex.StackTrace}";

            if (ex.InnerException != null)
            {
                errorMsg += $"\n\nInner Exception: {ex.InnerException.GetType().Name}\n" +
                           $"Inner Message: {ex.InnerException.Message}\n" +
                           $"Inner Stack:\n{ex.InnerException.StackTrace}";
            }

            Console.WriteLine(errorMsg);
            throw new Exception(errorMsg, ex);
        }
    }
}
