using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEditor;
using UnityEngine;

using Newtonsoft.Json.Linq;

using VRSuya.Core;
using static VRSuya.Core.Translator;

/*
 * VRSuya AvatarPatcher
 * Contact : vrsuya@gmail.com // Twitter : https://twitter.com/VRSuya
 */

namespace VRSuya.Installer {

	public class AvatarPatcher : EditorWindow {

		public GameObject AvatarGameObject;
		public TextAsset JSON_Asset;

		Animator AvatarAnimator;
		Material[] AvatarMaterials;

		AvatarWeight NewAvatarWeight;

		const float Threshold = 0.001f;
		const float BorderX = 30f;

		[MenuItem("Tools/VRSuya/Installer/AvatarPatcher", priority = 1000)]
		static void CreateWindow() {
			AvatarPatcher AppWindow = GetWindowWithRect<AvatarPatcher>(new Rect(0, 0, 400, 215), true, "VRSuya AvatarPatcher");
			AppWindow.Initialize();
		}

		void OnGUI() {
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
			AvatarGameObject = (GameObject)EditorGUILayout.ObjectField(GetTranslatedString("String_Avatar"), AvatarGameObject, typeof(GameObject), true);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			JSON_Asset = (TextAsset)EditorGUILayout.ObjectField("JSON", JSON_Asset, typeof(TextAsset), false);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			EditorGUILayout.HelpBox(GetTranslatedString("String_AvatarPatcher"), MessageType.Error);
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(BorderX);
			GUI.backgroundColor = Color.cyan;
			if (GUILayout.Button(GetTranslatedString("String_Replace"), GUILayout.Height(40f))) {
				RequestPatchAvatar();
				Close();
			}
			GUI.backgroundColor = Color.white;
			GUILayout.Space(BorderX);
			EditorGUILayout.EndHorizontal();
		}

		void Initialize() {
			AvatarGameObject = AvatarUtility.GetAvatarGameObject();
		}

		public GameObject RequestPatchAvatar() {
			if (VerifyVariable(out GameObject NewAvatarGameObject)) {
				JObject JSON_Object = JObject.Parse(JSON_Asset.text);
				string AvatarName = AvatarGameObject.name;
				AvatarGameObject.name = $"{AvatarName} (Backup)";
				AvatarGameObject.SetActive(false);
				NewAvatarGameObject.name = AvatarName;
				NewAvatarGameObject.transform.SetSiblingIndex(AvatarGameObject.transform.GetSiblingIndex() + 1);
				Dictionary<string, Transform> NewBoneTransforms = GetNewBoneTransfom(NewAvatarGameObject);
				if (PatchAvatar(AvatarGameObject, NewAvatarGameObject, NewBoneTransforms)) {
					EditorUtility.DisplayDialog("VRSuya AvatarPatcher",
						string.Format(GetTranslatedString("COMPLETED_PATCH"), AvatarName),
						GetTranslatedString("String_Okay")
					);
					return NewAvatarGameObject;
				}
			}
			return null;
		}

