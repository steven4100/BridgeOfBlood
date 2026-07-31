#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BridgeOfBlood.Editor.MeshNormalCapture
{
	/// <summary>
	/// Renders a Project mesh's normals through an orthographic camera into a PNG.
	/// Encoded as camera/view-space normals (tangent space of a camera-facing sprite).
	/// </summary>
	public sealed class MeshNormalCaptureWindow : EditorWindow
	{
		const string ShaderName = "Hidden/BridgeOfBlood/MeshNormalCapture";
		const string DefaultSaveFolder = "Assets";

		Mesh _mesh;
		Vector3 _cameraPosition = new Vector3(0f, 0f, -2f);
		Vector3 _cameraEuler = Vector3.zero;
		float _orthographicSize = 1f;
		float _nearClip = 0.01f;
		float _farClip = 100f;
		int _resolution = 512;
		bool _flipY;
		Color _backgroundColor = new Color(0.5f, 0.5f, 1f, 0f);
		string _saveFolder = DefaultSaveFolder;
		string _fileName = "MeshNormals";

		Material _material;
		RenderTexture _previewRt;
		Vector2 _scroll;

		[MenuItem("Window/Bridge of Blood/Mesh Normal Capture")]
		public static void Open()
		{
			var window = GetWindow<MeshNormalCaptureWindow>("Mesh Normal Capture");
			window.minSize = new Vector2(360f, 480f);
		}

		void OnEnable()
		{
			EnsureMaterial();
		}

		void OnDisable()
		{
			ReleasePreview();
			if (_material != null)
			{
				DestroyImmediate(_material);
				_material = null;
			}
		}

		void OnGUI()
		{
			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
			_mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", _mesh, typeof(Mesh), false);

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Orthographic Camera", EditorStyles.boldLabel);
			_cameraPosition = EditorGUILayout.Vector3Field("Position", _cameraPosition);
			_cameraEuler = EditorGUILayout.Vector3Field("Rotation (Euler)", _cameraEuler);
			_orthographicSize = EditorGUILayout.FloatField("Orthographic Size", _orthographicSize);
			_nearClip = EditorGUILayout.FloatField("Near Clip", _nearClip);
			_farClip = EditorGUILayout.FloatField("Far Clip", _farClip);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Frame Mesh"))
					FrameMesh();
				if (GUILayout.Button("Reset Camera"))
					ResetCamera();
			}

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
			_resolution = Mathf.Clamp(EditorGUILayout.IntField("Resolution", _resolution), 1, 8192);
			_flipY = EditorGUILayout.Toggle("Flip Y (DirectX)", _flipY);
			_backgroundColor = EditorGUILayout.ColorField(new GUIContent("Background", "Cleared before draw. Alpha 0 keeps empty pixels transparent."), _backgroundColor);

			using (new EditorGUILayout.HorizontalScope())
			{
				_saveFolder = EditorGUILayout.TextField("Save Folder", _saveFolder);
				if (GUILayout.Button("…", GUILayout.Width(28f)))
				{
					string picked = EditorUtility.OpenFolderPanel("Save Folder", Application.dataPath, "");
					if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
						_saveFolder = "Assets" + picked.Substring(Application.dataPath.Length).Replace('\\', '/');
				}
			}

			_fileName = EditorGUILayout.TextField("File Name", _fileName);

			EditorGUILayout.Space(8f);
			using (new EditorGUI.DisabledScope(_mesh == null))
			{
				if (GUILayout.Button("Render Preview", GUILayout.Height(28f)))
					RenderToPreview();

				if (GUILayout.Button("Export PNG", GUILayout.Height(28f)))
					ExportPng();
			}

			if (_mesh == null)
				EditorGUILayout.HelpBox("Select a Mesh sub-asset from an FBX (or any Mesh) in the Project window.", MessageType.Info);
			else
				EditorGUILayout.HelpBox(
					"Normals are written in camera/view space and packed as (n * 0.5 + 0.5). That matches tangent space for a camera-facing quad/sprite.",
					MessageType.None);

			DrawPreview();

			EditorGUILayout.EndScrollView();
		}

		void DrawPreview()
		{
			if (_previewRt == null)
				return;

			EditorGUILayout.Space(8f);
			EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

			float size = Mathf.Min(position.width - 40f, 320f);
			Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
			EditorGUI.DrawPreviewTexture(rect, _previewRt, null, ScaleMode.ScaleToFit);
		}

		void EnsureMaterial()
		{
			if (_material != null)
				return;

			Shader shader = Shader.Find(ShaderName);
			if (shader == null)
			{
				Debug.LogError($"[{nameof(MeshNormalCaptureWindow)}] Shader '{ShaderName}' not found.");
				return;
			}

			_material = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				name = "MeshNormalCapture Material"
			};
		}

		void FrameMesh()
		{
			if (_mesh == null)
				return;

			Bounds bounds = _mesh.bounds;
			float radius = bounds.extents.magnitude;
			if (radius < 1e-5f)
				radius = 0.5f;

			_orthographicSize = radius;
			_cameraEuler = Vector3.zero;
			_cameraPosition = bounds.center + new Vector3(0f, 0f, -radius * 2f);
			_nearClip = 0.01f;
			_farClip = radius * 4f + 1f;
		}

		void ResetCamera()
		{
			_cameraPosition = new Vector3(0f, 0f, -2f);
			_cameraEuler = Vector3.zero;
			_orthographicSize = 1f;
			_nearClip = 0.01f;
			_farClip = 100f;
		}

		void RenderToPreview()
		{
			EnsurePreviewRt();
			if (!RenderNormals(_previewRt))
				return;

			Repaint();
		}

		void ExportPng()
		{
			if (_mesh == null)
				return;

			EnsureMaterial();
			if (_material == null)
				return;

			string folder = string.IsNullOrWhiteSpace(_saveFolder) ? DefaultSaveFolder : _saveFolder.Replace('\\', '/');
			if (!AssetDatabase.IsValidFolder(folder) && folder != "Assets")
			{
				Debug.LogError($"[{nameof(MeshNormalCaptureWindow)}] Save folder is not a valid Assets path: {folder}");
				return;
			}

			string safeName = string.IsNullOrWhiteSpace(_fileName) ? "MeshNormals" : _fileName.Trim();
			if (!safeName.EndsWith(".png"))
				safeName += ".png";

			string assetPath = $"{folder.TrimEnd('/')}/{safeName}";
			assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

			var rt = new RenderTexture(_resolution, _resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "MeshNormalCapture Export"
			};
			rt.Create();

			try
			{
				if (!RenderNormals(rt))
					return;

				Texture2D tex = ReadTexture(rt);
				try
				{
					byte[] png = tex.EncodeToPNG();
					string absolutePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
					Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? folder);
					File.WriteAllBytes(absolutePath, png);
					AssetDatabase.ImportAsset(assetPath);

					var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
					if (importer != null)
					{
						importer.sRGBTexture = false;
						importer.textureType = TextureImporterType.NormalMap;
						importer.mipmapEnabled = false;
						importer.SaveAndReimport();
					}

					Debug.Log($"[{nameof(MeshNormalCaptureWindow)}] Wrote {assetPath}", AssetDatabase.LoadAssetAtPath<Object>(assetPath));
					EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(assetPath));
				}
				finally
				{
					DestroyImmediate(tex);
				}
			}
			finally
			{
				rt.Release();
				DestroyImmediate(rt);
			}
		}

		void EnsurePreviewRt()
		{
			if (_previewRt != null && (_previewRt.width != _resolution || _previewRt.height != _resolution))
				ReleasePreview();

			if (_previewRt != null)
				return;

			_previewRt = new RenderTexture(_resolution, _resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				hideFlags = HideFlags.HideAndDontSave,
				name = "MeshNormalCapture Preview"
			};
			_previewRt.Create();
		}

		void ReleasePreview()
		{
			if (_previewRt == null)
				return;

			_previewRt.Release();
			DestroyImmediate(_previewRt);
			_previewRt = null;
		}

		bool RenderNormals(RenderTexture target)
		{
			EnsureMaterial();
			if (_mesh == null || _material == null || target == null)
				return false;

			_material.SetFloat("_FlipY", _flipY ? 1f : 0f);

			Quaternion rotation = Quaternion.Euler(_cameraEuler);
			Matrix4x4 cameraLocalToWorld = Matrix4x4.TRS(_cameraPosition, rotation, Vector3.one);
			Matrix4x4 view = cameraLocalToWorld.inverse;
			// Unity cameras look along -Z; match Camera.worldToCameraMatrix Z flip.
			view.m20 *= -1f;
			view.m21 *= -1f;
			view.m22 *= -1f;
			view.m23 *= -1f;

			float aspect = 1f;
			float halfH = Mathf.Max(_orthographicSize, 1e-5f);
			float halfW = halfH * aspect;
			Matrix4x4 projection = Matrix4x4.Ortho(-halfW, halfW, -halfH, halfH, _nearClip, _farClip);
			projection = GL.GetGPUProjectionMatrix(projection, true);

			Matrix4x4 meshMatrix = Matrix4x4.identity;

			var cmd = new CommandBuffer { name = "MeshNormalCapture" };
			cmd.SetRenderTarget(target);
			cmd.ClearRenderTarget(true, true, _backgroundColor);
			cmd.SetViewProjectionMatrices(view, projection);
			cmd.DrawMesh(_mesh, meshMatrix, _material, 0, 0);
			Graphics.ExecuteCommandBuffer(cmd);
			cmd.Release();
			return true;
		}

		static Texture2D ReadTexture(RenderTexture source)
		{
			RenderTexture prev = RenderTexture.active;
			RenderTexture.active = source;
			var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
			tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
			tex.Apply(false, false);
			RenderTexture.active = prev;
			return tex;
		}
	}
}
#endif
