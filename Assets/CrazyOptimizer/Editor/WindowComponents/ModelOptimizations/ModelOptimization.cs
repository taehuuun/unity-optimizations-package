using CrazyGames.TreeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrazyGames.WindowComponents.ModelOptimizations
{
    public class ModelOptimization : EditorWindow
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static ModelTree _modelTree;

        private static bool _isAnalyzing;
        private static bool _includeFilesFromPackages;

        private static readonly List<string> _modelFormats = new List<string>() { ".fbx", ".dae", ".3ds", ".dxf", ".obj" };

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("\"모델 분석\" 버튼을 눌러 테이블을 로드하세요.");
            GUILayout.Label("데이터를 새로고침하려면 다시 누르세요.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _modelTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(_isAnalyzing ? "분석 중..." : "모델 분석", GUILayout.Width(200)))
            {
                AnalyzeModels();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            _includeFilesFromPackages = EditorGUILayout.Toggle("패키지의 파일 포함", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            GUILayout.Label(
                "이 도구는 프로젝트에서 사용하는 모델의 개요를 제공합니다. 여러 설정을 최적화하면 최종 빌드 크기를 크게 줄일 수 있습니다. 모델을 선택하려면 클릭하세요. 도구가 모델을 찾는 방법에 대해 자세히 알아보려면 GitHub 리포지토리를 확인하세요.",
                EditorStyles.wordWrappedLabel);

            BuildExplanation("R/W 활성화",
                "메쉬가 읽기/쓰기 가능한 경우, Unity는 메쉬 데이터를 GPU 주소 가능 메모리에 업로드하지만 CPU 주소 가능 메모리에도 유지합니다. 대부분의 경우 이 옵션을 비활성화하여 런타임 메모리 사용량을 줄이는 것이 좋습니다.");
            BuildExplanation("폴리곤 최적화",
                "GPU 내부 캐시를 더 잘 활용하여 렌더링 성능을 향상시키기 위해 메쉬 내 다각형의 순서를 최적화합니다.");
            BuildExplanation("정점 최적화",
                "GPU 내부 캐시를 더 잘 활용하여 렌더링 성능을 향상시키기 위해 메쉬 내 정점의 순서를 최적화합니다.");
            BuildExplanation("메시 압축",
                "메쉬를 압축하면 최종 빌드 크기가 줄어들지만, 압축이 많아질수록 정점 데이터에서 아티팩트가 더 많이 발생합니다.");
            BuildExplanation("애니메이션 압축",
                "애니메이션을 압축하면 최종 빌드 크기가 줄어들지만, 압축이 많아질수록 애니메이션에서 아티팩트가 더 많이 발생합니다.");
        }

        static void BuildExplanation(string label, string explanation)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(130));
            GUILayout.Label(explanation, EditorStyles.wordWrappedLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /**
         * Find recursively the models on which this scene depends.
         */
        static List<string> GetSceneModelDependencies(string scenePath)
        {
            var modelDependencies = new List<string>();
            var assetDependencies = AssetDatabase.GetDependencies(scenePath, true);

            foreach (var assetDependency in assetDependencies)
            {
                if (IsModelAtPath(assetDependency))
                {
                    modelDependencies.Add(assetDependency);
                }
            }

            return modelDependencies;
        }

        static List<string> GetUsedModelsInBuildScenes()
        {
            var usedModelPaths = new HashSet<string>();
            var scenesInBuild = OptimizerUtils.GetScenesInBuildPath();

            foreach (var scenePath in scenesInBuild)
            {
                var modelsUsedInScene = GetSceneModelDependencies(scenePath);

                foreach (var modelPath in modelsUsedInScene)
                {
                    usedModelPaths.Add(modelPath);
                }
            }

            return usedModelPaths.ToList();
        }

        /**
         * Get the list of models in the Resources folders, or on which assets from the Resources folder depend.
         */
        static List<string> GetUsedModelsInResources()
        {
            var usedModelPaths = new HashSet<string>();
            var allAssetPaths = AssetDatabase.FindAssets("", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();

            // keep only the assets inside a Resources folder, that is not inside an Editor folder
            var rx = new Regex(@"\w*(?<!Editor\/)Resources\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            allAssetPaths = allAssetPaths.Where(assetPath => (rx.IsMatch(assetPath))).ToList();

            // find all the models on which the assets from the Resources folder depend
            foreach (var assetPath in allAssetPaths)
            {
                var assetDependencies = AssetDatabase.GetDependencies(assetPath, true);

                foreach (var assetDependency in assetDependencies)
                {
                    if (IsModelAtPath(assetDependency))
                    {
                        string ext = System.IO.Path.GetExtension(assetDependency);
                        usedModelPaths.Add(assetDependency);
                    }
                }
            }

            return usedModelPaths.ToList();
        }

        static bool IsModelAtPath(string assetDependency)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(assetDependency) == typeof(GameObject) &&
                   _modelFormats.Contains(System.IO.Path.GetExtension(assetDependency).ToLowerInvariant());
        }

        static void AnalyzeModels()
        {
            _isAnalyzing = true;

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }

            var usedModelPaths = new HashSet<string>();

            GetUsedModelsInBuildScenes().ForEach(path => usedModelPaths.Add(path));
            GetUsedModelsInResources().ForEach(path => usedModelPaths.Add(path));

            var treeElements = new List<ModelTreeItem>();
            var idIncrement = 0;
            var root = new ModelTreeItem("Root", -1, idIncrement, null, null);
            treeElements.Add(root);

            foreach (var modelPath in usedModelPaths)
            {
                if (modelPath.StartsWith("Packages/") && !_includeFilesFromPackages)
                {
                    continue;
                }

                idIncrement++;

                try
                {
                    var modelImporter = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                    treeElements.Add(new ModelTreeItem("Model", 0, idIncrement, modelPath, modelImporter));
                }
                catch (Exception)
                {
                    Debug.LogWarning("Failed to analyze model at path: " + modelPath);
                }
            }

            var treeModel = new TreeModel<ModelTreeItem>(treeElements);
            var treeViewState = new TreeViewState();

            if (_multiColumnHeaderState == null)
                _multiColumnHeaderState = new MultiColumnHeaderState(new[]
                {
                    // when adding a new column don't forget to check the sorting method, and the CellGUI method
                    new MultiColumnHeaderState.Column() { headerContent = new GUIContent() { text = "모델" }, width = 150, minWidth = 150, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "R/W 활성화" }, width = 80, minWidth = 80, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "폴리곤 최적화" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "정점 최적화" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "메시 압축" }, width = 120, minWidth = 120, canSort = true },
                    new MultiColumnHeaderState.Column()
                        { headerContent = new GUIContent() { text = "애니메이션 압축" }, width = 140, minWidth = 140, canSort = true },
                });

            _modelTree = new ModelTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;

            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}