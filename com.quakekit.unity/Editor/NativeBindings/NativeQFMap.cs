using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

#nullable enable

namespace QuakeKit
{
    /// <summary>
    /// Native bindings for libquake MAP file API
    /// Uses the new QLibMap_* API with batch export
    /// </summary>
    public class NativeQFMap : IDisposable
    {
        // ============================================================================
        // P/Invoke Declarations
        // ============================================================================

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibMap_Load(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            byte enableCSG,
            byte convertToOpenGL);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibMap_GetRequiredWads(IntPtr mapPtr, out uint outCount);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibMap_GetTextureNames(IntPtr mapPtr, out uint outCount);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_RegisterTextureSize(
            IntPtr mapPtr,
            [MarshalAs(UnmanagedType.LPStr)] string textureName,
            uint width,
            uint height);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_GenerateGeometry(IntPtr mapPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibMap_ExportAll(IntPtr mapPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_SetFaceType(
            IntPtr mapPtr,
            [MarshalAs(UnmanagedType.LPStr)] string textureName,
            byte surfaceType);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int QLibMap_GenerateLightmaps(
            IntPtr mapPtr,
            uint atlasWidth,
            uint atlasHeight,
            float luxelSize);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern int QLibMap_GenerateLightmapsAuto(
            IntPtr mapPtr,
            uint atlasWidth,
            uint atlasHeight,
            float luxelSize,
            QLibVec3 ambientColor);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_CalculateLighting(
            IntPtr mapPtr,
            [MarshalAs(UnmanagedType.LPArray)] QLibMapLight[] lights,
            uint lightCount,
            QLibVec3 ambientColor);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibMap_GetLightmapData(IntPtr mapPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_FreeLightmapData(IntPtr lightmapData);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_FreeData(IntPtr data);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_Destroy(IntPtr mapPtr);

        // ============================================================================
        // Fields
        // ============================================================================

        private IntPtr _mapPtr = IntPtr.Zero;
        private IntPtr _dataPtr = IntPtr.Zero;
        private QLibMapData _data;
        private bool _disposed = false;
        private bool _geometryGenerated = false;

        // CSG and coordinate system settings
        private bool _enableCSG = true;
        private bool _convertToOpenGL = false;

        // ============================================================================
        // Public Properties
        // ============================================================================

        public List<string> TextureNames { get; private set; } = new List<string>();
        public List<string> RequiredWads { get; private set; } = new List<string>();
        public List<MapSolidEntity> SolidEntities { get; private set; } = new List<MapSolidEntity>();
        public List<MapPointEntity> PointEntities { get; private set; } = new List<MapPointEntity>();
        public uint LightmapWidth { get; private set; }
        public uint LightmapHeight { get; private set; }

        // ============================================================================
        // Public Methods
        // ============================================================================

        /// <summary>
        /// Load a MAP file (parse only, does not generate geometry yet)
        /// Call RegisterTextureSize for textures, then GenerateGeometry, then ExportData
        /// </summary>
        /// <param name="mapPath">Path to the .map file</param>
        /// <param name="enableCSG">Enable CSG operations (brush clipping)</param>
        /// <param name="convertToOpenGL">Convert coordinates to OpenGL system</param>
        public void Load(string mapPath, bool enableCSG = true, bool convertToOpenGL = false)
        {
            if (_mapPtr != IntPtr.Zero)
            {
                Dispose();
            }

            _enableCSG = enableCSG;
            _convertToOpenGL = convertToOpenGL;
            _geometryGenerated = false;

            _mapPtr = QLibMap_Load(mapPath, (byte)(enableCSG ? 1 : 0), (byte)(convertToOpenGL ? 1 : 0));

            if (_mapPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to load MAP file: {mapPath}");
            }
        }

        /// <summary>
        /// Get the list of required WAD files for this map
        /// </summary>
        public List<string> GetRequiredWads()
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            uint wadCount;
            IntPtr wadsPtr = QLibMap_GetRequiredWads(_mapPtr, out wadCount);

            var wads = new List<string>();
            if (wadsPtr != IntPtr.Zero && wadCount > 0)
            {
                IntPtr[] wadPtrs = new IntPtr[wadCount];
                Marshal.Copy(wadsPtr, wadPtrs, 0, (int)wadCount);

                foreach (IntPtr wadNamePtr in wadPtrs)
                {
                    string? wadName = Marshal.PtrToStringAnsi(wadNamePtr);
                    if (wadName != null)
                    {
                        wads.Add(wadName);
                    }
                }
            }

            return wads;
        }

        /// <summary>
        /// Get all texture names used in the map
        /// </summary>
        public List<string> GetTextureNames()
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            uint textureCount;
            IntPtr texturesPtr = QLibMap_GetTextureNames(_mapPtr, out textureCount);

            var textures = new List<string>();
            if (texturesPtr != IntPtr.Zero && textureCount > 0)
            {
                IntPtr[] texturePtrs = new IntPtr[textureCount];
                Marshal.Copy(texturesPtr, texturePtrs, 0, (int)textureCount);

                foreach (IntPtr textureNamePtr in texturePtrs)
                {
                    string? textureName = Marshal.PtrToStringAnsi(textureNamePtr);
                    if (textureName != null)
                    {
                        textures.Add(textureName);
                    }
                }
            }

            return textures;
        }

        /// <summary>
        /// Register texture dimensions for UV calculation
        /// Call this for each texture before calling GenerateGeometry()
        /// </summary>
        public void RegisterTextureSize(string textureName, uint width, uint height)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            QLibMap_RegisterTextureSize(_mapPtr, textureName, width, height);
        }

