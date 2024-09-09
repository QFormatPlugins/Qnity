using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Serialization;

namespace Qnity
{
    // Create a new type of Settings Asset.
    class QnityProjectSettingsData : ScriptableObject
    {
        public static string k_QnityProjectSettingsPath = "Assets/QnityProjectSettingsData.asset";
        public static string k_QnityortMapConfigDataPath = "Assets/DefaultQnityortMapConfigData.asset";
        private const string PackagePath = "Packages/com.qformats.qnity/Assets/";
        
        [FormerlySerializedAs("m_defaultQnityortMapConfigData")] [SerializeField]
        private QnityMapConfigData mDefaultQnityMapConfigData;
        
        public static QnityProjectSettingsData GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<QnityProjectSettingsData>(k_QnityProjectSettingsPath);
            if (settings == null)
            {
                settings = CreateInstance<QnityProjectSettingsData>();
                settings.mDefaultQnityMapConfigData = CreateInstance<QnityMapConfigData>();
                settings.mDefaultQnityMapConfigData.defaultSolidObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Default.prefab");
                settings.mDefaultQnityMapConfigData.defaultClipObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Clip.prefab");
                settings.mDefaultQnityMapConfigData.defaultTriggerObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath + "Prefabs/Solids/Trigger.prefab");
                settings.mDefaultQnityMapConfigData.defaultBaseMaterial = MaterialManager.Instance.GetBaseMaterial();
                settings.mDefaultQnityMapConfigData.pointEntities = new List<PointEntity>();
                settings.mDefaultQnityMapConfigData.solidEntities = new List<SolidEntity>();
                AssetDatabase.CreateAsset(settings.mDefaultQnityMapConfigData, k_QnityortMapConfigDataPath);
                AssetDatabase.CreateAsset(settings, k_QnityProjectSettingsPath);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        public QnityMapConfigData GetMapConfigData()
        {
            return mDefaultQnityMapConfigData;
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
                label = "Qnity",
                guiHandler = (searchContext) =>
                {
                    var settings = QnityProjectSettingsData.GetSerializedSettings();
                    EditorGUILayout.PropertyField(settings.FindProperty("m_defaultQnityortMapConfigData"), new GUIContent("Map Import Config"));
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
            public static GUIContent mapConfig = new GUIContent("Qnityort Map Config");
        }
    
        public MyCustomSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) {}
    
        public static bool IsSettingsAvailable()
        {
            return File.Exists(QnityProjectSettingsData.k_QnityProjectSettingsPath);
        }
    
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // This function is called when the user clicks on the MyCustom element in the Settings window.
            m_CustomSettings = QnityProjectSettingsData.GetSerializedSettings();
        }
    
        public override void OnGUI(string searchContext)
        {
            // Use IMGUI to display UI:
            EditorGUILayout.PropertyField(m_CustomSettings.FindProperty("m_defaultQnityortMapConfigData"), Styles.mapConfig);
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