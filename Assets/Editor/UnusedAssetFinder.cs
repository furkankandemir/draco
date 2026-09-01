using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EntropyOnline.Editor
{
    public class UnusedAssetFinder : EditorWindow
    {
        private List<string> _targetFolders = new List<string>
        {
            "Assets/Polyart",
            "Assets/NewMegaKits",
            "Assets/MonsterSources",
            "Assets/WeaponSources",
            "Assets/NPCSources",
            "Assets/MeshyAI_Models"
        };

        private List<string> _unusedAssets = new List<string>();
        private Vector2 _scrollPos;
        private bool _isAnalyzing = false;
        private string _backupFolderPath = "";

        [MenuItem("Draco Tools/Unused Asset Finder")]
        public static void ShowWindow()
        {
            GetWindow<UnusedAssetFinder>("Unused Asset Finder");
        }

        private void OnEnable()
        {
            // Default backup path is in the project parent directory
            string projectPath = Path.GetFullPath(Application.dataPath);
            string parentPath = Path.GetDirectoryName(Path.GetDirectoryName(projectPath));
            if (!string.IsNullOrEmpty(parentPath))
            {
                _backupFolderPath = Path.Combine(parentPath, "Temp_Unused_Backup");
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Safely find and move unused development assets to speed up Unity", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            GUILayout.Label("Target Folders to Clean:", EditorStyles.boldLabel);
            foreach (var folder in _targetFolders)
            {
                EditorGUILayout.LabelField("- " + folder);
            }

            EditorGUILayout.Space();
            _backupFolderPath = EditorGUILayout.TextField("Backup Folder Path (Outside Assets):", _backupFolderPath);

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Find Unused Assets", GUILayout.Height(30)))
            {
                FindUnused();
            }

            // Show Restore button if the backup folder exists and contains files
            bool hasBackup = Directory.Exists(_backupFolderPath) && Directory.GetFiles(_backupFolderPath, "*.*", SearchOption.AllDirectories).Length > 0;
            GUI.color = new Color(0.9f, 0.6f, 0.6f); // Light red/orange for restore button to stand out
            if (GUILayout.Button("Restore All Moved Assets", GUILayout.Height(30)))
            {
                RestoreAll();
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            if (_unusedAssets.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Found {_unusedAssets.Count} unused assets in the target folders.", MessageType.Info);

                if (GUILayout.Button("Safely Move Unused Assets to Backup Folder", GUILayout.Height(30)))
                {
                    MoveUnused();
                }

                EditorGUILayout.Space();
                GUILayout.Label("Unused Assets List:", EditorStyles.boldLabel);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
                foreach (var asset in _unusedAssets)
                {
                    EditorGUILayout.LabelField(asset);
                }
                EditorGUILayout.EndScrollView();
            }
            else if (_isAnalyzing)
            {
                EditorGUILayout.LabelField("No unused assets found. Ensure target folders contain files.");
            }
        }

        private void FindUnused()
        {
            _unusedAssets.Clear();
            _isAnalyzing = true;

            // 1. Gather all scenes and resources in the project
            var allScenes = new List<string>();
            var allResources = new List<string>();

            // Find all scenes (.unity files)
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
            foreach (var guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/"))
                {
                    allScenes.Add(path);
                }
            }

            // Find absolutely ALL assets inside Assets/Resources/ (recursively)
            // This ensures ScriptableObjects (like KOZoneAsset), Materials, Textures, and AudioClips
            // that are loaded dynamically by code are treated as dependency roots.
            if (Directory.Exists("Assets/Resources"))
            {
                string[] resourceFiles = Directory.GetFiles("Assets/Resources", "*.*", SearchOption.AllDirectories);
                foreach (var file in resourceFiles)
                {
                    string path = file.Replace('\\', '/');
                    if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    allResources.Add(path);
                }
            }

            // 2. Collect all active dependencies recursively
            var activeDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add scenes and their dependencies
            foreach (var scene in allScenes)
            {
                activeDependencies.Add(scene);
                string[] deps = AssetDatabase.GetDependencies(scene, true);
                foreach (var dep in deps)
                {
                    activeDependencies.Add(dep);
                }
            }

            // Add all resources (Prefabs, ZoneAssets, etc.) and their dependencies
            foreach (var resource in allResources)
            {
                activeDependencies.Add(resource);
                string[] deps = AssetDatabase.GetDependencies(resource, true);
                foreach (var dep in deps)
                {
                    activeDependencies.Add(dep);
                }
            }

            // 3. Scan the target folders and find files not in the active dependencies set
            foreach (var folder in _targetFolders)
            {
                if (!Directory.Exists(folder)) continue;

                string[] files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Convert backslashes to forward slashes for Unity compatibility
                    string assetPath = file.Replace('\\', '/');

                    // Skip meta files
                    if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                    // If this file is NOT in the dependencies, it's unused!
                    if (!activeDependencies.Contains(assetPath))
                    {
                        _unusedAssets.Add(assetPath);
                    }
                }
            }
        }

        private void MoveUnused()
        {
            if (_unusedAssets.Count == 0) return;
            if (string.IsNullOrEmpty(_backupFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Please specify a valid backup folder path.", "OK");
                return;
            }

            if (!Directory.Exists(_backupFolderPath))
            {
                Directory.CreateDirectory(_backupFolderPath);
            }

            int movedCount = 0;
            string projectPath = Path.GetFullPath(Application.dataPath); // C:\_dev\knightonline-mobil\Client\Assets
            string clientFolder = Path.GetDirectoryName(projectPath); // C:\_dev\knightonline-mobil\Client

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var assetPath in _unusedAssets)
                {
                    // Full path of the source file inside Client/Assets/...
                    string sourceFullPath = Path.GetFullPath(Path.Combine(clientFolder, assetPath));

                    if (!File.Exists(sourceFullPath)) continue;

                    // Calculate destination path keeping directory structure relative to Assets
                    string relativePath = assetPath.Substring(7); // Remove "Assets/" prefix
                    string destFullPath = Path.Combine(_backupFolderPath, relativePath);

                    string destDir = Path.GetDirectoryName(destFullPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Move file
                    File.Move(sourceFullPath, destFullPath);

                    // Move meta file if it exists
                    string sourceMetaPath = sourceFullPath + ".meta";
                    string destMetaPath = destFullPath + ".meta";
                    if (File.Exists(sourceMetaPath))
                    {
                        File.Move(sourceMetaPath, destMetaPath);
                    }

                    movedCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnusedAssetFinder] Error during file move: {ex.Message}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Success", 
                    $"Successfully moved {movedCount} unused assets outside the project assets folder.\nBackup path: {_backupFolderPath}", 
                    "OK");
                
                _unusedAssets.Clear();
                _isAnalyzing = false;
            }
        }

        private void RestoreAll()
        {
            if (string.IsNullOrEmpty(_backupFolderPath) || !Directory.Exists(_backupFolderPath))
            {
                EditorUtility.DisplayDialog("Error", "Backup folder does not exist or is empty.", "OK");
                return;
            }

            string[] backupFiles = Directory.GetFiles(_backupFolderPath, "*.*", SearchOption.AllDirectories);
            if (backupFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Info", "No files found in the backup folder to restore.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Restore Confirmation", 
                $"Are you sure you want to restore all {backupFiles.Length} files from the backup folder back into the Assets folder?", 
                "Yes, Restore All", "Cancel"))
            {
                return;
            }

            int restoredCount = 0;
            string projectPath = Path.GetFullPath(Application.dataPath); // C:\_dev\knightonline-mobil\Client\Assets

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var file in backupFiles)
                {
                    string fileFullPath = Path.GetFullPath(file);
                    
                    // Calculate relative path from the backup folder root
                    string relativePath = fileFullPath.Substring(_backupFolderPath.Length).TrimStart('\\', '/');
                    
                    // Target destination inside Assets/
                    string destFullPath = Path.Combine(projectPath, relativePath);
                    
                    string destDir = Path.GetDirectoryName(destFullPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    if (File.Exists(fileFullPath))
                    {
                        // Overwrite if exists, but normally it shouldn't exist
                        if (File.Exists(destFullPath))
                        {
                            File.Delete(destFullPath);
                        }
                        File.Move(fileFullPath, destFullPath);
                        restoredCount++;
                    }
                }

                // Clean up backup directory if it's empty
                if (Directory.GetFiles(_backupFolderPath, "*.*", SearchOption.AllDirectories).Length == 0)
                {
                    Directory.Delete(_backupFolderPath, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnusedAssetFinder] Error during restore: {ex.Message}");
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Restore Complete", 
                    $"Successfully restored {restoredCount} assets back to the project Assets folder.", 
                    "OK");
                
                _unusedAssets.Clear();
                _isAnalyzing = false;
            }
        }
    }
}
