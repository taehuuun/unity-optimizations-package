using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrazyGames.WindowComponents
{
    public class ExportOptimizations
    {
        public static void RenderGUI()
        {
            if (typeof(PlayerSettings.WebGL).GetProperty("compressionFormat") != null)
            {
                var compressionOk = PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Brotli;
                Action fixCompression = () => { PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli; };
                RenderFixableItem("Brotli 압축", compressionOk, fixCompression, "Project Settings - Player/WebGL - Publishing Settings - Compression Format");
            }

            if (typeof(PlayerSettings.WebGL).GetProperty("nameFilesAsHashes") != null)
            {
                var nameAsHashesOk = PlayerSettings.WebGL.nameFilesAsHashes;
                Action fixNameAsHashes = () => { PlayerSettings.WebGL.nameFilesAsHashes = true; };
                RenderFixableItem("파일명 해시 지정", nameAsHashesOk, fixNameAsHashes,"Project Settings - Player/WebGL - Publishing Settings - Name Files As Hashes");
            }

            if (typeof(PlayerSettings.WebGL).GetProperty("exceptionSupport") != null)
            {
                var exceptionsOk = PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                Action fixExceptions = () => { PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly; };
                RenderFixableItem("예외 지원", exceptionsOk, fixExceptions,
                    "\"수정\" 버튼은 예외 지원을 \"명시적으로 던진 예외만\"으로 설정합니다. 플레이어 설정에서 \"없음\"을 선택하면 성능이 더 좋아지지만, 먼저 개발자 문서를 읽어보세요.\nProject Settings - Player/WebGL - Publishing Settings - Enable Exceptions");
            }

            if (typeof(PlayerSettings).GetProperty("stripEngineCode") != null)
            {
                var stripEngineCodeOk = PlayerSettings.stripEngineCode;
                Action fixStripEngineCode = () => { PlayerSettings.stripEngineCode = true; };
                RenderFixableItem("엔진 코드 제거", stripEngineCodeOk, fixStripEngineCode,
                    "번들 크기를 더 줄이려면 플레이어 설정에서 중간 또는 높은 수준의 제거를 선택할 수 있지만, 먼저 개발자 문서를 읽어보세요.\nProject Settings - Player/WebGL - Other Settings - Optimization/Strip Engine Code");
            }


            if (UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null)
            {
                RenderInfoItem(
                    "URP를 사용하고 있지만 포스트 프로세싱을 사용하지 않는다면, 이를 비활성화하는 것이 좋습니다. 이렇게 하면 최종 빌드 크기가 약 1MB 줄어듭니다. 아래 링크에서 더 자세한 정보를 확인하세요.");
            }

#if UNITY_2021 || UNITY_2022
            // Unity is currently missing an API for accessing the GraphicsSettings preloaded shaders, so these need to be read from a serialized object
            var serializedGraphicsSettings = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
            var preloadedShadersCount = serializedGraphicsSettings.FindProperty("m_PreloadedShaders").arraySize;
            if (preloadedShadersCount > 0)
            {
                RenderInfoItem(
                    "프로젝트에서 " + preloadedShadersCount + "개의 셰이더를 미리 로딩하고 있습니다. WebGL에서는 셰이더를 미리 로딩하면 게임 로딩이 상당히 느려질 수 있습니다.");
            }
#endif


            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("개발자 문서에서 더 많은 팁을 확인하세요"))
            {
                Application.OpenURL("https://developer.crazygames.com/unity-export-tips");
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }


        /// <summary>
        /// Render OK/FAIL, option name, and "Fix" button.
        /// </summary>
        /// <param name="optionName"></param>
        /// <param name="ok">If the export option has the correct value</param>
        /// <param name="fixAction">Is called when the fix button is clicked.</param>
        /// <param name="additionalInfo">If specified, some additional info is displayed below label name</param>
        private static void RenderFixableItem(string optionName, bool ok, Action fixAction, string additionalInfo = null)
        {
            var okStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = Color.green
                }
            };

            var failStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = Color.red
                }
            };

            var labelStyle = new GUIStyle
            {
                normal =
                {
                    textColor = EditorStyles.label.normal.textColor
                }
            };
            var additionalInfoStyle = new GUIStyle
            {
                fontSize = 11,
                wordWrap = true,
                normal =
                {
                    textColor = EditorStyles.label.normal.textColor
                }
            };

            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            if (ok)
                GUILayout.Label("OK", okStyle, GUILayout.Width(35));
            else
                GUILayout.Label("FAIL", failStyle, GUILayout.Width(35));
            GUILayout.Label(optionName, labelStyle);
            GUILayout.FlexibleSpace();

            if (!ok && GUILayout.Button("수정"))
            {
                fixAction();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(additionalInfo))
            {
                GUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(35);
                GUILayout.Label(additionalInfo, additionalInfoStyle);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        private static void RenderInfoItem(string info)
        {
            var infoStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(0.1618f, 0.5568f, 1)
                }
            };
            var labelStyle = new GUIStyle
            {
                wordWrap = true,
                normal =
                {
                    textColor = EditorStyles.label.normal.textColor,
                }
            };

            EditorGUILayout.BeginVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("정보", infoStyle, GUILayout.Width(35));

            GUILayout.Label(info, labelStyle);
            GUILayout.FlexibleSpace();


            EditorGUILayout.EndHorizontal();


            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }
    }
}