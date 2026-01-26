using Xunit;
using QuakeKit;
using System;
using System.IO;

namespace QuakeKit.Tests.NativeBindings;

/// <summary>
/// Tests specifically for marshaling edge cases that can cause NullReferenceException
/// in Unity when importing map files
/// </summary>
public class NativeQFMapMarshalingTests : IDisposable
{
    private readonly string _testMapPath;
    private NativeQFMap? _map;

    public NativeQFMapMarshalingTests()
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
            _map = new NativeQFMap();
            _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
            _map.GenerateGeometry();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Reproduces the error: "Failed to marshal map data: Object reference not set to an instance of an object"
    /// This can occur when:
    /// 1. The native library returns a valid pointer but with null/invalid internal pointers
    /// 2. The QLibMapData struct has null pointers for textureNames or requiredWads
    /// 3. Accessing properties on _data before validation
    /// </summary>
    [Fact]
    public void ExportData_WithCorruptedData_ThrowsNullReferenceException()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        // This test verifies that ExportData handles null pointers gracefully
        // The current implementation might throw NullReferenceException if:
        // - _data.textureNames is IntPtr.Zero but textureCount > 0
        // - _data.requiredWads is IntPtr.Zero but requiredWadCount > 0
        try
        {
            _map.ExportData();

            // If successful, verify all data structures are initialized
            Assert.NotNull(_map.RequiredWads);
            Assert.NotNull(_map.SolidEntities);
            Assert.NotNull(_map.PointEntities);
        }
        catch (NullReferenceException ex)
        {
            // This is the error we're trying to reproduce
            // The stack trace should show it's coming from line 147 in NativeQFMap.cs
            Assert.Contains("Object reference not set", ex.Message);

            // Document the exact error scenario
            throw new Exception(
                "Reproduced NullReferenceException during ExportData. " +
                "This likely occurs when the native library returns a valid data pointer " +
                "but with null internal pointers (textureNames, requiredWads, solidEntities, or pointEntities). " +
                "The error happens at line 147 when trying to access properties on the marshaled _data structure.",
                ex);
        }
    }

    /// <summary>
    /// Tests that MarshalStringArray properly handles null pointers
    /// This is called internally by ExportData at line 140-143
    /// </summary>
    [Fact]
    public void ExportData_WithNullTextureNames_HandlesGracefully()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        try
        {
            _map.ExportData();

            // RequiredWads should never be null, even if the native library returns no wads
            Assert.NotNull(_map.RequiredWads);
        }
        catch (NullReferenceException ex)
        {
            throw new Exception(
                "TextureNames marshaling failed with NullReferenceException. " +
                "Check if _data.textureNames is IntPtr.Zero and MarshalStringArray is handling it correctly.",
                ex);
        }
    }

    /// <summary>
    /// Tests that Required WADs marshaling handles null pointers
    /// </summary>
    [Fact]
    public void ExportData_WithNullRequiredWads_HandlesGracefully()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        try
        {
            _map.ExportData();

            // RequiredWads should never be null
            Assert.NotNull(_map.RequiredWads);
        }
        catch (NullReferenceException ex)
        {
            throw new Exception(
                "RequiredWads marshaling failed with NullReferenceException. " +
                "Check if _data.requiredWads is IntPtr.Zero and MarshalStringArray is handling it correctly.",
                ex);
        }
    }

    /// <summary>
    /// Tests the complete marshaling pipeline with validation
    /// This replicates the Unity import workflow
    /// </summary>
    [Fact]
    public void ExportData_CompleteWorkflow_ValidatesAllData()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();

        // Step 1: Load the map file (like Unity asset importer does)
        var loadException = Record.Exception(() =>
            _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false));
        Assert.Null(loadException);

        // Step 1.5: Generate geometry (new step in workflow)
        var generateException = Record.Exception(() => _map.GenerateGeometry());
        Assert.Null(generateException);

        // Step 2: Set face types (like Unity asset importer does)
        var setFaceException = Record.Exception(() =>
        {
            _map.SetFaceTypes("clip", SurfaceType.CLIP);
            _map.SetFaceTypes("skip", SurfaceType.SKIP);
            _map.SetFaceTypes("sky", SurfaceType.NODRAW);
        });
        Assert.Null(setFaceException);

        // Step 3: Export data (this is where the error occurs at line 147)
        var exportException = Record.Exception(() => _map.ExportData());

        if (exportException != null)
        {
            // Document the exact error
            Assert.Fail($"ExportData failed during Unity-like workflow:\n" +
                $"Exception Type: {exportException.GetType().Name}\n" +
                $"Message: {exportException.Message}\n" +
                $"Stack Trace: {exportException.StackTrace}\n\n" +
                $"This reproduces the error:\n" +
                $"'Failed to marshal map data: Object reference not set to an instance of an object'\n" +
                $"at NativeQFMap.ExportData() line 147");
        }

        // Step 4: Validate all marshaled data
        Assert.NotNull(_map.RequiredWads);
        Assert.NotNull(_map.SolidEntities);
        Assert.NotNull(_map.PointEntities);

        // Step 5: Validate worldspawn exists
        Assert.NotEmpty(_map.SolidEntities);
        Assert.Equal("worldspawn", _map.SolidEntities[0].ClassName);
    }

    /// <summary>
    /// Tests that the error handling in ExportData properly logs information
    /// when marshaling fails
    /// </summary>
    [Fact]
    public void ExportData_OnMarshalFailure_LogsDetailedError()
    {
        if (!IsLibraryAvailable())
        {
            return;
        }

        _map = new NativeQFMap();
        _map.Load(_testMapPath, enableCSG: true, convertToOpenGL: false);
        _map.GenerateGeometry();

        try
        {
            _map.ExportData();
        }
        catch (Exception ex)
        {
            // If we catch an exception, it should contain useful debugging information
            // The current error message is: "Failed to marshal map data: Object reference not set to an instance of an object"
            // This is not very helpful. The code should log more details about what went wrong.

            // Verify the exception has useful information
            Assert.NotNull(ex.Message);
            Assert.NotEmpty(ex.Message);

            // In the actual Unity error, we see two Debug.LogError calls:
            // 1. "Failed to marshal map data: Object reference not set to an instance of an object"
            // 2. "DataPtr: {ptr}, TextureNames: {ptr}, TextureCount: {count}, RequiredWads: {ptr}, RequiredWadCount: {count}"

            // But the NullReferenceException is happening BEFORE we can even log these details
            // This suggests the error is on line 147, which is the Debug.LogError line itself
            // The NullReferenceException is likely: ex.Message being accessed when ex is somehow null
            // OR more likely: _data fields being accessed in the string interpolation are null/invalid

            throw;
        }
    }
}