		AvatarWeight GetAvatarWeight(JObject JSON_Object) {
			AvatarWeight NewAvatarWeight = null;
			JToken Version_Data = JSON_Object["JsonVersion"];
			JToken AvatarName_Data = JSON_Object["TargetAvatar"];
			JToken AvatarVersion_Data = JSON_Object["TargetAvatarVersion"];
			JToken ArmatureName_Data = JSON_Object["ArmatureObjectName"];
			JArray Bone_Data = (JArray)JSON_Object["Bones"];
			JArray Object_Data = (JArray)JSON_Object["TargetObjects"];
			int Version = Version_Data.Value<int>();
			string AvatarName = AvatarName_Data.Value<string>();
			string AvatarVersion = AvatarVersion_Data.Value<string>();
			string ArmatureName = ArmatureName_Data.Value<string>();
			List<Bone> BoneList = new List<Bone>();
			List<Object> ObjectList = new List<Object>();
			foreach (JToken TargetBone in Bone_Data) {
				string BoneName = TargetBone["Name"].ToString();
				JArray HeadPosition_Data = (JArray)TargetBone["Head"];
				JArray TailPosition_Data = (JArray)TargetBone["Tail"];
				Vector3 HeadPosition = new Vector3((float)HeadPosition_Data[0], (float)HeadPosition_Data[1], (float)HeadPosition_Data[2]);
				Vector3 TailPosition = new Vector3((float)TailPosition_Data[0], (float)TailPosition_Data[1], (float)TailPosition_Data[2]);
				string ParentName = TargetBone["Parent"].ToString();
				Bone NewBone = new Bone(BoneName, HeadPosition, TailPosition, ParentName);
				BoneList.Add(NewBone);
			}
			foreach (JToken TargetObject in Object_Data) {
				string ObjectName = TargetObject["Name"].ToString();
				JToken DisplayName_Data = TargetObject["DisplayName"];
				string EnglishName = DisplayName_Data["en"].ToString();
				string KoreanName = DisplayName_Data["ko"].ToString();
				string JapaneseName = DisplayName_Data["ja"].ToString();
				string RequiredVertexGroupName = TargetObject["RequiredVertexGroup"].ToString();
				JToken Weight_Data = TargetObject["Weights"];
				List<MeshWeight> MeshWeightList = new List<MeshWeight>();
				foreach (JProperty TargetProperty in Weight_Data.Cast<JProperty>()) {
					string NewBoneName = TargetProperty.Name;
					JToken MeshToken = TargetProperty.Value;
					string NewMaterialName = MeshToken["MaterialName"].ToString();
					JArray WeightDatas = (JArray)MeshToken["Vertices"];
					UVWeight[] NewUVWeights = WeightDatas
							.Select(Item => new UVWeight(
								new Vector2((float)Item["Position"][0], (float)Item["Position"][1]),
								(float)Item["Weight"]))
							.ToArray();
					SubMeshWeight NewSubMeshWeight = new SubMeshWeight (NewMaterialName, NewUVWeights );
					MeshWeightList.Add(new MeshWeight(NewBoneName, NewSubMeshWeight));
				}
				DisplayName NewDisplayName = new DisplayName(EnglishName, KoreanName, JapaneseName);
				Object NewObject = new Object(ObjectName, NewDisplayName, RequiredVertexGroupName, MeshWeightList.ToArray());
				ObjectList.Add(NewObject);
			}
			NewAvatarWeight = new AvatarWeight(
				Version,
				AvatarName,
				AvatarVersion,
				ArmatureName,
				BoneList.ToArray(),
				ObjectList.ToArray()
			);
			return NewAvatarWeight;
		}

		bool VerifyVariable(out GameObject NewAvatarGameObject) {
			NewAvatarGameObject = null;
			if (!AvatarGameObject) {
				return false;
			}
			if (!JSON_Asset) {
				return false;
			}
			NewAvatarGameObject = DuplicateUtility.DuplicateGameObject(AvatarGameObject);
			if (!NewAvatarGameObject) {
				return false;
			}
			NewAvatarGameObject.TryGetComponent(out Animator AvatarAnimatorComponent);
			if (AvatarAnimatorComponent) {
				AvatarAnimator = AvatarAnimatorComponent;
			} else {
				DestroyImmediate(NewAvatarGameObject);
				return false;
			}
			NewAvatarWeight = GetAvatarWeight(JObject.Parse(JSON_Asset.text));
			if (NewAvatarWeight.JSON_Version < 2) {
				DestroyImmediate(NewAvatarGameObject);
				return false;
			}
			AvatarMaterials = GetAvatarMaterials();
			return true;
		}

