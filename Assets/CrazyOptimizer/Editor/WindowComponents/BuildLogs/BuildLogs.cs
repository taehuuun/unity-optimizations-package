using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CrazyGames;
using CrazyGames.TreeLib;
using CrazyGames.WindowComponents.TextureOptimizations;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CrazyOptimizer.Editor.WindowComponents.BuildLogs
{
    public class BuildLogs
    {
        private static MultiColumnHeaderState _multiColumnHeaderState;
        private static BuildLogTree _buildLogTree;
        private static bool _isAnalyzing;
        private static string _errorMessage;
        private static bool _includeFilesFromPackages;

        public static void RenderGUI()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(300));
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            GUILayout.Label("\"빌드 로그 분석\" 버튼을 클릭하십시오. 그러나 이 컴퓨터에서 최소한 한 번은 프로젝트를 빌드했어야 합니다.");
            GUILayout.Label("데이터를 새로 고칠 때 다시 누르십시오.");
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            _buildLogTree?.OnGUI(rect);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(_isAnalyzing ? "분석 중..." : "빌드 로그 분석", GUILayout.Width(200)))
            {
                AnalyzeBuildLogs();
            }

            var originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 160;
            _includeFilesFromPackages = EditorGUILayout.Toggle("패키지의 파일 포함", _includeFilesFromPackages);
            EditorGUIUtility.labelWidth = originalValue;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Editor.log 열기", GUILayout.Width(200)))
            {
                Process.Start("notepad.exe", GetEditorLogPath());
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_errorMessage))
            {
                GUILayout.Label(_errorMessage, new GUIStyle
                {
                    wordWrap = true,
                    normal =
                    {
                        textColor = Color.red
                    }
                });
            }

            EditorGUILayout.Space(5);

            GUILayout.Label(
                "이 도구는 Editor.log 파일의 빌드 리포트를 분석합니다. 이는 최종 빌드에 포함된 모든 파일과 그들이 차지하는 메모리를 보여줍니다. 이 도구를 사용하면 최종 빌드 크기를 줄일 수 있는 더 많은 기회를 발견할 수 있습니다. 여전히 많은 메모리를 차지하는 텍스처, 압축되지 않은 소리 또는 빌드에 포함된 리소스 폴더에 잊혀진 항목이 있을 수 있습니다.",
                EditorStyles.wordWrappedLabel);
        }

        private static string GetEditorLogPath()
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = $@"{localAppDataPath}\Unity\Editor\Editor.log";
            return path;
        }

        /**
         * Return the contents of the Editor.log file.
         */
        private static string GetEditorLog()
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var originalEditorLogPath = GetEditorLogPath();
            var tempEditorLogPath = $@"{localAppDataPath}\Unity\Editor\EditorCrazyGamesTemp.log";
            // original file is blocked, perhaps by Unity editor. Need to copy it and read from the copied file.
            File.Copy(originalEditorLogPath, tempEditorLogPath, true);
            var editorLogStr = File.ReadAllText(tempEditorLogPath);
            File.Delete(tempEditorLogPath);
            return editorLogStr;
        }

        static void AnalyzeBuildLogs()
        {
            _isAnalyzing = true;
            _errorMessage = "";
            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }

            string editorLogStr;
            try
            {
                editorLogStr = GetEditorLog();
            }
            catch (Exception e)
            {
                _errorMessage = "Editor.log 파일을 읽는 데 실패했습니다. 자세한 내용은 콘솔을 확인해주세요.";
                Debug.LogError(e);
                return;
            }

            var buildReportStr = editorLogStr
                .Split(new[] {$"----------------------{Environment.NewLine}Build Report{Environment.NewLine}"}, StringSplitOptions.None).Last();
            if (!buildReportStr.StartsWith("Uncompressed usage by category"))
            {
                _errorMessage =
                    "Editor.log 파일에서 빌드 리포트를 찾지 못했습니다. 이 컴퓨터에서 최근에 프로젝트를 빌드한 것이 확실한지 확인해 주세요. 만약 오류가 계속된다면 언제든지 저희에게 연락해주세요.";
                return;
            }

            // clear the lines until we reach the lines with files and the memory they occupy
            var buildReportLines = buildReportStr.Split(new[] {Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (!buildReportLines[0].StartsWith("Used Assets and files from the Resources folder"))
            {
                buildReportLines.RemoveAt(0);
            }

            buildReportLines.RemoveAt(0);


            // start building the tree with the lines with files from the report
            var treeElements = new List<BuildLogTreeItem>();
            var idIncrement = 0;
            var root = new BuildLogTreeItem("Root", -1, idIncrement, 0, "", 0, "");
            treeElements.Add(root);

            while (!buildReportLines[0].StartsWith("------------"))
            {
                var line = buildReportLines[0];
                buildReportLines.RemoveAt(0);
                // the line has the following format " 0.1 kb	 0.0% Packages/com.unity.timeline/Runtime/Animation/ICurvesOwner.cs"

                idIncrement++;
                var splitLine = line.Replace("\t", " ").Split(new[] {" "}, StringSplitOptions.RemoveEmptyEntries);
                var size = float.Parse(splitLine[0], CultureInfo.InvariantCulture.NumberFormat);
                var sizeUnit = splitLine[1];
                var sizePercentage = float.Parse(splitLine[2].Replace("%", ""), CultureInfo.InvariantCulture.NumberFormat);
                
                // split the original line by percentage ("1.2%"), last part is the path of the asset
                var path = line.Split(new[] {splitLine[2]}, StringSplitOptions.None).Last().Trim();

                if (path.StartsWith("Packages/") && !_includeFilesFromPackages)
                {
                    continue;
                }

                treeElements.Add(new BuildLogTreeItem("BuildLogLine", 0, idIncrement, size, sizeUnit, sizePercentage, path));
            }


            var treeModel = new TreeModel<BuildLogTreeItem>(treeElements);
            var treeViewState = new TreeViewState();
            _multiColumnHeaderState = _multiColumnHeaderState ?? new MultiColumnHeaderState(new[]
            {
                // when adding a new column don't forget to check the sorting method, and the CellGUI method
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "사이즈"}, width = 80, minWidth = 60, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "사이즈 %"}, width = 60, minWidth = 40, canSort = true},
                new MultiColumnHeaderState.Column() {headerContent = new GUIContent() {text = "경로"}, width = 300, minWidth = 200, canSort = true},
            });
            _buildLogTree = new BuildLogTree(treeViewState, new MultiColumnHeader(_multiColumnHeaderState), treeModel);
            _isAnalyzing = false;
            if (OptimizerWindow.EditorWindowInstance != null)
            {
                OptimizerWindow.EditorWindowInstance.Repaint();
            }
        }
    }
}