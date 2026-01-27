using System.Collections.Generic;
using System.Diagnostics;
using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using Debug = UnityEngine.Debug;


namespace QuakeKit
{
    /// <summary>
    /// Import any files with the .map extension
    /// </summary>
    [ScriptedImporter(1, "map", AllowCaching = true)]
    public sealed class QMapAssetImporter : ScriptedImporter
    {
        public QnityMapConfigData configData;
        private NativeQFMap _nativeMap;
        private readonly Dictionary<string, Material> _usedMaterials = new Dictionary<string, Material>();
        private readonly Dictionary<string, int> _classCount = new Dictionary<string, int>();
        private readonly Dictionary<string, GameObject> _entityFolders = new Dictionary<string, GameObject>();
        private SolidEntityGenerator _solidEntityGenerator;
        private List<string> _requiredWads;


        public override void OnImportAsset(AssetImportContext ctx)
        {
            if (ctx.assetPath.Contains("autosave/"))
            {
                return;
            }

            try
            {
                if (configData == null)
                {
                    var settings = QnityProjectSettingsData.GetOrCreateSettings();
                    configData = settings.GetMapConfigData();
                }

                _solidEntityGenerator = new SolidEntityGenerator(configData);
                Stopwatch stopwatch = Stopwatch.StartNew();

                _nativeMap = new NativeQFMap();

                // Load MAP file with CSG enabled (parse only, no geometry yet)
                _nativeMap.Load(ctx.assetPath, enableCSG: true, convertToOpenGL: false);

                // Get required WADs for texture prioritization
                _requiredWads = _nativeMap.GetRequiredWads();

                // Get all texture names used in the map
                var textureNames = _nativeMap.GetTextureNames();

                // Register texture sizes from Unity materials
                foreach (var textureName in textureNames)
                {
                    // Get material (this will create/load it if needed)
                    var mat = MaterialManager.Instance.GetMaterial(textureName, configData.textureFolder, configData.materialFolder, _requiredWads);

                    if (mat != null && mat.mainTexture != null)
                    {
                        uint width = (uint)mat.mainTexture.width;
                        uint height = (uint)mat.mainTexture.height;
                        _nativeMap.RegisterTextureSize(textureName, width, height);
                    }
                    else
                    {
                        // Fallback to default size if texture not found
                        _nativeMap.RegisterTextureSize(textureName, 64, 64);
                        Debug.LogWarning($"[QMapImporter] Texture '{textureName}' not found, using default size 64x64");
                    }
                }

                // Generate geometry with proper UVs based on registered texture sizes
                _nativeMap.GenerateGeometry();

                // Set surface types for special textures
                _nativeMap.SetFaceTypes(configData.clipTexture, SurfaceType.CLIP);
                _nativeMap.SetFaceTypes(configData.skipTexture, SurfaceType.SKIP);
                _nativeMap.SetFaceTypes(configData.skyTexture, SurfaceType.NODRAW);

                // Export all geometry and entity data
                _nativeMap.ExportData();

                // Create worldspawn GameObject
                GameObject worldSpawnObj = new GameObject("worldSpawn");
                worldSpawnObj.isStatic = true; // Mark as static for lightmapping
                ctx.AddObjectToAsset("worldSpawn", worldSpawnObj);

                // Set worldspawn as the main asset (fixes icon issue)
                ctx.SetMainObject(worldSpawnObj);

                // Process worldspawn geometry (first solid entity)
                if (_nativeMap.SolidEntities.Count > 0)
                {
                    var worldspawnEntity = _nativeMap.SolidEntities[0];

                    var mr = worldSpawnObj.AddComponent<MeshRenderer>();
                    var mf = worldSpawnObj.AddComponent<MeshFilter>();
                    var mc = worldSpawnObj.AddComponent<MeshCollider>();

                    var materials = new List<Material>();
                    // Add mesh components directly to worldSpawnObj
                    var meshes = _solidEntityGenerator.Generate(ref worldspawnEntity, (textureName) =>
                    {
                        var mat = MaterialManager.Instance.GetMaterial(textureName, configData.textureFolder, configData.materialFolder, _requiredWads);
                        _usedMaterials.TryAdd(mat.name, mat);
                        materials.Add(mat);
                        return true;
                    });

                    if (meshes.Count > 0)
                    {

                        mr.sharedMaterials = materials.ToArray();

                        var combinedMesh = new Mesh();
                        var combineFilters = new CombineInstance[meshes.Count];
                        for (int i = 0; i < meshes.Count; i++)
                        {
                            combineFilters[i].mesh = meshes[i];
                            combineFilters[i].transform = mf.transform.localToWorldMatrix;
                        }

                        combinedMesh.CombineMeshes(combineFilters, false);
                        combinedMesh.name = "worldspawn_mesh";

                        // Clear UV2 to ensure clean state on reimport
                        combinedMesh.uv2 = null;

                        mf.sharedMesh = combinedMesh;
                        mc.sharedMesh = combinedMesh;

                        ctx.AddObjectToAsset("worldspawn_mesh", combinedMesh);
                    }
                }

                foreach (var pent in _nativeMap.PointEntities)
                {
                    if (!_classCount.TryAdd(pent.ClassName, 0))
                    {
                        _classCount[pent.ClassName] += 1;
                    }

                    var entObj = CreatePointEntityObject(ctx, $"{pent.ClassName}_{_classCount[pent.ClassName]}", pent);
                    if (entObj != null)
                    {
                        // Get or create the folder GameObject for this entity class
                        if (!_entityFolders.TryGetValue(pent.ClassName, out var folderObj))
                        {
                            folderObj = new GameObject(pent.ClassName);
                            folderObj.transform.parent = worldSpawnObj.transform;
                            _entityFolders[pent.ClassName] = folderObj;
                            ctx.AddObjectToAsset(pent.ClassName + "_folder", folderObj);
                        }

                        entObj.transform.parent = folderObj.transform;
                    }
                }

                // Process remaining solid entities (skip first as it's worldspawn)
                for (int i = 1; i < _nativeMap.SolidEntities.Count; i++)
                {
                    var sent = _nativeMap.SolidEntities[i];

                    if (!_classCount.TryAdd(sent.ClassName, 0))
                    {
                        _classCount[sent.ClassName] += 1;
                    }

                    var entObj = CreateSolidEntityObject(ctx, $"{sent.ClassName}_{_classCount[sent.ClassName]}", sent);

                    // Get or create the folder GameObject for this entity class
                    if (!_entityFolders.TryGetValue(sent.ClassName, out var folderObj))
                    {
                        folderObj = new GameObject(sent.ClassName);
                        folderObj.transform.parent = worldSpawnObj.transform;
                        _entityFolders[sent.ClassName] = folderObj;
                        ctx.AddObjectToAsset(sent.ClassName + "_folder", folderObj);
                    }

                    entObj.transform.parent = folderObj.transform;
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
            }
            catch (Exception ex)
            {
                // Log detailed error information before re-throwing
                Debug.LogError($"[QMapImporter] CRITICAL ERROR during map import: {ex?.Message ?? "Unknown error"}");
                Debug.LogError($"[QMapImporter] Exception type: {ex?.GetType().Name}");
                Debug.LogError($"[QMapImporter] Stack trace:\n{ex?.StackTrace}");

                // Clean up native resources to prevent memory leaks
                try
                {
                    _nativeMap?.Dispose();
                }
                catch (Exception disposeEx)
                {
                    Debug.LogError($"[QMapImporter] Error during cleanup: {disposeEx?.Message}");
                }

                // Create an empty object so Unity doesn't fail completely
                var errorObj = new GameObject("MAP_IMPORT_FAILED");
                ctx.AddObjectToAsset("error", errorObj);
                ctx.SetMainObject(errorObj);

                // Re-throw to show error in Unity console
                throw new Exception($"Failed to import map file {ctx.assetPath}: {ex?.Message}", ex);
            }
            finally
            {
                // Always dispose native resources
                try
                {
                    _nativeMap?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[QMapImporter] Error disposing native resources: {ex?.Message}");
                }
            }
        }

        private GameObject CreatePointEntityObject(AssetImportContext ctx, string name, MapPointEntity ent)
        {
            var obj = GetGameObjectForPointEntity(ent);
            if (obj == null) return obj;

            obj.name = name;
            ctx.AddObjectToAsset(name, obj);
            return obj;
        }

        private GameObject CreateSolidEntityObject(AssetImportContext ctx, string name, MapSolidEntity ent)
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
            var meshes = _solidEntityGenerator.Generate(ref ent, textureName =>
            {
                var mat = MaterialManager.Instance.GetMaterial(textureName, configData.textureFolder, configData.materialFolder, _requiredWads);
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

            // Clear UV2 to ensure clean state on reimport
            // User can regenerate via button if needed
            mesh.uv2 = null;

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

        GameObject GetGameObjectForPointEntity(MapPointEntity entity)
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