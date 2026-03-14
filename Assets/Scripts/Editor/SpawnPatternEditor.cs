#if UNITY_EDITOR
using BridgeOfBlood.Data.Enemies;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	[CustomEditor(typeof(SpawnPattern))]
	public class SpawnPatternEditor : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			SpawnPattern pattern = (SpawnPattern)target;
			if (pattern == null) return;

			EditorGUILayout.Space(5);
			if (GUILayout.Button("Open Preview Window"))
				SpawnPatternPreviewWindow.ShowWindow(pattern);
		}
	}
}
#endif