		Dictionary<string, Transform> GetNewBoneTransfom(GameObject TargetGameObject) {
			Dictionary<string, Transform> JSON_BoneTransforms = new Dictionary<string, Transform>();
			Transform[] AvatarHumanoidTransforms = Enum.GetValues(typeof(HumanBodyBones))
				.Cast<HumanBodyBones>()
				.Where(Item => Item != HumanBodyBones.LastBone)
				.Select(Item => AvatarAnimator.GetBoneTransform(Item))
				.Where(Item => Item != null)
				.ToArray();
			Transform[] AllAvatarTransforms = TargetGameObject.GetComponentsInChildren<Transform>(true);
			List<Transform> NewBoneTransforms = new List<Transform>();
			foreach (Bone TargetBone in NewAvatarWeight.AvatarBones) {
				GameObject NewGameObject = new GameObject(TargetBone.BoneName);
				Transform NewTransform = NewGameObject.transform;
				NewTransform.SetParent(TargetGameObject.transform);
				NewBoneTransforms.Add(NewTransform);
			}
			foreach (Bone TargetBone in NewAvatarWeight.AvatarBones) {
				string BoneName = TargetBone.BoneName;
				string ParentName = TargetBone.ParentName;
				Transform NewTransform = NewBoneTransforms.First(Item => Item.gameObject.name == BoneName);
				Vector3 HeadPosition = GetUnityBoneTransform(TargetBone.HeadPosition);
				Vector3 TailPosition = GetUnityBoneTransform(TargetBone.TailPosition);
				Transform ParentTransform = AllAvatarTransforms.FirstOrDefault(Item => Item.name == ParentName);
				if (NewBoneTransforms.Any(Item => Item.name == ParentName)) {
					ParentTransform = NewBoneTransforms.First(Item => Item.name == ParentName);
				} else if (AvatarHumanoidTransforms.Any(Item => Item.name == ParentName)) {
					ParentTransform = AvatarHumanoidTransforms.First(Item => Item.name == ParentName);
				} else {
					ParentTransform = AllAvatarTransforms.FirstOrDefault(Item => Item.name == ParentName);
				}
				if (ParentTransform) {
					NewTransform.SetParent(ParentTransform);
					NewTransform.position = TargetGameObject.transform.TransformPoint(HeadPosition);
					Vector3 BoneVector = (TargetGameObject.transform.TransformPoint(TailPosition) - NewTransform.position).normalized;
					if (BoneVector != Vector3.zero) {
						NewTransform.rotation = TargetGameObject.transform.rotation * Quaternion.FromToRotation(Vector3.up, BoneVector);
					}
					JSON_BoneTransforms.Add(BoneName, NewTransform);
				}
			}
			return JSON_BoneTransforms;
		}

		Vector3 GetUnityBoneTransform(Vector3 TargetPosition) {
			return new Vector3(-TargetPosition.x, TargetPosition.z, -TargetPosition.y);
		}

