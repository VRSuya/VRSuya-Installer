using UnityEngine;
using UnityEditor;

using VRSuya.Core;
using static VRSuya.Core.Translator;

/*
 * VRSuya Installer
 * Contact : vrsuya@gmail.com // Twitter : https://twitter.com/VRSuya
 */

namespace VRSuya.Installer {

    public class AvatarRebuilderEditor : EditorWindow {

		AvatarRebuilder AvatarRebuilderInstance;
		SerializedObject SerializedAvatarRebuilder;

		SerializedProperty SerializedOldAvatarGameObject;
		SerializedProperty SerializedNewAvatarGameObject;

		SerializedProperty SerializedCanRevert;

		const float BorderX = 30f;

		void Reinitialize() {
			if (!AvatarRebuilderInstance) AvatarRebuilderInstance = CreateInstance<AvatarRebuilder>();
			SerializedAvatarRebuilder = new SerializedObject(AvatarRebuilderInstance);
			SerializedOldAvatarGameObject = SerializedAvatarRebuilder.FindProperty("OldAvatarGameObject");
			SerializedNewAvatarGameObject = SerializedAvatarRebuilder.FindProperty("NewAvatarGameObject");
			SerializedCanRevert = SerializedAvatarRebuilder.FindProperty("CanRevert");
			AvatarRebuilderInstance.OldAvatarGameObject = AvatarUtility.GetAvatarGameObject();
		}

		void OnEnable() {
			Reinitialize();
		}

		[MenuItem("Tools/VRSuya/Installer/AvatarRebuilder", priority = 1000)]
		static void CreateWindow() {
			AvatarRebuilderEditor AppWindow = GetWindowWithRect<AvatarRebuilderEditor>(new Rect(0, 0, 400, 270), true, "VRSuya AvatarRebuilder");
		}

		void OnGUI() {
			if (SerializedAvatarRebuilder == null || !SerializedAvatarRebuilder.targetObject) {
				Reinitialize();
				if (SerializedAvatarRebuilder == null) {
					Close();
					return;
				}
			}
			SerializedAvatarRebuilder.Update();
			EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUIUtility.labelWidth = 100f;
			LanguageIndex = EditorGUILayout.Popup(GetTranslatedString("String_Language"), LanguageIndex, LanguageOption);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.PropertyField(SerializedOldAvatarGameObject, new GUIContent(GetTranslatedString("String_OldAvatar")));
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.PropertyField(SerializedNewAvatarGameObject, new GUIContent(GetTranslatedString("String_NewAvatar")));
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.HelpBox(GetTranslatedString("String_AvatarRebuilder"), MessageType.Warning);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			GUI.backgroundColor = Color.cyan;
			if (GUILayout.Button(GetTranslatedString("String_Replace"), GUILayout.Height(40f))) {
				string ReturnCode = AvatarRebuilderInstance.RequestRebuildAvatar();
				string DialogString = (ReturnCode == "COMPLETED")
					? string.Format(GetTranslatedString("COMPLETED_PATCH"), AvatarRebuilderInstance.OldAvatarGameObject.name)
					: GetTranslatedString(ReturnCode);
				EditorUtility.DisplayDialog("VRSuya AvatarRebuilder",
							DialogString,
							GetTranslatedString("String_Okay")
						);
			}
			GUI.backgroundColor = Color.white;
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			GUI.enabled = SerializedCanRevert.boolValue;
			if (GUILayout.Button(GetTranslatedString("String_Undo"))) {
				AvatarRebuilderInstance.RequestRevertAvatar();
			}
			GUI.enabled = true;
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			SerializedAvatarRebuilder.ApplyModifiedProperties();
		}
	}
}