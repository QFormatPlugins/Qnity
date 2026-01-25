using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Qnity
{
    /// <summary>
    /// Native bindings for libquake WAD file API
    /// WAD files contain texture archives used by Quake maps
    /// </summary>
    public class NativeQFWad : IDisposable
    {
        // ============================================================================
        // P/Invoke Declarations
        // ============================================================================

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibWad_Load(
            [MarshalAs(UnmanagedType.LPStr)] string filePath,
            IntPtr paletteData);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibWad_ExportAll(IntPtr wadPtr);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QLibWad_GetTexture(
            IntPtr wadPtr,
            [MarshalAs(UnmanagedType.LPStr)] string textureName);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibWad_FreeData(IntPtr data);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibWad_FreeTexture(IntPtr texture);

        [DllImport("quakelib", CallingConvention = CallingConvention.Cdecl)]
        private static extern void QLibWad_Destroy(IntPtr wadPtr);

        // ============================================================================
        // Fields
        // ============================================================================

        private IntPtr _wadPtr = IntPtr.Zero;
        private IntPtr _dataPtr = IntPtr.Zero;
        private QLibWadData _data;
        private bool _disposed = false;

        // ============================================================================
        // Public Properties
        // ============================================================================

        public List<WadTexture> Textures { get; private set; } = new List<WadTexture>();

        // ============================================================================
        // Public Methods
        // ============================================================================

        /// <summary>
        /// Load a WAD file
        /// </summary>
        /// <param name="wadPath">Path to the .wad file</param>
        /// <param name="paletteData">Optional 768-byte palette data (256 RGB values). Pass IntPtr.Zero to use default Quake palette.</param>
        public void Load(string wadPath, IntPtr paletteData = default)
        {
            if (_wadPtr != IntPtr.Zero)
            {
                Dispose();
            }

            _wadPtr = QLibWad_Load(wadPath, paletteData);

            if (_wadPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to load WAD file: {wadPath}");
            }
        }

        /// <summary>
        /// Export all textures from the WAD file
        /// </summary>
        public void ExportData()
        {
            if (_wadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("WAD not loaded. Call Load() first.");
            }

            // Free previous data if exists
            if (_dataPtr != IntPtr.Zero)
            {
                QLibWad_FreeData(_dataPtr);
                _dataPtr = IntPtr.Zero;
            }

            // Export all textures in one batch
            _dataPtr = QLibWad_ExportAll(_wadPtr);
            if (_dataPtr == IntPtr.Zero)
            {
                throw new Exception("Failed to export WAD data");
            }

            _data = Marshal.PtrToStructure<QLibWadData>(_dataPtr);

            // Parse textures
            Textures.Clear();
            for (uint i = 0; i < _data.textureCount; i++)
            {
                IntPtr texPtr = IntPtr.Add(_data.textures, (int)(i * Marshal.SizeOf<QLibWadTexture>()));
                QLibWadTexture texture = Marshal.PtrToStructure<QLibWadTexture>(texPtr);
                Textures.Add(new WadTexture(texture));
            }
        }

        /// <summary>
        /// Get a specific texture by name
        /// </summary>
        /// <param name="textureName">Name of the texture</param>
        /// <returns>WadTexture or null if not found</returns>
        public WadTexture? GetTexture(string textureName)
        {
            if (_wadPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("WAD not loaded. Call Load() first.");
            }

            IntPtr texPtr = QLibWad_GetTexture(_wadPtr, textureName);
            if (texPtr == IntPtr.Zero)
            {
                return null;
            }

            QLibWadTexture texture = Marshal.PtrToStructure<QLibWadTexture>(texPtr);
            var result = new WadTexture(texture);

            // Free the single texture data
            QLibWad_FreeTexture(texPtr);
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
                    QLibWad_FreeData(_dataPtr);
                    _dataPtr = IntPtr.Zero;
                }

                if (_wadPtr != IntPtr.Zero)
                {
                    QLibWad_Destroy(_wadPtr);
                    _wadPtr = IntPtr.Zero;
                }

                Textures.Clear();

                _disposed = true;
            }
        }

        ~NativeQFWad()
        {
            Dispose(false);
        }
    }

    // ============================================================================
    // Texture Wrapper Class
    // ============================================================================

    /// <summary>
    /// Represents a texture from a WAD file
    /// </summary>
    public class WadTexture
    {
        public string Name { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public byte[] Data { get; private set; }

        public WadTexture(QLibWadTexture texture)
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
}