		bool PatchAvatar(GameObject OldAvatarGameObject, GameObject NewAvatarGameObject, Dictionary<string, Transform> NewBoneTransforms) {
			string AvatarAssetPath = AssetDatabase.GetAssetPath(AvatarAnimator.avatar);
			SkinnedMeshRenderer[] AvatarSkinnedMeshRenderers = NewAvatarGameObject
					.GetComponentsInChildren<SkinnedMeshRenderer>(true)
					.Where(Item => AssetDatabase.GetAssetPath(Item.sharedMesh) == AvatarAssetPath)
					.ToArray();
			foreach (Object TargetObject in NewAvatarWeight.AvatarObjects) {
				string TargetName = TargetObject.MeshName;
				SkinnedMeshRenderer TargetSkinnedMeshRenderer = AvatarSkinnedMeshRenderers.FirstOrDefault(Item => Item.gameObject.name == TargetName);
				if (!TargetSkinnedMeshRenderer) TargetSkinnedMeshRenderer = NewAvatarGameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(Item => Item.name == TargetName);
				if (TargetSkinnedMeshRenderer) {
					Mesh OldMesh = TargetSkinnedMeshRenderer.sharedMesh;
					Mesh NewMesh = Instantiate(OldMesh);
					NewMesh.name = $"VRSuya_{OldMesh.name}";
					string[] TargetMaterialNames = TargetObject.MeshWeights
						.Select(Item => Item.SubMeshWeight)
						.Select(Item => Item.MaterialName)
						.Distinct()
						.ToArray();
					foreach (string TargetMaterialName in TargetMaterialNames) {
						int TargetMaterialIndex = GetMaterialIndex(TargetSkinnedMeshRenderer, TargetMaterialName, 0);
						Dictionary<int, Vector2> NewMeshUV = GetMaterialUV(NewMesh, TargetMaterialIndex);
						List<Transform> BoneTransforms = TargetSkinnedMeshRenderer.bones.ToList();
						List<Transform> NewBoneTransform = NewAvatarWeight.AvatarObjects
							.Where(Item => Item.MeshName == TargetSkinnedMeshRenderer.gameObject.name)
							.SelectMany(Item => Item.MeshWeights)
							.Select(Item => Item.BoneName)
							.Distinct()
							.Where(Item => NewBoneTransforms.ContainsKey(Item))
							.Select(Item => NewBoneTransforms[Item])
							.ToList();
						foreach (Transform NewBone in NewBoneTransform) {
							if (!BoneTransforms.Contains(NewBone)) BoneTransforms.Add(NewBone);
						}
						TargetSkinnedMeshRenderer.bones = BoneTransforms.ToArray();
						Matrix4x4[] OldBindPoses = OldMesh.bindposes;
						Matrix4x4[] NewBindPoses = new Matrix4x4[BoneTransforms.Count];
						for (int Index = 0; Index < OldBindPoses.Length; Index++) {
							NewBindPoses[Index] = OldBindPoses[Index];
						}
						for (int Index = OldBindPoses.Length; Index < BoneTransforms.Count; Index++) {
							NewBindPoses[Index] = BoneTransforms[Index].worldToLocalMatrix * TargetSkinnedMeshRenderer.transform.localToWorldMatrix;
						}
						NewMesh.bindposes = NewBindPoses;
						BoneWeight[] NewBoneWeights = NewMesh.boneWeights;
						Dictionary<int, List<VertexWeight>> VertexWeightList = new Dictionary<int, List<VertexWeight>>();
						for (int Index = 0; Index < NewBoneWeights.Length; Index++) {
							BoneWeight TargetBoneWeight = NewBoneWeights[Index];
							List<VertexWeight> NewVertexWeights = new List<VertexWeight>();
							if (TargetBoneWeight.weight0 > 0) NewVertexWeights.Add(new VertexWeight(TargetBoneWeight.boneIndex0, TargetBoneWeight.weight0));
							if (TargetBoneWeight.weight1 > 0) NewVertexWeights.Add(new VertexWeight(TargetBoneWeight.boneIndex1, TargetBoneWeight.weight1));
							if (TargetBoneWeight.weight2 > 0) NewVertexWeights.Add(new VertexWeight(TargetBoneWeight.boneIndex2, TargetBoneWeight.weight2));
							if (TargetBoneWeight.weight3 > 0) NewVertexWeights.Add(new VertexWeight(TargetBoneWeight.boneIndex3, TargetBoneWeight.weight3));
							VertexWeightList[Index] = NewVertexWeights;
						}
						foreach (MeshWeight TargetMeshWeights in TargetObject.MeshWeights) {
							if (!NewBoneTransforms.TryGetValue(TargetMeshWeights.BoneName, out Transform TargetBoneTransform)) continue;
							int TargetBoneIndex = BoneTransforms.IndexOf(TargetBoneTransform);
							List<UVWeight> UVWeightList = TargetMeshWeights.SubMeshWeight.UVWeights.ToList();
							HashSet<int> AssignedVertexList = new HashSet<int>();
							foreach (UVWeight TargetUVWeight in UVWeightList) {
								if (TargetUVWeight.WeightValue < 0) continue;
								var MatchResult = NewMeshUV
									.Where(Item => !AssignedVertexList.Contains(Item.Key))
									.Select(Item => new {
										VertexIndex = Item.Key,
										Distance = Vector2.Distance(TargetUVWeight.UVPosition, Item.Value)
									})
									.Where(Item => Item.Distance < Threshold)
									.OrderBy(Item => Item.Distance)
									.FirstOrDefault();
								if (MatchResult == null) continue;
								int VertexIndex = MatchResult.VertexIndex;
								AssignedVertexList.Add(VertexIndex);
								if (!VertexWeightList.ContainsKey(VertexIndex)) {
									VertexWeightList[VertexIndex] = new List<VertexWeight>();
								}
								VertexWeight ExistingVertexWeight = VertexWeightList[VertexIndex]
									.FirstOrDefault(Item => Item.VertexIndex == TargetBoneIndex);
								if (ExistingVertexWeight != null) {
									ExistingVertexWeight.WeightValue += TargetUVWeight.WeightValue;
								} else {
									VertexWeightList[VertexIndex].Add(new VertexWeight(TargetBoneIndex, TargetUVWeight.WeightValue));
								}
							}
						}
						for (int Index = 0; Index < NewBoneWeights.Length; Index++) {
							if (VertexWeightList.TryGetValue(Index, out List<VertexWeight> TargetVertexWeights)) {
								TargetVertexWeights = TargetVertexWeights.OrderByDescending(Item => Item.WeightValue).Take(4).ToList();
								float WeightSum = TargetVertexWeights.Sum(Item => Item.WeightValue);
								BoneWeight NormalizedBoneWeights = new BoneWeight();
								if (WeightSum > 0f) {
									if (TargetVertexWeights.Count > 0) {
										NormalizedBoneWeights.boneIndex0 = TargetVertexWeights[0].VertexIndex;
										NormalizedBoneWeights.weight0 = TargetVertexWeights[0].WeightValue / WeightSum;
									}
									if (TargetVertexWeights.Count > 1) {
										NormalizedBoneWeights.boneIndex1 = TargetVertexWeights[1].VertexIndex;
										NormalizedBoneWeights.weight1 = TargetVertexWeights[1].WeightValue / WeightSum;
									}
									if (TargetVertexWeights.Count > 2) {
										NormalizedBoneWeights.boneIndex2 = TargetVertexWeights[2].VertexIndex;
										NormalizedBoneWeights.weight2 = TargetVertexWeights[2].WeightValue / WeightSum;
									}
									if (TargetVertexWeights.Count > 3) {
										NormalizedBoneWeights.boneIndex3 = TargetVertexWeights[3].VertexIndex;
										NormalizedBoneWeights.weight3 = TargetVertexWeights[3].WeightValue / WeightSum;
									}
								}
								NewBoneWeights[Index] = NormalizedBoneWeights;
							}
						}
						NewMesh.boneWeights = NewBoneWeights;
					}
					string NewAssetPath = SaveMeshAsset(OldMesh, NewMesh, NewAvatarGameObject.name, TargetName);
					if (!string.IsNullOrEmpty(NewAssetPath)) {
						Mesh LoadedSavedMeshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(NewAssetPath);
						TargetSkinnedMeshRenderer.sharedMesh = LoadedSavedMeshAsset;
					}
				}
			}
			return true;
		}

