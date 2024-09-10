using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Qnity
{

    public class SolidEntityGenerator
    {
        public delegate bool ProcTextureID(int textureID);
        private readonly QnityMapConfigData _configData;

        public SolidEntityGenerator(QnityMapConfigData configData)
        {
            _configData = configData;
        }
        public List<Mesh> Generate(ref QuakeSolidEntity qent, ProcTextureID onTextureID)
        {
            List<Mesh> meshArray = new();
            foreach (var faceList in qent.FacesByTextureID)
            {
                if (faceList.Value.Count > 0 && faceList.Value[0].Type != QfaceType.Solid)
                {
                    continue;
                }
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var tangents = new List<Vector4>();
                var uvs = new List<Vector2>();
                var indices = new List<int>();
                var offsetIndex = 0;
                onTextureID(faceList.Key);
                foreach (var face in faceList.Value)
                {
                    for (int i = 0; i < face.Vertices.Length; i++)
                    {
                        ref QuakeVert v = ref face.Vertices[i];
                        vertices.Add(new Vector3(-v.Pos.Y, v.Pos.Z, v.Pos.X) / _configData.inverseScale);
                        normals.Add(new Vector3(v.Normal.Y, -v.Normal.Z, -v.Normal.X));
                        tangents.Add(v.Tangent.ToVector4());
                        uvs.Add(new Vector2(v.UV.X, -v.UV.Y));
                    }

                    foreach (var index in face.Indices)
                    {
                        indices.Add(Convert.ToInt32(index + offsetIndex));
                    }
                    offsetIndex += face.Vertices.Length;
                }
                var m = new Mesh();
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.vertices = vertices.ToArray();
                m.uv = uvs.ToArray();
                m.normals = normals.ToArray();
                m.tangents = tangents.ToArray();
                m.SetTriangles(indices, 0);
                meshArray.Add(m);
            }

            return meshArray;
        }

        public GameObject GetGameObjectForSolidEntity(QuakeEntity entity)
        {
            GameObject prefab = null;
            foreach (var entry in _configData.solidEntities)
            {
                if (entity.ClassName != entry.className || entry.prefab == null) continue;
                prefab = Object.Instantiate(entry.prefab);
                SolidEntity.SetupPrefab(prefab, entity.Attributes);
                return prefab;
            }

            if (entity.ClassName.Contains("trigger"))
            {
                prefab = Object.Instantiate(_configData.defaultTriggerObject);
                var emitter = prefab.GetComponent<EntityEventEmitter>();
                if (emitter != null && entity.Attributes.TryGetValue("target", out var attribute))
                {
                    emitter.SetTarget(attribute);
                }
                return prefab;
            }

            return Object.Instantiate(_configData.defaultSolidObject);
        }
    }
}