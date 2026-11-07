#if MODULAR_AVATAR
using nadena.dev.modular_avatar.core;
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using TrackingType = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType;
using VRSuya.Modular;
using VRSuya.Core;

/*
 * VRSuya Installer — AFK Tools
 * 적용 흐름: SceneInstaller.Apply → Analyzer.Inspect → Analysis.MapMotions → Generator.GenerateMapped.
 * 캐릭터 기본 AFK는 0번, 상품 모션은 프리팹 순서에 따라 배치한다.
 */

namespace VRSuya.Installer.AFKTools
{
    #region 편집기 창

    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "VRSuya.Installer.AFKTools", sourceAssembly: "com.vrsuya.installer.AFKTools.Editor", sourceClassName: "AFKToolsWindow")]
    public sealed class AFKToolsEditor : EditorWindow
    {
        [SerializeField] private GameObject avatar;
        [SerializeField] private List<GameObject> prefabs = new List<GameObject> { null };
        private Vector2 scroll;
        private string error;
        private bool usedSdkDefault;
        private int displayedLanguage;
        private GameObject applied;
        private Texture2D headerLogo;

        [MenuItem("Tools/VRSuya/Installer/AFK Tools", priority = 1000)]
        public static void Open()
        {
            var window = GetWindow<AFKToolsEditor>("AFK Tools");
            window.minSize = new Vector2(440, 520);
        }
        private void OnEnable()
        {
            displayedLanguage = Translator.LanguageIndex;
            headerLogo = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.vrsuya.installer/Icon/VRSuya_Logo.png");
            minSize = new Vector2(440, 520);
            if (prefabs == null || prefabs.Count == 0) prefabs = new List<GameObject> { null };
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(18, 18, 18, 16) }, GUILayout.Height(position.height)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("AFK Tools", new GUIStyle(EditorStyles.boldLabel) { fontSize = 23 }, GUILayout.Height(32), GUILayout.ExpandWidth(true));
                    if (headerLogo)
                        GUI.DrawTexture(GUILayoutUtility.GetRect(32, 32, GUILayout.ExpandWidth(false)), headerLogo, ScaleMode.ScaleToFit);
                }
                Translator.LanguageIndex = EditorGUILayout.Popup(Translator.GetTranslatedString("String_Language"),
                    Translator.LanguageIndex, Translator.LanguageOption);
                if (displayedLanguage != Translator.LanguageIndex)
                {
                    displayedLanguage = Translator.LanguageIndex;
                    error = null;
                }
                GUILayout.Label(AfkText.Intro, EditorStyles.wordWrappedLabel);
                GUILayout.Space(22);
                GUILayout.Label(AfkText.AvatarSection, EditorStyles.boldLabel);
                GUILayout.Label(AfkText.AvatarHint, EditorStyles.miniLabel);
                var nextAvatar = (GameObject)EditorGUILayout.ObjectField(avatar, typeof(GameObject), true, GUILayout.Height(40));
                if (nextAvatar != avatar)
                {
                    ClearMessage();
                    try { avatar = nextAvatar ? SceneInstaller.ResolveAvatar(nextAvatar) : null; }
                    catch (Exception e) { error = e.Message; }
                }
                if (avatar) GUILayout.Label("✓ " + avatar.name, EditorStyles.miniLabel);
                GUILayout.Space(20);
                GUILayout.Label(AfkText.PrefabSection, EditorStyles.boldLabel);
                GUILayout.Label(AfkText.PrefabHint, EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(!avatar))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(75), GUILayout.MaxHeight(position.height), GUILayout.ExpandHeight(true));
                    for (int i = 0; i < prefabs.Count; i++)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label((i + 1).ToString(), GUILayout.Width(22), GUILayout.Height(30));
                        EditorGUI.BeginChangeCheck();
                        prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false, GUILayout.Height(30));
                        if (EditorGUI.EndChangeCheck()) ClearMessage();
                        using (new EditorGUI.DisabledScope(i == 0))
                            if (GUILayout.Button("↑", GUILayout.Width(24), GUILayout.Height(30))) { Swap(i, i - 1); GUIUtility.ExitGUI(); }
                        using (new EditorGUI.DisabledScope(i == prefabs.Count - 1))
                            if (GUILayout.Button("↓", GUILayout.Width(24), GUILayout.Height(30))) { Swap(i, i + 1); GUIUtility.ExitGUI(); }
                        if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(30)))
                        { prefabs.RemoveAt(i); if (prefabs.Count == 0) prefabs.Add(null); ClearMessage(); GUIUtility.ExitGUI(); }
                    }
                    var drop = GUILayoutUtility.GetRect(100, 45, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    GUI.Box(drop, AfkText.DropPrefabs);
                    HandleDrop(drop);
                    EditorGUILayout.EndScrollView();
                    if (GUILayout.Button(AfkText.AddPrefab, GUILayout.Height(27))) { prefabs.Add(null); ClearMessage(); GUIUtility.ExitGUI(); }
                }
                GUILayout.Space(12);
                if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
                if (applied)
                {
                    var message = string.Format(AfkText.Applied, avatar.name);
                    if (usedSdkDefault) message += "\n" + AfkText.UsedDefault;
                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }
                GUILayout.Label(AfkText.UndoHint, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Label(AfkText.OutputFolderHint, EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(8);
                using (new EditorGUI.DisabledScope(!avatar || !prefabs.Any(p => p) || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button(Translator.GetTranslatedString("String_Apply"), GUILayout.Height(44))) { Apply(); GUIUtility.ExitGUI(); }
                if (applied && GUILayout.Button(AfkText.SelectResult, EditorStyles.linkLabel)) Selection.activeGameObject = applied;
            }
        }
        private void OnInspectorUpdate()
        {
            if (displayedLanguage != Translator.LanguageIndex) Repaint();
        }
        private void ClearMessage() { error = null; applied = null; usedSdkDefault = false; }
        private void Swap(int a, int b) { var old = prefabs[a]; prefabs[a] = prefabs[b]; prefabs[b] = old; ClearMessage(); }
        private void HandleDrop(Rect rect)
        {
            var e = Event.current;
            if (!avatar || !rect.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
            var incoming = DragAndDrop.objectReferences.OfType<GameObject>().Where(p => EditorUtility.IsPersistent(p) && AssetDatabase.GetAssetPath(p).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)).ToArray();
            DragAndDrop.visualMode = incoming.Length > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (e.type == EventType.DragPerform && incoming.Length > 0)
            {
                DragAndDrop.AcceptDrag();
                foreach (var obj in incoming)
                {
                    if (prefabs.Contains(obj)) continue;
                    int empty = prefabs.FindIndex(p => !p);
                    if (empty >= 0) prefabs[empty] = obj; else prefabs.Add(obj);
                }
                ClearMessage(); e.Use(); GUIUtility.ExitGUI();
            }
            e.Use();
        }
        private void Apply()
        {
            ClearMessage();
            try
            {
                EditorUtility.DisplayProgressBar("AFK Tools", AfkText.Applying, .5f);
                var result = SceneInstaller.Apply(avatar, prefabs);
                applied = result.instance;
                usedSdkDefault = result.defaultFallbackReason != null;
                Selection.activeGameObject = applied;
            }
            catch (Exception e) { error = e.Message; Debug.LogWarning("[AFK Tools] " + e.Message); }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }

    #endregion

    // Core Translator owns the shared language selection (English, Korean, Japanese).
    internal static class AfkText
    {
        private static string InLanguage(string english, string korean, string japanese)
        {
            switch (Translator.LanguageIndex)
            {
                case 1: return korean;
                case 2: return japanese;
                default: return english;
            }
        }

        internal static string Intro => InLanguage(
            "Add your avatar and AFK prefabs, then click Apply.",
            "캐릭터와 AFK를 넣고 적용하세요.",
            "アバターとAFKプレハブを追加して「適用」を押してください。");
        internal static string AvatarSection => InLanguage(
            "1. Avatar",
            "1. 캐릭터",
            "1. アバター");
        internal static string AvatarHint => InLanguage(
            "Drag your avatar here from the Hierarchy.",
            "Hierarchy에서 끌어다 놓으세요.",
            "Hierarchyからアバターをドラッグしてください。");
        internal static string PrefabSection => InLanguage(
            "2. AFK prefabs",
            "2. AFK 프리팹",
            "2. AFKプレハブ");
        internal static string PrefabHint => InLanguage(
            "Add prefabs from the Project window in the order you want them to appear.",
            "Project에서 사용할 프리팹을 순서대로 넣으세요.",
            "Projectから、表示したい順番にプレハブを追加してください。");
        internal static string DropPrefabs => InLanguage(
            "+ Drop prefabs here",
            "+ 프리팹을 여기에 놓기",
            "+ ここにプレハブをドロップ");
        internal static string AddPrefab => InLanguage(
            "+ Add prefab",
            "+ 프리팹 추가",
            "+ プレハブを追加");
        internal static string UndoHint => InLanguage(
            "Apply replaces the existing AFK installation. Press Ctrl+Z to undo.",
            "기존 AFK는 통합 결과로 교체됩니다. Ctrl+Z로 되돌릴 수 있습니다.",
            "既存のAFKは統合したAFKに置き換わります。Ctrl+Zで元に戻せます。");
        internal static string OutputFolderHint => InLanguage(
            "The combined prefab and its assets are saved in " + BuildSettings.DefaultOutputFolder + ".",
            "통합 결과물은 " + BuildSettings.DefaultOutputFolder + " 폴더에 저장됩니다.",
            "統合したプレハブと関連アセットは " + BuildSettings.DefaultOutputFolder + " フォルダーに保存されます。");
        internal static string SelectResult => InLanguage(
            "Select applied AFK",
            "적용된 AFK 선택",
            "適用したAFKを選択");
        internal static string Applying => InLanguage(
            "Applying AFK to the avatar…",
            "캐릭터에 AFK를 적용하고 있습니다…",
            "アバターにAFKを適用しています…");
        internal static string Applied => InLanguage(
            "Applied AFK to {0}.",
            "{0}에 AFK를 적용했습니다.",
            "{0}にAFKを適用しました。");
        internal static string UsedDefault => InLanguage(
            "VRChat’s default AFK is used for option 0.",
            "0번에는 VRChat 기본 AFK를 사용했습니다.",
            "0番にはVRChat標準のAFKを使用しました。");
        internal static string NeedSceneAvatar => InLanguage(
            "Choose an avatar from the Hierarchy.",
            "Hierarchy에 있는 캐릭터를 넣어주세요.",
            "Hierarchyからアバターを選択してください。");
        internal static string AvatarNotFound => InLanguage(
            "No VRChat avatar was found on this object or its parents.",
            "이 오브젝트에서 VRChat 캐릭터를 찾지 못했습니다.",
            "このオブジェクトとその親からVRChatアバターを見つけられませんでした。");
        internal static string NeedHumanoid => InLanguage(
            "Choose an avatar with a Humanoid rig.",
            "Humanoid로 설정된 캐릭터를 넣어주세요.",
            "Humanoidに設定したアバターを選択してください。");
        internal static string PrefabStage => InLanguage(
            "Choose an avatar in the scene. Exit Prefab Mode first.",
            "프리팹 편집 화면이 아닌 씬의 캐릭터를 넣어주세요.",
            "プレハブモードを終了し、シーン内のアバターを選択してください。");
        internal static string ExitPlayMode => InLanguage(
            "Exit Play Mode before applying.",
            "Play 모드를 종료한 뒤 적용해주세요.",
            "再生モードを終了してから適用してください。");
        internal static string NeedPrefab => InLanguage(
            "Add at least one AFK prefab.",
            "AFK 프리팹을 하나 이상 넣어주세요.",
            "AFKプレハブを1つ以上追加してください。");
        internal static string DuplicateInput => InLanguage(
            "The same prefab appears twice. Keep one copy.",
            "같은 프리팹이 두 번 들어 있습니다. 하나만 남겨주세요.",
            "同じプレハブが重複しています。1つだけ残してください。");
        internal static string ProjectPrefab => InLanguage(
            "Drag prefabs from the Project window into the AFK list.",
            "AFK 칸에는 Project 창의 프리팹을 넣어주세요.",
            "ProjectウィンドウからAFK欄にプレハブを追加してください。");
        internal static string ModifiedComponents => InLanguage(
            "Objects or components have been added to or removed from {0}. Resolve these changes before applying.",
            "{0}에 별도로 추가하거나 제거한 오브젝트/컴포넌트가 있습니다. 이 설치를 정리한 뒤 적용해주세요.",
            "{0}には追加または削除されたオブジェクトやコンポーネントがあります。変更を整理してから適用してください。");
        internal static string SceneOverrides => InLanguage(
            "{0} has scene overrides. Save your changes as a separate prefab and add that prefab.",
            "{0}에 씬에서 수정한 설정이 있습니다. 수정 내용을 별도 프리팹으로 저장한 뒤 그 프리팹을 넣어주세요.",
            "{0}にはシーン上で変更した設定があります。変更を別のプレハブとして保存し、そのプレハブを追加してください。");
        internal static string TransformOverrides => InLanguage(
            "The position, rotation or scale of {0} was changed in the scene. Save the changes as a separate prefab and add it.",
            "{0}의 위치·회전·크기가 씬에서 변경되어 있습니다. 설정을 별도 프리팹으로 저장한 뒤 그 프리팹을 넣어주세요.",
            "{0}の位置・回転・スケールがシーン上で変更されています。変更を別のプレハブとして保存し、そのプレハブを追加してください。");
        internal static string ConflictingInstallation => InLanguage(
            "Cannot safely combine the avatar’s '{0}' with AFK. If it is an AFK prefab, add it to the list. Otherwise detach that installation.",
            "캐릭터의 '{0}'이 AFK와 함께 작동하는 구성을 확인할 수 없습니다. 함께 쓸 AFK 프리팹이면 목록에 추가하고, 그 외에는 해당 설치를 분리해주세요.",
            "アバターの「{0}」とAFKの併用を確認できません。AFKプレハブなら一覧に追加し、それ以外なら該当する設定を切り離してください。");
        internal static string MultipleInstallations => InLanguage(
            "Several combined AFK installations were found. Keep one before applying.",
            "이전에 합친 AFK가 여러 개 있습니다. 하나만 남긴 뒤 적용해주세요.",
            "統合済みのAFKが複数あります。1つだけ残してから適用してください。");
        internal static string NameInUse => InLanguage(
            "The avatar already has a child named '{0}'. Rename that object.",
            "캐릭터 아래의 '{0}' 이름이 이미 사용 중입니다. 해당 오브젝트 이름을 바꿔주세요.",
            "アバター直下に「{0}」というオブジェクトがあります。そのオブジェクトの名前を変更してください。");
        internal static string UndoApply => InLanguage(
            "Apply AFK",
            "AFK 적용",
            "AFKを適用");
        internal static string InstallFailed => InLanguage(
            "Could not add the combined AFK to the avatar.",
            "통합 AFK를 캐릭터에 넣지 못했습니다.",
            "統合したAFKをアバターに追加できませんでした。");
        internal static string NeedMotionSource => InLanguage(
            "Add at least one prefab to import motions from.",
            "모션을 가져올 프리팹을 하나 이상 추가하세요.",
            "モーションの読み込み元となるプレハブを1つ以上追加してください。");
        internal static string DuplicatePrefab => InLanguage(
            "The same prefab was added more than once.",
            "동일한 프리팹이 중복되었습니다.",
            "同じプレハブが複数回追加されています。");
        internal static string EmptySlot => InLanguage(
            "An input prefab slot is empty.",
            "비어 있는 입력 프리팹 슬롯이 있습니다.",
            "入力プレハブの欄に空きがあります。");
        internal static string NeedDefaultSource => InLanguage(
            "Choose an avatar or an avatar-specific default prefab for option 0.",
            "0번 기본 AFK를 읽을 캐릭터 또는 캐릭터용 기본 프리팹을 지정하세요.",
            "0番のAFKを読み込むアバター、またはそのアバター用のデフォルトプレハブを指定してください。");
        internal static string DefaultMismatch => InLanguage(
            ": The motion for option 0 does not match the SDK default AFK.",
            ": 0번 모션이 SDK 기본 AFK와 일치하지 않습니다.",
            "：0番のモーションがSDK標準のAFKと一致しません。");
        internal static string MixedAvatars => InLanguage(
            "The list contains prefabs made for different avatars.",
            "서로 다른 캐릭터용 프리팹이 섞여 있습니다.",
            "異なるアバター用のプレハブが混在しています。");
        internal static string SelectMotion => InLanguage(
            "Select at least one motion to include.",
            "출력할 모션을 하나 이상 선택하세요.",
            "出力するモーションを1つ以上選択してください。");
        internal static string InvalidIds => InLanguage(
            "TypeAFK values must be unique and between 1 and 255. Option 0 is the avatar’s default AFK.",
            "TypeAFK 번호는 중복 없는 1~255여야 합니다. 0은 캐릭터 기본 AFK입니다.",
            "TypeAFKには1～255の重複しない番号を指定してください。0番はアバターのデフォルトAFKです。");
        internal static string NeedMenuLabel => InLanguage(
            "Enter a menu label.",
            "메뉴 이름을 입력하세요.",
            "メニュー名を入力してください。");
        internal static string InvalidInitialType => InLanguage(
            "The initial selection is not among the enabled motions.",
            "초기 선택 번호가 활성 모션에 없습니다.",
            "初期選択の番号に対応する有効なモーションがありません。");
        internal static string InvalidFolder => InLanguage(
            "Choose a valid output folder under Assets/.",
            "출력 폴더는 Assets/ 아래의 올바른 경로여야 합니다.",
            "Assets/配下の有効な出力フォルダーを指定してください。");
        internal static string InvalidPrefabName => InLanguage(
            "Enter a valid name for the output prefab.",
            "올바른 출력 프리팹 이름을 입력하세요.",
            "出力プレハブに有効な名前を指定してください。");
        internal static string NeedMenuName => InLanguage(
            "Enter a name for the combined menu.",
            "통합 메뉴 이름을 입력하세요.",
            "統合メニューの名前を入力してください。");
        internal static string KeepMaskRemoval => InLanguage(
            "Advanced: Keep the source RemoveFXMask component. This affects every FX mask on the avatar.",
            "고급 설정: 원본 RemoveFXMask 기능을 유지합니다. 아바타의 모든 FX 마스크에 영향을 줍니다.",
            "詳細設定：元のRemoveFXMaskを保持します。アバターのすべてのFXマスクに影響します。");
        internal static string OmitMaskRemoval => InLanguage(
            "RemoveFXMask is excluded from the output. If the avatar’s FX uses masks, check the result on that avatar.",
            "원본의 RemoveFXMask는 출력에서 제외합니다. 캐릭터 기본 FX에 마스크가 있다면 대상 아바타에서 반드시 확인하세요.",
            "出力からRemoveFXMaskを除外します。アバターのFXでマスクを使用している場合は、実際のアバターで結果を確認してください。");
        internal static string NeedDescriptor => InLanguage(
            "The validation avatar needs a VRC Avatar Descriptor.",
            "확인용 아바타에는 VRC Avatar Descriptor가 필요합니다.",
            "検証用アバターにはVRC Avatar Descriptorが必要です。");
        internal static string ExistingReplace => InLanguage(
            "The avatar already has an Action Replace installation. Replace the existing AFK installation when installing the result.",
            "대상 아바타에 Action Replace 설치가 이미 있습니다. 통합 결과를 설치할 때 기존 AFK 설치를 대체해야 합니다.",
            "アバターにはすでにAction Replace設定があります。統合結果を導入する際に既存のAFKを置き換えてください。");
        internal static string StructuralCheck => InLanguage(
            "This check verifies the avatar’s structure. Test playback in VRChat after applying.",
            "대상 아바타 검사는 구조 확인입니다. 이 창에서 아바타를 수정하거나 VRChat 재생을 검증하지 않습니다.",
            "ここではアバターの構造を確認します。適用後にVRChatで再生を確認してください。");
        internal static string ReplaceOriginal => InLanguage(
            "Replace the existing AFK with the generated prefab. Installing both causes an Action Replace conflict.",
            "결과 프리팹은 기존 AFK 설치를 대체해서 사용하세요. 원본과 결과를 함께 넣으면 Action Replace가 충돌합니다.",
            "既存のAFKを生成したプレハブに置き換えてください。両方を入れるとAction Replaceが競合します。");
        internal static string DefaultWarning => InLanguage(
            "Using the VRChat default AFK for option 0. ",
            "0번에 VRChat 기본 AFK를 사용했습니다. ",
            "0番にはVRChat標準のAFKを使用しました。 ");
        internal static string NeedProjectAsset => InLanguage(
            "Choose a prefab asset from the Project window.",
            "Project 창의 프리팹 에셋을 넣으세요.",
            "Projectウィンドウからプレハブアセットを指定してください。");
        internal static string MissingScript => InLanguage(
            "Missing script on object: ",
            "누락된 스크립트가 있습니다: ",
            "スクリプトが見つからないオブジェクト： ");
        internal static string UnsupportedInstallLayout => InLanguage(
            "Use exactly one Action installation and only FX installations alongside it.",
            "Action 설치 1개와 FX 설치만 있는 규격을 지원합니다.",
            "Action設定は1つ、そのほかはFX設定のみの構成に対応しています。");
        internal static string UnsupportedFxLayout => InLanguage(
            "Use one combined FX, or an Absolute FX for the avatar and a Relative FX for props.",
            "통합 FX 1개 또는 캐릭터용 Absolute FX와 소품용 Relative FX 한 쌍이 필요합니다.",
            "統合FXを1つ、またはアバター用のAbsolute FXと小物用のRelative FXを1つずつ指定してください。");
        internal static string UnsupportedMAParameter => InLanguage(
            "Renamed, internal or prefix-based MA parameters require a separate adapter.",
            "이름 변경 / 내부 / 접두사 MA 파라미터는 별도 어댑터가 필요합니다.",
            "名前変更、内部または接頭辞を使うMAパラメーターには専用の変換処理が必要です。");
        internal static string SplitFxPaths => InLanguage(
            "Separate FX installations require one Absolute and one Relative FX.",
            "분리된 FX는 Absolute와 Relative가 하나씩 필요합니다.",
            "分割したFXにはAbsoluteとRelativeが1つずつ必要です。");
        internal static string SyncedBehaviour => InLanguage(
            "{0}/{1}: Cannot preserve the synced layer’s Behaviour override.",
            "{0}/{1}: 동기화 레이어의 Behaviour override를 보존할 수 없습니다.",
            "{0}/{1}：同期レイヤーのBehaviourオーバーライドを保持できません。");
        internal static string EntryMismatch => InLanguage(
            "Action and FX use different AFK entry conditions.",
            "Action과 FX의 AFK 진입 조건이 다릅니다.",
            "ActionとFXでAFKの開始条件が異なります。");
        internal static string BranchMismatch => InLanguage(
            "Action and FX have different TypeAFK branches.",
            "Action과 FX의 TypeAFK 분기가 다릅니다.",
            "ActionとFXでTypeAFKの分岐が異なります。");
        internal static string MaskedLayer => InLanguage(
            "Input layers with an AvatarMask require separate review.",
            "입력 레이어에 AvatarMask가 있는 변형은 별도 검토가 필요합니다.",
            "入力レイヤーにAvatarMaskがある構成は個別の確認が必要です。");
        internal static string UnsupportedBehaviour => InLanguage(
            "Unsupported Animator Behaviour: ",
            "지원하지 않는 Animator Behaviour: ",
            "未対応のAnimator Behaviour： ");
        internal static string AnimationEvents => InLanguage(
            "This clip contains Animation Events. Check that they will not run twice.",
            "Animation Event가 있는 클립은 중복 실행 방지 검토가 필요합니다.",
            "Animation Eventを含むクリップがあります。イベントが二重に実行されないか確認してください。");
        internal static string ExtraStateMachine => InLanguage(
            "A child state machine outside the AFK branches was found.",
            "AFK 분기 외의 하위 상태 머신이 있습니다.",
            "AFK分岐以外のサブステートマシンがあります。");
        internal static string InvalidRecipe => InLanguage(
            "Unsupported Recipe format.",
            "지원하지 않는 Recipe 형식입니다.",
            "未対応のRecipe形式です。");
        internal static string SourceChanged => InLanguage(
            "Settings or source assets have changed. Run the analysis again.",
            "설정 또는 원본 에셋이 변경되었습니다. 다시 분석하세요.",
            "設定または元のアセットが変更されました。もう一度解析してください。");
        internal static string DuplicateObjectPath => InLanguage(
            "Product object paths overlap: ",
            "상품 오브젝트 경로가 겹칩니다: ",
            "商品のオブジェクトパスが重複しています： ");
        internal static string ParameterConflict => InLanguage(
            "Conflicting MA parameter settings: ",
            "MA 파라미터 설정 충돌: ",
            "MAパラメーター設定が競合しています： ");
        internal static string SaveFailed => InLanguage(
            "Could not save the prefab.",
            "프리팹 저장에 실패했습니다.",
            "プレハブを保存できませんでした。");
        internal static string DefaultLabel => InLanguage(
            "Default AFK",
            "기본 AFK",
            "デフォルトAFK");
        internal static string NextPage => InLanguage(
            "Next",
            "다음",
            "次へ");
        internal static string InvalidMergeCount => InLanguage(
            "Unexpected number of MA installations on the combined prefab.",
            "중앙 MA 설치 개수가 잘못되었습니다.",
            "統合プレハブのMA設定数が正しくありません。");
        internal static string ExternalGraphReference => InLanguage(
            "The output graph still refers to a source or unsaved object: ",
            "출력 그래프에 원본 또는 저장되지 않은 참조가 남았습니다: ",
            "出力グラフに元のオブジェクトまたは未保存の参照が残っています： ");
        internal static string Severity(IssueLevel level)
        {
            switch (level)
            {
                case IssueLevel.Warning: return InLanguage("Warning", "경고", "警告");
                case IssueLevel.Error: return InLanguage("Error", "오류", "エラー");
                default: return InLanguage("Info", "안내", "情報");
            }
        }

        internal static string ReportTitle => InLanguage(
            "# VRSuya AFK Tools generation report\n\n",
            "# VRSuya AFK Tools 생성 보고서\n\n",
            "# VRSuya AFK Tools 生成レポート\n\n");
        internal static string ReportTime => InLanguage(
            "Generated: ",
            "생성 시간: ",
            "生成日時： ");
        internal static string ReportOutput => InLanguage(
            "\nOutput: `{0}`\n\nThe generated prefab uses the original models, materials, textures, VRSuya Installer and Modular Avatar. It adds no tool-specific runtime component. Apply installs it on the selected scene avatar.",
            "\n출력: `{0}`\n\n원본 상품 에셋은 수정하지 않습니다. 생성물은 원본 모델·마테리얼·텍스처 및 기존 VRSuya Installer/MA에 의존합니다. 툴 전용 런타임 컴포넌트는 없습니다. 적용 버튼은 생성 후 선택한 씬 캐릭터에 결과를 설치합니다.",
            "\n出力先：`{0}`\n\n生成したプレハブは元のモデル・マテリアル・テクスチャ、VRSuya Installer、Modular Avatarを使用します。ツール専用のランタイムコンポーネントは追加しません。「適用」で選択中のシーンアバターに導入します。");
        internal static string ReportMotions => InLanguage(
            "\n## Motions\n\n| TypeAFK | Menu |\n|---|---|\n| 0 | Default AFK ({0}) |",
            "\n## 모션\n\n| TypeAFK | 메뉴 |\n|---|---|\n| 0 | 기본 AFK ({0}) |",
            "\n## モーション\n\n| TypeAFK | メニュー |\n|---|---|\n| 0 | デフォルトAFK ({0}) |");
        internal static string ReportInstallation => InLanguage(
            "\n## Installation\n\nApply places the combined prefab under the avatar and replaces the recognized AFK installation. Ctrl+Z undoes the scene change. Generated assets are kept.\n\nKeep the output root name and Modules hierarchy. Run an NDMF test build. In VRChat, check the default and each motion, early exit, switching and remote sync.",
            "\n## 적용\n\n적용 버튼은 통합 프리팹을 캐릭터 아래에 넣고 확인된 기존 AFK 설치를 교체합니다. Ctrl+Z로 씬 변경을 되돌릴 수 있습니다. 생성 에셋은 남습니다.\n\n결과 루트 이름 및 Modules 계층을 유지하세요. NDMF 테스트 빌드와 VRChat에서 기본/각 모션/중도 해제/모션 전환/원격 동기화를 확인하세요.",
            "\n## 適用\n\n「適用」は統合プレハブをアバターの子に配置し、認識した既存のAFKを置き換えます。Ctrl+Zでシーンの変更を元に戻せます。生成したアセットは残ります。\n\nルート名とModulesの階層を維持してください。NDMFテストビルドを行い、VRChatでデフォルトと各モーション、途中解除、切り替え、リモート同期を確認してください。");
        internal static string ReportValidation => InLanguage(
            "\n## Validation\n\nSaved graph references and installation counts passed validation. Test playback and upload separately. More-AFK is supported for the Phone setup.",
            "\n## 검증 범위\n\n저장된 그래프 참조와 설치 개수 검사는 통과했습니다. 이 생성은 실제 아바타 재생 또는 업로드 검증을 대신하지 않습니다. More-AFK는 Phone 구조를 지원합니다.",
            "\n## 検証範囲\n\n保存したグラフの参照と設定数を確認しました。実際のアバターでの再生とアップロードは別途確認してください。More-AFKはPhone構成に対応しています。");
        internal static string DriverConflict => InLanguage(
            "{0} / {1}: Conflicting shared Parameter Driver settings ({2}).",
            "{0} / {1}: 공통 Parameter Driver 설정 충돌 ({2}).",
            "{0} / {1}：共通のParameter Driver設定が競合しています（{2}）。");
        internal static string ParameterTypeConflict => InLanguage(
            "Conflicting parameter types: ",
            "파라미터 타입 충돌: ",
            "パラメーターの型が競合しています： ");
        internal static string SingleBranchTransition => InLanguage(
            "Cannot convert transition conditions for this single AFK branch: ",
            "단일 AFK 분기의 전환 조건을 변환할 수 없습니다: ",
            "単一AFK分岐の遷移条件を変換できません： ");
        internal static string SingleBranchConditions => InLanguage(
            "A single AFK branch needs AFK on and off conditions: ",
            "단일 AFK 분기에는 AFK 켜기·끄기 조건이 필요합니다: ",
            "単一AFK分岐にはAFKのオン・オフ条件が必要です： ");
        internal static string OutsideBranch => InLanguage(
            "Transition targets a state outside the branch: ",
            "분기 밖 상태로 향하는 전이: ",
            "分岐の外にあるステートへの遷移です： ");
        internal static string InternalTypeDriver => InLanguage(
            "A TypeAFK driver inside a branch requires separate conversion.",
            "분기 내부 TypeAFK 드라이버는 별도 변환 검토가 필요합니다.",
            "分岐内のTypeAFKドライバーには個別の変換が必要です。");
        internal static string UnsavedReference => InLanguage(
            "Cannot save this reference in the output: ",
            "출력에 저장할 수 없는 참조가 있습니다: ",
            "出力に保存できない参照があります： ");
        internal static string SyncedMotionNotClip => InLanguage(
            "The synced motion is not an animation clip: ",
            "동기화된 모션이 클립이 아닙니다: ",
            "同期モーションがアニメーションクリップではありません： ");
        internal static string CommonFxBlendTree => InLanguage(
            "Cannot merge a BlendTree in a shared FX state: ",
            "공통 FX 상태의 BlendTree를 합칠 수 없습니다: ",
            "共通FXステートのBlendTreeを統合できません： ");
        internal static string CurveCollision => InLanguage(
            "Curves overlap after remapping the clip path: ",
            "클립 경로 변환 후 커브가 겹칩니다: ",
            "クリップのパス変換後にカーブが重複しました： ");
        internal static string ExplicitRelativeRoot => InLanguage(
            "An explicit Relative Path Root is not supported: ",
            "명시적 Relative Path Root는 현재 지원하지 않습니다: ",
            "明示的なRelative Path Rootには対応していません： ");
        internal static string MissingBinding => InLanguage(
            "{0} / {1}: Cannot find the target of '{2}' ({3}). Product path: {4}, avatar: {5}.",
            "{0} / {1}: '{2}' ({3})의 대상을 확인할 수 없습니다. 상품 경로: {4}, 캐릭터: {5}",
            "{0} / {1}：「{2}」（{3}）の対象が見つかりません。商品内のパス：{4}、アバター：{5}");
        internal static string Unspecified => InLanguage(
            "not specified",
            "미지정",
            "未指定");
        internal static string CommonBlendTree => InLanguage(
            "Cannot merge a BlendTree in a shared state: ",
            "공통 상태의 BlendTree 병합은 지원하지 않습니다: ",
            "共通ステートのBlendTreeの統合には対応していません： ");
        internal static string OverlappingCurves => InLanguage(
            "{0}: Kept the earlier values for {2} overlapping curves in {1}. Example: {3}",
            "{0}: {1}의 중복 커브 {2}개에 앞서 합친 값을 유지합니다. 예: {3}",
            "{0}：{1}の重複カーブ{2}件には先に統合した値を使用します。例：{3}");
        internal static string AvatarDefaultReason => InLanguage(
            "Avatar default AFK: ",
            "캐릭터 기본 AFK: ",
            "アバターのデフォルトAFK： ");
        internal static string MultipleActionSlots => InLanguage(
            "The avatar Descriptor has multiple Action slots.",
            "캐릭터 Descriptor에 Action 슬롯이 여러 개입니다.",
            "アバターのDescriptorにActionスロットが複数あります。");
        internal static string UnknownActionPath => InLanguage(
            "Cannot identify the AFK path in the Action layer.",
            "단일 Action 레이어의 AFK 경로를 식별할 수 없습니다.",
            "ActionレイヤーのAFK経路を特定できません。");
        internal static string MissingSdkAction => InLanguage(
            "Check the VRChat SDK default Action controller.",
            "VRChat SDK 기본 Action 컨트롤러를 확인해주세요.",
            "VRChat SDK標準のActionコントローラーを確認してください。");
        internal static string InvalidSdkAfk => InLanguage(
            "Could not read the VRChat SDK default AFK.",
            "VRChat SDK 기본 AFK를 읽지 못했습니다.",
            "VRChat SDK標準のAFKを読み込めませんでした。");
        internal static string UnsupportedDefaultFx => InLanguage(
            "Cannot preserve the avatar’s default AFK FX layer structure: ",
            "캐릭터의 기본 AFK FX 레이어 구성을 그대로 통합할 수 없습니다: ",
            "アバターのデフォルトAFKのFXレイヤー構成を保持できません： ");
        internal static string FxExitClone => InLanguage(
            "Could not copy the FX exit motion: ",
            "FX 종료 모션을 복제할 수 없습니다: ",
            "FXの終了モーションを複製できません： ");
        internal static string AvatarFx => InLanguage(
            "Avatar FX ",
            "캐릭터 FX ",
            "アバターFX ");
        internal static string DefaultEntry => InLanguage(
            "Cannot identify the entry condition for TypeAFK 0.",
            "TypeAFK 0번 진입 조건을 구분할 수 없습니다.",
            "TypeAFK 0番の開始条件を特定できません。");
        internal static string DefaultBoundary => InLanguage(
            "Cannot identify the boundaries of the TypeAFK 0 sub-state machine.",
            "TypeAFK 0번 하위 상태 머신의 경계를 확인할 수 없습니다.",
            "TypeAFK 0番のサブステートマシンの範囲を特定できません。");
        internal static string UnknownEntryExit => InLanguage(
            "Cannot identify a unique AFK entry and exit path.",
            "AFK 진입·종료 경로를 하나로 식별할 수 없습니다.",
            "AFKの開始・終了経路を1つに特定できません。");
        internal static string UnknownExit => InLanguage(
            "Cannot identify the AFK exit path.",
            "AFK 종료 경로를 확인할 수 없습니다.",
            "AFKの終了経路を特定できません。");
        internal static string LayerWeights => InLanguage(
            "{0}/{1} changes the weights of {2}. This layer control and its reset on exit cannot be merged.",
            "{0}/{1}에서 {2}의 가중치를 변경합니다. 이 레이어 제어와 종료 시 복원은 아직 통합할 수 없습니다.",
            "{0}/{1}で{2}のウェイトを変更しています。このレイヤー制御と終了時の復元には対応していません。");
        internal static string StateControl => InLanguage(
            "{0} uses an unsupported state behaviour or control.",
            "{0}의 동작 또는 제어 방식은 아직 지원하지 않습니다.",
            "{0}の動作または制御方式には対応していません。");
        internal static string StateConditions => InLanguage(
            "Cannot preserve the transition conditions of {0}.",
            "{0}의 전환 조건을 보존할 수 없습니다.",
            "{0}の遷移条件を保持できません。");
        internal static string LeavesDefault => InLanguage(
            "Cannot identify the transition from {0} out of branch 0.",
            "{0}에서 0번 분기 밖으로 나가는 전이를 확인할 수 없습니다.",
            "{0}から0番分岐の外へ出る遷移を特定できません。");
        internal static string MissingOffCondition => InLanguage(
            "Cannot identify the AFK off condition.",
            "AFK 해제 조건을 확인할 수 없습니다.",
            "AFKを解除する条件を特定できません。");
        internal static string OtherTransitions => InLanguage(
            "The same state machine contains transitions unrelated to AFK.",
            "AFK 외 전환이 같은 상태 머신에 있습니다.",
            "同じステートマシンにAFK以外の遷移があります。");
        internal static string DefaultFxExitClone => InLanguage(
            "Could not copy the default AFK FX exit motion: ",
            "기본 AFK FX 종료 모션을 복제할 수 없습니다: ",
            "デフォルトAFKのFX終了モーションを複製できません： ");
        internal static string OverrideCycle => InLanguage(
            "Circular Animator Override reference.",
            "순환 Animator Override 참조입니다.",
            "Animator Overrideの参照が循環しています。");
        internal static string InputLayerCount => InLanguage(
            "Each input controller must have one internal layer. This controller cannot be merged without losing a layer.",
            "현재 지원 규격은 입력 컨트롤러 내부 레이어 1개입니다. 알 수 없는 레이어는 임의로 누락하지 않습니다.",
            "入力コントローラーの内部レイヤーは1つにしてください。この構成ではすべてのレイヤーを保持して統合できません。");
        internal static string NeedAfkBool => InLanguage(
            ": An AFK Bool parameter is required.",
            ": AFK Bool 파라미터가 필요합니다.",
            "：AFKのBoolパラメーターが必要です。");
        internal static string MissingPrepare => InLanguage(
            "Unsupported controller: the Prepare AFK state is missing: ",
            "Prepare AFK 상태가 없는 미지원 컨트롤러입니다: ",
            "Prepare AFKステートがないため、このコントローラーには対応していません： ");
        internal static string SingleEntry => InLanguage(
            ": Cannot identify the entry path for a single AFK branch.",
            ": 단일 AFK 분기의 진입 경로를 확인할 수 없습니다.",
            "：単一AFK分岐の開始経路を特定できません。");
        internal static string SyncedSingleOnly => InLanguage(
            ": Synced auxiliary layers are supported only with a single AFK branch.",
            ": 동기화된 보조 레이어는 단일 AFK 분기에서만 지원합니다.",
            "：同期補助レイヤーは単一AFK分岐でのみ使用できます。");
        internal static string NeedTypeInt => InLanguage(
            ": Selectable AFK requires a TypeAFK Int parameter.",
            ": 선택형 AFK에는 TypeAFK Int 파라미터가 필요합니다.",
            "：選択式AFKにはTypeAFKのIntパラメーターが必要です。");
        internal static string SharedState => InLanguage(
            "Two or more branches share the same state.",
            "둘 이상의 분기가 같은 상태를 공유합니다.",
            "複数の分岐が同じステートを共有しています。");
        internal static string NeedEquals => InLanguage(
            "An AFK entry branch needs exactly one TypeAFK Equals condition.",
            "AFK 진입 분기에 TypeAFK Equals 조건이 정확히 하나 필요합니다.",
            "AFKの開始分岐にはTypeAFK Equals条件が1つだけ必要です。");
        internal static string InvalidBranch => InLanguage(
            "A TypeAFK branch is duplicated or invalid.",
            "중복 또는 잘못된 TypeAFK 분기입니다.",
            "TypeAFKの分岐が重複しているか無効です。");
        internal static string NotIndependentBranch => InLanguage(
            "The AFK branch is not an independent sub-state machine.",
            "AFK 분기를 독립 하위 상태 머신으로 확인할 수 없습니다.",
            "AFK分岐を独立したサブステートマシンとして認識できません。");
        internal static string SameDestination => InLanguage(
            "Multiple TypeAFK values point to the same branch.",
            "여러 TypeAFK 번호가 같은 분기를 가리킵니다.",
            "複数のTypeAFK番号が同じ分岐を指しています。");
        internal static string MissingDefault => InLanguage(
            "The AFK_Default branch for option 0 is missing.",
            "0번 AFK_Default 분기가 없습니다.",
            "0番のAFK_Default分岐がありません。");
        internal static string RangeCondition => InLanguage(
            "TypeAFK range conditions cannot be converted.",
            "TypeAFK 범위 조건은 현재 변환을 지원하지 않습니다.",
            "TypeAFKの範囲条件は変換できません。");
        internal static string ForeignType => InLanguage(
            "An AFK branch refers to a different motion number. A dedicated adapter is required.",
            "하나의 AFK 분기가 다른 모션 번호를 참조합니다. 전용 어댑터가 필요합니다.",
            "AFK分岐が別のモーション番号を参照しています。専用の変換処理が必要です。");
        internal static string OutputUnderAssets => InLanguage(
            "The output folder must be under Assets.",
            "출력은 Assets 아래여야 합니다.",
            "出力先はAssets配下に指定してください。");
    }

    #region 적용 순서와 씬 교체

    public sealed class SceneApplyResult
    {
        public GameObject instance;
        public GenerationResult generated;
        public int replacedCount;
        public string defaultFallbackReason;
    }

    public static class SceneInstaller
    {
        private sealed class Installation
        {
            public GameObject root;
            public BuildSettings previous;
        }

        public static GameObject ResolveAvatar(GameObject obj)
        {
            if (!obj || EditorUtility.IsPersistent(obj) || !obj.scene.IsValid() || !obj.scene.isLoaded || EditorSceneManager.IsPreviewScene(obj.scene))
                throw new InvalidOperationException(AfkText.NeedSceneAvatar);
            var avatar = obj.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (!avatar) throw new InvalidOperationException(AfkText.AvatarNotFound);
            var animator = avatar.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.isHuman) throw new InvalidOperationException(AfkText.NeedHumanoid);
            if (PrefabStageUtility.GetPrefabStage(avatar.gameObject) != null)
                throw new InvalidOperationException(AfkText.PrefabStage);
            return avatar.gameObject;
        }

        public static bool UsesSdkDefault(GameObject avatar)
        {
            var d = avatar ? avatar.GetComponent<VRCAvatarDescriptor>() : null;
            if (!d) return false;
            if (!d.customizeAnimationLayers) return true;
            // Never infer a custom character AFK from an unrelated product or its name.
            foreach (var type in new[] { VRCAvatarDescriptor.AnimLayerType.Base, VRCAvatarDescriptor.AnimLayerType.Action, VRCAvatarDescriptor.AnimLayerType.FX })
            {
                var layers = d.baseAnimationLayers.Where(l => l.type == type).ToArray();
                if (layers.Length != 1 || (!layers[0].isDefault && layers[0].animatorController)) return false;
            }
            return true;
        }

        public static SceneApplyResult Apply(GameObject selectedAvatar, IEnumerable<GameObject> prefabs)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException(AfkText.ExitPlayMode);
            var avatar = ResolveAvatar(selectedAvatar);
            var inputs = prefabs?.Where(p => p).ToList() ?? new List<GameObject>();
            if (inputs.Count == 0) throw new InvalidOperationException(AfkText.NeedPrefab);
            if (inputs.Distinct().Count() != inputs.Count) throw new InvalidOperationException(AfkText.DuplicateInput);
            if (inputs.Any(p => !EditorUtility.IsPersistent(p) || !AssetDatabase.GetAssetPath(p).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(AfkText.ProjectPrefab);

            var installations = FindInstallations(avatar, inputs.ToHashSet());
            foreach (var installation in installations)
            {
                // Scene edits must not silently disappear when replacing an installation.
                if (PrefabUtility.GetAddedGameObjects(installation.root).Count > 0 || PrefabUtility.GetAddedComponents(installation.root).Count > 0 || PrefabUtility.GetRemovedComponents(installation.root).Count > 0)
                    throw new InvalidOperationException(string.Format(AfkText.ModifiedComponents, installation.root.name));
                if (PrefabUtility.HasPrefabInstanceAnyOverrides(installation.root, false))
                    throw new InvalidOperationException(string.Format(AfkText.SceneOverrides, installation.root.name));
                var assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(installation.root));
                if (installation.root.transform.localPosition != assetRoot.transform.localPosition || installation.root.transform.localRotation != assetRoot.transform.localRotation || installation.root.transform.localScale != assetRoot.transform.localScale)
                    throw new InvalidOperationException(string.Format(AfkText.TransformOverrides, installation.root.name));
            }
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (installations.Any(i => merge.transform.IsChildOf(i.root.transform))) continue;
                bool usesAfk = merge.animator && GraphUtility.BaseController(merge.animator) && GraphUtility.BaseController(merge.animator).parameters.Any(p => p.name == "AFK" || p.name == "TypeAFK");
                if (merge.layerType == VRCAvatarDescriptor.AnimLayerType.Action && merge.mergeAnimatorMode == MergeAnimatorMode.Replace || usesAfk)
                    throw new InvalidOperationException(string.Format(AfkText.ConflictingInstallation, merge.gameObject.name));
            }
            var settings = new BuildSettings { prefabs = inputs };
            var previous = installations.Where(i => i.previous != null).ToArray();
            if (previous.Length > 1) throw new InvalidOperationException(AfkText.MultipleInstallations);
            if (previous.Length == 1)
            {
                settings.motions = previous[0].previous.motions;
                settings.nextMotionId = previous[0].previous.nextMotionId;
            }
            // 0번은 매 적용 시 이 캐릭터의 현재 Action에서 다시 읽는다. Recipe에는 상품 모션 번호만 의존한다.
            settings.sceneAvatar = avatar;
            // Canonical root name must stay unique because prop clips use avatar-relative paths.
            if (avatar.transform.Cast<Transform>().Any(t => t.name == settings.prefabName && !installations.Any(i => i.root == t.gameObject)))
                throw new InvalidOperationException(string.Format(AfkText.NameInUse, settings.prefabName));
            var analysis = Analyzer.Inspect(settings, usePrefabOrder: true);
            var generated = Generator.GenerateMapped(analysis);

            Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(AfkText.UndoApply);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(generated.prefab, avatar.scene);
                if (!instance) throw new InvalidOperationException(AfkText.InstallFailed);
                Undo.RegisterCreatedObjectUndo(instance, AfkText.UndoApply);
                Undo.SetTransformParent(instance.transform, avatar.transform, AfkText.UndoApply);
                Undo.RecordObject(instance.transform, AfkText.UndoApply);
                instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
                foreach (var installation in installations) Undo.DestroyObjectImmediate(installation.root);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                EditorSceneManager.MarkSceneDirty(avatar.scene);
                Undo.CollapseUndoOperations(undoGroup);
                return new SceneApplyResult { instance = instance, generated = generated, replacedCount = installations.Count, defaultFallbackReason = analysis.fallbackReason };
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (generated.folder.StartsWith(settings.outputFolder + "/", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(generated.folder);
                throw;
            }
        }

        private static List<Installation> FindInstallations(GameObject avatar, HashSet<GameObject> inputs)
        {
            var result = new List<Installation>();
            foreach (var merge in avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Where(m => m.layerType == VRCAvatarDescriptor.AnimLayerType.Action))
            {
                var root = PrefabUtility.GetNearestPrefabInstanceRoot(merge.gameObject);
                if (!root || root == avatar || root.transform.parent != avatar.transform || result.Any(i => i.root == root)) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!source) continue;
                string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                string recipe = folder + "/Recipe.json";
                var sourceMerges = source.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                bool generated = sourceMerges.Length == 2 && source.transform.Find("Modules") && sourceMerges.All(m => m.transform == source.transform && m.animator && AssetDatabase.GetAssetPath(m.animator).StartsWith(folder + "/", StringComparison.Ordinal)) && File.Exists(recipe) && File.Exists(folder + "/Report.md");
                if (generated)
                    result.Add(new Installation { root = root, previous = Recipe.Load(recipe) });
                else if (inputs.Contains(source))
                    result.Add(new Installation { root = root });
            }
            return result;
        }
    }

    #endregion

    #region 입력 · 프리팹과 MA 레이어 읽기

    public static class Analyzer
    {
        public static string Fingerprint(BuildSettings s)
        {
            var assets = s.prefabs.Concat(new[] { s.defaultSourcePrefab }).Where(p => p).Distinct();
            return Hash128.Compute(JsonUtility.ToJson(s) + string.Join("|", assets.Select(p => AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(p)).ToString()))).ToString();
        }
        // 기존 분석 API의 편집 가능한 모션 목록을 유지한다. 씬 적용은 별도 계획 사본을 쓴다.
        public static Analysis Inspect(BuildSettings settings)
        {
            var plan = Inspect(settings, false);
            settings.motions = plan.settings.motions.Select(c => c.Copy()).ToList();
            settings.nextMotionId = plan.settings.nextMotionId;
            plan.fingerprint = Fingerprint(settings);
            return plan;
        }

        internal static Analysis Inspect(BuildSettings request, bool usePrefabOrder)
        {
            var s = request.Copy();
            var a = new Analysis { settings = s };
            if (s.prefabs.Count == 0) a.Error(AfkText.NeedMotionSource);
            if (s.prefabs.Distinct().Count() != s.prefabs.Count) a.Error(AfkText.DuplicatePrefab);
            foreach (var prefab in s.prefabs.Where(p => p).Distinct())
            {
                var m = Read(prefab, a, s.sceneAvatar != null); if (m != null) a.modules.Add(m);
            }
            if (s.prefabs.Any(p => !p)) a.Error(AfkText.EmptySlot);
            if (s.defaultSourcePrefab)
                a.defaultSource = a.modules.FirstOrDefault(m => m.prefab == s.defaultSourcePrefab) ?? Read(s.defaultSourcePrefab, a, s.sceneAvatar != null);
            else a.defaultSource = a.modules.FirstOrDefault(m => m.fx.Count == 2 && !m.actionShape.singleMotion);
            bool sdkDefault = s.sdkDefaultAvatar && SceneInstaller.UsesSdkDefault(s.sdkDefaultAvatar);
            if (a.defaultSource == null && (sdkDefault || s.sceneAvatar != null)) a.defaultSource = a.modules.FirstOrDefault(m => !m.actionShape.singleMotion) ?? (s.sceneAvatar != null ? a.modules.FirstOrDefault() : null);
            // 씬 적용에서는 Descriptor에서 읽은 Action이 0번 모션을 제공한다.
            // 상품의 AFK_Default 이름은 상품만으로 기본 모션을 선택할 때 확인한다.
            bool characterDefault = a.defaultSource != null && !a.defaultSource.actionShape.singleMotion && !string.IsNullOrEmpty(a.defaultSource.profile);
            bool descriptorDefault = s.sceneAvatar != null && a.defaultSource != null;
            bool sdkProductDefault = sdkDefault && a.defaultSource != null && a.defaultSource.actionShape.branches.ContainsKey(0);
            if (!descriptorDefault && !characterDefault && !sdkProductDefault)
                a.Error(AfkText.NeedDefaultSource);
            if (sdkProductDefault && !characterDefault && !descriptorDefault)
            {
                var defaults = GraphUtility.States(a.defaultSource.actionShape.branches[0].machine)
                    .Select(st => st.motion is AnimationClip clip ? GraphUtility.EffectiveClip(a.defaultSource.action.animator, clip) : st.motion).Where(m => m).ToArray();
                if (defaults.Length != 1 || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(defaults[0])) != "806c242c97b686d4bac4ad50defd1fdb")
                    a.Error(a.defaultSource.prefab.name + AfkText.DefaultMismatch);
            }
            var all = a.modules.Concat(new[] { a.defaultSource }).Where(m => m != null).Distinct().ToArray();
            const string afkPrefabPrefix = "VRSuya_AFK_Modular_";
            var productProfiles = all.Select(m => m.profile ?? (m.fx.Count == 2 && !m.actionShape.singleMotion && m.prefab.name.StartsWith(afkPrefabPrefix, StringComparison.Ordinal)
                ? m.prefab.name.Substring(afkPrefabPrefix.Length) : null));
            if (productProfiles.Where(name => !string.IsNullOrEmpty(name)).Distinct().Count() > 1)
                a.Error(AfkText.MixedAvatars);
            foreach (var module in a.Sources)
            {
                var visited = new HashSet<VRCExpressionsMenu>();
                foreach (var installer in module.prefab.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
                    ReadMenu(installer.menuToAppend, module, null, visited, a);
            }
            a.MapMotions(s, usePrefabOrder);
            var active = a.selectedMotions.Select(m => m.choice).ToArray();
            if (active.Length == 0) a.Error(AfkText.SelectMotion);
            if (active.Any(c => c.id < 1 || c.id > 255) || active.Select(c => c.id).Distinct().Count() != active.Length)
                a.Error(AfkText.InvalidIds);
            if (active.Any(c => string.IsNullOrWhiteSpace(c.label))) a.Error(AfkText.NeedMenuLabel);
            if (s.initialType != 0 && !active.Any(c => c.id == s.initialType)) a.Error(AfkText.InvalidInitialType);
            string folder = s.outputFolder ?? "";
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Contains("..") || folder.Contains('\\') || folder.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                a.Error(AfkText.InvalidFolder);
            if (string.IsNullOrWhiteSpace(s.prefabName) || s.prefabName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || s.prefabName.Contains('/')) a.Error(AfkText.InvalidPrefabName);
            if (string.IsNullOrWhiteSpace(s.menuName)) a.Error(AfkText.NeedMenuName);
            if (all.Any(m => m.prefab.GetComponentInChildren<VRSuya.Modular.RemoveFXMask>(true)))
                a.Warn(s.preserveGlobalMaskRemoval ? AfkText.KeepMaskRemoval : AfkText.OmitMaskRemoval);
            if (s.validationAvatar)
            {
                var descriptor = s.validationAvatar.GetComponent<VRCAvatarDescriptor>();
                if (!descriptor) a.Error(AfkText.NeedDescriptor);
                if (s.validationAvatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Any(c => c.layerType == VRCAvatarDescriptor.AnimLayerType.Action && c.mergeAnimatorMode == MergeAnimatorMode.Replace))
                    a.Error(AfkText.ExistingReplace);
                a.Warn(AfkText.StructuralCheck);
            }
            a.Warn(AfkText.ReplaceOriginal);
            if (a.CanGenerate && s.sceneAvatar != null)
            {
                try
                {
                    a.defaultAction = AvatarDefault.Read(s.sceneAvatar);
                    a.defaultFx = a.defaultAction.ReadRemovedFx(a.Sources
                        .SelectMany(m => m.prefab.GetComponentsInChildren<VRSuya.Modular.RemoveAnimatorLayer>(true))
                        .SelectMany(r => r.TargetLayerName ?? Array.Empty<string>()));
                }
                catch (AvatarDefault.UnsupportedGraph e)
                {
                    // 0번 Action과 FX는 한 쌍이다. 상품 그래프의 오류는 여기서 대체하지 않는다.
                    a.defaultAction = AvatarDefault.ReadSdk(s.sceneAvatar);
                    a.fallbackReason = e.Message;
                    a.Warn(AfkText.DefaultWarning + e.Message);
                }
            }
            if (a.CanGenerate)
            {
                foreach (CurveGroup group in Enum.GetValues(typeof(CurveGroup)))
                    a.layerPlans.Add(group, new LayerPlan(a, group));
            }
            a.fingerprint = Fingerprint(s);
            return a;
        }
        // 원본 메뉴의 TypeAFK 값은 상품 내부 번호다. 재적용 시에도 상품 키로 아이콘을 찾는다.
        private static void ReadMenu(VRCExpressionsMenu menu, Module module, Texture2D parentIcon,
            HashSet<VRCExpressionsMenu> visited, Analysis analysis)
        {
            if (!menu || !visited.Add(menu)) return;
            foreach (var control in menu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    ReadMenu(control.subMenu, module, control.icon ? control.icon : parentIcon, visited, analysis);
                else if (control.parameter?.name == "TypeAFK" && control.type == VRCExpressionsMenu.Control.ControlType.Toggle)
                {
                    int id = (int)control.value;
                    if (id != control.value || !module.actionShape.branches.ContainsKey(id)) continue;
                    if (!analysis.entryIcon && parentIcon) analysis.entryIcon = parentIcon;
                    if (id != 0 && !string.IsNullOrWhiteSpace(control.name)) module.labels.TryAdd(id, control.name);
                    if (!control.icon) continue;
                    if (id == 0 && !analysis.defaultIcon) analysis.defaultIcon = control.icon;
                    else if (id != 0) analysis.icons.TryAdd(module.Key(id), control.icon);
                }
            }
        }

        internal static Module Read(GameObject prefab, Analysis a, bool replaceDefault)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || !EditorUtility.IsPersistent(prefab)) { a.Error(AfkText.NeedProjectAsset, prefab); return null; }

            try
            {
                foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0) throw new InvalidOperationException(AfkText.MissingScript + t.name);
                var merges = prefab.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
                var action = merges.Where(c => c.layerType == VRCAvatarDescriptor.AnimLayerType.Action).ToArray();
                if (action.Length != 1 || merges.Any(c => c.layerType != VRCAvatarDescriptor.AnimLayerType.Action && c.layerType != VRCAvatarDescriptor.AnimLayerType.FX))
                    throw new InvalidOperationException(AfkText.UnsupportedInstallLayout);
                var shape = GraphUtility.ReadShape(action[0].animator);
                var m = new Module { prefab = prefab, path = path, guid = AssetDatabase.AssetPathToGUID(path),
                    action = action[0], actionShape = shape,
                    fx = merges.Where(c => c.layerType == VRCAvatarDescriptor.AnimLayerType.FX).ToList() };
                // 기존 상품에 프로필 표기가 있으면 캐릭터 혼합 검사에만 사용한다.
                const string phonePrefix = "VRSuya_More-AFK_Phone_";
                if (shape.singleMotion && prefab.name.StartsWith(phonePrefix, StringComparison.Ordinal))
                    m.profile = prefab.name.Substring(phonePrefix.Length);
                else if (shape.branches.TryGetValue(0, out var zero) && zero.machine.name.StartsWith("AFK_Default_", StringComparison.Ordinal))
                    m.profile = zero.machine.name.Substring("AFK_Default_".Length);
                if (m.fx.Count < 1 || m.fx.Count > 2)
                    throw new InvalidOperationException(AfkText.UnsupportedFxLayout);
                foreach (var cfg in prefab.GetComponentsInChildren<ModularAvatarParameters>(true).SelectMany(p => p.parameters))
                    if (cfg.isPrefix || !string.IsNullOrEmpty(cfg.remapTo) || cfg.internalParameter)
                        throw new InvalidOperationException(AfkText.UnsupportedMAParameter);
                foreach (var merge in merges)
                {
                    var graph = merge == m.action ? shape : GraphUtility.ReadShape(merge.animator);
                    if (merge == m.action) m.layers.Add(CurveGroup.All, (merge, graph));
                    else if (m.fx.Count == 1)
                    {
                        m.layers.Add(CurveGroup.Face, (merge, graph));
                        m.layers.Add(CurveGroup.Objects, (merge, graph));
                    }
                    else
                    {
                        var role = merge.pathMode == MergeAnimatorPathMode.Relative ? CurveGroup.Objects : CurveGroup.Face;
                        if (!m.layers.TryAdd(role, (merge, graph)))
                            throw new InvalidOperationException(AfkText.SplitFxPaths);
                    }
                    if (GraphUtility.HasSyncedCompanion(graph.controller))
                    {
                        var companion = graph.controller.layers[1];
                        foreach (var state in GraphUtility.States(graph.root))
                            if (companion.GetOverrideBehaviours(state)?.Length > 0)
                                throw new InvalidOperationException(string.Format(AfkText.SyncedBehaviour, graph.controller.name, state.name));
                    }
                    if (graph.singleMotion != shape.singleMotion)
                        throw new InvalidOperationException(AfkText.EntryMismatch);
                    if (graph.branches.Count != shape.branches.Count || graph.branches.Keys.Any(id => !shape.branches.ContainsKey(id)))
                        throw new InvalidOperationException(AfkText.BranchMismatch);
                    if (graph.controller.layers[0].avatarMask) throw new InvalidOperationException(AfkText.MaskedLayer);
                    foreach (var behaviour in GraphUtility.Reachable(graph.controller, graph.ReplacedStates(replaceDefault)).OfType<StateMachineBehaviour>())
                        if (!new[] { "VRCPlayableLayerControl", "VRCAnimatorTrackingControl", "VRCAvatarParameterDriver" }.Contains(behaviour.GetType().Name)) throw new InvalidOperationException(AfkText.UnsupportedBehaviour + behaviour.GetType().Name);
                    if (merge.animator.animationClips.Any(c => AnimationUtility.GetAnimationEvents(c).Length > 0)) throw new InvalidOperationException(AfkText.AnimationEvents);
                    if (graph.root.stateMachines.Length != graph.branches.Count) throw new InvalidOperationException(AfkText.ExtraStateMachine);
                }
                return m;
            }
            catch (Exception e) { a.Error(prefab.name + ": " + e.Message, prefab); return null; }
        }
    }

    #endregion

    #region 매핑 · 모션 번호와 공통 모델

    public enum CurveGroup { All, Face, Objects }
    public enum IssueLevel { Info, Warning, Error }

    [Serializable] public sealed class MotionChoice
    {
        public string key;
        public string label;
        public int id;
        public bool enabled = true;
        public Texture2D icon;
        internal MotionChoice Copy() => (MotionChoice)MemberwiseClone();
    }

    [Serializable] public sealed class BuildSettings
    {
        public List<GameObject> prefabs = new List<GameObject>();
        public GameObject defaultSourcePrefab;
        public GameObject validationAvatar;
        // Set only by the scene installer after proving that the avatar uses SDK defaults.
        public GameObject sdkDefaultAvatar;
        [NonSerialized] internal GameObject sceneAvatar;
        public string menuName = "VRSuya AFK";
        internal const string DefaultOutputFolder = "Assets/VRSuya/AFKToolsGenerated";
        public string outputFolder = DefaultOutputFolder;
        public string prefabName = "VRSuya_AFK_Combined";
        public int initialType;
        public List<MotionChoice> motions = new List<MotionChoice>();
        // This is deliberately not enabled automatically: Installer removes ALL FX masks.
        public bool preserveGlobalMaskRemoval;
        public int nextMotionId = 1;
        internal BuildSettings Copy()
        {
            var copy = (BuildSettings)MemberwiseClone();
            copy.prefabs = new List<GameObject>(prefabs);
            copy.motions = motions.Select(c => c.Copy()).ToList();
            return copy;
        }
    }

    public sealed class Issue
    {
        public IssueLevel level;
        public string message;
        public UnityEngine.Object context;
        public Issue(IssueLevel level, string message, UnityEngine.Object context = null)
        { this.level = level; this.message = message; this.context = context; }
    }

    public sealed class Branch
    {
        public int localId;
        public AnimatorStateMachine machine;
        public AnimatorStateTransition entry;
        internal AnimatorTransition[] exits;
    }

    public sealed class ControllerShape
    {
        public AnimatorController controller;
        public AnimatorStateMachine root;
        internal bool singleMotion;
        public AnimatorState prepare;
        public Dictionary<int, Branch> branches = new Dictionary<int, Branch>();
        internal HashSet<AnimatorState> ReplacedStates(bool replaceDefault)
        {
            return replaceDefault && branches.TryGetValue(0, out var zero)
                ? GraphUtility.States(zero.machine).ToHashSet() : null;
        }
    }

    public sealed class Module
    {
        public GameObject prefab;
        public string path, guid, profile;
        internal readonly Dictionary<int, string> labels = new Dictionary<int, string>();
        public ModularAvatarMergeAnimator action;
        public List<ModularAvatarMergeAnimator> fx = new List<ModularAvatarMergeAnimator>();
        public ControllerShape actionShape;
        internal readonly Dictionary<CurveGroup, (ModularAvatarMergeAnimator merge, ControllerShape shape)> layers =
            new Dictionary<CurveGroup, (ModularAvatarMergeAnimator, ControllerShape)>();
        public string Key(int localId) { return guid + ":" + localId; }
        public string Label(int id)
        {
            if (labels.TryGetValue(id, out var label)) return label;
            var branch = actionShape.branches[id].machine.name;
            return branch.StartsWith("AFK_", StringComparison.Ordinal) ? branch.Substring(4) : branch;
        }
    }

    public sealed class Analysis
    {
        public readonly List<Module> modules = new List<Module>();
        public readonly List<Issue> issues = new List<Issue>();
        internal readonly List<(Module module, int localId, MotionChoice choice)> selectedMotions =
            new List<(Module, int, MotionChoice)>();
        internal readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        internal Texture2D entryIcon, defaultIcon;
        internal AvatarDefault defaultAction, defaultFx;
        internal BuildSettings settings;
        internal readonly Dictionary<CurveGroup, LayerPlan> layerPlans = new Dictionary<CurveGroup, LayerPlan>();
        internal string fallbackReason;
        public Module defaultSource;
        internal IEnumerable<Module> Sources => new[] { defaultSource }.Concat(modules).Where(m => m != null).Distinct();

        internal void MapMotions(BuildSettings settings, bool usePrefabOrder)
        {
            var sources = new Dictionary<string, (Module module, int localId)>();
            var choices = settings.motions.ToDictionary(c => c.key);
            var ordered = new List<MotionChoice>();
            foreach (var module in modules)
            foreach (var id in module.actionShape.branches.Keys.Where(id => id != 0).OrderBy(id => id))
            {
                var key = module.Key(id);
                if (sources.ContainsKey(key)) continue;
                sources.Add(key, (module, id));
                if (!choices.TryGetValue(key, out var choice))
                {
                    choice = new MotionChoice { key = key, id = settings.nextMotionId++, label = module.Label(id) };
                    settings.motions.Add(choice);
                    choices.Add(key, choice);
                }
                else if (usePrefabOrder)
                    choice.label = module.Label(id);
                ordered.Add(choice);
            }
            // 메뉴 순서는 현재 프리팹 순서를 따르고 Recipe의 기존 동기화 번호는 유지한다.
            if (usePrefabOrder)
            {
                foreach (var choice in settings.motions)
                    choice.enabled = choice.key != null && sources.ContainsKey(choice.key);
                settings.motions = ordered.Concat(settings.motions.Where(c => !c.enabled)).ToList();
            }
            foreach (var choice in settings.motions)
                if (choice.enabled && choice.key != null && sources.TryGetValue(choice.key, out var source))
                    selectedMotions.Add((source.module, source.localId, choice));
        }
        public string fingerprint;
        public bool CanGenerate { get { return !issues.Any(i => i.level == IssueLevel.Error); } }
        public void Error(string message, UnityEngine.Object context = null) { issues.Add(new Issue(IssueLevel.Error, message, context)); }
        public void Warn(string message, UnityEngine.Object context = null) { issues.Add(new Issue(IssueLevel.Warning, message, context)); }
    }

    public sealed class GenerationResult
    {
        public GameObject prefab;
        public AnimatorController action, fx;
        public string folder, reportPath;
    }

    #endregion

    #region 매핑 · 레시피 저장과 복원

    public static class Recipe
    {
        [Serializable] private sealed class Data
        {
            public int version = 1;
            public string[] prefabGuids;
            public string defaultGuid;
            public BuildSettings settings;
            public string[] iconGuids;
        }
        public static void Save(BuildSettings settings, string path)
        {
            var clone = JsonUtility.FromJson<BuildSettings>(JsonUtility.ToJson(settings));
            clone.prefabs.Clear(); clone.defaultSourcePrefab = null; clone.validationAvatar = null; clone.sdkDefaultAvatar = null;
            foreach (var c in clone.motions) c.icon = null;
            File.WriteAllText(path, JsonUtility.ToJson(new Data { prefabGuids = settings.prefabs.Select(Guid).ToArray(), defaultGuid = Guid(settings.defaultSourcePrefab), settings = clone, iconGuids = settings.motions.Select(c => Guid(c.icon)).ToArray() }, true));
        }
        public static BuildSettings Load(string path)
        {
            var data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            if (data == null || data.version != 1 || data.settings == null || data.prefabGuids == null) throw new InvalidOperationException(AfkText.InvalidRecipe);
            data.settings.prefabs = data.prefabGuids.Select(g => Load<GameObject>(g)).ToList();
            data.settings.defaultSourcePrefab = Load<GameObject>(data.defaultGuid);
            for (int i = 0; i < data.settings.motions.Count; i++) data.settings.motions[i].icon = data.iconGuids != null && i < data.iconGuids.Length ? Load<Texture2D>(data.iconGuids[i]) : null;
            return data.settings;
        }
        private static string Guid(UnityEngine.Object obj) { return obj ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj)) : ""; }
        private static T Load<T>(string guid) where T : UnityEngine.Object { return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)); }
    }

    #endregion

    #region 생성 · 컨트롤러, 메뉴, 프리팹

    public static class Generator
    {
        public static GenerationResult Generate(BuildSettings settings, Analysis reviewed)
        {
            if (reviewed == null || reviewed.fingerprint != Analyzer.Fingerprint(settings)) throw new InvalidOperationException(AfkText.SourceChanged);
            return GenerateMapped(Analyzer.Inspect(settings, false));
        }

        internal static GenerationResult GenerateMapped(Analysis a)
        {
            var settings = a.settings;
            if (!a.CanGenerate) throw new InvalidOperationException(string.Join("\n", a.issues.Where(i => i.level == IssueLevel.Error).Select(i => i.message)));
            GraphUtility.EnsureFolder(settings.outputFolder);
            string folder = AssetDatabase.GenerateUniqueAssetPath(settings.outputFolder + "/" + settings.prefabName);
            GraphUtility.EnsureFolder(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(settings.prefabName); SceneManager.MoveGameObjectToScene(root, scene);
                var container = new GameObject("Modules"); container.transform.SetParent(root.transform, false);
                var modules = a.Sources.ToArray();
                var roots = new Dictionary<Module, string>();
                var usedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var m in modules)
                {
                    var copy = (GameObject)PrefabUtility.InstantiatePrefab(m.prefab, scene);
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    copy.transform.SetParent(container.transform, false);
                    copy.name = m.prefab.name;
                    if (!usedNames.Add(copy.name))
                    {
                        copy.name += "_" + m.guid;
                        if (!usedNames.Add(copy.name)) throw new InvalidOperationException(AfkText.DuplicateObjectPath + copy.name);
                    }
                    roots[m] = root.name + "/Modules/" + copy.name;
                    RebaseReferences(copy, m.prefab.name, roots[m]);
                    foreach (var merge in copy.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
                    { if (merge.deleteAttachedAnimator && merge.GetComponent<Animator>()) Object.DestroyImmediate(merge.GetComponent<Animator>()); Object.DestroyImmediate(merge); }
                    Remove<ModularAvatarParameters>(copy); Remove<ModularAvatarMenuInstaller>(copy); Remove<MenuSelector>(copy); Remove<AnimationClipRenamer>(copy);
                    // Common installer operations are centralized once on the output root.
                    Remove<FixLocomotion>(copy); Remove<ChangeStandingPose>(copy); Remove<RemoveAnimatorLayer>(copy); Remove<RemoveFXMask>(copy);
                }
                CentralizeInstallers(root, modules, a, settings);
                string animationFolder = folder + "/Animation"; GraphUtility.EnsureFolder(animationFolder);
                using var assets = new GraphAssets(a);
                var action = new AnimatorController { name = settings.prefabName + "_Action" };
                AssetDatabase.CreateAsset(action, folder + "/" + action.name + ".controller");
                var actionGraph = new GraphCompiler(action, animationFolder, assets).Merge(settings, a, CurveGroup.All, roots);
                action.layers = new[] { new AnimatorControllerLayer { name = "Action", defaultWeight = 1, stateMachine = actionGraph } };
                actionGraph.name = "Action";
                var fx = new AnimatorController { name = settings.prefabName + "_FX" };
                AssetDatabase.CreateAsset(fx, folder + "/" + fx.name + ".controller");
                var compiler = new GraphCompiler(fx, animationFolder, assets);
                fx.layers = new[] {
                    new AnimatorControllerLayer { name = "AFK/Combined/Facial", defaultWeight = 1, stateMachine = compiler.Merge(settings, a, CurveGroup.Face, roots) },
                    new AnimatorControllerLayer { name = "AFK/Combined/Object", defaultWeight = 1, stateMachine = compiler.Merge(settings, a, CurveGroup.Objects, roots) }
                };
                foreach (var layer in fx.layers) layer.stateMachine.name = layer.name;
                if (a.layerPlans[CurveGroup.All].actionOwnsHead) GraphCompiler.KeepAfkHeadTrackingOnAction(fx);
                foreach (var controller in new[] { action, fx })
                {
                    var parameters = controller.parameters;
                    foreach (var p in parameters) if (p.name == "TypeAFK") p.defaultInt = settings.initialType;
                    controller.parameters = parameters; EditorUtility.SetDirty(controller);
                }
                assets.Save(action, fx);
                AddMerge(root, action, VRCAvatarDescriptor.AnimLayerType.Action, MergeAnimatorMode.Replace, a.defaultSource.action);
                AddMerge(root, fx, VRCAvatarDescriptor.AnimLayerType.FX, MergeAnimatorMode.Append, a.defaultSource.fx[0]);
                var parametersComponent = root.AddComponent<ModularAvatarParameters>();
                foreach (var config in modules.SelectMany(m => m.prefab.GetComponentsInChildren<ModularAvatarParameters>(true)).SelectMany(p => p.parameters))
                {
                    if (config.nameOrPrefix == "TypeAFK" || config.nameOrPrefix == "AFK") continue;
                    var previous = parametersComponent.parameters.Where(p => p.nameOrPrefix == config.nameOrPrefix).ToArray();
                    if (previous.Length > 0 && JsonUtility.ToJson(previous[0]) != JsonUtility.ToJson(config)) throw new InvalidOperationException(AfkText.ParameterConflict + config.nameOrPrefix);
                    if (previous.Length == 0) parametersComponent.parameters.Add(config);
                }
                parametersComponent.parameters.Add(new ParameterConfig { nameOrPrefix = "TypeAFK", syncType = ParameterSyncType.Int, defaultValue = settings.initialType, hasExplicitDefaultValue = true, saved = true });
                root.AddComponent<ModularAvatarMenuInstaller>().menuToAppend = CreateMenu(settings, a, folder);
                // Do not flush unrelated dirty assets in the user's open project.
                foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
                    AssetDatabase.SaveAssetIfDirty(new GUID(guid));
                string prefabPath = folder + "/" + settings.prefabName + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success || !prefab) throw new InvalidOperationException(AfkText.SaveFailed);
                ValidateSaved(prefab, action, fx, folder);
                Recipe.Save(settings, folder + "/Recipe.json");
                string reportPath = folder + "/Report.md";
                File.WriteAllText(reportPath, Report(settings, a, folder), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(folder + "/Recipe.json");
                AssetDatabase.ImportAsset(reportPath);
                return new GenerationResult { prefab = prefab, action = action, fx = fx, folder = folder, reportPath = reportPath };
            }
            catch
            {
                // Only the freshly allocated, unique child folder belongs to this transaction.
                if (folder.StartsWith(settings.outputFolder + "/", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                throw;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Remove<T>(GameObject root) where T : Component { foreach (var c in root.GetComponentsInChildren<T>(true)) Object.DestroyImmediate(c); }
        private static void RebaseReferences(GameObject copy, string oldRoot, string newRoot)
        {
            foreach (var c in copy.GetComponentsInChildren<Component>(true).Where(c => c && !(c is Transform)))
            {
                var so = new SerializedObject(c); var p = so.GetIterator(); bool changed = false;
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.String && p.name == "referencePath" && (p.stringValue == oldRoot || p.stringValue.StartsWith(oldRoot + "/", StringComparison.Ordinal)))
                    { p.stringValue = newRoot + p.stringValue.Substring(oldRoot.Length); changed = true; }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        private static void CentralizeInstallers(GameObject root, Module[] modules, Analysis analysis, BuildSettings settings)
        {
            var fixes = modules.SelectMany(m => m.prefab.GetComponentsInChildren<FixLocomotion>(true)).ToArray();
            if (fixes.Length > 0)
            {
                var fix = root.AddComponent<FixLocomotion>();
                fix.TargetAnimationClip = analysis.defaultSource.prefab.GetComponentInChildren<FixLocomotion>(true)?.TargetAnimationClip ?? fixes[0].TargetAnimationClip;
                fix.TargetParameters = fixes.SelectMany(f => f.TargetParameters ?? Array.Empty<string>()).Distinct().ToArray();
            }
            if (modules.Any(m => m.prefab.GetComponentInChildren<ChangeStandingPose>(true))) root.AddComponent<ChangeStandingPose>();
            var removes = modules.SelectMany(m => m.prefab.GetComponentsInChildren<RemoveAnimatorLayer>(true)).ToArray();
            var names = removes.SelectMany(r => r.TargetLayerName ?? Array.Empty<string>());
            if (analysis.fallbackReason != null)
                names = names.Concat(analysis.defaultAction.DedicatedFxToggles());
            var removed = names.Distinct().ToArray();
            if (removed.Length > 0) root.AddComponent<RemoveAnimatorLayer>().TargetLayerName = removed;
            if (settings.preserveGlobalMaskRemoval && modules.Any(m => m.prefab.GetComponentInChildren<RemoveFXMask>(true))) root.AddComponent<RemoveFXMask>();
        }
        private static void AddMerge(GameObject root, AnimatorController controller, VRCAvatarDescriptor.AnimLayerType layer, MergeAnimatorMode mode, ModularAvatarMergeAnimator template)
        {
            var c = root.AddComponent<ModularAvatarMergeAnimator>(); c.animator = controller; c.layerType = layer; c.mergeAnimatorMode = mode;
            c.pathMode = MergeAnimatorPathMode.Absolute; c.matchAvatarWriteDefaults = template.matchAvatarWriteDefaults; c.deleteAttachedAnimator = false; c.layerPriority = template.layerPriority;
        }
        private static VRCExpressionsMenu CreateMenu(BuildSettings s, Analysis a, string folder)
        {
            var choices = new[] { new MotionChoice { id = 0, label = AfkText.DefaultLabel, icon = a.defaultIcon } }
                .Concat(a.selectedMotions.Select(m => m.choice)).ToArray();
            var top = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); top.name = s.menuName;
            AssetDatabase.CreateAsset(top, folder + "/Menu.asset");
            VRCExpressionsMenu previous = null;
            for (int offset = 0, page = 0; offset < choices.Length; page++)
            {
                var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>(); menu.name = s.menuName + " " + (page + 1);
                AssetDatabase.CreateAsset(menu, folder + "/Menu_Page" + (page + 1) + ".asset");
                (previous ?? top).controls.Add(new VRCExpressionsMenu.Control { name = previous ? AfkText.NextPage : s.menuName, icon = previous ? null : a.entryIcon, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu });
                int take = choices.Length - offset > 8 ? 7 : choices.Length - offset;
                foreach (var c in choices.Skip(offset).Take(take))
                {
                    var icon = c.icon;
                    if (!icon && c.key != null) a.icons.TryGetValue(c.key, out icon);
                    menu.controls.Add(new VRCExpressionsMenu.Control { name = c.label, icon = icon, type = VRCExpressionsMenu.Control.ControlType.Toggle, parameter = new VRCExpressionsMenu.Control.Parameter { name = "TypeAFK" }, value = c.id });
                }
                offset += take; previous = menu; EditorUtility.SetDirty(menu);
            }
            EditorUtility.SetDirty(top); return top;
        }
        public static void ValidateSaved(GameObject prefab, AnimatorController action, AnimatorController fx, string folder)
        {
            var merges = prefab.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
            if (merges.Length != 2 || merges.Count(m => m.mergeAnimatorMode == MergeAnimatorMode.Replace) != 1) throw new InvalidOperationException(AfkText.InvalidMergeCount);
            foreach (var controller in new[] { action, fx })
            foreach (var obj in GraphUtility.Reachable(controller))
            {
                if ((obj is AnimatorStateMachine || obj is AnimatorState || obj is AnimatorTransitionBase || obj is StateMachineBehaviour || obj is BlendTree || obj is AnimationClip) && !AssetDatabase.GetAssetPath(obj).StartsWith(folder + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException(AfkText.ExternalGraphReference + obj.name + " / " + AssetDatabase.GetAssetPath(obj));
            }
        }
        private static string Report(BuildSettings s, Analysis a, string folder)
        {
            var b = new StringBuilder(AfkText.ReportTitle);
            b.AppendLine(AfkText.ReportTime + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            b.AppendLine(string.Format(AfkText.ReportOutput, folder));
            b.AppendLine(string.Format(AfkText.ReportMotions, a.defaultAction?.Source ?? a.defaultSource.prefab.name));
            foreach (var c in a.selectedMotions.Select(m => m.choice)) b.AppendLine("| " + c.id + " | " + c.label.Replace("|", "\\|") + " |");
            b.AppendLine(AfkText.ReportInstallation);
            b.AppendLine(AfkText.ReportValidation);
            foreach (var issue in a.issues) b.AppendLine("\n- " + AfkText.Severity(issue.level) + ": " + issue.message);
            return b.ToString();
        }
    }

    #endregion

    // 기준 상품이 공통 상태를 제공하고 다른 상품은 선택한 분기, 공통 FX와 추가 Driver만 제공한다.
    internal sealed class LayerPlan
    {
        internal readonly Module[] modules;
        internal readonly Dictionary<Module, HashSet<int>> ids = new Dictionary<Module, HashSet<int>>();
        internal readonly Dictionary<string, List<(Module module, AnimatorState state)>> donors =
            new Dictionary<string, List<(Module, AnimatorState)>>();
        internal readonly Dictionary<string, List<VRCAvatarParameterDriver>> drivers =
            new Dictionary<string, List<VRCAvatarParameterDriver>>();
        internal readonly bool actionOwnsHead;
        internal readonly bool normalizeWeight;

        internal LayerPlan(Analysis analysis, CurveGroup group)
        {
            modules = analysis.Sources.ToArray();
            foreach (var module in modules) ids[module] = new HashSet<int>();
            foreach (var motion in analysis.selectedMotions) ids[motion.module].Add(motion.localId);
            var root = modules[0].layers[group].shape.root;
            var states = root.states.Select(child => child.state).ToDictionary(st => st.name);
            var known = new Dictionary<string, List<VRCAvatarParameterDriver>>();
            foreach (var state in states.Values)
            {
                donors[state.name] = new List<(Module, AnimatorState)>();
                drivers[state.name] = new List<VRCAvatarParameterDriver>();
                known[state.name] = state.behaviours.OfType<VRCAvatarParameterDriver>().ToList();
            }
            foreach (var module in modules.Skip(1))
            foreach (var child in module.layers[group].shape.root.states)
            {
                var state = child.state;
                if (!donors.TryGetValue(state.name, out var list)) continue;
                list.Add((module, state));
                foreach (var driver in state.behaviours.OfType<VRCAvatarParameterDriver>())
                {
                    var previous = known[state.name];
                    var names = driver.parameters.Select(p => p.name).ToHashSet();
                    if (previous.Any(d => d.parameters.Any(p => names.Contains(p.name))))
                    {
                        if (!previous.Any(d => SameDriver(d, driver)))
                            analysis.Error(string.Format(AfkText.DriverConflict, module.prefab.name, state.name, string.Join(", ", names)));
                        continue;
                    }
                    drivers[state.name].Add(driver);
                    previous.Add(driver);
                }
            }
            if (group == CurveGroup.All)
            {
                actionOwnsHead = HasHeadMode(states, "Prepare AFK", TrackingType.Animation) &&
                    HasHeadMode(states, "Restore Tracking AFK", TrackingType.Tracking);
                normalizeWeight = GraphCompiler.HasAfkWeightPair(states);
            }
        }
        private static bool SameDriver(VRCAvatarParameterDriver a, VRCAvatarParameterDriver b) =>
            a.localOnly == b.localOnly && a.parameters.Select(p => JsonUtility.ToJson(p))
                .SequenceEqual(b.parameters.Select(p => JsonUtility.ToJson(p)));
        private static bool HasHeadMode(Dictionary<string, AnimatorState> states, string name, TrackingType mode) =>
            states.TryGetValue(name, out var state) && state.behaviours.OfType<VRCAnimatorTrackingControl>()
                .Where(c => c.trackingHead != TrackingType.NoChange).Select(c => c.trackingHead).SequenceEqual(new[] { mode });
    }

    #region 생성 · Action과 FX 그래프

    internal sealed class GraphCompiler
    {
        // 0번 소품 FX는 AFK 해제 즉시 끝나도 몸의 Outro는 계속된다.
        // 공통 진입·복귀에서 Action이 머리를 제어하는 경우에만 FX의 중복 지시를 없앤다.
        internal static void KeepAfkHeadTrackingOnAction(AnimatorController fx)
        {
            VRCAnimatorTrackingControl[] HeadControls(AnimatorStateMachine root, string stateName) =>
                root.states.Where(s => s.state.name == stateName).SelectMany(s => s.state.behaviours)
                    .OfType<VRCAnimatorTrackingControl>().Where(c => c.trackingHead != TrackingType.NoChange).ToArray();
            const string enter = "Prepare AFK", restore = "Restore Tracking AFK";
            foreach (var layer in fx.layers)
            foreach (var state in new[] { enter, restore })
            foreach (var control in HeadControls(layer.stateMachine, state))
            {
                if (control.trackingHead != (state == enter ? TrackingType.Animation : TrackingType.Tracking)) continue;
                control.trackingHead = TrackingType.NoChange;
                EditorUtility.SetDirty(control);
            }
        }

        private readonly AnimatorController output;
        private readonly string folder;
        private readonly GraphAssets assets;
        public GraphCompiler(AnimatorController output, string folder, GraphAssets assets)
        { this.output = output; this.folder = folder; this.assets = assets; }
        private ControllerShape Clone(Module module, CurveGroup group, ClipCompiler compiler, BuildSettings settings)
        {
            var (merge, inputShape) = module.layers[group];
            var source = inputShape.controller;
            var replaced = inputShape.ReplacedStates(settings.sceneAvatar != null);
            var owned = GraphUtility.Reachable(source, replaced).Where(o => o is AnimatorStateMachine || o is AnimatorState || o is AnimatorTransitionBase || o is BlendTree || o is StateMachineBehaviour).ToArray();
            var map = new Dictionary<Object, Object>();
            foreach (var obj in owned)
            {
                map[obj] = CopyGraphObject(obj);
            }
            if (replaced != null)
                foreach (var state in replaced)
                {
                    var copy = (AnimatorState)map[state];
                    copy.motion = null;
                    copy.behaviours = Array.Empty<StateMachineBehaviour>();
                }
            foreach (var obj in map.Values) GraphUtility.RewriteReferences(obj, old => map.TryGetValue(old, out var next) ? next : old is AnimationClip clip ? compiler.Compile(clip) : old);
            foreach (var state in GraphUtility.States(inputShape.root))
                compiler.ApplyCompanion(state, (AnimatorState)map[state]);
            var s = inputShape;
            var shape = new ControllerShape { controller = output, root = (AnimatorStateMachine)map[s.root], prepare = (AnimatorState)map[s.prepare] };
            foreach (var b in s.branches.Values) shape.branches[b.localId] = new Branch { localId = b.localId, machine = (AnimatorStateMachine)map[b.machine], entry = (AnimatorStateTransition)map[b.entry] };
            AddParameters(source);
            if (inputShape.singleMotion) AdaptSingleMotion(shape);
            return shape;
        }

        private void AddParameters(AnimatorController source)
        {
            foreach (var parameter in source.parameters)
            {
                var current = output.parameters.FirstOrDefault(p => p.name == parameter.name);
                if (current != null && current.type != parameter.type) throw new InvalidOperationException(AfkText.ParameterTypeConflict + parameter.name);
                if (current == null) output.AddParameter(parameter);
            }
        }

        // 추가 상품의 루트는 전환 목적지를 대조할 때만 쓴다. 선택한 분기의 의존성만 복제한다.
        private ControllerShape CloneBranches(ControllerShape input, HashSet<int> ids, ClipCompiler compiler)
        {
            var result = new ControllerShape { controller = output, root = input.root, prepare = input.prepare };
            var branches = input.branches.Values.Where(b => ids.Contains(b.localId)).ToArray();
            var nodes = new HashSet<Object>();
            var seeds = new List<Object>();
            foreach (var branch in branches)
            {
                nodes.UnionWith(GraphUtility.Machines(branch.machine));
                nodes.UnionWith(GraphUtility.States(branch.machine));
                seeds.Add(branch.machine);
                seeds.Add(branch.entry);
                seeds.AddRange(input.root.GetStateMachineTransitions(branch.machine));
            }
            var map = new Dictionary<Object, Object>();
            foreach (var obj in GraphUtility.Reachable(seeds, null, nodes))
            {
                if (!(obj is AnimatorStateMachine || obj is AnimatorState || obj is AnimatorTransitionBase ||
                    obj is BlendTree || obj is StateMachineBehaviour)) continue;
                map[obj] = CopyGraphObject(obj);
            }
            foreach (var obj in map.Values)
                GraphUtility.RewriteReferences(obj, old => map.TryGetValue(old, out var clone) ? clone :
                    old is AnimationClip clip ? compiler.Compile(clip) : old);
            foreach (var b in branches)
            {
                result.branches.Add(b.localId, new Branch { localId = b.localId,
                    machine = (AnimatorStateMachine)map[b.machine], entry = (AnimatorStateTransition)map[b.entry],
                    exits = input.root.GetStateMachineTransitions(b.machine).Select(t => (AnimatorTransition)map[t]).ToArray() });
            }
            if (input.singleMotion && branches.Length > 0)
            {
                foreach (var state in nodes.OfType<AnimatorState>())
                    compiler.ApplyCompanion(state, (AnimatorState)map[state]);
                AdaptSingleMotion(result);
            }
            AddParameters(input.controller);
            return result;
        }

        private Object CopyGraphObject(Object obj)
        {
            // Animator 네이티브 객체는 Instantiate 대신 CopySerialized로 복제한다.
            var copy = obj is StateMachineBehaviour ? ScriptableObject.CreateInstance(obj.GetType()) : (Object)System.Activator.CreateInstance(obj.GetType());
            assets.Track(copy);
            EditorUtility.CopySerialized(obj, copy);
            copy.name = obj.name;
            copy.hideFlags = HideFlags.HideInHierarchy;
            return copy;
        }

        // Phone 원본에는 TypeAFK가 없다. 복사한 전이에만 선택 조건을 넣고,
        // AFK 중 메뉴가 바뀌면 Outro에서 소품을 정리한 뒤 Prepare가 다음 모션을 선택하게 한다.
        private void AdaptSingleMotion(ControllerShape shape)
        {
            var branch = shape.branches[1];
            branch.entry.AddCondition(AnimatorConditionMode.Equals, 1, "TypeAFK");
            foreach (var state in GraphUtility.States(branch.machine))
            foreach (var t in state.transitions)
            {
                if (t.conditions.Length == 0) continue;
                if (t.conditions.Length != 1 || t.conditions[0].parameter != "AFK" || !t.destinationState)
                    throw new InvalidOperationException(AfkText.SingleBranchTransition + state.name);
                if (t.conditions[0].mode == AnimatorConditionMode.If)
                    t.AddCondition(AnimatorConditionMode.Equals, 1, "TypeAFK");
                else if (t.conditions[0].mode == AnimatorConditionMode.IfNot)
                {
                    var target = t.destinationState.name == "BlendOut AFK" ? shape.prepare : t.destinationState;
                    var change = state.AddTransition(target);
                    EditorUtility.CopySerialized(t, change);
                    change.destinationState = target;
                    change.conditions = Array.Empty<AnimatorCondition>();
                    change.AddCondition(AnimatorConditionMode.If, 0, "AFK");
                    change.AddCondition(AnimatorConditionMode.NotEqual, 1, "TypeAFK");
                }
                else throw new InvalidOperationException(AfkText.SingleBranchConditions + state.name);
            }
            if (!output.parameters.Any(p => p.name == "TypeAFK")) output.AddParameter("TypeAFK", AnimatorControllerParameterType.Int);
        }

        public AnimatorStateMachine Merge(BuildSettings settings, Analysis analysis, CurveGroup group, Dictionary<Module, string> roots)
        {
            var plan = analysis.layerPlans[group];
            var modules = plan.modules;
            var graphs = new Dictionary<Module, ControllerShape>();
            var compilers = new Dictionary<Module, ClipCompiler>();
            for (int i = 0; i < modules.Length; i++)
            {
                var m = modules[i];
                var clips = folder + "/" + group + "_" + i;
                var compiler = new ClipCompiler(m, m.layers[group].merge, group, roots[m], clips,
                    settings.sceneAvatar ?? settings.validationAvatar, assets);
                compilers.Add(m, compiler);
                graphs.Add(m, i == 0 ? Clone(m, group, compiler, settings) :
                    CloneBranches(m.layers[group].shape, plan.ids[m], compiler));

            }
            var canonical = graphs[analysis.defaultSource];
            var root = canonical.root;
            // 원본의 기본 분기 위치를 기준으로 사용한다. 단일 모션 원본은 기존 1번 자리가 기준이다.
            var templateBranch = canonical.branches.TryGetValue(0, out var originalDefault) ? originalDefault : canonical.branches[1];
            var defaultPosition = root.stateMachines.First(child => child.stateMachine == templateBranch.machine).position;
            if (!canonical.branches.ContainsKey(0))
            {
                // AvatarDefault.Write populates this slot from the scene descriptor.
                var placeholder = root.AddStateMachine("AFK_Default");
                var entry = canonical.prepare.AddTransition(placeholder);
                entry.hasExitTime = false; entry.duration = 0;
                entry.AddCondition(AnimatorConditionMode.If, 0, "AFK");
                entry.AddCondition(AnimatorConditionMode.Equals, 0, "TypeAFK");
                canonical.branches[0] = new Branch { localId = 0, entry = entry, machine = placeholder };
            }
            var rootStates = root.states.Select(s => s.state).ToDictionary(s => s.name);
            var selected = new List<(ControllerShape graph, Branch branch, int id)> { (canonical, canonical.branches[0], 0) };
            foreach (var motion in analysis.selectedMotions)
            {
                var graph = graphs[motion.module];
                selected.Add((graph, graph.branches[motion.localId], motion.choice.id));
            }
            var machineExits = selected.ToDictionary(item => item.branch, item => item.branch.exits ?? item.graph.root.GetStateMachineTransitions(item.branch.machine));
            root.stateMachines = Array.Empty<ChildAnimatorStateMachine>();
            canonical.prepare.transitions = Array.Empty<AnimatorStateTransition>();
            var entries = new List<AnimatorStateTransition>(selected.Count);
            const float branchRowHeight = 50f;
            var branchPosition = defaultPosition;
            foreach (var item in selected)
            {
                var branchObjects = GraphUtility.Machines(item.branch.machine).Cast<Object>()
                    .Concat(GraphUtility.States(item.branch.machine)).ToHashSet();
                var branchTransitions = new HashSet<AnimatorTransitionBase>();
                foreach (var sm in GraphUtility.Machines(item.branch.machine))
                {
                    foreach (var t in sm.anyStateTransitions) branchTransitions.Add(t);
                    foreach (var t in sm.entryTransitions) branchTransitions.Add(t);
                    foreach (var child in sm.stateMachines) foreach (var t in sm.GetStateMachineTransitions(child.stateMachine)) branchTransitions.Add(t);
                }
                foreach (var state in GraphUtility.States(item.branch.machine)) foreach (var t in state.transitions) branchTransitions.Add(t);
                branchTransitions.Add(item.branch.entry);
                foreach (var t in machineExits[item.branch]) branchTransitions.Add(t);
                foreach (var t in branchTransitions)
                {
                    GraphUtility.RemapType(t, item.branch.localId, item.id);
                    GraphUtility.RewriteReferences(t, old =>
                    {
                        if (branchObjects.Contains(old)) return old;
                        if (old is AnimatorState state)
                        {
                            if (rootStates.TryGetValue(state.name, out var replacement)) return replacement;
                            throw new InvalidOperationException(AfkText.OutsideBranch + state.name);
                        }
                        if (old is AnimatorStateMachine machine && machine == item.graph.root) return root;
                        return old;
                    });
                }
                foreach (var driver in GraphUtility.States(item.branch.machine).SelectMany(s => s.behaviours).OfType<VRCAvatarParameterDriver>())
                    if (driver.parameters.Any(p => p.name == "TypeAFK")) throw new InvalidOperationException(AfkText.InternalTypeDriver);
                item.branch.machine.name = "AFK_" + item.id + "_" + item.branch.machine.name.Replace("AFK_", "");
                root.AddStateMachine(item.branch.machine, branchPosition);
                branchPosition.y += branchRowHeight;
                root.SetStateMachineTransitions(item.branch.machine, machineExits[item.branch]);
                entries.Add(item.branch.entry);
            }
            canonical.prepare.transitions = entries.ToArray();
            // Combine common reset motions per family, and preserve additional Busy drivers.
            foreach (var state in rootStates.Values)
            {
                if (group != CurveGroup.All)
                {
                    var motions = new List<Motion> { state.motion };
                    foreach (var donor in plan.donors[state.name])
                    {
                        var clipCompiler = compilers[donor.module];
                        motions.Add(clipCompiler.CommonMotion(donor.state));
                    }
                    state.motion = ClipCompiler.Union(motions, group + "_" + state.name, folder, assets);
                }
                foreach (var driver in plan.drivers[state.name])
                {
                    var copy = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
                    assets.Track(copy);
                    EditorUtility.CopySerialized(driver, copy);
                    state.behaviours = state.behaviours.Concat(new[] { copy }).ToArray();
                }
            }
            if (plan.normalizeWeight) NormalizeAfkActionWeight(rootStates);
            // Saved values from a removed motion recover to character default, without another synced parameter.
            var reset = root.AddState("Recover invalid TypeAFK", defaultPosition + Vector3.down * branchRowHeight);
            var pd = reset.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            pd.localOnly = true;
            pd.parameters.Add(new VRC.SDKBase.VRC_AvatarParameterDriver.Parameter { name = "TypeAFK", value = 0, type = VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set });
            var invalid = canonical.prepare.AddTransition(reset); invalid.hasExitTime = false; invalid.duration = 0;
            foreach (int id in selected.Select(s => s.id)) invalid.AddCondition(AnimatorConditionMode.NotEqual, id, "TypeAFK");
            var back = reset.AddTransition(canonical.prepare); back.hasExitTime = false; back.duration = 0;
            back.AddCondition(AnimatorConditionMode.Equals, 0, "TypeAFK");
            var nativeFx = group == CurveGroup.Face ? analysis.defaultFx : null;
            (nativeFx ?? analysis.defaultAction)?.Write(canonical, group, folder, nativeFx != null, assets);
            return root;
        }

        // 진입 시 Action을 즉시 켜고, 종료 자세 전환이 끝난 Restore에서 끈다.
        // 기본 템플릿의 AFK 제어만 보정한다. Emote/Seated 및 분기 내부 전환 시간은 유지한다.
        internal static bool HasAfkWeightPair(Dictionary<string, AnimatorState> states)
        {
            if (!states.TryGetValue("BlendOut AFK", out var blendOut) ||
                !states.TryGetValue("Restore Tracking AFK", out var restore) || !states.TryGetValue("Prepare AFK", out var prepare)) return false;
            bool IsAction(VRCPlayableLayerControl control) =>
                control.layer == VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.Action;
            var enter = prepare.behaviours.OfType<VRCPlayableLayerControl>().Where(IsAction).ToArray();
            var early = blendOut.behaviours.OfType<VRCPlayableLayerControl>().Where(IsAction).ToArray();
            var late = restore.behaviours.OfType<VRCPlayableLayerControl>().Where(IsAction).ToArray();
            if (enter.Length != 1 || enter[0].goalWeight != 1 || early.Length + late.Length != 1) return false;
            var release = early.Length == 1 ? early[0] : late[0];
            if (release.goalWeight != 0) return false;
            if (early.Length == 0) return true;
            var exits = blendOut.transitions.Where(t => !t.mute).ToArray();
            return exits.Length > 0 && exits.All(t => t.destinationState == restore);
        }

        private static void NormalizeAfkActionWeight(Dictionary<string, AnimatorState> states)
        {
            var blendOut = states["BlendOut AFK"];
            var restore = states["Restore Tracking AFK"];
            VRCPlayableLayerControl Control(AnimatorState state) => state.behaviours.OfType<VRCPlayableLayerControl>()
                .SingleOrDefault(c => c.layer == VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.Action);
            var enter = Control(states["Prepare AFK"]);
            var early = Control(blendOut);
            var release = early ? early : Control(restore);
            if (early)
            {
                blendOut.behaviours = blendOut.behaviours.Where(b => b != release).ToArray();
                restore.behaviours = new StateMachineBehaviour[] { release }.Concat(restore.behaviours).ToArray();
            }
            enter.blendDuration = release.blendDuration = 0f;
        }
    }

    #endregion

    #region 생성 · 클립과 오브젝트 경로

    // 통합이 끝난 뒤 실제 Action/FX에서 참조하는 클립과 그래프 객체만 저장한다.
    internal sealed class GraphAssets : IDisposable
    {
        private readonly HashSet<Object> temporary = new HashSet<Object>();
        private readonly Dictionary<AnimationClip, string> clipPaths = new Dictionary<AnimationClip, string>();
        private readonly Analysis analysis;
        internal GraphAssets(Analysis analysis) { this.analysis = analysis; }
        internal void Warn(string message) => analysis.Warn(message);
        internal void Track(Object obj) => temporary.Add(obj);
        internal void QueueClip(AnimationClip clip, string path)
        {
            Track(clip);
            clipPaths.Add(clip, path);
        }

        internal void Save(params AnimatorController[] controllers)
        {
            var pending = controllers.Select(c => (controller: c, objects: GraphUtility.Reachable(c).ToArray())).ToArray();
            var folders = pending.SelectMany(p => p.objects).OfType<AnimationClip>()
                .Where(clipPaths.ContainsKey).Select(c => Path.GetDirectoryName(clipPaths[c]).Replace('\\', '/')).Distinct();
            foreach (var folder in folders) GraphUtility.EnsureFolder(folder);
            // 경로와 참조를 확정한 뒤 저장한다. 중간 실패 시에도 임포트를 재개한다.
            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var graph in pending)
                {
                    foreach (var obj in graph.objects)
                    {
                        if (EditorUtility.IsPersistent(obj)) continue;
                        if (obj is AnimationClip clip && clipPaths.TryGetValue(clip, out var path))
                        {
                            var candidate = path;
                            for (int suffix = 1; !usedPaths.Add(candidate); suffix++)
                                candidate = Path.ChangeExtension(path, null) + "_" + suffix + ".anim";
                            string name = clip.name;
                            AssetDatabase.CreateAsset(clip, candidate);
                            clip.name = name;
                            EditorUtility.SetDirty(clip);
                        }
                        else if (obj is AnimatorStateMachine || obj is AnimatorState || obj is AnimatorTransitionBase ||
                            obj is StateMachineBehaviour || obj is BlendTree)
                        {
                            obj.hideFlags = HideFlags.HideInHierarchy;
                            AssetDatabase.AddObjectToAsset(obj, graph.controller);
                        }
                        else throw new InvalidOperationException(AfkText.UnsavedReference + obj.name);
                    }
                    EditorUtility.SetDirty(graph.controller);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        public void Dispose()
        {
            foreach (var obj in temporary)
                if (obj && !EditorUtility.IsPersistent(obj)) Object.DestroyImmediate(obj);
        }
    }

    internal sealed class ClipCompiler
    {
        private readonly Module module;
        private readonly GameObject avatar;
        private readonly ModularAvatarMergeAnimator merge;
        private readonly CurveGroup group;
        private readonly string rootPath, folder;
        private readonly Dictionary<AnimationClip, AnimationClip> cache = new Dictionary<AnimationClip, AnimationClip>();
        private int index;
        private readonly GraphAssets assets;
        private readonly Dictionary<AnimatorState, (AnimationClip clip, int index)> companions = new Dictionary<AnimatorState, (AnimationClip, int)>();
        private readonly AnimationClipRenamer[] renamers;
        private readonly string relativeOwner;
        private readonly Dictionary<AnimationClip, AnimationClipRenamer[]> clipRules = new Dictionary<AnimationClip, AnimationClipRenamer[]>();
        public ClipCompiler(Module module, ModularAvatarMergeAnimator merge, CurveGroup group, string rootPath, string folder, GameObject avatar, GraphAssets assets)
        {
            this.module = module; this.merge = merge; this.group = group; this.rootPath = rootPath; this.folder = folder;
            this.avatar = avatar; this.assets = assets;
            renamers = module.prefab.GetComponentsInChildren<AnimationClipRenamer>(true);
            relativeOwner = AnimationUtility.CalculateTransformPath(merge.transform, module.prefab.transform);
            var shape = module.layers[group].shape;
            if (shape.singleMotion && GraphUtility.HasSyncedCompanion(shape.controller))
            {
                var layer = shape.controller.layers[1];
                foreach (var state in GraphUtility.States(shape.root))
                {
                    var motion = layer.GetOverrideMotion(state);
                    if (!motion || motion == state.motion) continue;
                    if (!(motion is AnimationClip clip)) throw new InvalidOperationException(AfkText.SyncedMotionNotClip + state.name);
                    companions.Add(state, (clip, companions.Count));
                }
            }
        }

        internal Motion CommonMotion(AnimatorState state)
        {
            if (state.motion && !(state.motion is AnimationClip))
                throw new InvalidOperationException(AfkText.CommonFxBlendTree + state.name);
            return CompanionMotion(state, Compile(state.motion as AnimationClip));
        }

        internal void ApplyCompanion(AnimatorState source, AnimatorState copy)
        {
            if (companions.ContainsKey(source)) copy.motion = CompanionMotion(source, copy.motion);
        }

        private Motion CompanionMotion(AnimatorState source, Motion motion)
        {
            if (!companions.TryGetValue(source, out var companion)) return motion;
            return Union(new[] { motion, Compile(companion.clip) }, "Synced_" + companion.index, folder, assets);
        }

        public AnimationClip Compile(AnimationClip original)
        {
            if (!original) return null;
            if (cache.TryGetValue(original, out var known)) return known;
            var source = GraphUtility.EffectiveClip(merge.animator, original);
            var output = UnityEngine.Object.Instantiate(source);
            output.name = source.name;
            foreach (var b in AnimationUtility.GetCurveBindings(output)) AnimationUtility.SetEditorCurve(output, b, null);
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(output)) AnimationUtility.SetObjectReferenceCurve(output, b, null);
            var bindings = new HashSet<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                var next = Map(source, b, out bool owned);
                if (!Include(owned)) continue;
                if (!bindings.Add(next.path + "|" + next.type + "|" + next.propertyName)) throw new InvalidOperationException(AfkText.CurveCollision + source.name);
                AnimationUtility.SetEditorCurve(output, next, AnimationUtility.GetEditorCurve(source, b));
            }
            foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                var next = Map(source, b, out bool owned); if (!Include(owned)) continue;
                if (!bindings.Add(next.path + "|" + next.type + "|" + next.propertyName)) throw new InvalidOperationException(AfkText.CurveCollision + source.name);
                AnimationUtility.SetObjectReferenceCurve(output, next, AnimationUtility.GetObjectReferenceCurve(source, b));
            }
            // SDK motion names are significant to VRSuya ChangeStandingPose.
            if (!output.name.StartsWith("proxy_")) output.name = module.prefab.name + "_" + group + "_" + source.name;
            assets.QueueClip(output, folder + "/Clip_" + index++ + ".anim");
            cache[original] = output; return output;
        }
        private bool Include(bool owned) { return group == CurveGroup.All || (group == CurveGroup.Objects ? owned : !owned); }
        private EditorCurveBinding Map(AnimationClip clip, EditorCurveBinding binding, out bool owned)
        {
            if (!clipRules.TryGetValue(clip, out var rules))
                clipRules[clip] = rules = renamers.Where(r => r.TargetAnimationClips?.Any(c => c && (c == clip || c.name == clip.name)) == true).ToArray();
            foreach (var renamer in rules)
            {
                if (renamer.TargetPathRenameList != null)
                    foreach (var r in renamer.TargetPathRenameList) if (!string.IsNullOrEmpty(r.Before) && (binding.path == r.Before || binding.path.StartsWith(r.Before + "/", StringComparison.Ordinal))) binding.path = (r.After ?? "") + binding.path.Substring(r.Before.Length);
                if (renamer.TargetBlendshapeRenameList != null)
                    foreach (var r in renamer.TargetBlendshapeRenameList) if (!string.IsNullOrEmpty(r.Before) && binding.propertyName == "blendShape." + r.Before) binding.propertyName = "blendShape." + (r.After ?? "");
            }
            owned = false;
            // Humanoid Animator bindings always address the avatar Animator.
            if (binding.type == typeof(Animator) && string.IsNullOrEmpty(binding.path)) return binding;
            string relative = null;
            if (merge.pathMode == MergeAnimatorPathMode.Relative)
            {
                string owner = relativeOwner;
                // Explicit relative roots outside this prefab cannot be resolved safely here.
                if (!string.IsNullOrEmpty(merge.relativePathRoot.referencePath))
                    throw new InvalidOperationException(AfkText.ExplicitRelativeRoot + module.prefab.name);
                relative = string.IsNullOrEmpty(owner) ? binding.path : owner + (string.IsNullOrEmpty(binding.path) ? "" : "/" + binding.path);
                if (relative.Length > 0 && !module.prefab.transform.Find(relative))
                {
                    // Sio의 서기 클립처럼 상품 소품과 별도로 캐릭터 자체의 오브젝트를 제어할 수 있다.
                    if (avatar && AnimationUtility.GetAnimatedObject(avatar, binding)) return binding;
                    throw new InvalidOperationException(string.Format(AfkText.MissingBinding, module.prefab.name, clip.name, binding.path, binding.propertyName, relative, avatar ? avatar.name : AfkText.Unspecified));
                }
            }
            else
            {
                if (binding.path == module.prefab.name) relative = "";
                else if (binding.path.StartsWith(module.prefab.name + "/", StringComparison.Ordinal)) relative = binding.path.Substring(module.prefab.name.Length + 1);
            }
            if (relative != null) { owned = true; binding.path = rootPath + (relative.Length == 0 ? "" : "/" + relative); }
            return binding;
        }

        public static AnimationClip Union(IEnumerable<Motion> motions, string name, string folder, GraphAssets assets)
        {
            var inputs = motions.Where(m => m).Distinct().ToArray();
            if (inputs.Any(m => !(m is AnimationClip))) throw new InvalidOperationException(AfkText.CommonBlendTree + name);
            if (inputs.Length == 0) return null;
            var result = UnityEngine.Object.Instantiate((AnimationClip)inputs[0]); result.name = name;
            // Canonical default source has precedence for shared avatar bindings; other modules add their prop resets.
            var floatKeys = new HashSet<EditorCurveBinding>(AnimationUtility.GetCurveBindings(result));
            var objectKeys = new HashSet<EditorCurveBinding>(AnimationUtility.GetObjectReferenceCurveBindings(result));
            foreach (var clip in inputs.Skip(1).Cast<AnimationClip>())
            {
                var conflicts = new List<string>();
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    var next = AnimationUtility.GetEditorCurve(clip, b);
                    if (floatKeys.Add(b)) AnimationUtility.SetEditorCurve(result, b, next);
                    else
                    {
                        var current = AnimationUtility.GetEditorCurve(result, b);
                        if (current.preWrapMode != next.preWrapMode || current.postWrapMode != next.postWrapMode ||
                            !current.keys.SequenceEqual(next.keys)) conflicts.Add(b.path + "/" + b.propertyName);
                    }
                }
                foreach (var b in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var next = AnimationUtility.GetObjectReferenceCurve(clip, b);
                    if (objectKeys.Add(b)) AnimationUtility.SetObjectReferenceCurve(result, b, next);
                    else if (!AnimationUtility.GetObjectReferenceCurve(result, b).SequenceEqual(next)) conflicts.Add(b.path + "/" + b.propertyName);
                }
                if (conflicts.Count > 0) assets.Warn(string.Format(AfkText.OverlappingCurves, name, clip.name, conflicts.Count, conflicts[0]));
            }
            result.name = name.Replace(' ', '_');
            assets.QueueClip(result, folder + "/" + result.name + ".anim");
            return result;
        }
    }

    #endregion

    #region 캐릭터 기본 AFK · 원본 읽기와 0번 분기 생성

    // The descriptor is the source of truth. The product supplies only the surrounding selector.
    internal sealed class AvatarDefault
    {
        private readonly RuntimeAnimatorController runtime;
        private readonly AnimatorState start;
        private readonly AnimatorState exit;
        private readonly AnimatorState selector;
        private readonly List<AnimatorState> states = new List<AnimatorState>();
        public string Source => AssetDatabase.GetAssetPath(runtime);
        public GameObject Avatar { get; }

        internal sealed class UnsupportedGraph : InvalidOperationException
        {
            public UnsupportedGraph(string message) : base(message) { }
        }

        private static UnsupportedGraph Unsupported(string reason)
        {
            return new UnsupportedGraph(AfkText.AvatarDefaultReason + reason);
        }

        public static AvatarDefault Read(GameObject avatar)
        {
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            RuntimeAnimatorController runtime = null;
            if (descriptor.customizeAnimationLayers)
            {
                var actions = descriptor.baseAnimationLayers?.Where(l => l.type == VRCAvatarDescriptor.AnimLayerType.Action).ToArray();
                if (actions != null && actions.Length > 1) throw new InvalidOperationException(AfkText.MultipleActionSlots);
                if (actions?.Length == 1 && !actions[0].isDefault) runtime = actions[0].animatorController;
            }
            if (!runtime) return ReadSdk(avatar);
            var controller = GraphUtility.BaseController(runtime);
            if (!controller || controller.layers.Length != 1 || controller.layers[0].avatarMask)
                throw Unsupported(AfkText.UnknownActionPath);
            return new AvatarDefault(runtime, avatar, controller.layers[0].stateMachine);
        }

        public static AvatarDefault ReadSdk(GameObject avatar)
        {
            var path = AssetDatabase.GUIDToAssetPath("3e479eeb9db24704a828bffb15406520");
            var sdk = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!sdk || sdk.layers.Length != 1)
                throw new InvalidOperationException(AfkText.MissingSdkAction);
            try { return new AvatarDefault(sdk, avatar, sdk.layers[0].stateMachine); }
            catch (UnsupportedGraph e) { throw new InvalidOperationException(AfkText.InvalidSdkAfk, e); }
        }

        // Installer removes named native FX layers at build time. Capture their AFK motion
        // from the descriptor before that pass, so it can follow the combined selector.
        private AnimatorController NativeFx(out RuntimeAnimatorController runtime)
        {
            var d = Avatar.GetComponent<VRCAvatarDescriptor>();
            runtime = null;
            if (!d.customizeAnimationLayers || d.baseAnimationLayers == null) return null;
            var fx = d.baseAnimationLayers.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
            if (!fx.isDefault) runtime = fx.animatorController;
            return runtime ? GraphUtility.BaseController(runtime) : null;
        }

        // 다른 상품만 설치해도 원본 AFK 소품이 따라 나오지 않도록, 빈 기본 상태와
        // AFK on/off 전이만 가진 독립 레이어를 찾는다. 표정 등 복합 레이어는 유지한다.
        public IEnumerable<string> DedicatedFxToggles()
        {
            var controller = NativeFx(out _);
            if (!controller) yield break;
            var layers = controller.layers;
            for (int i = 1; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (!(layer.name == "AFK" || layer.name.EndsWith("_AFK", StringComparison.Ordinal)) ||
                    layers.Count(l => l.name == layer.name) != 1 || layer.syncedLayerIndex >= 0 ||
                    layers.Any(l => l.syncedLayerIndex == i)) continue;
                var sm = layer.stateMachine;
                if (!sm || sm.states.Length != 2 || sm.stateMachines.Length != 0 || sm.behaviours.Length != 0 ||
                    sm.entryTransitions.Length != 0 || sm.anyStateTransitions.Length != 0) continue;
                var off = sm.defaultState;
                var on = sm.states.Select(child => child.state).FirstOrDefault(st => st != off);
                if (off && on && !off.motion && on.motion && off.behaviours.Length == 0 &&
                    on.behaviours.All(b => b is VRCAnimatorTrackingControl) && IsAfkToggle(off, on, AnimatorConditionMode.If) &&
                    IsAfkToggle(on, off, AnimatorConditionMode.IfNot)) yield return layer.name;
            }
        }

        private static bool IsAfkToggle(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            if (from.transitions.Length != 1) return false;
            var t = from.transitions[0];
            return t.destinationState == to && !t.isExit && !t.mute && !t.solo && t.conditions.Length == 1 &&
                t.conditions[0].mode == mode && t.conditions[0].parameter == "AFK";
        }

        public AvatarDefault ReadRemovedFx(IEnumerable<string> removedLayers)
        {
            var controller = NativeFx(out var fx);
            if (!controller) return null;
            var candidates = controller.layers.Where(l => removedLayers.Contains(l.name) && !l.name.Contains("AFK/") &&
                GraphUtility.Reachable(l.stateMachine).OfType<AnimatorTransitionBase>().Any(t => t.conditions.Any(c => c.parameter == "AFK"))).ToArray();
            if (candidates.Length == 0) return null;
            if (candidates.Length != 1 || candidates[0].avatarMask || candidates[0].syncedLayerIndex >= 0 ||
                candidates[0].blendingMode != AnimatorLayerBlendingMode.Override || candidates[0].defaultWeight != 1f)
                throw new UnsupportedGraph(AfkText.UnsupportedDefaultFx + controller.name);
            try
            {
                var native = new AvatarDefault(fx, Avatar, candidates[0].stateMachine);
                if (native.exit.motion && !(native.exit.motion is AnimationClip))
                    throw Unsupported(AfkText.FxExitClone + native.exit.name);
                return native;
            }
            catch (UnsupportedGraph e) { throw new UnsupportedGraph(AfkText.AvatarFx + controller.name + " / " + candidates[0].name + " — " + e.Message); }
        }

        private AvatarDefault(RuntimeAnimatorController runtime, GameObject avatar, AnimatorStateMachine root)
        {
            Avatar = avatar;
            this.runtime = runtime;
            var all = GraphUtility.States(root).ToArray();
            var entries = all.SelectMany(s => s.transitions).Concat(GraphUtility.Machines(root).SelectMany(m => m.anyStateTransitions))
                .Where(t => t.conditions.Any(c => c.parameter == "AFK" && c.mode == AnimatorConditionMode.If))
                .Select(t => t.destinationState).Where(st => st).Distinct().ToArray();
            var gates = entries.Where(st => st.name == "Afk Init" || st.name == "Prepare AFK").ToArray();
            // Some avatar controllers already select their native AFK with TypeAFK == 0.
            // Its Outro can return to the selector without ever leaving AFK.
            AnimatorStateMachine zeroMachine = null;
            if (gates.Length == 1 && gates[0].name == "Prepare AFK" && gates[0].transitions.Any(t => t.conditions.Any(IsZeroEntry)))
            {
                var transitions = gates[0].transitions.Where(t => !t.mute).ToArray();
                var zero = transitions.Where(t => t.conditions.Any(IsZeroEntry)).ToArray();
                if (zero.Length != 1 || transitions.Any(t => t.solo || t.isExit || !t.conditions.Any(c => c.parameter == "TypeAFK" && c.mode == AnimatorConditionMode.Equals)) ||
                    zero[0].conditions.Any(c => !IsZeroEntry(c) && !(c.parameter == "AFK" && c.mode == AnimatorConditionMode.If)))
                    throw Unsupported(AfkText.DefaultEntry);
                selector = gates[0];
                start = zero[0].destinationState ? zero[0].destinationState : zero[0].destinationStateMachine?.defaultState;
                zeroMachine = root.stateMachines.Select(c => c.stateMachine).SingleOrDefault(sm => GraphUtility.States(sm).Contains(start));
                if (!zeroMachine || zeroMachine.stateMachines.Length != 0 || zeroMachine.entryTransitions.Length != 0 ||
                    zeroMachine.anyStateTransitions.Length != 0 || root.GetStateMachineTransitions(zeroMachine).Length != 0)
                    throw Unsupported(AfkText.DefaultBoundary);
            }
            else if (gates.Length == 1 && gates[0].transitions.Length == 1 && gates[0].transitions[0].conditions.All(c => c.parameter == "AFK" && c.mode == AnimatorConditionMode.If))
                start = gates[0].transitions[0].destinationState;
            else if (gates.Length == 0 && entries.Length == 1)
                start = entries[0];
            var ends = all.Where(s => s.name == (gates.Length == 1 && gates[0].name == "Prepare AFK" ? "BlendOut AFK" : "BlendOut")).ToArray();
            if (ends.Length == 1) exit = ends[0];
            else if (start && start.transitions.Length == 1 && start.transitions[0].conditions.Length == 1 && IsOff(start.transitions[0].conditions[0]))
                exit = start.transitions[0].destinationState;
            if (!start || !exit)
            {
                var routes = NestedRoutes(root, all).ToArray();
                if (routes.Length != 1) throw Unsupported(AfkText.UnknownEntryExit);
                start = routes[0].Item1;
                exit = routes[0].Item2;
            }
            if (!exit || exit == start) throw Unsupported(AfkText.UnknownExit);

            var visitedStates = new HashSet<AnimatorState>();
            var queue = new Queue<AnimatorState>();
            queue.Enqueue(start);
            bool exits = false;
            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                if (!visitedStates.Add(state)) continue;
                var layerControls = state.behaviours.OfType<VRCAnimatorLayerControl>().ToArray();
                if (layerControls.Length != 0)
                {
                    var owner = GraphUtility.Machines(root).First(sm => sm.states.Any(child => child.state == state));
                    var targets = layerControls.Select(c => c.playable + "[" + c.layer + "]").Distinct();
                    throw Unsupported(string.Format(AfkText.LayerWeights, owner.name, state.name, string.Join(", ", targets)));

                }
                if (!(state.motion is AnimationClip) || state.behaviours.Length > 0 || state.timeParameterActive ||
                    state.speedParameterActive || state.mirrorParameterActive || state.cycleOffsetParameterActive)
                    throw Unsupported(string.Format(AfkText.StateControl, state.name));
                states.Add(state);
                foreach (var t in state.transitions)
                {
                    if (!t.destinationState || t.isExit || t.mute || t.solo || t.conditions.Any(c =>
                        !(c.parameter == "AFK" && (c.mode == AnimatorConditionMode.If || c.mode == AnimatorConditionMode.IfNot)) &&
                        !(selector && (IsZeroEntry(c) || IsZeroChange(c)))))
                        throw Unsupported(string.Format(AfkText.StateConditions, state.name));
                    if (selector && t.destinationState == selector && t.conditions.Any(IsZeroChange)) continue;
                    if (t.destinationState == exit) exits = true;
                    else if (zeroMachine && !zeroMachine.states.Any(child => child.state == t.destinationState))
                        throw Unsupported(string.Format(AfkText.LeavesDefault, state.name));
                    else queue.Enqueue(t.destinationState);
                }
            }
            if (!exits || !states.SelectMany(st => st.transitions).Any(t => t.conditions.Any(IsOff)))
                throw Unsupported(AfkText.MissingOffCondition);
            foreach (var machine in GraphUtility.Machines(root).Where(sm => sm.states.Any(st => visitedStates.Contains(st.state))))
                if (machine.behaviours.Length > 0 || machine.anyStateTransitions.Any(t => !entries.Contains(t.destinationState)))
                    throw Unsupported(AfkText.OtherTransitions);
        }

        // A self-contained motion group can use arbitrary state and machine names.
        // Accept a single entry gated by AFK and one explicit exit back to the parent.
        private static IEnumerable<Tuple<AnimatorState, AnimatorState>> NestedRoutes(AnimatorStateMachine root, AnimatorState[] all)
        {
            foreach (var child in root.stateMachines)
            {
                var machine = child.stateMachine;
                if (machine.stateMachines.Length != 0 || machine.entryTransitions.Length != 0 ||
                    machine.anyStateTransitions.Length != 0 || machine.behaviours.Length != 0 ||
                    root.GetStateMachineTransitions(machine).Length != 0) continue;
                var inside = machine.states.Select(x => x.state).ToHashSet();
                var incoming = all.Where(s => !inside.Contains(s)).SelectMany(s => s.transitions)
                    .Concat(root.anyStateTransitions).Where(t => inside.Contains(t.destinationState) || t.destinationStateMachine == machine).ToArray();
                if (incoming.Length != 1 || incoming[0].conditions.Length != 1 || incoming[0].conditions[0].parameter != "AFK" ||
                    incoming[0].conditions[0].mode != AnimatorConditionMode.If || incoming[0].mute || incoming[0].solo) continue;
                var outgoing = inside.SelectMany(s => s.transitions).Where(t => !inside.Contains(t.destinationState)).ToArray();
                if (outgoing.Length == 0 || outgoing.Any(t => !t.destinationState || !t.conditions.Any(IsOff))) continue;
                var destinations = outgoing.Select(t => t.destinationState).Distinct().ToArray();
                var first = incoming[0].destinationState ? incoming[0].destinationState : machine.defaultState;
                if (first && destinations.Length == 1) yield return Tuple.Create(first, destinations[0]);
            }
        }

        private static bool IsOff(AnimatorCondition c) => c.parameter == "AFK" && c.mode == AnimatorConditionMode.IfNot;
        private static bool IsZeroEntry(AnimatorCondition c) => c.parameter == "TypeAFK" && c.threshold == 0 && c.mode == AnimatorConditionMode.Equals;
        private static bool IsZeroChange(AnimatorCondition c) => c.parameter == "TypeAFK" && c.threshold == 0 && c.mode == AnimatorConditionMode.NotEqual;

        // 분석한 원본 상태와 전환을 통합 컨트롤러의 0번 분기로 복사한다.
        public void Write(ControllerShape shape, CurveGroup group, string folder, bool nativeFx, GraphAssets assets)
        {
            var branch = shape.branches[0];
            var machine = branch.machine;
            machine.name = "AFK_0_AvatarDefault";
            machine.states = Array.Empty<ChildAnimatorState>();
            machine.stateMachines = Array.Empty<ChildAnimatorStateMachine>();
            machine.anyStateTransitions = Array.Empty<AnimatorStateTransition>();
            machine.entryTransitions = Array.Empty<AnimatorTransition>();
            shape.root.SetStateMachineTransitions(machine, Array.Empty<AnimatorTransition>());
            var blendOut = shape.root.states.Single(s => s.state.name == "BlendOut AFK").state;
            if (group != CurveGroup.All && !nativeFx)
            {
                var idle = machine.AddState("Avatar Default");
                idle.writeDefaultValues = false;
                if (group == CurveGroup.Objects) idle.motion = shape.prepare.motion;
                var off = idle.AddTransition(blendOut);
                off.hasExitTime = false; off.duration = 0; off.AddCondition(AnimatorConditionMode.IfNot, 0, "AFK");
                var change = idle.AddTransition(shape.prepare);
                change.hasExitTime = false; change.duration = 0; change.AddCondition(AnimatorConditionMode.NotEqual, 0, "TypeAFK");
                machine.defaultState = idle;
            }
            else
            {
                if (nativeFx && exit.motion)
                {
                    if (!(exit.motion is AnimationClip resetClip))
                        throw new InvalidOperationException(AfkText.DefaultFxExitClone + exit.name);
                    var reset = GraphUtility.EffectiveClip(runtime, resetClip);
                    foreach (var state in new[] { shape.prepare, blendOut })
                        state.motion = ClipCompiler.Union(new Motion[] { reset, state.motion }, "AvatarDefaultFX_" + state.name, folder, assets);
                }
                var map = new Dictionary<AnimatorState, AnimatorState>();
                var clips = new Dictionary<AnimationClip, AnimationClip>();
                foreach (var source in states)
                {
                    var state = machine.AddState(source.name, new Vector3(260, map.Count * 80));
                    EditorUtility.CopySerialized(source, state);
                    state.transitions = Array.Empty<AnimatorStateTransition>();
                    var clip = GraphUtility.EffectiveClip(runtime, (AnimationClip)source.motion);
                    if (!clips.TryGetValue(clip, out var owned))
                    {
                        owned = Object.Instantiate(clip);
                        owned.name = clip.name;
                        assets.QueueClip(owned, folder + (nativeFx ? "/AvatarDefaultFX_" : "/AvatarDefault_") + clips.Count + ".anim");
                        clips.Add(clip, owned);
                    }
                    state.motion = owned; map.Add(source, state);
                }
                foreach (var source in states)
                // With AFK still on, return to selection before a same-time unconditional BlendOut.
                foreach (var original in source.transitions.OrderBy(t => selector && t.destinationState == exit && t.conditions.Length == 0))
                {
                    var target = original.destinationState == exit ? blendOut : original.destinationState == selector ? shape.prepare : map[original.destinationState];
                    var transition = CopyTransition(original, map[source], target);
                    if (original.destinationState == selector && !original.conditions.Any(c => c.parameter == "AFK"))
                        transition.AddCondition(AnimatorConditionMode.If, 0, "AFK");
                    if (original.conditions.Any(c => c.mode == AnimatorConditionMode.If) && !original.conditions.Any(IsZeroEntry))
                        transition.AddCondition(AnimatorConditionMode.Equals, 0, "TypeAFK");
                    if (!original.conditions.Any(IsOff) || source.transitions.Any(t => t.conditions.Any(IsZeroChange))) continue;
                    var change = CopyTransition(original, map[source], target == blendOut ? shape.prepare : target);
                    change.conditions = original.conditions.Where(c => !IsOff(c)).ToArray();
                    change.AddCondition(AnimatorConditionMode.NotEqual, 0, "TypeAFK");
                    change.AddCondition(AnimatorConditionMode.If, 0, "AFK");
                }
                machine.defaultState = map[start];
            }
            branch.entry.destinationState = machine.defaultState;
        }

        private static AnimatorStateTransition CopyTransition(AnimatorStateTransition original, AnimatorState source, AnimatorState destination)
        {
            var transition = source.AddTransition(destination);
            EditorUtility.CopySerialized(original, transition);
            transition.destinationState = destination;
            return transition;
        }
    }

    #endregion

    #region 공통 그래프 연산

    public static class GraphUtility
    {
        public static AnimatorController BaseController(RuntimeAnimatorController controller)
        {
            var seen = new HashSet<RuntimeAnimatorController>();
            while (controller is AnimatorOverrideController overrides)
            {
                if (!seen.Add(controller)) throw new InvalidOperationException(AfkText.OverrideCycle);
                controller = overrides.runtimeAnimatorController;
            }
            return controller as AnimatorController;
        }

        public static AnimationClip EffectiveClip(RuntimeAnimatorController runtime, AnimationClip clip)
        {
            var chain = new List<AnimatorOverrideController>();
            while (runtime is AnimatorOverrideController overrides) { chain.Add(overrides); runtime = overrides.runtimeAnimatorController; }
            var original = clip;
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                var values = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                chain[i].GetOverrides(values);
                var pair = values.FirstOrDefault(p => p.Key == original || p.Key == clip);
                if (pair.Value) clip = pair.Value;
            }
            return clip;
        }

        public static IEnumerable<AnimatorStateMachine> Machines(AnimatorStateMachine root)
        {
            return VRSuya.Core.AnimatorHelper.GetAllStateMachines(root);
        }
        public static IEnumerable<AnimatorState> States(AnimatorStateMachine root)
        {
            return VRSuya.Core.AnimatorHelper.GetAllStates(root);
        }

        public static ControllerShape ReadShape(RuntimeAnimatorController runtime)
        {
            var controller = BaseController(runtime);
            if (!controller || (controller.layers.Length != 1 && !HasSyncedCompanion(controller)))
                throw new InvalidOperationException(AfkText.InputLayerCount);
            var root = controller.layers[0].stateMachine;
            if (!controller.parameters.Any(p => p.name == "AFK" && p.type == AnimatorControllerParameterType.Bool))
                throw new InvalidOperationException(controller.name + AfkText.NeedAfkBool);
            var prepare = root.states.Select(s => s.state).SingleOrDefault(s => s.name == "Prepare AFK");
            if (!prepare) throw new InvalidOperationException(AfkText.MissingPrepare + controller.name);
            var result = new ControllerShape { controller = controller, root = root, prepare = prepare };
            // Original Phone products have one AFK branch and no selector parameter.
            var single = root.stateMachines.Length == 1 ? root.stateMachines[0].stateMachine : null;
            if (single && prepare.transitions.Length == 1 && !controller.parameters.Any(p => p.name == "TypeAFK"))
            {
                var entry = prepare.transitions[0];
                if (entry.conditions.Length != 1 || entry.conditions[0].parameter != "AFK" || entry.conditions[0].mode != AnimatorConditionMode.If ||
                    !(entry.destinationStateMachine == single || States(single).Contains(entry.destinationState)))
                    throw new InvalidOperationException(controller.name + AfkText.SingleEntry);
                result.singleMotion = true;
                result.branches[1] = new Branch { localId = 1, machine = single, entry = entry };
                return result;
            }
            var owners = new Dictionary<AnimatorState, AnimatorStateMachine>();
            if (controller.layers.Length != 1)
                throw new InvalidOperationException(controller.name + AfkText.SyncedSingleOnly);
            if (!controller.parameters.Any(p => p.name == "TypeAFK" && p.type == AnimatorControllerParameterType.Int))
                throw new InvalidOperationException(controller.name + AfkText.NeedTypeInt);
            var children = root.stateMachines.Select(child => child.stateMachine).ToHashSet();
            var destinations = new HashSet<AnimatorStateMachine>();
            foreach (var child in children)
            foreach (var state in States(child))
                if (!owners.TryAdd(state, child)) throw new InvalidOperationException(AfkText.SharedState);
            foreach (var transition in prepare.transitions)
            {
                var condition = transition.conditions.Where(c => c.parameter == "TypeAFK" && c.mode == AnimatorConditionMode.Equals).ToArray();
                if (condition.Length != 1) throw new InvalidOperationException(AfkText.NeedEquals);
                int id = Mathf.RoundToInt(condition[0].threshold);
                if (Mathf.Abs(id - condition[0].threshold) > 0.001f || id < 0 || result.branches.ContainsKey(id))
                    throw new InvalidOperationException(AfkText.InvalidBranch);
                var machine = transition.destinationStateMachine;
                if (!machine && transition.destinationState) owners.TryGetValue(transition.destinationState, out machine);
                if (!machine || !children.Contains(machine)) throw new InvalidOperationException(AfkText.NotIndependentBranch);
                if (!destinations.Add(machine)) throw new InvalidOperationException(AfkText.SameDestination);
                result.branches[id] = new Branch { localId = id, machine = machine, entry = transition };
            }
            if (!result.branches.ContainsKey(0)) throw new InvalidOperationException(AfkText.MissingDefault);
            return result;
        }

        internal static bool HasSyncedCompanion(AnimatorController controller)
        {
            var layers = controller.layers;
            if (layers.Length != 2 || layers[0].syncedLayerIndex >= 0) return false;
            var companion = layers[1];
            return companion.syncedLayerIndex == 0 &&
                !companion.syncedLayerAffectsTiming && !companion.avatarMask && !companion.iKPass &&
                companion.blendingMode == AnimatorLayerBlendingMode.Override && companion.defaultWeight == 1;
        }

        public static void RewriteReferences(Object target, Func<Object, Object> replace)
        {
            var so = new SerializedObject(target);
            var prop = so.GetIterator();
            bool changed = false;
            while (prop.Next(true))
            {
                if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.propertyPath == "m_Script") continue;
                var old = prop.objectReferenceValue;
                if (!old) continue;
                var next = replace(old);
                if (next != old) { prop.objectReferenceValue = next; changed = true; }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static IEnumerable<Object> Reachable(Object root) => Reachable(root, null);

        internal static IEnumerable<Object> Reachable(Object root, HashSet<AnimatorState> replaced)
        {
            return Reachable(new[] { root }, replaced, null);
        }

        internal static IEnumerable<Object> Reachable(IEnumerable<Object> roots, HashSet<AnimatorState> replaced, HashSet<Object> allowedNodes)
        {
            var stack = new Stack<Object>(roots);
            var seen = new HashSet<Object>();
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (!obj || !seen.Add(obj)) continue;
                if (allowedNodes != null && (obj is AnimatorState || obj is AnimatorStateMachine) && !allowedNodes.Contains(obj)) continue;
                yield return obj;
                if (!(obj is AnimatorController || obj is AnimatorStateMachine || obj is AnimatorState || obj is AnimatorTransitionBase || obj is BlendTree || obj is StateMachineBehaviour)) continue;
                var so = new SerializedObject(obj); var p = so.GetIterator();
                bool discardContents = obj is AnimatorState state && replaced != null && replaced.Contains(state);
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.propertyPath == "m_Script") continue;
                    var target = p.objectReferenceValue;
                    if (!target || (discardContents && (target is Motion || target is StateMachineBehaviour))) continue;
                    stack.Push(target);
                }
            }
        }

        public static void RemapType(AnimatorTransitionBase transition, int from, int to)
        {
            var conditions = transition.conditions;
            for (int i = 0; i < conditions.Length; i++)
            {
                if (conditions[i].parameter != "TypeAFK") continue;
                if (conditions[i].mode != AnimatorConditionMode.Equals && conditions[i].mode != AnimatorConditionMode.NotEqual)
                    throw new InvalidOperationException(AfkText.RangeCondition);
                if (Mathf.Abs(conditions[i].threshold - from) > .001f)
                    throw new InvalidOperationException(AfkText.ForeignType);
                conditions[i].threshold = to;
            }
            transition.conditions = conditions;
        }

        public static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); string current = parts[0];
            if (current != "Assets") throw new InvalidOperationException(AfkText.OutputUnderAssets);
            foreach (var part in parts.Skip(1)) { string next = current + "/" + part; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part); current = next; }
        }
    }

    #endregion
}
#endif
