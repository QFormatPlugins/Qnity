using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Qnity
{
    // ============================================================================
    // QLib Common Structures (from wrapper.h)
    // ============================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibVec2
    {
        public float x;
        public float y;

        public Vector2 ToVector2()
        {
            return new Vector2(x, y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibVec3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibVec4
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4 ToVector4()
        {
            return new Vector4(x, y, z, w);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibVertex
    {
        public QLibVec3 pos;
        public QLibVec3 normal;
        public QLibVec4 tangent;
        public QLibVec2 uv;
        public QLibVec2 lightmapUV;
    }

    // ============================================================================
    // Surface Types (from map_provider.h)
    // ============================================================================

    public enum SurfaceType : byte
    {
        SOLID = 0,
        CLIP = 1,
        SKIP = 2,
        NODRAW = 3,
    }

    // ============================================================================
    // Texture Bounds Callback
    // ============================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct TextureBounds
    {
        public float Width;
        public float Height;
    }

    // ============================================================================
    // MAP Structures (from wrapper.h)
    // ============================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibMapSubmesh
    {
        public uint vertexOffset;
        public uint vertexCount;
        public uint indexOffset;
        public uint indexCount;
        public int textureID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string textureName;
        public byte surfaceType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibMapEntityMesh
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string className;
        public QLibVec3 center;
        public QLibVec3 boundsMin;
        public QLibVec3 boundsMax;

        public uint totalVertexCount;
        public uint totalIndexCount;
        public uint submeshCount;

        public IntPtr vertices;
        public IntPtr indices;
        public IntPtr submeshes;

        public uint attributeCount;
        public IntPtr attributeKeys;
        public IntPtr attributeValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibMapPointEntity
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string className;
        public QLibVec3 origin;
        public float angle;

        public uint attributeCount;
        public IntPtr attributeKeys;
        public IntPtr attributeValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibMapData
    {
        public uint textureCount;
        public IntPtr textureNames;

        public uint requiredWadCount;
        public IntPtr requiredWads;

        public uint solidEntityCount;
        public IntPtr solidEntities;

        public uint pointEntityCount;
        public IntPtr pointEntities;
    }

    // ============================================================================
    // BSP Structures (from wrapper.h)
    // ============================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibBspSubmesh
    {
        public uint vertexOffset;
        public uint vertexCount;
        public uint indexOffset;
        public uint indexCount;
        public int textureIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string textureName;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibBspEntityMesh
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string className;
        public QLibVec3 center;
        public QLibVec3 boundsMin;
        public QLibVec3 boundsMax;

        public uint totalVertexCount;
        public uint totalIndexCount;
        public uint submeshCount;

        public IntPtr vertices;
        public IntPtr indices;
        public IntPtr submeshes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibBspPointEntity
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string className;
        public QLibVec3 origin;
        public float angle;

        public uint attributeCount;
        public IntPtr attributeKeys;
        public IntPtr attributeValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibBspTexture
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string name;
        public uint width;
        public uint height;
        public uint dataSize;
        public IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibBspData
    {
        public uint version;
        public uint textureCount;
        public uint solidEntityCount;
        public uint pointEntityCount;

        public IntPtr textures;
        public IntPtr solidEntities;
        public IntPtr pointEntities;

        public uint lightmapWidth;
        public uint lightmapHeight;
        public IntPtr lightmapData;
    }

    // ============================================================================
    // WAD Structures (from wrapper.h)
    // ============================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibWadTexture
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string name;
        public uint width;
        public uint height;
        public uint dataSize;
        public IntPtr data;
        public byte isSky;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QLibWadData
    {
        public uint textureCount;
        public IntPtr textures;
    }

    // ============================================================================
    // Helper Delegates
    // ============================================================================

    public static class NativeTools
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void AddStringAnsi([MarshalAs(UnmanagedType.LPStr)] string str);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void AddStringPairAnsi([MarshalAs(UnmanagedType.LPStr)] string first,
            [MarshalAs(UnmanagedType.LPStr)] string second);
    }

    // ============================================================================
    // Extension Methods
    // ============================================================================

    public static class QLibVectorExtensions
    {
        public static UnityEngine.Vector2 ToVector2(this QLibVec2 v) => new UnityEngine.Vector2(v.x, v.y);
        public static UnityEngine.Vector3 ToVector3(this QLibVec3 v) => new UnityEngine.Vector3(v.x, v.y, v.z);
        public static UnityEngine.Vector4 ToVector4(this QLibVec4 v) => new UnityEngine.Vector4(v.x, v.y, v.z, v.w);
    }
}
