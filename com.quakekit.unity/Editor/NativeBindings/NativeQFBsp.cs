using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;

namespace Qnity
{
    /// <summary>
    /// Native bindings for libquake BSP file API
    /// BSP files are compiled Quake map files with optimized geometry
    /// </summary>
    public class NativeQFBsp : IDisposable
    {
        // ============================================================================
        // P/Invoke Declarations
        // ============================================================================

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibBsp_Load(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            byte convertToOpenGL);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibBsp_ExportAll(IntPtr bspPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibBsp_GetEntityMesh(IntPtr bspPtr, uint entityIndex);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibBsp_FreeData(IntPtr data);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibBsp_FreeEntityMesh(IntPtr mesh);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibBsp_Destroy(IntPtr bspPtr);

        // ============================================================================
        // Fields
        // ============================================================================

        private IntPtr _bspPtr = IntPtr.Zero;
        private IntPtr _dataPtr = IntPtr.Zero;
        private QLibBspData _data;
        private bool _disposed = false;

        // ============================================================================
        // Public Properties
        // ============================================================================

        public List<BspTexture> Textures { get; private set; } = new List<BspTexture>();
        public List<BspEntityMesh> SolidEntities { get; private set; } = new List<BspEntityMesh>();
        public List<BspEntity> PointEntities { get; private set; } = new List<BspEntity>();
        public uint LightmapWidth { get; private set; }
        public uint LightmapHeight { get; private set; }
        public byte[]? LightmapData { get; private set; }

        /// <summary>
        /// Load a BSP file
        /// </summary>
        /// <param name="bspPath">Path to the .bsp file</param>
        /// <param name="convertToOpenGL">Convert coordinates to OpenGL system</param>
        public void Load(string bspPath, bool convertToOpenGL = false)
        {
            if (_bspPtr != IntPtr.Zero)
            {
                Dispose();
            }

            _bspPtr = QLibBsp_Load(bspPath, (byte)(convertToOpenGL ? 1 : 0));

            if (_bspPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to load BSP file: {bspPath}");
            }
        }

        /// <summary>
        /// Export all BSP data from the native library
        /// </summary>
        public void ExportData()
        {
            if (_bspPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("BSP not loaded. Call Load() first.");
            }

            // Free previous data if exists
            if (_dataPtr != IntPtr.Zero)
            {
                QLibBsp_FreeData(_dataPtr);
                _dataPtr = IntPtr.Zero;
            }

            // Export all data in one batch
            _dataPtr = QLibBsp_ExportAll(_bspPtr);
            if (_dataPtr == IntPtr.Zero)
            {
                throw new Exception("Failed to export BSP data");
            }

            _data = Marshal.PtrToStructure<QLibBspData>(_dataPtr);

            // Parse textures
            Textures.Clear();
            for (uint i = 0; i < _data.textureCount; i++)
            {
                IntPtr texPtr = IntPtr.Add(_data.textures, (int)(i * Marshal.SizeOf<QLibBspTexture>()));
                QLibBspTexture texture = Marshal.PtrToStructure<QLibBspTexture>(texPtr);
                Textures.Add(new BspTexture(texture));
            }

            // Parse solid entities (with geometry)
            SolidEntities.Clear();
            for (uint i = 0; i < _data.solidEntityCount; i++)
            {
                IntPtr meshPtr = IntPtr.Add(_data.solidEntities, (int)(i * Marshal.SizeOf<QLibBspEntityMesh>()));
                QLibBspEntityMesh mesh = Marshal.PtrToStructure<QLibBspEntityMesh>(meshPtr);
                SolidEntities.Add(new BspEntityMesh(mesh));
            }

            // Parse point entities (no geometry)
            PointEntities.Clear();
            for (uint i = 0; i < _data.pointEntityCount; i++)
            {
                IntPtr entPtr = IntPtr.Add(_data.pointEntities, (int)(i * Marshal.SizeOf<QLibBspPointEntity>()));
                QLibBspPointEntity entity = Marshal.PtrToStructure<QLibBspPointEntity>(entPtr);
                PointEntities.Add(new BspEntity(entity));
            }

            // Parse global lightmap data
            LightmapWidth = _data.lightmapWidth;
            LightmapHeight = _data.lightmapHeight;
            if (_data.lightmapData != IntPtr.Zero && LightmapWidth > 0 && LightmapHeight > 0)
            {
                uint lightmapSize = LightmapWidth * LightmapHeight * 3; // RGB
                LightmapData = new byte[lightmapSize];
                Marshal.Copy(_data.lightmapData, LightmapData, 0, (int)lightmapSize);
            }
            else
            {
                LightmapData = new byte[0];
            }
        }

        /// <summary>
        /// Get mesh for a specific entity
        /// </summary>
        /// <param name="entityIndex">Index of the entity</param>
        /// <returns>BspEntityMesh or null if entity has no mesh</returns>
        public BspEntityMesh? GetEntityMesh(uint entityIndex)
        {
            if (_bspPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("BSP not loaded. Call Load() first.");
            }

            IntPtr meshPtr = QLibBsp_GetEntityMesh(_bspPtr, entityIndex);
            if (meshPtr == IntPtr.Zero)
            {
                return null;
            }

            QLibBspEntityMesh mesh = Marshal.PtrToStructure<QLibBspEntityMesh>(meshPtr);
            var result = new BspEntityMesh(mesh);

            // Free the entity mesh
            QLibBsp_FreeEntityMesh(meshPtr);
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
                    QLibBsp_FreeData(_dataPtr);
                    _dataPtr = IntPtr.Zero;
                }

                if (_bspPtr != IntPtr.Zero)
                {
                    QLibBsp_Destroy(_bspPtr);
                    _bspPtr = IntPtr.Zero;
                }

                Textures.Clear();
                SolidEntities.Clear();
                PointEntities.Clear();

                _disposed = true;
            }
        }

