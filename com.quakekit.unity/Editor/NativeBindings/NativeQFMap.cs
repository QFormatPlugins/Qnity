using System.Runtime.InteropServices;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Qnity
{
    public class NativeQMap
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate TextureBounds GetTextureBounds([MarshalAs(UnmanagedType.LPStr)] string textureName);

        [DllImport("qmap")]
        private static extern IntPtr LoadMap([MarshalAs(UnmanagedType.LPStr)] string mapFile,
            GetTextureBounds getTextureBounds);

        [DllImport("qmap")]
        private static extern IntPtr SetFaceType(
            IntPtr ptr,
            [MarshalAs(UnmanagedType.LPStr)] string mapFile,
            [MarshalAs(UnmanagedType.U1)] byte type);


        [DllImport("qmap")]
        private static extern void DestroyMap(IntPtr ptr);

        [DllImport("qmap", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GenerateGeometry(
            IntPtr ptr, 
            out int outPointEntCount, 
            out int outSolidEntCount,
            bool clipBrushes = true);

        [DllImport("qmap")]
        private static extern void GetTextures(IntPtr ptr, NativeTools.AddStringAnsi add);

        private IntPtr _nativePtr;
        private readonly List<string> _texturesNames = new();
        public List<string> TextureNames => _texturesNames;
        public QuakeSolidEntity WorldSpawn;
        public QuakeSolidEntity[] SolidEntities;
        public QuakePointEntity[] PointEntities;

        public void Load(string mapPath, GetTextureBounds textureBounds)
        {
            _nativePtr = LoadMap(mapPath, textureBounds);
        }

        public void Generate()
        {
            GenerateGeometry(_nativePtr, out var pointEntCount ,out var solidEntCount);
            GetTextures(_nativePtr, _texturesNames.Add);
            
            PointEntities = new QuakePointEntity[pointEntCount];
            for (var i = 0; i < pointEntCount; i++)
            {
                PointEntities[i] = new QuakePointEntity(_nativePtr, i);
            }
            
            WorldSpawn = new QuakeSolidEntity(_nativePtr, 0);
            SolidEntities = new QuakeSolidEntity[solidEntCount - 1];
            for (var i = 0; i < solidEntCount - 2; i++)
            {
                SolidEntities[i] = new QuakeSolidEntity(_nativePtr, i + 1);
            }
        }

        public void AddTextureToFaceType(string textureNames, QfaceType type)
        {
            var texNames = textureNames.Split(';');
            foreach (var t in texNames)
            {
                SetFaceType(_nativePtr, t, (byte)type);
                SetFaceType(_nativePtr, t.ToUpper(), (byte)type);
            }
        }

        ~NativeQMap()
        {
            if (_nativePtr != IntPtr.Zero)
            {
                // DestroyMap(_nativePtr);
            }

            _nativePtr = IntPtr.Zero;
        }
    }
}