using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Serialization;

namespace QuakeKit
{
    // Create a new type of Settings Asset.
    class QnityProjectSettingsData : ScriptableObject
    {
        public static string k_QuakeKitProjectSettingsPath = "Assets/QuakeKitProjectSettingsData.asset";
        public static string k_QuakeKitMapConfigDataPath = "Assets/DefaultQuakeKitMapConfigData.asset";
        private const string PackagePath = "Packages/com.quakekit.unity/Assets/";
        
        [SerializeField ]
        private QnityMapConfigData defaultQnityMapConfigData;
        
        public static QnityProjectSettingsData GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<QnityProjectSettingsData>(k_QuakeKitProjectSettingsPath);
            if (settings == null)
            {
                settings = CreateInstance<QnityProjectSettingsData>();
                settings.defaultQnityMapConfigData = CreateInstance<QnityMapConfigData>();
                settings.defaultQnityMapConfigData.defaultSolidObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Default.prefab");
                settings.defaultQnityMapConfigData.defaultClipObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Clip.prefab");
                settings.defaultQnityMapConfigData.defaultTriggerObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Trigger.prefab");
                settings.defaultQnityMapConfigData.defaultBaseMaterial = MaterialManager.Instance.GetBaseMaterial();
                settings.defaultQnityMapConfigData.pointEntities = new List<PointEntity>();
                settings.defaultQnityMapConfigData.solidEntities = new List<SolidEntity>();
                
                settings.defaultQnityMapConfigData.pointEntities.Add(new PointEntity 
                {
                    className = "light",
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Point/PointLight.prefab")
                });
                
                AssetDatabase.CreateAsset(settings.defaultQnityMapConfigData, k_QuakeKitMapConfigDataPath);
                AssetDatabase.CreateAsset(settings, k_QuakeKitProjectSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public QnityMapConfigData GetMapConfigData()
        {
            return defaultQnityMapConfigData;
        }
    
        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }
    
    static class QnitySettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/QnitySettings", SettingsScope.Project)
            {
                label = "QuakeKit",
                guiHandler = (searchContext) =>
                {
                    var settings = QnityProjectSettingsData.GetSerializedSettings();
                    EditorGUILayout.PropertyField(settings.FindProperty("defaultQnityMapConfigData"), new GUIContent("Map Import Config"));
                    settings.ApplyModifiedPropertiesWithoutUndo();
  
                },
    
                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "Map Import Config", "Quake" })
            };
    
            return provider;
        }
    }
    
    // Create MyCustomSettingsProvider by deriving from SettingsProvider:
    class MyCustomSettingsProvider : SettingsProvider
    {
        private SerializedObject m_CustomSettings;
    
        class Styles
        {
            public static GUIContent mapConfig = new GUIContent("QuakeKit Map Config");
        }
    
        public MyCustomSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) {}
    
        public static bool IsSettingsAvailable()
        {
            return File.Exists(QnityProjectSettingsData.k_QuakeKitProjectSettingsPath);
        }
    
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // This function is called when the user clicks on the MyCustom element in the Settings window.
            m_CustomSettings = QnityProjectSettingsData.GetSerializedSettings();
        }
    
        public override void OnGUI(string searchContext)
        {
            // Use IMGUI to display UI:
            EditorGUILayout.PropertyField(m_CustomSettings.FindProperty("m_defaultQnityMapConfigData"), Styles.mapConfig);
            m_CustomSettings.ApplyModifiedPropertiesWithoutUndo();
        }
    
        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            if (!IsSettingsAvailable()) return null;
            var provider = new MyCustomSettingsProvider("Project/MyCustomSettingsProvider", SettingsScope.Project)
            {
                // Automatically extract all keywords from the Styles.
                keywords = GetSearchKeywordsFromGUIContentProperties<Styles>()
            };

            return provider;

            // Settings Asset doesn't exist yet; no need to display anything in the Settings window.
        }
    }
}