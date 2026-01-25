using System.Collections.Generic;

namespace Qnity
{
    using UnityEngine;

    [CreateAssetMenu(fileName = "QnityortMapConfigData", menuName = "Qnityort/Configs/MapConfig", order = 1)]
    public class QnityMapConfigData : ScriptableObject
    {
        [Header("General")]
        public float inverseScale = 24;
        public bool generateLightMapUV;
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
        
        void OnEnable()
        {
            if (defaultBaseMaterial == null)
            {
                solidEntities = new List<SolidEntity>();
            }
        }
    }
}