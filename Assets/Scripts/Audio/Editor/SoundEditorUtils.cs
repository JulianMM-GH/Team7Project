using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using EventInstance = FMOD.Studio.EventInstance;
using PLAYBACK_STATE = FMOD.Studio.PLAYBACK_STATE;

// shared stuff for the sound inspectors + Sound Library window
public static class SoundEditorUtils
{
	// FMOD events

	// events only, no snapshots
	public static List<EditorEventRef> Sounds =>
		EventManager.Events.Where(e => e != null && e.Path.StartsWith("event:/")).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();

	public static List<EditorEventRef> Snapshots =>
		EventManager.Events.Where(e => e != null && e.Path.StartsWith("snapshot:/")).OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();

	public static string NameOf(string path) => string.IsNullOrEmpty(path) ? "" : path.Substring(path.LastIndexOf('/') + 1);

	public static string FolderOf(string path)
	{
		if (string.IsNullOrEmpty(path)) return "";
		int prefix = path.IndexOf(":/", StringComparison.Ordinal) + 2;
		int last = path.LastIndexOf('/');
		return last >= prefix ? path.Substring(prefix, last - prefix) : "";
	}

	public static EditorEventRef Find(EventReference reference)
	{
		if (reference.IsNull) return null;
		EditorEventRef found = EventManager.EventFromGUID(reference.Guid);
		if (found == null && !string.IsNullOrEmpty(reference.Path)) found = EventManager.EventFromString(reference.Path);
		return found;
	}

	// same lookup rules as RAudio at runtime (full path or just the name)
	public static EditorEventRef FindByName(string nameOrPath, out List<EditorEventRef> matches, bool snapshots = false)
	{
		matches = new List<EditorEventRef>();
		if (string.IsNullOrWhiteSpace(nameOrPath)) return null;
		nameOrPath = nameOrPath.Trim();

		if (nameOrPath.Contains(":/"))
		{
			EditorEventRef exact = EventManager.Events.FirstOrDefault(e => e != null && string.Equals(e.Path, nameOrPath, StringComparison.OrdinalIgnoreCase));
			if (exact != null) matches.Add(exact);
			return exact;
		}

		string prefix = snapshots ? "snapshot:/" : "event:/";
		matches = EventManager.Events
			.Where(e => e != null && e.Path.StartsWith(prefix) && string.Equals(NameOf(e.Path), nameOrPath, StringComparison.OrdinalIgnoreCase))
			.ToList();
		return matches.FirstOrDefault();
	}

	public static string SuggestName(string name, bool snapshots = false)
	{
		if (string.IsNullOrEmpty(name)) return null;
		IEnumerable<string> candidates = (snapshots ? Snapshots : Sounds).Select(e => NameOf(e.Path)).Distinct();

		string best = null;
		int bestDistance = Mathf.Max(2, name.Length / 3) + 1;
		foreach (string candidate in candidates)
		{
			int distance = EditDistance(name.ToLowerInvariant(), candidate.ToLowerInvariant());
			if (distance >= bestDistance) continue;
			best = candidate;
			bestDistance = distance;
		}
		return best;
	}

