using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace Qnity
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
        private static extern IntPtr QLibMap_ExportAll(IntPtr mapPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibMap_SetFaceType(
            IntPtr mapPtr,
            [MarshalAs(UnmanagedType.LPStr)] string textureName,
            byte surfaceType);

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

        // ============================================================================
        // Public Properties
        // ============================================================================

        public List<string> TextureNames { get; private set; } = new List<string>();
        public List<string> RequiredWads { get; private set; } = new List<string>();
        public List<MapSolidEntity> SolidEntities { get; private set; } = new List<MapSolidEntity>();
        public List<MapPointEntity> PointEntities { get; private set; } = new List<MapPointEntity>();

        // ============================================================================
        // Public Methods
        // ============================================================================

        /// <summary>
        /// Load a MAP file and generate geometry
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

            _mapPtr = QLibMap_Load(mapPath, (byte)(enableCSG ? 1 : 0), (byte)(convertToOpenGL ? 1 : 0));

            if (_mapPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to load MAP file: {mapPath}");
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
                throw new Exception("Failed to export map data");
            }

            _data = Marshal.PtrToStructure<QLibMapData>(_dataPtr);

            // Parse texture names
            TextureNames = MarshalStringArray(_data.textureNames, _data.textureCount);

            // Parse required WADs
            RequiredWads = MarshalStringArray(_data.requiredWads, _data.requiredWadCount);

            // Parse solid entities
            SolidEntities.Clear();
            for (uint i = 0; i < _data.solidEntityCount; i++)
            {
                IntPtr meshPtr = IntPtr.Add(_data.solidEntities, (int)(i * Marshal.SizeOf<QLibMapEntityMesh>()));
                QLibMapEntityMesh mesh = Marshal.PtrToStructure<QLibMapEntityMesh>(meshPtr);
                SolidEntities.Add(new MapSolidEntity(mesh));
            }

            // Parse point entities
            PointEntities.Clear();
            for (uint i = 0; i < _data.pointEntityCount; i++)
            {
                IntPtr entPtr = IntPtr.Add(_data.pointEntities, (int)(i * Marshal.SizeOf<QLibMapPointEntity>()));
                QLibMapPointEntity entity = Marshal.PtrToStructure<QLibMapPointEntity>(entPtr);
                PointEntities.Add(new MapPointEntity(entity));
            }
        }

        // ============================================================================
        // Helper Methods
        // ============================================================================

        private List<string> MarshalStringArray(IntPtr arrayPtr, uint count)
        {
            var result = new List<string>();
            if (arrayPtr == IntPtr.Zero || count == 0)
                return result;

            for (uint i = 0; i < count; i++)
            {
                IntPtr strPtr = Marshal.ReadIntPtr(arrayPtr, (int)(i * IntPtr.Size));
                if (strPtr != IntPtr.Zero)
                {
                    result.Add(Marshal.PtrToStringAnsi(strPtr)!);
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

                TextureNames.Clear();
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
            ClassName = mesh.className;
            Center = mesh.center.ToVector3();
            BoundsMin = mesh.boundsMin.ToVector3();
            BoundsMax = mesh.boundsMax.ToVector3();

            // Marshal attributes
            Attributes = MarshalAttributes(mesh.attributeKeys, mesh.attributeValues, mesh.attributeCount);

            // Marshal vertices
            Vertices = new QLibVertex[mesh.totalVertexCount];
            if (mesh.vertices != IntPtr.Zero && mesh.totalVertexCount > 0)
            {
                int vertexSize = Marshal.SizeOf<QLibVertex>();
                for (int i = 0; i < mesh.totalVertexCount; i++)
                {
                    IntPtr vertPtr = IntPtr.Add(mesh.vertices, i * vertexSize);
                    Vertices[i] = Marshal.PtrToStructure<QLibVertex>(vertPtr);
                }
            }

            // Marshal indices
            Indices = new uint[mesh.totalIndexCount];
            if (mesh.indices != IntPtr.Zero && mesh.totalIndexCount > 0)
            {
                Marshal.Copy(mesh.indices, (int[])(object)Indices, 0, (int)mesh.totalIndexCount);
            }

            // Marshal submeshes
            Submeshes = new List<MapSubmesh>();
            if (mesh.submeshes != IntPtr.Zero && mesh.submeshCount > 0)
            {
                int submeshSize = Marshal.SizeOf<QLibMapSubmesh>();
                for (int i = 0; i < mesh.submeshCount; i++)
                {
                    IntPtr submeshPtr = IntPtr.Add(mesh.submeshes, i * submeshSize);
                    QLibMapSubmesh submesh = Marshal.PtrToStructure<QLibMapSubmesh>(submeshPtr);
                    Submeshes.Add(new MapSubmesh(submesh));
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
            ClassName = entity.className;
            Origin = entity.origin.ToVector3();
            Angle = entity.angle;
            Attributes = MarshalAttributes(entity.attributeKeys, entity.attributeValues, entity.attributeCount);
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
}