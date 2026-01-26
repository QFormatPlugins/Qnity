using System;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuakeKit
{

    public class SolidEntityGenerator
    {
        public delegate bool ProcTextureName(string textureName);
        private readonly QnityMapConfigData _configData;

        public SolidEntityGenerator(QnityMapConfigData configData)
        {
            _configData = configData;
        }

        public List<Mesh> Generate(ref MapSolidEntity mapEntity, ProcTextureName onTextureName)
        {
            List<Mesh> meshArray = new();

            foreach (var submesh in mapEntity.Submeshes)
            {
                // Skip non-solid surfaces (SKIP surfaces don't render at all)
                if (submesh.SurfaceType == SurfaceType.SKIP)
                {
                    continue;
                }

                onTextureName(submesh.TextureName);

                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var tangents = new List<Vector4>();
                var uvs = new List<Vector2>();
                var indices = new List<int>();

                // Extract vertices for this submesh
                for (uint i = 0; i < submesh.VertexCount; i++)
                {
                    uint vertexIndex = submesh.VertexOffset + i;
                    ref QLibVertex v = ref mapEntity.Vertices[vertexIndex];

                    // Convert from Quake coordinates to Unity coordinates
                    vertices.Add(new Vector3(-v.pos.y, v.pos.z, v.pos.x) / _configData.inverseScale);
                    normals.Add(new Vector3(v.normal.y, -v.normal.z, -v.normal.x));
                    tangents.Add(v.tangent.ToVector4());
                    uvs.Add(new Vector2(v.uv.x, -v.uv.y));
                }

                // Extract indices for this submesh (relative to submesh vertices)
                for (uint i = 0; i < submesh.IndexCount; i++)
                {
                    uint globalIndex = mapEntity.Indices[submesh.IndexOffset + i];
                    uint localIndex = globalIndex - submesh.VertexOffset;
                    indices.Add(Convert.ToInt32(localIndex));
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

        public GameObject GetGameObjectForSolidEntity(MapSolidEntity entity)
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