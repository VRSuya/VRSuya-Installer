using UnityEditor;
using UnityEngine;

using static VRSuya.Core.Translator;

/*
 * VRSuya Avatar Setting Updater Editor
 * Contact : vrsuya@gmail.com // Twitter : https://twitter.com/VRSuya
 */

namespace VRSuya.Installer {

    [CustomEditor(typeof(AvatarSettingUpdater))]
    public class AvatarSettingUpdaterEditor : Editor {

        public override void OnInspectorGUI() {
			LanguageIndex = EditorGUILayout.Popup(GetTranslatedString("String_Language"), LanguageIndex, LanguageOption);
			EditorGUILayout.Space();
			EditorGUILayout.HelpBox(GetTranslatedString("String_AvatarSettingUpdater"), MessageType.Info);
			if (GUILayout.Button(GetTranslatedString("String_OpenBOOTH"), GUILayout.Height(30f))) {
				Application.OpenURL("https://accounts.booth.pm/orders");
				AvatarSettingUpdater TargetComponent = (AvatarSettingUpdater)target;
				Undo.DestroyObjectImmediate(TargetComponent);
			}
		}
	}
}
