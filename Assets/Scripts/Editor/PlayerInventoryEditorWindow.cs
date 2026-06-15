#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Live debug window for mutating the runtime <see cref="PlayerInventory"/>.
	/// Lists every <see cref="IInventoryItem"/> ScriptableObject in the project on the left
	/// and the rows currently held by the registered <see cref="IInventoryService"/> on the right.
	/// Add and Remove buttons mutate the live inventory through public APIs (so listeners refresh).
	/// </summary>
	public class PlayerInventoryEditorWindow : EditorWindow
	{
		readonly List<UnityEngine.Object> _availableAssets = new List<UnityEngine.Object>();
		Vector2 _availableScroll;
		Vector2 _inventoryScroll;
		string _availableFilter = "";
		Filter _typeFilter = Filter.All;

		enum Filter
		{
			All,
			Spells,
			Items,
		}

		[MenuItem("Window/Bridge of Blood/Player Inventory")]
		public static void Open()
		{
			var w = GetWindow<PlayerInventoryEditorWindow>("Player Inventory");
			w.minSize = new Vector2(560, 380);
		}

		void OnEnable()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
			RefreshAvailableAssets();
		}

		void OnDisable()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		}

		void OnPlayModeChanged(PlayModeStateChange _)
		{
			Repaint();
		}

		void Update()
		{
			if (Application.isPlaying)
				Repaint();
		}

		void OnGUI()
		{
			DrawToolbar();

			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox(
					"Enter Play mode to add or remove items from the live PlayerInventory.\n" +
					"The list below shows every IInventoryItem asset discovered in the project.",
					MessageType.Info);
				DrawAvailable(null);
				return;
			}

			PlayerInventory inv = ResolveRuntimeInventory();
			if (inv == null)
			{
				EditorGUILayout.HelpBox(
					"No PlayerInventory registered in the ServiceLocator yet. " +
					"Wait for TestSceneManager.Start() to run, or load a scene that registers IInventoryService.",
					MessageType.Warning);
				return;
			}

			EditorGUILayout.BeginHorizontal();
			DrawAvailable(inv);
			DrawCurrent(inv);
			EditorGUILayout.EndHorizontal();
		}

		void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button("Refresh Catalog", EditorStyles.toolbarButton, GUILayout.Width(110)))
				RefreshAvailableAssets();

			GUILayout.Space(8);
			_typeFilter = (Filter)EditorGUILayout.EnumPopup(_typeFilter, EditorStyles.toolbarPopup, GUILayout.Width(90));

			GUILayout.Space(8);
			GUILayout.Label("Filter:", GUILayout.Width(40));
			_availableFilter = GUILayout.TextField(_availableFilter ?? "", EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		void DrawAvailable(PlayerInventory inv)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true), GUILayout.MinWidth(240));
			EditorGUILayout.LabelField($"Available ({_availableAssets.Count})", EditorStyles.boldLabel);

			_availableScroll = EditorGUILayout.BeginScrollView(_availableScroll);

			string filter = (_availableFilter ?? string.Empty).Trim();
			bool isPlaying = inv != null;

			for (int i = 0; i < _availableAssets.Count; i++)
			{
				UnityEngine.Object asset = _availableAssets[i];
				if (asset == null) continue;

				if (!MatchesTypeFilter(asset)) continue;
				if (filter.Length > 0 && asset.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.ObjectField(asset, asset.GetType(), false);
				using (new EditorGUI.DisabledScope(!isPlaying))
				{
					if (GUILayout.Button("Add", GUILayout.Width(48)))
						AddAssetToInventory(inv, asset);
				}
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		void DrawCurrent(PlayerInventory inv)
		{
			IReadOnlyList<InventoryItem> rows = inv.StoredRows;

			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true), GUILayout.MinWidth(240));

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField($"Inventory ({rows.Count})", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			using (new EditorGUI.DisabledScope(rows.Count == 0))
			{
				if (GUILayout.Button("Clear All", GUILayout.Width(80)))
					inv.Clear();
			}
			EditorGUILayout.EndHorizontal();

			_inventoryScroll = EditorGUILayout.BeginScrollView(_inventoryScroll);

			InventoryItem toRemove = null;
			for (int i = 0; i < rows.Count; i++)
			{
				InventoryItem row = rows[i];

				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(LabelForRow(row, i), GUILayout.Width(28));
				UnityEngine.Object payloadObj = row.Payload as UnityEngine.Object;
				if (payloadObj != null)
					EditorGUILayout.ObjectField(payloadObj, payloadObj.GetType(), false);
				else
					EditorGUILayout.LabelField(row.Payload != null ? row.Payload.ToString() : "<null>");

				if (GUILayout.Button("Remove", GUILayout.Width(64)))
					toRemove = row;
				EditorGUILayout.EndHorizontal();
			}

			if (toRemove != null)
				inv.RemoveRow(toRemove);

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		bool MatchesTypeFilter(UnityEngine.Object asset)
		{
			switch (_typeFilter)
			{
				case Filter.Spells: return asset is SpellAuthoringData;
				case Filter.Items: return asset is Item;
				default: return true;
			}
		}

		static string LabelForRow(InventoryItem row, int index)
		{
			if (row.Payload is SpellAuthoringData) return $"S{index}";
			if (row.Payload is Item) return $"I{index}";
			return index.ToString();
		}

		void AddAssetToInventory(PlayerInventory inv, UnityEngine.Object asset)
		{
			switch (asset)
			{
				case SpellAuthoringData spell:
					inv.AddSpell(spell);
					break;
				case Item item:
					inv.AddItem(item);
					break;
				case IInventoryItem generic:
					inv.AddInventoryItem(new InventoryItem(generic));
					break;
				default:
					Debug.LogWarning($"Asset '{asset.name}' does not implement IInventoryItem.");
					break;
			}
		}

		static PlayerInventory ResolveRuntimeInventory()
		{
			IInventoryService svc = ServiceLocator.Current.GetService<IInventoryService>(throwError: false);
			return svc as PlayerInventory;
		}

		void RefreshAvailableAssets()
		{
			_availableAssets.Clear();
			string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
				if (so is IInventoryItem)
					_availableAssets.Add(so);
			}
			_availableAssets.Sort((a, b) =>
			{
				int t = TypeRank(a).CompareTo(TypeRank(b));
				if (t != 0) return t;
				return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
			});
		}

		static int TypeRank(UnityEngine.Object asset)
		{
			if (asset is SpellAuthoringData) return 0;
			if (asset is Item) return 1;
			return 2;
		}
	}
}
#endif
