using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Qnity
{
    public enum QuakeEntityType
    {
        Solid = 0,
        Point = 1,
    }
    public class QuakeEntity
    {
        private IntPtr _mapPtr;
        private IntPtr _entityPtr;

        public string ClassName;
        protected IntPtr EntityPtr => _entityPtr;
        public readonly Dictionary<string, string> Attributes = new Dictionary<string, string>();

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetEntityAttributes(
            IntPtr mapPtr, 
            byte type,
            int entIdx, 
            out IntPtr entPtr, 
            NativeTools.AddStringPairAnsi add);
        
        public QuakeEntity(IntPtr nativeMapPtr, int entityIndex, QuakeEntityType entityType)
        {
            _mapPtr = nativeMapPtr;
            GetEntityAttributes(nativeMapPtr, (byte)entityType, entityIndex, out _entityPtr, Attributes.Add);
            Attributes.TryGetValue("classname", out ClassName);
        }
    }

    public class QuakePointEntity : QuakeEntity
    {
        private struct NativePointData
        {
            public QuakeVec3 Origin;
            public float Angle;
        }
        
        private NativePointData _data;
        public Vector3 Origin => _data.Origin.ToVector3();
        public float Angle => _data.Angle;

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern NativePointData GetPointEntityData(IntPtr entPtr);
        
        public QuakePointEntity(IntPtr nativeMapPtr, int entityIndex) : base(nativeMapPtr, entityIndex, QuakeEntityType.Point)
        {
            _data = GetPointEntityData(EntityPtr);
        }
    }

    public class QuakeSolidEntity : QuakeEntity
    {
        private struct NativeSoldData
        {
            public QuakeVec3 Center;
            public QuakeVec3 Min;
            public QuakeVec3 Max;
        }

        private NativeSoldData data;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void AddBrush(int brushIdx, QuakeVec3 min, QuakeVec3 max);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void AddFace(int faceIdx, int textureId, byte type);

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern NativeSoldData GetSolidEntityData(IntPtr entPtr);

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetSolidEntityBrushes(IntPtr ent, AddBrush addFunc);

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetBrushFaces(IntPtr ent, int bruishIndex, AddFace addFunc);

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GetFaceData(
            IntPtr ent,
            int brushIndex,
            int faceIndex,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.Struct, SizeParamIndex = 5)]
            out QuakeVert[] verts,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 6)]
            out UInt16[] indices,
            out int vertCount,
            out int indexCount);

        public List<QuakeBrush> Brushes = new List<QuakeBrush>();
        public readonly Dictionary<int, List<QuakeFace>> FacesByTextureID = new Dictionary<int, List<QuakeFace>>();

        public QuakeSolidEntity(IntPtr nativeMapPtr, int entityIndex) : base(nativeMapPtr, entityIndex, QuakeEntityType.Solid)
        {
            data = GetSolidEntityData(EntityPtr);
            GetSolidEntityBrushes(EntityPtr, (int brushId, QuakeVec3 min, QuakeVec3 max) =>
            {
                var b = new QuakeBrush() { Min = min, Max = max, Faces = new List<QuakeFace>() };
                GetBrushFaces(EntityPtr, brushId, (faceIdx, texId, type) =>
                {
                    var f = new QuakeFace() { TextureID = texId, Type = (QfaceType)type };
                    GetFaceData(EntityPtr, brushId, faceIdx, out f.Vertices, out f.Indices, out var vertCount,
                        out var indexCount);
                    b.Faces.Add(f);
                    if (!FacesByTextureID.ContainsKey(texId))
                    {
                        FacesByTextureID.Add(texId, new List<QuakeFace>());
                    }

                    FacesByTextureID[texId].Add(f);
                });
                Brushes.Add(b);
            });
        }
    }
}