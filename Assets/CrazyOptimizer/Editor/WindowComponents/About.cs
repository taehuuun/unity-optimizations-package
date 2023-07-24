using UnityEditor;
using UnityEngine;

namespace CrazyGames.WindowComponents
{
    public class About
    {
        public static void RenderGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            GUILayout.Label(
                "이 패키지의 목적은 빌드 크기를 줄이고 성능을 향상시킴으로써 게임 최적화를 도와주는 것입니다. 현재는 WebGL 게임에 대상으로 하고 있습니다.",
                EditorStyles.wordWrappedLabel);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }
}