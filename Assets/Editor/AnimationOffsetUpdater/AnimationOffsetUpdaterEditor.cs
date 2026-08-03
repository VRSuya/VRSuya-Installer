using UnityEditor;
using UnityEngine;

using static VRSuya.Core.Translator;

/*
 * VRSuya Animation Offset Updater Editor for Mogumogu Project
 * Contact : vrsuya@gmail.com // Twitter : https://twitter.com/VRSuya
 */

namespace VRSuya.Installer {

    public class AnimationOffsetUpdaterEditor : EditorWindow {

		AnimationOffsetUpdater AnimationOffsetUpdaterInstance;
		SerializedObject SerializedAnimationOffsetUpdater;

		SerializedProperty SerializedAvatarGameObject;
		SerializedProperty SerializedAvatarAnimationClips;
		SerializedProperty SerializedAnimationStrength;

		const float BorderX = 30f;
		float ButtonWidth = float.NaN;

		void Reinitialize() {
			if (!AnimationOffsetUpdaterInstance) AnimationOffsetUpdaterInstance = CreateInstance<AnimationOffsetUpdater>();
			SerializedAnimationOffsetUpdater = new SerializedObject(AnimationOffsetUpdaterInstance);
			SerializedAvatarGameObject = SerializedAnimationOffsetUpdater.FindProperty("AvatarGameObject");
			SerializedAvatarAnimationClips = SerializedAnimationOffsetUpdater.FindProperty("AvatarAnimationClips");
			SerializedAnimationStrength = SerializedAnimationOffsetUpdater.FindProperty("AnimationStrength");
		}

		void OnEnable() {
			Reinitialize();
		}

		[MenuItem("Tools/VRSuya/Installer/AnimationOffsetUpdater", priority = 1000)]
		static void CreateWindow() {
			AnimationOffsetUpdaterEditor AppWindow = GetWindow<AnimationOffsetUpdaterEditor>(true, "VRSuya AnimationOffsetUpdater");
			AppWindow.minSize = new Vector2(500, 220);
		}

		void OnGUI() {
			if (SerializedAnimationOffsetUpdater == null || !SerializedAnimationOffsetUpdater.targetObject) {
				Reinitialize();
				if (SerializedAnimationOffsetUpdater == null) {
					Close();
					return;
				}
			}
			SerializedAnimationOffsetUpdater.Update();
			Vector2 WindowSize = position.size;
			UpdateRect(WindowSize);
			EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUIUtility.labelWidth = 100f;
			LanguageIndex = EditorGUILayout.Popup(GetTranslatedString("String_Language"), LanguageIndex, LanguageOption);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.PropertyField(SerializedAvatarGameObject, new GUIContent(GetTranslatedString("String_Avatar")));
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.PropertyField(SerializedAvatarAnimationClips, new GUIContent(GetTranslatedString("String_AnimationClip")));
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.PropertyField(SerializedAnimationStrength, new GUIContent(GetTranslatedString("String_Strength")));
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			GUILayout.FlexibleSpace();
			GUI.backgroundColor = Color.cyan;
			if (GUILayout.Button(GetTranslatedString("String_Update"), GUILayout.Width(ButtonWidth * 1.44f), GUILayout.Height(40f))) {
				string StatusCode = AnimationOffsetUpdaterInstance.RequestUpdateAnimationClips();
				EditorUtility.DisplayDialog("VRSuya AnimationOffsetUpdater",
					GetTranslatedString(StatusCode),
					GetTranslatedString("String_Okay")
				);
			}
			GUI.backgroundColor = Color.white;
			GUILayout.FlexibleSpace();
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			SerializedAnimationOffsetUpdater.ApplyModifiedProperties();
		}

		void UpdateRect(Vector2 CurrentWindowSize) {
			ButtonWidth = (CurrentWindowSize.x / 5f);
		}
	}
}