		string SaveMeshAsset(Mesh OldMesh, Mesh NewMesh, string TargetName, string TargetMeshName) {
			string MeshAssetPath = AssetDatabase.GetAssetPath(OldMesh);
			if (string.IsNullOrEmpty(MeshAssetPath)) {
				MeshAssetPath = "Assets/VRSuya/Export";
			} else {
				MeshAssetPath = Path.GetDirectoryName(MeshAssetPath);
			}
			string NewDirectoryPath = Path.Combine(MeshAssetPath, "VRSuya");
			if (!AssetDatabase.IsValidFolder(NewDirectoryPath)) {
				AssetDatabase.CreateFolder(MeshAssetPath, "VRSuya");
			}
			string Date = DateTime.Now.ToString("yyMMdd");
			string NewAssetName = $"VRSuya_{TargetName}_{TargetMeshName}_{Date}.asset";
			string NewAssetPath = Path.Combine(NewDirectoryPath, NewAssetName).Replace("\\", "/");
			AssetDatabase.CreateAsset(NewMesh, NewAssetPath);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			return NewAssetPath;
		}

		Dictionary<int, Vector2> GetMaterialUV(Mesh TargetMesh, int MaterialIndex) {
			Vector2[] MeshUV = TargetMesh.uv;
			Dictionary<int, Vector2> MaterialUV = new Dictionary<int, Vector2>();
			int[] Indices = TargetMesh.GetIndices(MaterialIndex);
			foreach (int VertexIndex in new HashSet<int>(Indices)) {
				if (VertexIndex < MeshUV.Length) {
					MaterialUV[VertexIndex] = MeshUV[VertexIndex];
				}
			}
			return MaterialUV;
		}

		Material[] GetAvatarMaterials() {
			UnityEngine.Avatar TargetAvatar = AvatarAnimator.avatar;
			string ModelAssetPath = AssetDatabase.GetAssetPath(TargetAvatar);
			if (!string.IsNullOrEmpty(ModelAssetPath)) {
				ModelImporter TargetModelImporter = AssetImporter.GetAtPath(ModelAssetPath) as ModelImporter;
				if (TargetModelImporter) {
					return TargetModelImporter.GetExternalObjectMap()
						.Where(Item => Item.Value is Material)
						.Select(Item => Item.Value as Material)
						.ToArray();
				}
			}
			return null;
		}

