using UnityEditor.AssetImporters;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.Events;


namespace Qnity
{
    /// <summary>
    /// Import any files with the .map extension
    /// </summary>
    [ScriptedImporter(1, "map", AllowCaching = true)]
    public sealed class QMapAssetImporter : ScriptedImporter
    {
        public QnityMapConfigData configData;
        private NativeQMap _nativeMap;
        private readonly Dictionary<string, Material> _usedMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, int> _classCount = new Dictionary<string, int>();
        private SolidEntityGenerator _solidEntityGenerator;


        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (ctx.assetPath.Contains("autosave/"))
            {
                return;
            }

            if (configData == null)
            {
                var settings = QnityProjectSettingsData.GetOrCreateSettings();
                configData = settings.GetMapConfigData();
            }

            _solidEntityGenerator = new SolidEntityGenerator(configData);
            Stopwatch stopwatch = Stopwatch.StartNew();

            _nativeMap = new NativeQMap();
            _nativeMap.Load(ctx.assetPath, texName =>
            {
                var tex = MaterialManager.Instance.GetMaterial(texName, configData.textureFolder, configData.materialFolder);
                var tb = new TextureBounds() { Width = 64, Height = 64 };
                if (tex.mainTexture == null) return tb;
                tb.Width = tex.mainTexture.width;
                tb.Height = tex.mainTexture.height;
                return tb;
            });

            _nativeMap.AddTextureToFaceType(configData.clipTexture, QfaceType.Clip);
            _nativeMap.AddTextureToFaceType(configData.skipTexture, QfaceType.Skip);
            _nativeMap.AddTextureToFaceType(configData.skyTexture, QfaceType.NoDraw);

            _nativeMap.Generate();

            var worldSpawnObj = CreateSolidEntityObject(ctx, "worldSpawn", _nativeMap.WorldSpawn);

            foreach (var pent in _nativeMap.PointEntities)
            {
                if (pent?.ClassName == null)
                {
                    continue;
                }
                
                if (!_classCount.TryAdd(pent.ClassName, 0))
                {
                    _classCount[pent.ClassName] += 1;
                }
                
                var entObj = CreatePointEntityObject(ctx, $"{pent.ClassName}_{_classCount[pent.ClassName]}", pent);
                if (entObj != null)
                {
                    entObj.transform.parent = worldSpawnObj.transform;
                }
            }
            
            foreach (var sent in _nativeMap.SolidEntities)
            {
                if (sent == null)
                {
                    continue;
                }

                if (!_classCount.TryAdd(sent.ClassName, 0))
                {
                    _classCount[sent.ClassName] += 1;
                }

                var entObj = CreateSolidEntityObject(ctx, $"{sent.ClassName}_{_classCount[sent.ClassName]}", sent);
                entObj.transform.parent = worldSpawnObj.transform;
            }

            foreach (var mat in _usedMaterials.Values.ToArray())
            {
                ctx.AddObjectToAsset(mat.name, mat);
            }

            var evbus = worldSpawnObj.AddComponent<QnityEventBus>();

            foreach (var c in worldSpawnObj.GetComponentsInChildren<EntityEventReceiver>())
            {
                AddEventToBus(ref evbus, c.targetName, c.OnTrigger);
            }

            foreach (var c in worldSpawnObj.GetComponentsInChildren<EntityEventEmitter>())
            {
                c.SetLocalEventBus(evbus);
            }

            stopwatch.Stop();
            UnityEngine.Debug.Log("parsed " + ctx.assetPath + " in: " + stopwatch.Elapsed);
        }

        private GameObject CreatePointEntityObject(AssetImportContext ctx, string name, QuakePointEntity ent)
        {
            var obj = GetGameObjectForPointEntity(ent);
            if (obj == null) return obj;
            
            obj.name = name;
            ctx.AddObjectToAsset(name, obj);
            return obj;
        }

        private GameObject CreateSolidEntityObject(AssetImportContext ctx, string name, QuakeSolidEntity ent)
        {
            var obj = _solidEntityGenerator.GetGameObjectForSolidEntity(ent);
            obj.name = name;
            ctx.AddObjectToAsset(name, obj);
            var mr = obj.GetComponent<MeshRenderer>();
            var mc = obj.GetComponent<MeshCollider>();
            var mf = obj.GetComponent<MeshFilter>();
            if (mf == null)
            {
                mf = obj.AddComponent<MeshFilter>();
            }

            var materials = new List<Material>();

            var meshes = _solidEntityGenerator.Generate(ref ent, id =>
            {
                var mat = MaterialManager.Instance.GetMaterial(_nativeMap.TextureNames[id], configData.textureFolder, configData.materialFolder);
                _usedMaterials.TryAdd(mat.name, mat);
                materials.Add(mat);
                return true;
            });

            if (mr != null)
            {
                mr.sharedMaterials = materials.ToArray();
            }

            var mesh = new Mesh();
            var combineFilters = new CombineInstance[meshes.Count];

            for (int i = 0; i < meshes.Count; i++)
            {
                combineFilters[i].mesh = meshes[i];
                combineFilters[i].transform = mf.transform.localToWorldMatrix;
            }

            mesh.CombineMeshes(combineFilters, false);
            mesh.name = name + "_mesh";
            if (mf != null)
            {
                mf.sharedMesh = mesh;
            }

            if (mc != null)
            {
                mc.sharedMesh = mesh;
            }

            ctx.AddObjectToAsset(name + "_mesh", mesh);
            return obj;
        }
        
        GameObject GetGameObjectForPointEntity(QuakePointEntity entity)
        {
            foreach (var entry in configData.pointEntities)
            {
                if (entity.ClassName != entry.className || entry.prefab == null) continue;
                var prefab = Instantiate(entry.prefab);
                entry.SetupPrefab(prefab, entity.Attributes, entity.Origin, entity.Angle, configData.inverseScale);
                return prefab;
            }
            return null;
        }

        private void AddEventToBus(ref QnityEventBus evbus, string targetName, UnityAction cb)
        {
            var ev = evbus.FindEvent(targetName);
            if (ev == null)
            {
                var uev = new QnityEventEntry(targetName);
                evbus.eventList.Add(uev);
                UnityEventTools.AddVoidPersistentListener(uev.unityEvent, cb);
                return;
            }

            UnityEventTools.AddVoidPersistentListener(ev, cb);
        }
    }
}