        /// <summary>
        /// Register texture dimensions from a WAD file
        /// </summary>
        public void RegisterTextureSizesFromWad(NativeQFWad wad)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            foreach (var textureMeta in wad.Textures)
            {
                var texture = wad.GetTexture(textureMeta.Name);
                if (texture != null)
                {
                    RegisterTextureSize(texture.Name, texture.Width, texture.Height);
                }
            }
        }

        /// <summary>
        /// Generate geometry with proper UVs (call after registering texture sizes)
        /// </summary>
        public void GenerateGeometry()
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            QLibMap_GenerateGeometry(_mapPtr);
            _geometryGenerated = true;
        }

        /// <summary>
        /// Generate lightmap atlas and update vertex lightmapUV coordinates to normalized (0-1) atlas space.
        /// Call this after GenerateGeometry() but before ExportData().
        /// </summary>
        /// <param name="atlasWidth">Width of lightmap atlas in pixels (e.g., 512, 1024, 2048)</param>
        /// <param name="atlasHeight">Height of lightmap atlas in pixels</param>
        /// <param name="luxelSize">Size of each lightmap texel in world units (typical: 16.0)</param>
        /// <returns>True if all faces packed successfully, false if atlas is too small</returns>
        public bool GenerateLightmaps(uint atlasWidth, uint atlasHeight, float luxelSize)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            if (!_geometryGenerated)
            {
                throw new InvalidOperationException("Geometry not generated. Call GenerateGeometry() first.");
            }

            int result = QLibMap_GenerateLightmaps(_mapPtr, atlasWidth, atlasHeight, luxelSize);
            return result == 1;
        }

        /// <summary>
        /// One-shot lightmap generation with automatic light extraction from MAP file.
        /// Extracts all "light" entities, reads position/radius/color, and bakes lighting.
        /// Call this after GenerateGeometry() but before ExportData().
        /// </summary>
        /// <param name="atlasWidth">Width of lightmap atlas in pixels</param>
        /// <param name="atlasHeight">Height of lightmap atlas in pixels</param>
        /// <param name="luxelSize">Size of each lightmap texel in world units</param>
        /// <param name="ambientColor">Ambient light color in RGB (0-1 range)</param>
        /// <returns>True if successful, false if atlas is too small</returns>
        public bool GenerateLightmapsAuto(uint atlasWidth, uint atlasHeight, float luxelSize, Color ambientColor)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            if (!_geometryGenerated)
            {
                throw new InvalidOperationException("Geometry not generated. Call GenerateGeometry() first.");
            }

            QLibVec3 ambient = new QLibVec3 { x = ambientColor.r, y = ambientColor.g, z = ambientColor.b };
            int result = QLibMap_GenerateLightmapsAuto(_mapPtr, atlasWidth, atlasHeight, luxelSize, ambient);
            return result == 1;
        }

        /// <summary>
        /// Calculate baked lighting for the lightmap atlas using point lights.
        /// Must call GenerateLightmaps() first.
        /// </summary>
        /// <param name="lights">Array of point lights</param>
        /// <param name="ambientColor">Ambient light color in RGB (0-1 range)</param>
        public void CalculateLighting(QLibMapLight[] lights, Color ambientColor)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            QLibVec3 ambient = new QLibVec3 { x = ambientColor.r, y = ambientColor.g, z = ambientColor.b };
            QLibMap_CalculateLighting(_mapPtr, lights, (uint)lights.Length, ambient);
        }

        /// <summary>
        /// Export the generated lightmap atlas data.
        /// Returns a tuple of (width, height, pixelData) where pixelData is RGBA bytes.
        /// Returns null if no lightmaps have been generated.
        /// </summary>
        public (uint width, uint height, byte[] pixels)? GetLightmapData()
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            IntPtr lightmapPtr = QLibMap_GetLightmapData(_mapPtr);
            if (lightmapPtr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                QLibMapLightmapData lightmapData = Marshal.PtrToStructure<QLibMapLightmapData>(lightmapPtr);

                if (lightmapData.data == IntPtr.Zero || lightmapData.width == 0 || lightmapData.height == 0)
                {
                    return null;
                }

                // Copy RGBA data from native memory
                byte[] pixels = new byte[lightmapData.dataSize];
                Marshal.Copy(lightmapData.data, pixels, 0, (int)lightmapData.dataSize);

                return (lightmapData.width, lightmapData.height, pixels);
            }
            finally
            {
                // Free native lightmap data
                QLibMap_FreeLightmapData(lightmapPtr);
            }
        }

        /// <summary>
        /// Set surface type for textures (e.g., clip, skip, nodraw)
        /// </summary>
        public void SetFaceType(string textureName, SurfaceType type)
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            QLibMap_SetFaceType(_mapPtr, textureName, (byte)type);
        }

        /// <summary>
        /// Set multiple texture names to the same surface type
        /// </summary>
        public void SetFaceTypes(string textureNames, SurfaceType type)
        {
            var names = textureNames.Split(';');
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name))
                {
                    SetFaceType(name.Trim(), type);
                    SetFaceType(name.Trim().ToUpper(), type);
                }
            }
        }

        /// <summary>
        /// Export all map data from the native library
        /// </summary>
        public void ExportData()
        {
            if (_mapPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Map not loaded. Call Load() first.");
            }

            if (!_geometryGenerated)
            {
                throw new InvalidOperationException("Geometry not generated. Call GenerateGeometry() first.");
            }

            // Free previous data if exists
            if (_dataPtr != IntPtr.Zero)
            {
                QLibMap_FreeData(_dataPtr);
                _dataPtr = IntPtr.Zero;
            }

            // Export all data in one batch
            _dataPtr = QLibMap_ExportAll(_mapPtr);
            if (_dataPtr == IntPtr.Zero)
            {
                throw new Exception("Failed to export map data - null pointer returned from QLibMap_ExportAll");
            }

            // Marshal the main data structure
            _data = Marshal.PtrToStructure<QLibMapData>(_dataPtr);

            // Parse texture names
            if (_data.textureCount > 0 && _data.textureNames == IntPtr.Zero)
            {
                Debug.LogWarning($"[QFMap] WARNING: textureCount is {_data.textureCount} but textureNames pointer is null");
            }
            TextureNames = MarshalStringArray(_data.textureNames, _data.textureCount);

            // Parse required WADs
            if (_data.requiredWadCount > 0 && _data.requiredWads == IntPtr.Zero)
            {
                Debug.LogWarning($"[QFMap] WARNING: requiredWadCount is {_data.requiredWadCount} but requiredWads pointer is null");
            }
            RequiredWads = MarshalStringArray(_data.requiredWads, _data.requiredWadCount);

            // Parse solid entities
            SolidEntities.Clear();
            if (_data.solidEntityCount > 0 && _data.solidEntities != IntPtr.Zero)
            {
                for (uint i = 0; i < _data.solidEntityCount; i++)
                {
                    IntPtr meshPtr = IntPtr.Add(_data.solidEntities, (int)(i * Marshal.SizeOf<QLibMapEntityMesh>()));
                    QLibMapEntityMesh mesh = Marshal.PtrToStructure<QLibMapEntityMesh>(meshPtr);
                    SolidEntities.Add(new MapSolidEntity(mesh));
                }
            }

            // Parse point entities
            PointEntities.Clear();
            if (_data.pointEntityCount > 0 && _data.pointEntities != IntPtr.Zero)
            {
                for (uint i = 0; i < _data.pointEntityCount; i++)
                {
                    IntPtr entPtr = IntPtr.Add(_data.pointEntities, (int)(i * Marshal.SizeOf<QLibMapPointEntity>()));
                    QLibMapPointEntity entity = Marshal.PtrToStructure<QLibMapPointEntity>(entPtr);
                    PointEntities.Add(new MapPointEntity(entity));
                }
            }
        }

        // ============================================================================
        // Helper Methods
        // ============================================================================

        private List<string> MarshalStringArray(IntPtr arrayPtr, uint count)
        {
            var result = new List<string>();

            if (arrayPtr == IntPtr.Zero || count == 0)
            {
                return result;
            }

            // Try format 1: Array of pointers (char**)
            for (uint i = 0; i < count; i++)
            {
                try
                {
                    IntPtr strPtr = Marshal.ReadIntPtr(arrayPtr, (int)(i * IntPtr.Size));
                    if (strPtr != IntPtr.Zero)
                    {
                        string? str = Marshal.PtrToStringAnsi(strPtr);
                        if (!string.IsNullOrEmpty(str))
                        {
                            result.Add(str);
                        }
                    }
                }
                catch
                {
                    break;
                }
            }

            if (result.Count > 0)
            {
                return result;
            }

            // Try format 2: Consecutive null-terminated strings
            int offset = 0;
            for (uint i = 0; i < count; i++)
            {
                try
                {
                    IntPtr strPtr = IntPtr.Add(arrayPtr, offset);
                    string? str = Marshal.PtrToStringAnsi(strPtr);
                    if (str != null)
                    {
                        result.Add(str);
                        offset += str.Length + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                catch
                {
                    break;
                }
            }

            return result;
        }

        // ============================================================================
        // IDisposable Implementation
        // ============================================================================

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_dataPtr != IntPtr.Zero)
                {
                    QLibMap_FreeData(_dataPtr);
                    _dataPtr = IntPtr.Zero;
                }

                if (_mapPtr != IntPtr.Zero)
                {
                    QLibMap_Destroy(_mapPtr);
                    _mapPtr = IntPtr.Zero;
                }

                RequiredWads.Clear();
                SolidEntities.Clear();
                PointEntities.Clear();

                _disposed = true;
            }
        }

        ~NativeQFMap()
        {
            Dispose(false);
        }
    }

    // ============================================================================
    // Entity Wrapper Classes
    // ============================================================================

    /// <summary>
    /// Represents a solid (brush-based) entity from a MAP file
    /// </summary>
    public class MapSolidEntity
    {
        public string ClassName { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 BoundsMin { get; private set; }
        public Vector3 BoundsMax { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; }

        public QLibVertex[] Vertices { get; private set; }
        public uint[] Indices { get; private set; }
        public List<MapSubmesh> Submeshes { get; private set; }

        public MapSolidEntity(QLibMapEntityMesh mesh)
        {
            ClassName = mesh.className ?? "unknown";
            Center = mesh.center.ToVector3();
            BoundsMin = mesh.boundsMin.ToVector3();
            BoundsMax = mesh.boundsMax.ToVector3();

            // Marshal attributes
            try
            {
                Attributes = MarshalAttributes(mesh.attributeKeys, mesh.attributeValues, mesh.attributeCount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapSolidEntity] Failed to marshal attributes for {ClassName}: {ex?.Message}");
                Attributes = new Dictionary<string, string>();
            }

            // Marshal vertices with safety checks
            Vertices = new QLibVertex[mesh.totalVertexCount];
            if (mesh.totalVertexCount > 0)
            {
                if (mesh.vertices == IntPtr.Zero)
                {
                    Debug.LogError($"[MapSolidEntity] {ClassName}: vertexCount is {mesh.totalVertexCount} but vertices pointer is null!");
                }
                else
                {
                    try
                    {
                        int vertexSize = Marshal.SizeOf<QLibVertex>();
                        for (int i = 0; i < mesh.totalVertexCount; i++)
                        {
                            IntPtr vertPtr = IntPtr.Add(mesh.vertices, i * vertexSize);
                            Vertices[i] = Marshal.PtrToStructure<QLibVertex>(vertPtr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MapSolidEntity] {ClassName}: Failed to marshal vertices: {ex?.Message}");
                        throw;
                    }
                }
            }

            // Marshal indices with safety checks
            Indices = new uint[mesh.totalIndexCount];
            if (mesh.totalIndexCount > 0)
            {
                if (mesh.indices == IntPtr.Zero)
                {
                    Debug.LogError($"[MapSolidEntity] {ClassName}: indexCount is {mesh.totalIndexCount} but indices pointer is null!");
                }
                else
                {
                    try
                    {
                        // CRITICAL: Indices must be marshaled as uint[], not int[]
                        // Using unsafe code to properly copy uint data
                        unsafe
                        {
                            uint* srcPtr = (uint*)mesh.indices.ToPointer();
                            fixed (uint* dstPtr = Indices)
                            {
                                for (int i = 0; i < mesh.totalIndexCount; i++)
                                {
                                    dstPtr[i] = srcPtr[i];
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MapSolidEntity] {ClassName}: Failed to marshal indices: {ex?.Message}");
                        Debug.LogError($"[MapSolidEntity] IndexCount: {mesh.totalIndexCount}, Indices ptr: {mesh.indices}");
                        throw;
                    }
                }
            }

            // Marshal submeshes with safety checks
            Submeshes = new List<MapSubmesh>();
            if (mesh.submeshCount > 0)
            {
                if (mesh.submeshes == IntPtr.Zero)
                {
                    Debug.LogError($"[MapSolidEntity] {ClassName}: submeshCount is {mesh.submeshCount} but submeshes pointer is null!");
                }
                else
                {
                    try
                    {
                        int submeshSize = Marshal.SizeOf<QLibMapSubmesh>();
                        for (int i = 0; i < mesh.submeshCount; i++)
                        {
                            IntPtr submeshPtr = IntPtr.Add(mesh.submeshes, i * submeshSize);
                            QLibMapSubmesh submesh = Marshal.PtrToStructure<QLibMapSubmesh>(submeshPtr);
                            Submeshes.Add(new MapSubmesh(submesh));
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MapSolidEntity] {ClassName}: Failed to marshal submeshes: {ex?.Message}");
                        throw;
                    }
                }
            }
        }

        private Dictionary<string, string> MarshalAttributes(IntPtr keys, IntPtr values, uint count)
        {
            var attrs = new Dictionary<string, string>();
            if (keys == IntPtr.Zero || values == IntPtr.Zero || count == 0)
                return attrs;

            for (uint i = 0; i < count; i++)
            {
                IntPtr keyPtr = Marshal.ReadIntPtr(keys, (int)(i * IntPtr.Size));
                IntPtr valuePtr = Marshal.ReadIntPtr(values, (int)(i * IntPtr.Size));

                if (keyPtr != IntPtr.Zero && valuePtr != IntPtr.Zero)
                {
                    string key = Marshal.PtrToStringAnsi(keyPtr)!;
                    string value = Marshal.PtrToStringAnsi(valuePtr)!;
                    attrs[key] = value;
                }
            }

            return attrs;
        }
    }

    /// <summary>
    /// Represents a submesh within a solid entity
    /// </summary>
    public class MapSubmesh
    {
        public uint VertexOffset { get; private set; }
        public uint VertexCount { get; private set; }
        public uint IndexOffset { get; private set; }
        public uint IndexCount { get; private set; }
        public int TextureID { get; private set; }
        public string TextureName { get; private set; }
        public SurfaceType SurfaceType { get; private set; }

        public MapSubmesh(QLibMapSubmesh submesh)
        {
            VertexOffset = submesh.vertexOffset;
            VertexCount = submesh.vertexCount;
            IndexOffset = submesh.indexOffset;
            IndexCount = submesh.indexCount;
            TextureID = submesh.textureID;
            TextureName = submesh.textureName;
            SurfaceType = (SurfaceType)submesh.surfaceType;
        }
    }

    /// <summary>
    /// Represents a point entity (non-brush) from a MAP file
    /// </summary>
    public class MapPointEntity
    {
        public string ClassName { get; private set; }
        public Vector3 Origin { get; private set; }
        public float Angle { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; }

        public MapPointEntity(QLibMapPointEntity entity)
        {
            ClassName = entity.className ?? "unknown";
            Origin = entity.origin.ToVector3();
            Angle = entity.angle;

            try
            {
                Attributes = MarshalAttributes(entity.attributeKeys, entity.attributeValues, entity.attributeCount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapPointEntity] Failed to marshal attributes for {ClassName}: {ex?.Message}");
                Attributes = new Dictionary<string, string>();
            }
        }

        private Dictionary<string, string> MarshalAttributes(IntPtr keys, IntPtr values, uint count)
        {
            var attrs = new Dictionary<string, string>();
            if (keys == IntPtr.Zero || values == IntPtr.Zero || count == 0)
                return attrs;

            try
            {
                for (uint i = 0; i < count; i++)
                {
                    IntPtr keyPtr = Marshal.ReadIntPtr(keys, (int)(i * IntPtr.Size));
                    IntPtr valuePtr = Marshal.ReadIntPtr(values, (int)(i * IntPtr.Size));

                    if (keyPtr != IntPtr.Zero && valuePtr != IntPtr.Zero)
                    {
                        string? key = Marshal.PtrToStringAnsi(keyPtr);
                        string? value = Marshal.PtrToStringAnsi(valuePtr);

                        if (!string.IsNullOrEmpty(key) && value != null)
                        {
                            attrs[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MapPointEntity.MarshalAttributes] Failed to marshal attribute {attrs.Count}/{count}: {ex.Message}");
            }

            return attrs;
        }
    }
}