	private static int EditDistance(string a, string b)
	{
		int[,] d = new int[a.Length + 1, b.Length + 1];
		for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
		for (int j = 0; j <= b.Length; j++) d[0, j] = j;
		for (int i = 1; i <= a.Length; i++)
		for (int j = 1; j <= b.Length; j++)
			d[i, j] = Mathf.Min(Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
		return d[a.Length, b.Length];
	}

	public static string Describe(EditorEventRef sound)
	{
		List<string> lines = new List<string>();

		if (sound.Path.StartsWith("snapshot:/"))
		{
			lines.Add("Snapshot - mixer state that's applied while it's active");
			return string.Join("\n\n", lines);
		}

		lines.Add(sound.IsOneShot
			? "One shot - stops on its own, multiple can overlap"
			: "Loop - has a loop region or sustain point so something needs to stop it");

		lines.Add(sound.Is3D
			? $"3D - min distance {sound.MinDistance:0.#}, max distance {sound.MaxDistance:0.#} (plays on the camera if no position is given)"
			: "2D - not positional (needs a spatializer in FMOD for that)");

		if (sound.IsStream) lines.Add("Streamed");

		lines.Add(sound.Length > 0 ? $"Length: {sound.Length / 1000f:0.##}s" : "Length: -");

		if (sound.Parameters != null && sound.Parameters.Count > 0)
		{
			IEnumerable<string> parameters = sound.Parameters.Select(DescribeParameter);
			lines.Add("Parameters:\n  " + string.Join("\n  ", parameters));
		}

		if (sound.Banks != null && sound.Banks.Count > 0)
			lines.Add("Bank: " + string.Join(", ", sound.Banks.Where(b => b != null).Select(b => b.Name)));

		return string.Join("\n", lines);
	}

	public static string DescribeParameter(EditorParamRef parameter)
	{
		string scope = parameter.IsGlobal ? "global" : "local";
		switch (parameter.Type)
		{
			case ParameterType.Labeled: return $"{parameter.Name} ({scope}, labels: {string.Join(", ", parameter.Labels)})";
			case ParameterType.Discrete: return $"{parameter.Name} ({scope}, whole numbers {parameter.Min:0}–{parameter.Max:0})";
			default: return $"{parameter.Name} ({scope}, {parameter.Min:0.##}–{parameter.Max:0.##})";
		}
	}

	public static string CodeSnippet(EditorEventRef sound)
	{
		string name = CodeName(sound);
		if (sound.Path.StartsWith("snapshot:/")) return $"RAudio.StartSnapshot(\"{name}\");\nRAudio.StopSnapshot(\"{name}\");";
		return sound.IsOneShot
			? $"RAudio.PlayOneShot(\"{name}\");\nRAudio.PlayOneShot(\"{name}\", transform.position);"
			: $"RAudio.Play(\"{name}\");\nRAudio.Stop(\"{name}\");";
	}

	// full path if another event has the same name
	public static string CodeName(EditorEventRef sound)
	{
		string name = NameOf(sound.Path);
		FindByName(name, out List<EditorEventRef> matches, sound.Path.StartsWith("snapshot:/"));
		return matches.Count > 1 ? sound.Path : name;
	}

	// Library

	private static SoundLibrary s_library;
	private static bool s_librarySearched;

	[InitializeOnLoadMethod]
	private static void WatchForLibraryChanges()
	{
		// inspectors call this every repaint so cache it
		EditorApplication.projectChanged += ForgetLibrary;
	}

	private static void ForgetLibrary()
	{
		s_library = null;
		s_librarySearched = false;
		SoundLibrary.ClearCache();
	}

	public static SoundLibrary FindLibrary()
	{
		if (s_librarySearched && (ReferenceEquals(s_library, null) || s_library != null)) return s_library;

		s_library = SoundLibrary.Instance;
		if (s_library == null)
		{
			// not in Resources, still find it so the settings tab can warn about it
			string guid = AssetDatabase.FindAssets("t:SoundLibrary").FirstOrDefault();
			if (guid != null) s_library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(AssetDatabase.GUIDToAssetPath(guid));
		}

		s_librarySearched = true;
		return s_library;
	}

	public static SoundLibrary FindOrCreateLibrary()
	{
		SoundLibrary library = FindLibrary();
		if (library != null) return library;

		Directory.CreateDirectory(Path.GetDirectoryName(SoundLibrary.DefaultAssetPath));
		library = ScriptableObject.CreateInstance<SoundLibrary>();
		AssetDatabase.CreateAsset(library, SoundLibrary.DefaultAssetPath);
		AssetDatabase.SaveAssets();
		ForgetLibrary();
		Debug.Log($"[Audio] Created Sound Library at {SoundLibrary.DefaultAssetPath}", library);
		return library;
	}

	public static SoundSettings SettingsFor(EditorEventRef sound)
	{
		SoundLibrary library = FindLibrary();
		return sound != null && library != null && library.TryGet(sound.Guid, out SoundSettings settings) ? settings : null;
	}

	// Preview

	private static EventInstance s_preview;
	private static string s_previewPath;

	public static bool IsPreviewing(EditorEventRef sound)
	{
		if (sound == null || !s_preview.isValid() || s_previewPath != sound.Path) return false;
		s_preview.getPlaybackState(out PLAYBACK_STATE state);
		return state != PLAYBACK_STATE.STOPPED;
	}

	public static bool AnyPreviewPlaying => s_preview.isValid() && s_previewPath != null && IsPreviewing(EventManager.EventFromString(s_previewPath));

	public static void Preview(EditorEventRef sound, Dictionary<string, float> parameters = null)
	{
		StopPreview();
		if (sound == null) return;

		try
		{
			EditorUtils.LoadPreviewBanks();
			SoundSettings settings = SettingsFor(sound) ?? SoundSettings.Defaults;
			s_preview = EditorUtils.PreviewEvent(sound, parameters ?? new Dictionary<string, float>(), settings.muted ? 0f : settings.RollVolume());
			s_preview.setPitch(settings.RollPitch());
			s_previewPath = sound.Path;
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[Audio] Couldn't preview {sound.Path}: {e.Message}");
		}
	}

	public static void StopPreview()
	{
		if (s_preview.isValid()) EditorUtils.PreviewStop(s_preview);
		s_preview.clearHandle();
		s_previewPath = null;
	}

	public static void PreviewButton(EditorEventRef sound, Dictionary<string, float> parameters = null, float width = 60f)
	{
		bool playing = IsPreviewing(sound);
		GUIContent content = playing
			? new GUIContent("Stop")
			: new GUIContent("Play");

		if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(width)))
		{
			if (playing) StopPreview();
			else Preview(sound, parameters);
		}
	}

	// FMOD Studio

	public static void OpenInFmodStudio(EditorEventRef sound)
	{
		if (sound != null && EditorUtils.SendScriptCommand($"studio.window.navigateTo(studio.project.lookup(\"{sound.Guid}\"))")) return;

		if (EditorUtility.DisplayDialog("FMOD Studio", "FMOD Studio isn't open. Open the project?", "Open", "Cancel"))
		{
			OpenFmodProject();
		}
	}

	public static void OpenFmodProject()
	{
		string relative = Settings.Instance.SourceProjectPath;
		string full = string.IsNullOrEmpty(relative) ? null : Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

		if (full != null && File.Exists(full)) EditorUtility.OpenWithDefaultApp(full);
		else EditorUtility.DisplayDialog("FMOD Studio", $"Couldn't find the FMOD project{(full != null ? $" at {full}" : "")} (check FMOD > Edit Settings)", "OK");
	}

	// Styles

	private static GUIStyle s_badge;
	private static GUIStyle s_wrappedMini;
	private static GUIStyle s_title;

	public static GUIStyle WrappedMini => s_wrappedMini ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };
	public static GUIStyle Title => s_title ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };

	private static GUIStyle Badge
	{
		get
		{
			if (s_badge == null || s_badge.normal.background == null)
			{
				s_badge = new GUIStyle(EditorStyles.miniLabel)
				{
					alignment = TextAnchor.MiddleCenter,
					fontStyle = FontStyle.Bold,
					padding = new RectOffset(5, 5, 1, 1),
					margin = new RectOffset(0, 3, 2, 2),
					normal = { background = Texture2D.whiteTexture, textColor = Color.white },
				};
			}
			return s_badge;
		}
	}

	public static readonly Color OneShotColor = new Color(0.25f, 0.55f, 0.9f);
	public static readonly Color LoopColor = new Color(0.85f, 0.5f, 0.15f);
	public static readonly Color SpatialColor = new Color(0.35f, 0.65f, 0.35f);
	public static readonly Color FlatColor = new Color(0.45f, 0.45f, 0.45f);
	public static readonly Color TweakColor = new Color(0.65f, 0.35f, 0.8f);
	public static readonly Color WarningColor = new Color(0.85f, 0.25f, 0.25f);

	public static void DrawBadge(string text, Color color, string tooltip = null)
	{
		Color old = GUI.backgroundColor;
		GUI.backgroundColor = color;
		GUILayout.Label(new GUIContent(text, tooltip), Badge, GUILayout.ExpandWidth(false));
		GUI.backgroundColor = old;
	}

	public static void DrawBadges(EditorEventRef sound, bool includeTweaks = true)
	{
		if (sound.Path.StartsWith("snapshot:/"))
		{
			DrawBadge("SNAPSHOT", TweakColor);
			return;
		}

		if (sound.IsOneShot) DrawBadge("ONE-SHOT", OneShotColor);
		else DrawBadge("LOOP", LoopColor);

		if (sound.Is3D) DrawBadge("3D", SpatialColor, $"Max distance {sound.MaxDistance:0.#}");
		else DrawBadge("2D", FlatColor);

		if (includeTweaks)
		{
			SoundSettings settings = SettingsFor(sound);
			if (settings != null && settings.muted) DrawBadge("MUTED", WarningColor);
			else if (settings != null && settings.HasTweaks) DrawBadge("TWEAKED", TweakColor);
		}
	}

	// Inspector widgets

	// foldout state is saved per component type
	public static void HowItWorks(string key, string text)
	{
		string prefKey = "Team7.SoundHelp." + key;
		bool open = EditorPrefs.GetBool(prefKey, true);
		bool nowOpen = EditorGUILayout.Foldout(open, "Info", true);
		if (nowOpen != open) EditorPrefs.SetBool(prefKey, nowOpen);
		if (nowOpen) EditorGUILayout.HelpBox(text, MessageType.None);
		EditorGUILayout.Space(2);
	}

	// EventReference field with the badges/preview button underneath. returns null if empty or missing
	public static EditorEventRef SoundField(SerializedProperty property, GUIContent label = null, string emptyHint = null)
	{
		if (label != null) EditorGUILayout.PropertyField(property, label);
		else EditorGUILayout.PropertyField(property);

		if (property.hasMultipleDifferentValues) return null;

		EventReference reference = property.GetEventReference();
		if (reference.IsNull)
		{
			if (emptyHint != null) EditorGUILayout.HelpBox(emptyHint, MessageType.Info);
			return null;
		}

		EditorEventRef sound = Find(reference);
		if (sound == null)
		{
			EditorGUILayout.HelpBox(
				$"{reference.Path} isn't in the banks (renamed/deleted?) - try FMOD > Refresh Banks or FMOD > Update Event References", MessageType.Warning);
			return null;
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Space(EditorGUIUtility.labelWidth + 2f);
			DrawBadges(sound);
			GUILayout.FlexibleSpace();
			PreviewButton(sound);
			if (GUILayout.Button("Library", EditorStyles.miniButton, GUILayout.Width(52f)))
				SoundLibraryWindow.Open(sound.Path);
		}

		return sound;
	}

	public static void TagField(SerializedProperty property, GUIContent label)
	{
		string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
		List<string> options = new List<string> { "(anything)" };
		options.AddRange(tags);

		string current = property.stringValue;
		int index = string.IsNullOrEmpty(current) ? 0 : Array.IndexOf(tags, current) + 1;
		bool missing = !string.IsNullOrEmpty(current) && index == 0;
		if (missing)
		{
			options.Add($"{current} (missing)");
			index = options.Count - 1;
		}

		EditorGUI.BeginChangeCheck();
		int chosen = EditorGUILayout.Popup(label, index, options.ToArray());
		if (EditorGUI.EndChangeCheck())
		{
			if (chosen == 0) property.stringValue = "";
			else if (chosen <= tags.Length) property.stringValue = tags[chosen - 1];
		}

		if (missing)
			EditorGUILayout.HelpBox($"Tag \"{current}\" doesn't exist, CompareTag will throw errors", MessageType.Error);
	}

	// uses the event's actual params for the dropdowns/sliders
	public static void ParameterList(SerializedProperty list, EditorEventRef sound, GUIContent label)
	{
		List<EditorParamRef> available = sound?.Parameters?.Where(p => p != null && !p.IsGlobal).OrderBy(p => p.Name).ToList() ?? new List<EditorParamRef>();

		if (list.arraySize == 0 && sound != null && available.Count == 0) return;

		EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
		EditorGUI.indentLevel++;

		for (int i = 0; i < list.arraySize; i++)
		{
			SerializedProperty item = list.GetArrayElementAtIndex(i);
			SerializedProperty name = item.FindPropertyRelative("name");
			SerializedProperty value = item.FindPropertyRelative("value");
			SerializedProperty labelText = item.FindPropertyRelative("label");
			EditorParamRef parameter = available.Find(p => p.Name == name.stringValue);

			using (new EditorGUILayout.HorizontalScope())
			{
				if (available.Count > 0)
				{
					string[] names = available.Select(p => p.Name).ToArray();
					int index = Array.IndexOf(names, name.stringValue);
					string[] shown = index >= 0 ? names : names.Append($"{name.stringValue} (not on this sound)").ToArray();
					int chosen = EditorGUILayout.Popup(index >= 0 ? index : shown.Length - 1, shown, GUILayout.Width(150f));
					if (chosen < names.Length) name.stringValue = names[chosen];
				}
				else
				{
					name.stringValue = EditorGUILayout.TextField(name.stringValue, GUILayout.Width(150f));
				}

				if (parameter != null && parameter.Type == ParameterType.Labeled)
				{
					int labelIndex = Array.IndexOf(parameter.Labels, labelText.stringValue);
					if (labelIndex < 0) labelIndex = Mathf.Clamp((int)value.floatValue, 0, parameter.Labels.Length - 1);
					int chosen = EditorGUILayout.Popup(labelIndex, parameter.Labels);
					if (chosen >= 0 && chosen < parameter.Labels.Length)
					{
						labelText.stringValue = parameter.Labels[chosen];
						value.floatValue = chosen;
					}
				}
				else if (parameter != null)
				{
					labelText.stringValue = "";
					value.floatValue = EditorUtils.DrawParameterValueLayout(value.floatValue, parameter);
				}
				else
				{
					value.floatValue = EditorGUILayout.FloatField(value.floatValue);
				}

				if (GUILayout.Button(new GUIContent("−", "Remove"), EditorStyles.miniButton, GUILayout.Width(22f)))
				{
					list.DeleteArrayElementAtIndex(i);
					break;
				}
			}
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Space(EditorGUI.indentLevel * 15f);
			if (GUILayout.Button("Add parameter", EditorStyles.miniButton, GUILayout.Width(110f)))
			{
				GenericMenu menu = new GenericMenu();
				List<string> used = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue).ToList();

				foreach (EditorParamRef parameter in available)
				{
					if (used.Contains(parameter.Name)) menu.AddDisabledItem(new GUIContent(parameter.Name));
					else menu.AddItem(new GUIContent(parameter.Name), false, () => AddParameter(list, parameter.Name, parameter.Default));
				}

				if (available.Count == 0) menu.AddItem(new GUIContent("Custom name"), false, () => AddParameter(list, "", 0f));
				menu.ShowAsContext();
			}
		}

		EditorGUI.indentLevel--;
	}

	private static void AddParameter(SerializedProperty list, string name, float value)
	{
		list.serializedObject.Update();
		int index = list.arraySize;
		list.InsertArrayElementAtIndex(index);
		SerializedProperty item = list.GetArrayElementAtIndex(index);
		item.FindPropertyRelative("name").stringValue = name;
		item.FindPropertyRelative("value").floatValue = value;
		item.FindPropertyRelative("label").stringValue = "";
		list.serializedObject.ApplyModifiedProperties();
	}

	public static EditorParamRef GlobalParameterField(SerializedProperty property, GUIContent label)
	{
		List<EditorParamRef> globals = EventManager.Parameters.Where(p => p != null && p.IsGlobal).OrderBy(p => p.Name).ToList();

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.PropertyField(property, label);
			using (new EditorGUI.DisabledScope(globals.Count == 0))
			{
				if (GUILayout.Button("Pick", EditorStyles.miniButton, GUILayout.Width(40f)))
				{
					GenericMenu menu = new GenericMenu();
					menu.AddItem(new GUIContent("(none)"), string.IsNullOrEmpty(property.stringValue), () => SetString(property, ""));
					foreach (EditorParamRef parameter in globals)
						menu.AddItem(new GUIContent(parameter.Name), parameter.Name == property.stringValue, () => SetString(property, parameter.Name));
					menu.ShowAsContext();
				}
			}
		}

		if (string.IsNullOrEmpty(property.stringValue)) return null;

		EditorParamRef match = globals.Find(p => string.Equals(p.Name, property.stringValue, StringComparison.OrdinalIgnoreCase));
		if (match == null)
		{
			EditorGUILayout.HelpBox($"No global parameter called \"{property.stringValue}\" in FMOD", MessageType.Warning);
		}
		return match;
	}

	private static void SetString(SerializedProperty property, string value)
	{
		property.serializedObject.Update();
		property.stringValue = value;
		property.serializedObject.ApplyModifiedProperties();
	}

	public static void ParameterValueField(SerializedProperty property, GUIContent label, EditorParamRef parameter)
	{
		if (parameter == null)
		{
			EditorGUILayout.PropertyField(property, label);
			return;
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.PrefixLabel(label);
			property.floatValue = EditorUtils.DrawParameterValueLayout(property.floatValue, parameter);
		}
	}

	public static void RequireCollider2D(Component component, bool mustBeTrigger, string purpose)
	{
		Collider2D collider = component.GetComponent<Collider2D>();
		if (collider == null)
		{
			EditorGUILayout.HelpBox($"{purpose} needs a 2D collider", MessageType.Warning);
			if (GUILayout.Button("Add Box Collider 2D"))
			{
				BoxCollider2D box = Undo.AddComponent<BoxCollider2D>(component.gameObject);
				box.isTrigger = mustBeTrigger;
			}
			return;
		}

		if (collider.isTrigger != mustBeTrigger)
		{
			EditorGUILayout.HelpBox(mustBeTrigger
				? $"{purpose} needs Is Trigger ticked on the collider"
				: $"{purpose} needs Is Trigger unticked on the collider", MessageType.Warning);

			if (GUILayout.Button("Fix"))
			{
				Undo.RecordObject(collider, "Change Is Trigger");
				collider.isTrigger = mustBeTrigger;
			}
		}
	}
}
