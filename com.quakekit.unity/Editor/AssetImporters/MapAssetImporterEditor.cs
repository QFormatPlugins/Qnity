using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Collections.Generic;

namespace QuakeKit
{
    [CustomEditor(typeof(QMapAssetImporter))]
    public class QMapAssetImporterEditor : ScriptedImporterEditor
    {
        private static List<Mesh> _meshesToProcess;
        private static int _currentMeshIndex;
        private static int _progressId;
        private static string _assetPath;
        private static bool _isProcessing;

        public override void OnInspectorGUI()
        {
            // Show default import settings
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lightmap UV Generation", EditorStyles.boldLabel);

            EditorGUI.BeginDisabledGroup(_isProcessing);

            if (GUILayout.Button("Generate Lightmap UVs", GUILayout.Height(30)))
            {
                GenerateLightmapUVs();
            }

            EditorGUI.EndDisabledGroup();

            if (_isProcessing)
            {
                EditorGUILayout.HelpBox("Generating lightmap UVs... Editor remains responsive.", MessageType.Info);
            }
        }

        private void GenerateLightmapUVs()
        {
            var importer = target as QMapAssetImporter;
            if (importer == null) return;

            _assetPath = importer.assetPath;
            _progressId = Progress.Start("Generate Lightmap UVs", "Finding meshes...", Progress.Options.Sticky);

            try
            {
                // Load all assets from the imported file
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(_assetPath);
                _meshesToProcess = new List<Mesh>();

                foreach (var asset in allAssets)
                {
                    if (asset is Mesh mesh && mesh.vertexCount > 3)
                    {
                        _meshesToProcess.Add(mesh);
                    }
                }

                if (_meshesToProcess.Count == 0)
                {
                    Debug.LogWarning("No meshes found to generate UVs for.");
                    Progress.Finish(_progressId, Progress.Status.Failed);
                    return;
                }

                Progress.Report(_progressId, 0f, $"Processing {_meshesToProcess.Count} meshes...");

                // Start processing meshes incrementally
                _currentMeshIndex = 0;
                _isProcessing = true;
                EditorApplication.update += ProcessNextMesh;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error starting lightmap UV generation: {e.Message}");
                Progress.Finish(_progressId, Progress.Status.Failed);
            }
        }

        private static void ProcessNextMesh()
        {
            if (_currentMeshIndex >= _meshesToProcess.Count)
            {
                // All meshes processed, finish up
                EditorApplication.update -= ProcessNextMesh;

                Progress.Report(_progressId, 1f, "Saving changes...");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Progress.Finish(_progressId, Progress.Status.Succeeded);

                _isProcessing = false;
                _meshesToProcess = null;
                return;
            }

            var mesh = _meshesToProcess[_currentMeshIndex];
            float progress = (float)_currentMeshIndex / _meshesToProcess.Count;

            Progress.Report(_progressId, progress, $"Unwrapping mesh {_currentMeshIndex + 1}/{_meshesToProcess.Count}: {mesh.name}");

            // Generate secondary UV set
            UnwrapParam.SetDefaults(out var settings);
            if (!Unwrapping.GenerateSecondaryUVSet(mesh, settings))
            {
                Debug.LogWarning($"Failed to generate UVs for mesh: {mesh.name}");
            }
            else
            {

                EditorUtility.SetDirty(mesh);
            }

            _currentMeshIndex++;
        }

        private void OnDisable()
        {
            // Clean up if editor is closed during processing
            if (_isProcessing)
            {
                EditorApplication.update -= ProcessNextMesh;
                _isProcessing = false;
                _meshesToProcess = null;
            }

            // Required: call base implementation
            base.OnDisable();
        }
    }
}
