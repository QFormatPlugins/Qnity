using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

#nullable enable

namespace QuakeKit
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
        /// Get the list of all texture names in the WAD file (no pixel data is loaded).
        /// Use GetTexture(name) to load actual texture data for specific textures.
        /// This uses lazy loading - texture dimensions and pixel data are not loaded until GetTexture() is called.
        /// </summary>
        public void GetTextureNames()
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

            // Get all texture names (lazy loading - no pixel data)
            _dataPtr = QLibWad_ExportAll(_wadPtr);
            if (_dataPtr == IntPtr.Zero)
            {
                throw new Exception("Failed to export WAD data");
            }

            _data = Marshal.PtrToStructure<QLibWadData>(_dataPtr);

            // Parse texture metadata only (width/height/data will be 0/empty due to lazy loading)
            // To get actual texture data, use GetTexture(name) instead
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

        public unsafe WadTexture(QLibWadTexture texture)
        {
            // Convert fixed byte buffer name to string (null-terminated C string)
            byte[] nameBytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                nameBytes[i] = texture.name[i];
            }
            int nullIndex = Array.IndexOf(nameBytes, (byte)0);
            int nameLength = nullIndex >= 0 ? nullIndex : 16;
            Name = System.Text.Encoding.ASCII.GetString(nameBytes, 0, nameLength);

            Width = texture.width;
            Height = texture.height;

            // Debug: Check what we're reading
            var structSize = System.Runtime.InteropServices.Marshal.SizeOf<QLibWadTexture>();
            UnityEngine.Debug.Log($"[WadTexture] Struct size: {structSize}, name={Name}, width={Width}, height={Height}, dataSize={texture.dataSize}");

            // Use the dataSize from the C API (properly set after v1.0.1 RGBA fix)
            Data = new byte[texture.dataSize];
            if (texture.data != IntPtr.Zero && texture.dataSize > 0)
            {
                Marshal.Copy(texture.data, Data, 0, (int)texture.dataSize);
            }
        }
    }
}