        ~NativeQFBsp()
        {
            Dispose(false);
        }
    }

    // ============================================================================
    // Wrapper Classes
    // ============================================================================

    /// <summary>
    /// Represents a texture from a BSP file
    /// </summary>
    public class BspTexture
    {
        public string Name { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public byte[] Data { get; private set; }

        public BspTexture(QLibBspTexture texture)
        {
            Name = texture.name;
            Width = texture.width;
            Height = texture.height;

            // Marshal texture data
            uint dataSize = Width * Height * 4; // RGBA
            Data = new byte[dataSize];
            if (texture.data != IntPtr.Zero && dataSize > 0)
            {
                Marshal.Copy(texture.data, Data, 0, (int)dataSize);
            }
        }
    }

    /// <summary>
    /// Represents an entity from a BSP file
    /// </summary>
    public class BspEntity
    {
        public string ClassName { get; private set; }
        public Vector3 Origin { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; }

        public BspEntity(QLibBspPointEntity entity)
        {
            ClassName = entity.className;
            Origin = entity.origin.ToVector3();
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

    /// <summary>
    /// Represents entity mesh data from a BSP file
    /// </summary>
    public class BspEntityMesh
    {
        public string ClassName { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 BoundsMin { get; private set; }
        public Vector3 BoundsMax { get; private set; }
        public QLibVertex[] Vertices { get; private set; }
        public uint[] Indices { get; private set; }
        public List<BspSubmesh> Submeshes { get; private set; }

        public BspEntityMesh(QLibBspEntityMesh mesh)
        {
            ClassName = mesh.className;
            Center = mesh.center.ToVector3();
            BoundsMin = mesh.boundsMin.ToVector3();
            BoundsMax = mesh.boundsMax.ToVector3();

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
            Submeshes = new List<BspSubmesh>();
            if (mesh.submeshes != IntPtr.Zero && mesh.submeshCount > 0)
            {
                int submeshSize = Marshal.SizeOf<QLibBspSubmesh>();
                for (int i = 0; i < mesh.submeshCount; i++)
                {
                    IntPtr submeshPtr = IntPtr.Add(mesh.submeshes, i * submeshSize);
                    QLibBspSubmesh submesh = Marshal.PtrToStructure<QLibBspSubmesh>(submeshPtr);
                    Submeshes.Add(new BspSubmesh(submesh));
                }
            }
        }
    }

    /// <summary>
    /// Represents a submesh within a BSP entity
    /// </summary>
    public class BspSubmesh
    {
        public uint VertexOffset { get; private set; }
        public uint VertexCount { get; private set; }
        public uint IndexOffset { get; private set; }
        public uint IndexCount { get; private set; }
        public int TextureIndex { get; private set; }
        public string TextureName { get; private set; }

        public BspSubmesh(QLibBspSubmesh submesh)
        {
            VertexOffset = submesh.vertexOffset;
            VertexCount = submesh.vertexCount;
            IndexOffset = submesh.indexOffset;
            IndexCount = submesh.indexCount;
            TextureIndex = submesh.textureIndex;
            TextureName = submesh.textureName;
        }
    }
}