		int GetMaterialIndex(SkinnedMeshRenderer TargetRenderer, string TargetMaterialName, int FallbackIndex) {
			Material[] TargetMaterials = TargetRenderer.sharedMaterials;
			Material TargetMaterial = AvatarMaterials.FirstOrDefault(Item => Item.name == TargetMaterialName);
			if (TargetMaterial) {
				for (int Index = 0; Index < TargetMaterials.Length; Index++) {
					if (TargetMaterials[Index]) {
						if (TargetMaterials[Index] == TargetMaterial) {
							return Index;
						}
					}

				}
			}
			for (int Index = 0; Index < TargetMaterials.Length; Index++) {
				if (TargetMaterials[Index]) {
					if (TargetMaterials[Index].name.Contains(TargetMaterialName, StringComparison.OrdinalIgnoreCase)) {
						return Index;
					}
				}
				
			}
			return FallbackIndex;
		}

		class VertexWeight {
			public int VertexIndex;
			public float WeightValue;

			public VertexWeight(int TargetVertexIndex, float TargetWeightValue) {
				VertexIndex = TargetVertexIndex;
				WeightValue = TargetWeightValue;
			}
		}

		class AvatarWeight {
			public int JSON_Version;
			public string AvatarName;
			public string AvatarVersion;
			public string ArmatureName;
			public Bone[] AvatarBones;
			public Object[] AvatarObjects;

			public AvatarWeight(int TargetJSON_Version, string TargetAvatarName, string TargetAvatarVersion, string TargetArmatureName, Bone[] TargetAvatarBones, Object[] TargetAvatarObjects) {
				JSON_Version = TargetJSON_Version;
				AvatarName = TargetAvatarName;
				AvatarVersion = TargetAvatarVersion;
				ArmatureName = TargetArmatureName;
				AvatarBones = TargetAvatarBones;
				AvatarObjects = TargetAvatarObjects;
			}
		}

		class Bone {
			public string BoneName;
			public Vector3 HeadPosition;
			public Vector3 TailPosition;
			public string ParentName;

			public Bone(string TargetBoneName, Vector3 TargetHeadPosition, Vector3 TargetTailPosition, string TargetParentName) {
				BoneName = TargetBoneName;
				HeadPosition = TargetHeadPosition;
				TailPosition = TargetTailPosition;
				ParentName = TargetParentName;
			}
		}

		class Object {
			public string MeshName;
			public DisplayName MeshDisplayName;
			public string RequiredVertexGroup;
			public MeshWeight[] MeshWeights;

			public Object(string TargetMeshName, DisplayName TargetMeshDisplayName, string TargetRequiredVertexGroup, MeshWeight[] TargetMeshWeights) {
				MeshName = TargetMeshName;
				MeshDisplayName = TargetMeshDisplayName;
				RequiredVertexGroup = TargetRequiredVertexGroup;
				MeshWeights = TargetMeshWeights;
			}
		}

		class DisplayName {
			public string EnglishName;
			public string KoreanName;
			public string JapaneseName;

			public DisplayName(string TargetEnglishName, string TargetKoreanName, string TargetJapaneseName) {
				EnglishName = TargetEnglishName;
				KoreanName = TargetKoreanName;
				JapaneseName = TargetJapaneseName;
			}
		}

		class MeshWeight {
			public string BoneName;
			public SubMeshWeight SubMeshWeight;

			public MeshWeight(string TargetBoneName, SubMeshWeight TargetSubMeshWeight) {
				BoneName = TargetBoneName;
				SubMeshWeight = TargetSubMeshWeight;
			}
		}

		class SubMeshWeight {
			public string MaterialName;
			public UVWeight[] UVWeights;

			public SubMeshWeight(string TargetMaterialName, UVWeight[] TargetUVWeights) {
				MaterialName = TargetMaterialName;
				UVWeights = TargetUVWeights;
			}
		}

		class UVWeight {
			public Vector2 UVPosition;
			public float WeightValue;

			public UVWeight(Vector2 TargetUVPosition, float TargetWeightValue) {
				UVPosition = TargetUVPosition;
				WeightValue = TargetWeightValue;
			}
		}
	}
}