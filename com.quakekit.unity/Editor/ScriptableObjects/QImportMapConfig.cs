using System.Collections.Generic;
using UnityEditor;

namespace QuakeKit
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "QuakeMapConfigData", menuName = "QuakeKit/Configs/MapConfig", order = 1)]
    public class QnityMapConfigData : ScriptableObject
    {

        private const string PackagePath = "Packages/com.quakekit.unity/Assets/";

        [Header("General")]
        public float inverseScale = 24;
        [Header("Lightmapping")]
        [Tooltip("Generate lightmap UVs using Unity's unwrapper. SLOW but enables Unity's lightmapper and GI. Default: false")]
        public bool generateLightmapUVs = false;
        [Header("Materials and Folders")]
        public Material defaultBaseMaterial;
        public string textureFolder = "Assets/Textures/";
        public string materialFolder = "Assets/Materials/";
        [Header("Special Textures")]
        public string clipTexture = "clip";
        public string skipTexture = "skip";
        public string skyTexture = "sky1";
        [Header("Entities")]
        public GameObject defaultSolidObject;
        public GameObject defaultClipObject;
        public GameObject defaultTriggerObject;
        public List<PointEntity> pointEntities;
        public List<SolidEntity> solidEntities;


        void Reset()
        {
            defaultSolidObject = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Default.prefab");
            defaultClipObject = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Clip.prefab");
            defaultTriggerObject = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Trigger.prefab");
            defaultBaseMaterial = MaterialManager.Instance.GetBaseMaterial();

            pointEntities = new List<PointEntity>();
            solidEntities = new List<SolidEntity>();
            pointEntities.Add(new PointEntity
            {
                className = "light",
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Point/PointLight.prefab")
            });
        }
    }
}