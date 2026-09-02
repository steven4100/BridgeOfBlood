#if UNITY_EDITOR
using BridgeOfBlood.Data.Inventory;
using BridgeOfBlood.Data.Spells;
using BridgeOfBlood.Effects;
using EZServiceLocation;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BridgeOfBlood.Editor
{
	/// <summary>
	/// Live debug window for mutating the runtime <see cref="PlayerInventory"/>.
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
					"The list below shows every Item and SpellAuthoringData asset discovered in the project.",
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
			EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true), GUILayout.MinWidth(240));

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Inventory", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			using (new EditorGUI.DisabledScope(inv.SpellCollection.Count == 0 && inv.Items.Count == 0 && inv.Stash.Placements.Count == 0))
			{
				if (GUILayout.Button("Clear All", GUILayout.Width(80)))
					inv.Clear();
			}
			EditorGUILayout.EndHorizontal();

			_inventoryScroll = EditorGUILayout.BeginScrollView(_inventoryScroll);

			DrawOccupantList("Loop", inv.SpellCollection.RuntimeSpells, spell =>
			{
				EditorGUILayout.ObjectField(spell.Definition, typeof(SpellAuthoringData), false);
				if (GUILayout.Button("Remove", GUILayout.Width(64)))
					inv.SpellCollection.TryRemove(spell);
			});

			DrawOccupantList("Jokers", inv.Items.Items, item =>
			{
				EditorGUILayout.ObjectField(item.Definition, typeof(Item), false);
				if (GUILayout.Button("Remove", GUILayout.Width(64)))
					inv.Items.TryRemove(item);
			});

			EditorGUILayout.LabelField($"Stash ({inv.Stash.Placements.Count})", EditorStyles.boldLabel);
			for (int i = 0; i < inv.Stash.Placements.Count; i++)
			{
				StashPlacement placement = inv.Stash.Placements[i];
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label($"{placement.Origin.x},{placement.Origin.y}", GUILayout.Width(40));
				DrawOccupantField(placement.Occupant);
				if (GUILayout.Button("Remove", GUILayout.Width(64)))
					inv.Stash.TryRemove(placement.Occupant);
				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		void DrawOccupantList<T>(string title, IReadOnlyList<T> rows, Action<T> drawRow)
		{
			EditorGUILayout.LabelField($"{title} ({rows.Count})", EditorStyles.boldLabel);
			for (int i = 0; i < rows.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Label(i.ToString(), GUILayout.Width(28));
				drawRow(rows[i]);
				EditorGUILayout.EndHorizontal();
			}
		}

		static void DrawOccupantField(IInventoryOccupant occupant)
		{
			switch (occupant)
			{
				case RuntimeSpell spell:
					EditorGUILayout.ObjectField(spell.Definition, typeof(SpellAuthoringData), false);
					break;
				case RuntimeGem gem:
					EditorGUILayout.ObjectField(gem.Definition, typeof(SpellItem), false);
					break;
				case RuntimeItem item:
					EditorGUILayout.ObjectField(item.Definition, typeof(Item), false);
					break;
				default:
					EditorGUILayout.LabelField(occupant?.GetType().Name ?? "<null>");
					break;
			}
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

		void AddAssetToInventory(PlayerInventory inv, UnityEngine.Object asset)
		{
			switch (asset)
			{
				case SpellAuthoringData spell:
					inv.AddSpell(spell);
					break;
				case SpellItem gem:
					if (inv.Stash.TryFindFirstFreeCell(out Vector2Int cell))
						inv.Stash.TryPlace(new RuntimeGem(gem), cell);
					break;
				case Item item:
					inv.AddItem(item);
					break;
				default:
					Debug.LogWarning($"Asset '{asset.name}' is not an Item or SpellAuthoringData.");
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
			AddAssetsOfType<Item>();
			AddAssetsOfType<SpellAuthoringData>();
			_availableAssets.Sort((a, b) =>
			{
				int t = TypeRank(a).CompareTo(TypeRank(b));
				if (t != 0) return t;
				return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
			});
		}

		void AddAssetsOfType<T>() where T : ScriptableObject
		{
			string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
			for (int i = 0; i < guids.Length; i++)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[i]);
				T asset = AssetDatabase.LoadAssetAtPath<T>(path);
				if (asset != null)
					_availableAssets.Add(asset);
			}
		}

		static int TypeRank(UnityEngine.Object asset)
		{
			if (asset is SpellAuthoringData) return 0;
			return 1;
		}
	}
}
#endif
