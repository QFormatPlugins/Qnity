using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Qnity
{
    [StructLayout(LayoutKind.Sequential)]
    public struct QuakeVec4
    {
        [MarshalAs(UnmanagedType.R4)] public float X;
        [MarshalAs(UnmanagedType.R4)] public float Y;
        [MarshalAs(UnmanagedType.R4)] public float Z;
        [MarshalAs(UnmanagedType.R4)] public float W;
        
        public Vector4 ToVector4()
        {
            return new Vector4(X, Y, Z, W);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QuakeVec3
    {
        [MarshalAs(UnmanagedType.R4)] public float X;
        [MarshalAs(UnmanagedType.R4)] public float Y;
        [MarshalAs(UnmanagedType.R4)] public float Z;
        
        public Vector3 ToVector3()
        {
            return new Vector3(X, Y, Z);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QuakeVec2
    {
        [MarshalAs(UnmanagedType.R4)] public float X;
        [MarshalAs(UnmanagedType.R4)] public float Y;
        
        public Vector2 ToVector3()
        {
            return new Vector2(X, Y);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QuakeVert
    {
        public QuakeVec3 Pos;
        public QuakeVec3 Normal;
        public QuakeVec4 Tangent;
        public QuakeVec2 UV;
    };


    public enum QfaceType
    {
        Solid = 0,
        Clip = 1,
        Skip = 2,
        NoDraw = 3,
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct QuakeFace
    {
        public int TextureID;
        public QfaceType Type;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct)]
        public QuakeVert[] Vertices;

        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U2)]
        public UInt16[] Indices;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TextureBounds
    {
        [MarshalAs(UnmanagedType.R4)] public float Width;
        [MarshalAs(UnmanagedType.R4)] public float Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct QuakeBrush
    {
        public bool IsBlockVolume;
        public QuakeVec3 Min;
        public QuakeVec3 Max;
        public List<QuakeFace> Faces;
    }

    public static class NativeTools
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void AddStringAnsi([MarshalAs(UnmanagedType.LPStr)] string str);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void AddStringPairAnsi([MarshalAs(UnmanagedType.LPStr)] string rirst,
            [MarshalAs(UnmanagedType.LPStr)] string second);
    }
}