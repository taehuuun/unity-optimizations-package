using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrazyGames.TreeLib;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.TextureOptimizations
{
    public class TextureOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static TextureTree _textureCompressionTree;

        private static bool _isAnalyzing;
        private static bool _includeFilesFromPackages;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("\"텍스처 분석\" 버튼을 눌러 테이블을 로드하세요.");
            GUILayout.Label("데이터를 새로고침하려면 다시 누르세요.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _textureCompressionTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_isAnalyzing ? "분석 중..." : "텍스처 분석", GUILayout.Width(200)))
            {
                AnalyzeTextures();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            _includeFilesFromPackages = EditorGUILayout.Toggle("패키지의 파일 포함", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "이 유틸리티는 프로젝트에서 사용되는 텍스처의 개요를 제공합니다. 여러 설정을 최적화함으로써 최종 빌드 크기를 상당히 줄일 수 있습니다. 프로젝트 뷰에서 텍스처를 선택하려면 클릭하세요. 도구가 텍스처를 찾는 방법에 대해 자세히 알아보려면 GitHub 저장소를 확인하세요.",
                EditorStyles.wordWrappedLabel);


            BuildExplanation("최대 크기",
                "텍스처가 게임에서 여전히 좋아 보이는 한 최대한 크기를 줄입니다. Unity에서 설정한 기본값 2048은 아마 필요하지 않을 것입니다.");
            BuildExplanation("압축", "품질을 낮추면 최종 빌드 크기가 줄어듭니다.");
            BuildExplanation("크런치 압축",
                "크런치 압축이 활성화된 모든 텍스처는 함께 압축되어 최종 빌드 크기를 줄입니다.");
            BuildExplanation("크런치 압축 품질", "압축 품질이 높을수록 텍스처 크기가 크고 압축 시간이 더 길어집니다.");
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(
                explanation,
                EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /**
         * Find recursively the textures on which this scene depends.
         */
        static List<string> GetSceneTextureDependencies(string scenePath)
        {
            var textureDependencies = new List<string>();
            var assetDependencies = AssetDatabase.GetDependencies(scenePath, true);
            foreach (var assetDependency in assetDependencies)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(Texture2D))
                {
                    textureDependencies.Add(assetDependency);
                }
            }

            return textureDependencies;
        }

        static List<string> GetUsedTexturesInBuildScenes()
        {
            var usedTexturePaths = new HashSet<string>();

            var scenesInBuild = OptimizerUtils.GetScenesInBuildPath();
            foreach (var scenePath in scenesInBuild)
            {
                var texturesUsedInScene = GetSceneTextureDependencies(scenePath);
                foreach (var texturePath in texturesUsedInScene)
                {
                    usedTexturePaths.Add(texturePath);
                }
            }

            return usedTexturePaths.ToList();
        }

        /**
         * Get the list of textures in the Resources folders, or on which assets from the Resources folder depend.
         */
        static List<string> GetUsedTexturesInResources()
        {
            var usedTexturePaths = new HashSet<string>();
            var allAssetPaths = AssetDatabase.FindAssets("", new[] {"Assets"}).Select(AssetDatabase.GUIDToAssetPath).ToList();

            // keep only the assets inside a Resources folder, that is not inside an Editor folder
            var rx = new Regex(@"\w*(?<!Editor\/)Resources\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            allAssetPaths = allAssetPaths.Where(assetPath => (rx.IsMatch(assetPath))).ToList();

            // find all the textures on which the assets from the Resources folder depend
            foreach (var assetPath in allAssetPaths)
            {
                var assetDependencies = AssetDatabase.GetDependencies(assetPath, true);
                foreach (var assetDependency in assetDependencies)
                {
                    if (AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(Texture2D))
                    {
                        usedTexturePaths.Add(assetDependency);
                    }
                }
            }

            return usedTexturePaths.ToList();
        }

        static void AnalyzeTextures()
        {
            _isAnalyzing = true;
            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }

            var usedTexturePaths = new HashSet<string>();

            GetUsedTexturesInBuildScenes().ForEach(path => usedTexturePaths.Add(path));
            GetUsedTexturesInResources().ForEach(path => usedTexturePaths.Add(path));

            var treeElements = new List<TextureTreeItem>();
            var idIncrement = 0;
            var root = new TextureTreeItem("Root", -1, idIncrement, null, null);
            treeElements.Add(root);

            foreach (var texturePath in usedTexturePaths)
            {
                if (texturePath.StartsWith("Packages/") && !_includeFilesFromPackages)
                {
                    continue;
                }

                idIncrement++;
                try
                {
                    var textureImporter = (TextureImporter) AssetImporter.GetAtPath(texturePath);
                    treeElements.Add(new TextureTreeItem("Texture2D", 0, idIncrement, texturePath, textureImporter));
                }
                catch (Exception)
                {
                    Debug.LogWarning("Failed to analyze texture at path: " + texturePath);
                }
            }

            var treeModel = new TreeModel<TextureTreeItem>(treeElements);
            var treeViewState = new TreeViewState();
            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "텍스처"}, width = 150, minWidth = 150, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "타입"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "최대 크기"}, width = 60, minWidth = 60, canSort = true},
                    new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "압축"}, width = 80, minWidth = 80, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "크런치 압축"}, width = 120, minWidth = 120, canSort = true},
                    new MultiColumnHeaderState.Column()
                        {headerContent = new GUIContent() {text = "크런치 압축 품질"}, width = 128, minWidth = 128, canSort = true},
                });
            _textureCompressionTree = new TextureTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}