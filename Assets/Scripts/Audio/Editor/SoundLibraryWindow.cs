using System;
using System.Collections.Generic;
using System.Linq;
using FMOD.Studio;
using FMODUnity;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

using UnityEngine;

/// <summary>
/// Audio > Sound Library - browse/preview/tweak sounds, find problems and see what's playing in play mode
/// </summary>
public class SoundLibraryWindow : EditorWindow
{
	public enum Tab { Sounds, Problems, Live, Settings }

	private enum Filter { All, OneShots, Loops, Positional3D, NotPositional2D, Tweaked, Unused, Snapshots }

	[SerializeField] private Tab m_tab;
	[SerializeField] private string m_selectedPath;
	[SerializeField] private string m_search = "";
	[SerializeField] private Filter m_filter;
	[SerializeField] private List<string> m_collapsedFolders = new List<string>();

	private const float ListWidth = 270f;

	private SearchField m_searchField;
	private Vector2 m_listScroll, m_detailScroll, m_problemScroll, m_liveScroll, m_settingsScroll;
	private SoundUsageFinder.ScanResult m_scan;
	private readonly Dictionary<string, float> m_previewParameters = new Dictionary<string, float>();
	private string m_previewParametersFor;
	private bool m_saveLibraryPending;
	private SerializedObject m_librarySerialized;

	// Opening

	[MenuItem("Audio/Sound Library", priority = 0)]
	private static void OpenFromMenu() => Open(null);

	[MenuItem("Audio/Find Sound Problems", priority = 1)]
	private static void OpenProblems() => Open(null, Tab.Problems);

	[MenuItem("Audio/Live Sound Debugger", priority = 2)]
	private static void OpenLive() => Open(null, Tab.Live);

	public static SoundLibraryWindow Open(string selectPath, Tab tab = Tab.Sounds)
	{
		SoundLibraryWindow window = GetWindow<SoundLibraryWindow>();
		window.m_tab = tab;
		if (!string.IsNullOrEmpty(selectPath))
		{
			window.m_selectedPath = selectPath;
			window.m_search = "";
			window.m_filter = selectPath.StartsWith("snapshot:/") ? Filter.Snapshots : Filter.All;
			window.m_collapsedFolders.Remove(SoundEditorUtils.FolderOf(selectPath));
		}
		window.Show();
		window.Focus();
		return window;
	}

	private void OnEnable()
	{
		titleContent = new GUIContent("Sound Library", EditorGUIUtility.IconContent("AudioSource Icon").image);
		minSize = new Vector2(680f, 420f);
		wantsMouseMove = true;
		EditorApplication.playModeStateChanged += OnPlayModeChanged;
	}

	private void OnDisable()
	{
		EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		SoundEditorUtils.StopPreview();
		SaveLibraryIfPending();
	}

	private void OnPlayModeChanged(PlayModeStateChange change) => Repaint();

	private void OnInspectorUpdate()
	{
		if (GUIUtility.hotControl == 0) SaveLibraryIfPending();
		if ((m_tab == Tab.Live && Application.isPlaying) || SoundEditorUtils.AnyPreviewPlaying) Repaint();
	}

	private void SaveLibraryIfPending()
	{
		if (!m_saveLibraryPending) return;
		m_saveLibraryPending = false;
		SoundLibrary library = SoundEditorUtils.FindLibrary();
		if (library != null) AssetDatabase.SaveAssetIfDirty(library);
	}

	// Layout

	private void OnGUI()
	{
		if (Event.current.type == EventType.MouseMove) Repaint();

		DrawToolbar();

		switch (m_tab)
		{
			case Tab.Sounds: DrawSoundsTab(); break;
			case Tab.Problems: DrawProblemsTab(); break;
			case Tab.Live: DrawLiveTab(); break;
			case Tab.Settings: DrawSettingsTab(); break;
		}
	}

	private void DrawToolbar()
	{
		using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
		{
			int serious = m_scan?.problems.Count(p => p.severity != MessageType.Info) ?? 0;
			string problems = serious > 0 ? $"Problems ({serious})" : "Problems";

			m_tab = (Tab)GUILayout.Toolbar((int)m_tab, new[] { "Sounds", problems, "Live", "Settings" }, EditorStyles.toolbarButton, GUILayout.Width(340f));
			GUILayout.FlexibleSpace();

			if (GUILayout.Button(new GUIContent("Refresh from FMOD", "Reload after building banks"), EditorStyles.toolbarButton))
			{
				EventManager.RefreshBanks();
				m_scan = null;
			}

			if (GUILayout.Button("Open FMOD Studio", EditorStyles.toolbarButton))
				SoundEditorUtils.OpenFmodProject();
		}
	}

	private static void Section(string title)
	{
		EditorGUILayout.Space(10f);
		EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
		Rect line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
		EditorGUI.DrawRect(line, new Color(0.5f, 0.5f, 0.5f, 0.35f));
		EditorGUILayout.Space(3f);
	}

	private void EnsureScan()
	{
		if (m_scan == null) m_scan = SoundUsageFinder.Scan();
	}

	// Sounds tab

	private void DrawSoundsTab()
	{
		List<EditorEventRef> everything = EventManager.Events.Where(e => e != null).ToList();
		if (everything.Count == 0)
		{
			EditorGUILayout.HelpBox("No FMOD events found, build the banks in FMOD Studio then hit Refresh from FMOD", MessageType.Warning);
			return;
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
				DrawSoundList();

			Rect divider = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1f), GUILayout.ExpandHeight(true));
			EditorGUI.DrawRect(divider, new Color(0f, 0f, 0f, 0.3f));

			using (new EditorGUILayout.VerticalScope())
				DrawDetails();
		}
	}

	private void DrawSoundList()
	{
		m_searchField ??= new SearchField();
		EditorGUILayout.Space(3f);
		m_search = m_searchField.OnGUI(m_search);
		m_filter = (Filter)EditorGUILayout.EnumPopup(m_filter);

		if (m_filter == Filter.Unused && m_scan == null)
		{
			EditorGUILayout.HelpBox("Needs a project scan first", MessageType.Info);
			if (GUILayout.Button("Scan project")) EnsureScan();
			return;
		}

		List<EditorEventRef> all = m_filter == Filter.Snapshots ? SoundEditorUtils.Snapshots : SoundEditorUtils.Sounds;
		List<EditorEventRef> shown = all.Where(PassesFilter).ToList();
		bool searching = !string.IsNullOrEmpty(m_search);

		m_listScroll = EditorGUILayout.BeginScrollView(m_listScroll);

		foreach (IGrouping<string, EditorEventRef> folder in shown.GroupBy(e => SoundEditorUtils.FolderOf(e.Path)))
		{
			bool inFolder = folder.Key.Length > 0;
			if (inFolder)
			{
				bool open = searching || !m_collapsedFolders.Contains(folder.Key);
				bool nowOpen = EditorGUILayout.Foldout(open, folder.Key, true);
				if (!searching && nowOpen != open)
				{
					if (nowOpen) m_collapsedFolders.Remove(folder.Key);
					else m_collapsedFolders.Add(folder.Key);
				}
				if (!nowOpen) continue;
			}

			foreach (EditorEventRef sound in folder) DrawListRow(sound, inFolder ? 14f : 0f);
		}

		if (shown.Count == 0) EditorGUILayout.LabelField("Nothing found", EditorStyles.centeredGreyMiniLabel);

		EditorGUILayout.EndScrollView();
		EditorGUILayout.LabelField($"{shown.Count} of {all.Count} {(m_filter == Filter.Snapshots ? "snapshots" : "sounds")}", EditorStyles.centeredGreyMiniLabel);
	}

	private bool PassesFilter(EditorEventRef sound)
	{
		if (!string.IsNullOrEmpty(m_search) && sound.Path.IndexOf(m_search, StringComparison.OrdinalIgnoreCase) < 0) return false;

		switch (m_filter)
		{
			case Filter.OneShots: return sound.IsOneShot;
			case Filter.Loops: return !sound.IsOneShot;
			case Filter.Positional3D: return sound.Is3D;
			case Filter.NotPositional2D: return !sound.Is3D;
			case Filter.Tweaked: return SoundEditorUtils.SettingsFor(sound)?.HasTweaks == true;
			case Filter.Unused: return m_scan != null && m_scan.UsagesOf(sound.Path).Count == 0;
			default: return true;
		}
	}

	private void DrawListRow(EditorEventRef sound, float indent)
	{
		Rect row = GUILayoutUtility.GetRect(10f, 20f, GUILayout.ExpandWidth(true));
		bool selected = sound.Path == m_selectedPath;
		Event current = Event.current;

		if (current.type == EventType.Repaint)
		{
			if (selected) EditorGUI.DrawRect(row, new Color(0.24f, 0.48f, 0.9f, 0.45f));
			else if (row.Contains(current.mousePosition)) EditorGUI.DrawRect(row, new Color(0.5f, 0.5f, 0.5f, 0.12f));
		}

		SoundSettings settings = SoundEditorUtils.SettingsFor(sound);
		string name = SoundEditorUtils.NameOf(sound.Path);
		if (settings != null && settings.muted) name += "  (muted)";
		else if (settings != null && settings.HasTweaks) name += " *";

		GUI.Label(new Rect(row.x + 4f + indent, row.y + 2f, row.width - 100f - indent, 16f), new GUIContent(name, sound.Path));

		bool snapshot = sound.Path.StartsWith("snapshot:/");
		if (!snapshot)
		{
			DrawRowBadge(new Rect(row.xMax - 92f, row.y + 3f, 52f, 14f), sound.IsOneShot ? "ONE-SHOT" : "LOOP", sound.IsOneShot ? SoundEditorUtils.OneShotColor : SoundEditorUtils.LoopColor);
			DrawRowBadge(new Rect(row.xMax - 36f, row.y + 3f, 26f, 14f), sound.Is3D ? "3D" : "2D", sound.Is3D ? SoundEditorUtils.SpatialColor : SoundEditorUtils.FlatColor);
		}

		if (current.type == EventType.MouseDown && row.Contains(current.mousePosition))
		{
			Select(sound);
			if (current.button == 0 && current.clickCount == 2) SoundEditorUtils.Preview(sound);
			if (current.button == 1) ShowRowMenu(sound);
			current.Use();
		}
	}

	private static void DrawRowBadge(Rect rect, string text, Color color)
	{
		EditorGUI.DrawRect(rect, color);
		GUI.Label(rect, text, new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 8, normal = { textColor = Color.white } });
	}

	private void Select(EditorEventRef sound)
	{
		if (m_selectedPath != sound.Path) GUI.FocusControl(null);
		m_selectedPath = sound.Path;
		Repaint();
	}

	private void ShowRowMenu(EditorEventRef sound)
	{
		GenericMenu menu = new GenericMenu();
		menu.AddItem(new GUIContent("Preview"), false, () => SoundEditorUtils.Preview(sound));
		menu.AddItem(new GUIContent("Copy name"), false, () => EditorGUIUtility.systemCopyBuffer = SoundEditorUtils.CodeName(sound));
		menu.AddItem(new GUIContent("Copy full path"), false, () => EditorGUIUtility.systemCopyBuffer = sound.Path);
		menu.AddItem(new GUIContent("Copy code"), false, () => EditorGUIUtility.systemCopyBuffer = SoundEditorUtils.CodeSnippet(sound));
		menu.AddSeparator("");
		menu.AddItem(new GUIContent("Show in FMOD Studio"), false, () => SoundEditorUtils.OpenInFmodStudio(sound));
		menu.ShowAsContext();
	}

	// Details

	private void DrawDetails()
	{
		EditorEventRef sound = string.IsNullOrEmpty(m_selectedPath) ? null : EventManager.EventFromString(m_selectedPath);
		if (sound == null)
		{
			GUILayout.FlexibleSpace();
			EditorGUILayout.LabelField("Select a sound\n(double click to preview)", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(70f));
			GUILayout.FlexibleSpace();
			return;
		}

		if (m_previewParametersFor != sound.Path)
		{
			m_previewParameters.Clear();
			m_previewParametersFor = sound.Path;
		}

		bool snapshot = sound.Path.StartsWith("snapshot:/");
		m_detailScroll = EditorGUILayout.BeginScrollView(m_detailScroll);
		EditorGUILayout.Space(4f);

		EditorGUILayout.LabelField(SoundEditorUtils.NameOf(sound.Path), SoundEditorUtils.Title, GUILayout.Height(22f));
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.SelectableLabel(sound.Path, EditorStyles.miniLabel, GUILayout.Height(16f));
			if (GUILayout.Button("Copy path", EditorStyles.miniButton, GUILayout.Width(70f))) EditorGUIUtility.systemCopyBuffer = sound.Path;
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			SoundEditorUtils.DrawBadges(sound);
			GUILayout.FlexibleSpace();
		}

		EditorGUILayout.Space(4f);
		using (new EditorGUILayout.HorizontalScope())
		{
			bool playing = SoundEditorUtils.IsPreviewing(sound);
			if (GUILayout.Button(playing ? "Stop" : "Preview", GUILayout.Height(26f), GUILayout.Width(110f)))
			{
				if (playing) SoundEditorUtils.StopPreview();
				else SoundEditorUtils.Preview(sound, m_previewParameters);
			}

			if (GUILayout.Button(new GUIContent("Show in FMOD Studio", "FMOD Studio needs to be open"), GUILayout.Height(26f)))
				SoundEditorUtils.OpenInFmodStudio(sound);

			if (!snapshot && GUILayout.Button("Add to scene...", GUILayout.Height(26f)))
				ShowUseMenu(sound);
		}

		DrawPreviewParameters(sound);

		Section("Info");
		EditorGUILayout.LabelField(SoundEditorUtils.Describe(sound), EditorStyles.wordWrappedLabel);

		Section("Usage");
		DrawHowToPlay(sound);

		if (!snapshot)
		{
			Section("Tweaks");
			DrawTweaks(sound);
		}

		Section("Used in");
		DrawUsages(sound);

		EditorGUILayout.Space(12f);
		EditorGUILayout.EndScrollView();
	}

	private void DrawPreviewParameters(EditorEventRef sound)
	{
		if (sound.Parameters == null || sound.Parameters.Count == 0) return;

		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("Preview params", EditorStyles.miniBoldLabel);
		foreach (EditorParamRef parameter in sound.Parameters.Where(p => p != null).OrderBy(p => p.Name))
		{
			if (!m_previewParameters.TryGetValue(parameter.Name, out float value)) value = parameter.Default;
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(new GUIContent(parameter.Name, SoundEditorUtils.DescribeParameter(parameter)), GUILayout.Width(140f));
				m_previewParameters[parameter.Name] = EditorUtils.DrawParameterValueLayout(value, parameter);
			}
		}
	}

	private static void DrawHowToPlay(EditorEventRef sound)
	{
		EditorGUILayout.LabelField("Components", EditorStyles.miniBoldLabel);
		string noCode;
		if (sound.Path.StartsWith("snapshot:/"))
			noCode = "- Sound Zone (Snapshot While Inside)\n- Settings tab > Pause Snapshot";
		else if (sound.IsOneShot)
			noCode = "- Sound Emitter\n" +
			         $"- Animation Sounds > PlaySound(\"{SoundEditorUtils.CodeName(sound)}\")\n" +
			         "- UI Sounds / Footstep Sounds";
		else
			noCode = "- Level Audio (music/ambience)\n- Sound Zone (Loop While Inside)\n" +
			         "- Sound Emitter with a Stop When\n" +
			         $"- Animation Sounds > StartLoop/StopLoop(\"{SoundEditorUtils.CodeName(sound)}\")";
		EditorGUILayout.LabelField(noCode, EditorStyles.wordWrappedMiniLabel);

		EditorGUILayout.Space(4f);
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField("Code", EditorStyles.miniBoldLabel);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50f))) EditorGUIUtility.systemCopyBuffer = SoundEditorUtils.CodeSnippet(sound);
		}

		string code = SoundEditorUtils.CodeSnippet(sound);
		GUIStyle codeStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false, font = MonospaceFont };
		EditorGUILayout.SelectableLabel(code, codeStyle, GUILayout.Height(codeStyle.CalcHeight(new GUIContent(code), 400f) + 4f));
	}

	private static Font s_mono;
	private static bool s_monoSearched;

	private static Font MonospaceFont
	{
		get
		{
			if (!s_monoSearched)
			{
				s_mono = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;
				s_monoSearched = true;
			}
			return s_mono;
		}
	}

	private static void ShowUseMenu(EditorEventRef sound)
	{
		GameObject selected = Selection.activeGameObject;
		bool inScene = selected != null && selected.scene.IsValid();
		GenericMenu menu = new GenericMenu();
		string target = inScene ? $" on \"{selected.name}\"" : " (select an object first)";

		if (inScene) menu.AddItem(new GUIContent("Sound Emitter" + target), false, () => SoundMenuItems.AddEmitter(selected, sound));
		else menu.AddDisabledItem(new GUIContent("Sound Emitter" + target));

		if (!sound.IsOneShot)
		{
			menu.AddItem(new GUIContent("Level Audio music"), false, () => SoundMenuItems.SetLevelAudio(sound, "music"));
			menu.AddItem(new GUIContent("Level Audio ambience"), false, () => SoundMenuItems.SetLevelAudio(sound, "ambience"));
			if (inScene) menu.AddItem(new GUIContent("Sound Zone" + target), false, () => SoundMenuItems.AddZone(selected, sound));
			else menu.AddDisabledItem(new GUIContent("Sound Zone" + target));
		}

		menu.ShowAsContext();
	}

	private void DrawTweaks(EditorEventRef sound)
	{
		EditorGUILayout.LabelField("Applied on top of the FMOD mix, no bank rebuild needed", EditorStyles.wordWrappedMiniLabel);

		SoundLibrary library = SoundEditorUtils.FindLibrary();
		SoundSettings existing = null;
		if (library != null) library.TryGet(sound.Guid, out existing);
		SoundSettings shown = existing ?? SoundSettings.Defaults;

		EditorGUI.BeginChangeCheck();

		float volumeDb = EditorGUILayout.Slider(new GUIContent("Volume (dB)", "0 = same as FMOD"), shown.volumeDb, -40f, 10f);
		HintLine(volumeDb == 0f ? "" : $"~{Mathf.Pow(2f, volumeDb / 10f) * 100f:0}% loudness");

		float randomVolumeDb = EditorGUILayout.Slider(new GUIContent("Random volume (dB)", "Randomly up to this much quieter each play"), shown.randomVolumeDb, 0f, 12f);
		float pitch = EditorGUILayout.Slider(new GUIContent("Pitch (semitones)", "12 = octave up, also changes the length"), shown.pitchSemitones, -24f, 24f);
		float randomPitch = EditorGUILayout.Slider(new GUIContent("Random pitch (semitones)", "1-2 works well for footsteps"), shown.randomPitchSemitones, 0f, 12f);
		float cooldown = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Cooldown (seconds)", "Min time between plays"), shown.cooldown));

		bool keepPlaying = shown.keepPlayingOnSceneChange;
		if (!sound.IsOneShot)
			keepPlaying = EditorGUILayout.Toggle(new GUIContent("Keep playing on scene change", "Loops from RAudio.Play normally stop on scene change"), keepPlaying);

		bool muted = EditorGUILayout.Toggle("Mute", shown.muted);

		EditorGUILayout.LabelField("Notes");
		string notes = EditorGUILayout.TextArea(shown.notes ?? "", EditorStyles.textArea, GUILayout.MinHeight(38f));

		if (EditorGUI.EndChangeCheck())
		{
			bool creatingLibrary = library == null;
			library = SoundEditorUtils.FindOrCreateLibrary();
			Undo.RecordObject(library, "Tweak " + SoundEditorUtils.NameOf(sound.Path));

			if (existing == null)
			{
				existing = new SoundSettings();
				library.sounds.Add(existing);
			}

			existing.sound = new EventReference { Guid = sound.Guid, Path = sound.Path };
			existing.volumeDb = volumeDb;
			existing.randomVolumeDb = randomVolumeDb;
			existing.pitchSemitones = pitch;
			existing.randomPitchSemitones = randomPitch;
			existing.cooldown = cooldown;
			existing.keepPlayingOnSceneChange = keepPlaying;
			existing.muted = muted;
			existing.notes = notes;

			CommitLibrary(library, existing);

			// creating the asset mid OnGUI messes up the layout for this frame
			if (creatingLibrary) GUIUtility.ExitGUI();
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			using (new EditorGUI.DisabledScope(existing == null))
			{
				if (GUILayout.Button("Reset tweaks", EditorStyles.miniButton, GUILayout.Width(90f)))
				{
					Undo.RecordObject(library, "Reset " + SoundEditorUtils.NameOf(sound.Path));
					library.sounds.Remove(existing);
					CommitLibrary(library, null);
				}
			}

			if (Application.isPlaying) EditorGUILayout.LabelField("Applies next time it plays", EditorStyles.miniLabel);
		}
	}

	private void CommitLibrary(SoundLibrary library, SoundSettings changed)
	{
		// only store changed sounds, sorted so the asset diffs nicely
		if (changed != null && !changed.HasTweaks) library.sounds.Remove(changed);
		library.sounds.Sort((a, b) => string.Compare(a.sound.Path, b.sound.Path, StringComparison.OrdinalIgnoreCase));
		library.InvalidateLookup();
		EditorUtility.SetDirty(library);
		m_saveLibraryPending = true;
	}

	private static void HintLine(string text)
	{
		using (new EditorGUILayout.HorizontalScope())
		{
			GUILayout.Space(EditorGUIUtility.labelWidth + 2f);
			EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
		}
	}

	private void DrawUsages(EditorEventRef sound)
	{
		if (m_scan == null)
		{
			if (GUILayout.Button("Scan project", GUILayout.Width(160f))) EnsureScan();
			return;
		}

		List<SoundUsageFinder.Usage> usages = m_scan.UsagesOf(sound.Path);
		if (usages.Count == 0)
		{
			EditorGUILayout.LabelField("Not used anywhere", EditorStyles.miniLabel);
		}

		foreach (SoundUsageFinder.Usage usage in usages)
		{
			using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField(usage.source.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(62f));
				using (new EditorGUILayout.VerticalScope())
				{
					EditorGUILayout.LabelField(new GUIContent(usage.Location, usage.Location), EditorStyles.miniLabel);
					EditorGUILayout.LabelField(usage.detail, EditorStyles.wordWrappedMiniLabel);
				}
				if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(44f))) SoundUsageFinder.Open(usage);
			}
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField($"Scanned {m_scan.scannedAt:HH:mm}", EditorStyles.miniLabel, GUILayout.Width(110f));
			if (GUILayout.Button("Rescan", EditorStyles.miniButton, GUILayout.Width(60f))) m_scan = SoundUsageFinder.Scan();
		}
	}

	// Problems tab

	private void DrawProblemsTab()
	{
		EditorGUILayout.Space(4f);
		EditorGUILayout.LabelField("Checks scripts, scenes, prefabs and animations for bad sound names, loops played as one shots and missing events", EditorStyles.wordWrappedMiniLabel);

		if (m_scan == null)
		{
			if (GUILayout.Button("Scan project", GUILayout.Height(28f))) EnsureScan();
			return;
		}

		using (new EditorGUILayout.HorizontalScope())
		{
			int errors = m_scan.problems.Count(p => p.severity == MessageType.Error);
			int warnings = m_scan.problems.Count(p => p.severity == MessageType.Warning);
			int notes = m_scan.problems.Count(p => p.severity == MessageType.Info);
			EditorGUILayout.LabelField($"Scanned {m_scan.scannedAt:HH:mm} - {errors} errors, {warnings} warnings, {notes} info", EditorStyles.boldLabel);
			if (GUILayout.Button("Rescan", GUILayout.Width(80f))) m_scan = SoundUsageFinder.Scan();
		}

		if (m_scan.problems.Count == 0)
		{
			EditorGUILayout.HelpBox("No problems found", MessageType.Info);
			return;
		}

		m_problemScroll = EditorGUILayout.BeginScrollView(m_problemScroll);
		foreach (SoundUsageFinder.Problem problem in m_scan.problems.ToList())
		{
			EditorGUILayout.HelpBox(problem.message, problem.severity);

			bool hasButtons = problem.usage != null || problem.soundPath != null || problem.fix != null;
			if (!hasButtons) continue;

			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				if (problem.usage != null && GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(60f))) SoundUsageFinder.Open(problem.usage);
				if (problem.soundPath != null && GUILayout.Button("Show sound", EditorStyles.miniButton, GUILayout.Width(80f))) Open(problem.soundPath);
				if (problem.fix != null && GUILayout.Button(problem.fixLabel, EditorStyles.miniButton, GUILayout.Width(150f)))
				{
					problem.fix();
					m_scan = null;
					GUIUtility.ExitGUI();
				}
			}
		}
		EditorGUILayout.EndScrollView();
	}

	// Live tab

	private void DrawLiveTab()
	{
		if (!Application.isPlaying)
		{
			EditorGUILayout.HelpBox("Only works in play mode", MessageType.Info);
			return;
		}

		AudioManager manager = AudioManager.Current;
		if (manager == null)
		{
			EditorGUILayout.HelpBox("No AudioManager yet (gets made when the first sound plays)", MessageType.Info);
			return;
		}

		m_liveScroll = EditorGUILayout.BeginScrollView(m_liveScroll);

		if (!manager.IsReady) EditorGUILayout.HelpBox("Waiting for banks to load", MessageType.Warning);

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField(
				$"Listener: {(manager.UsingAutomaticListener ? "auto (main camera)" : "scene")}    Paused: {(manager.IsGamePaused ? "yes" : "no")}",
				EditorStyles.miniLabel);
			if (GUILayout.Button("Stop all", EditorStyles.miniButton, GUILayout.Width(110f))) manager.StopAllSounds(true);
		}

		Section("Playing now");
		List<AudioManager.ActiveSound> active = manager.GetActiveSounds();
		if (active.Count == 0)
			EditorGUILayout.LabelField("Nothing (one shots show up under recent)", EditorStyles.wordWrappedMiniLabel);

		foreach (AudioManager.ActiveSound sound in active)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(sound.role, EditorStyles.miniBoldLabel, GUILayout.Width(70f));
				EditorGUILayout.LabelField(new GUIContent(SoundEditorUtils.NameOf(sound.path), sound.path));
				EditorGUILayout.LabelField(sound.state.ToString().ToLowerInvariant(), EditorStyles.miniLabel, GUILayout.Width(80f));
				if (GUILayout.Button("Stop", EditorStyles.miniButton, GUILayout.Width(44f))) manager.StopActiveSound(sound.role, sound.path);
			}
		}

		Section("Recent");
		IReadOnlyList<AudioManager.SoundLogEntry> log = manager.RecentSounds;
		if (log.Count == 0) EditorGUILayout.LabelField("Nothing yet", EditorStyles.miniLabel);

		GUIStyle problemStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.45f, 0.4f) } };
		GUIStyle skippedStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.75f, 0.3f) } };

		for (int i = log.Count - 1; i >= Mathf.Max(0, log.Count - 25); i--)
		{
			AudioManager.SoundLogEntry entry = log[i];
			GUIStyle style = entry.note == null ? EditorStyles.miniLabel
				: entry.note == "not found" ? problemStyle
				: entry.note.Contains("muted") || entry.note.Contains("cooldown") ? skippedStyle
				: EditorStyles.miniLabel;

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField($"{Time.unscaledTime - entry.time:0.0}s ago", EditorStyles.miniLabel, GUILayout.Width(70f));
				EditorGUILayout.LabelField(new GUIContent(SoundEditorUtils.NameOf(entry.path) + (entry.note != null ? $"  -  {entry.note}" : ""), entry.path), style);
			}
		}

		Section("Buses (not saved)");
		foreach (string busPath in manager.GetBusPaths())
		{
			if (!manager.TryGetBus(busPath, out Bus bus)) continue;
			bus.getVolume(out float volume);
			bus.getMute(out bool mute);

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(busPath == SONIC_AUDIO_BUS.Master ? "Master" : busPath, GUILayout.Width(140f));
				float newVolume = EditorGUILayout.Slider(volume, 0f, 1f);
				bool newMute = GUILayout.Toggle(mute, "Mute", EditorStyles.miniButton, GUILayout.Width(44f));
				if (!Mathf.Approximately(newVolume, volume)) bus.setVolume(newVolume);
				if (newMute != mute) bus.setMute(newMute);
			}
		}

		Section("Global parameters");
		RuntimeManager.StudioSystem.getParameterDescriptionList(out PARAMETER_DESCRIPTION[] parameters);
		List<PARAMETER_DESCRIPTION> globals = (parameters ?? Array.Empty<PARAMETER_DESCRIPTION>())
			.Where(p => (p.flags & PARAMETER_FLAGS.GLOBAL) != 0 && (p.flags & PARAMETER_FLAGS.READONLY) == 0).ToList();
		if (globals.Count == 0) EditorGUILayout.LabelField("No global parameters", EditorStyles.miniLabel);

		foreach (PARAMETER_DESCRIPTION parameter in globals)
		{
			RuntimeManager.StudioSystem.getParameterByID(parameter.id, out float value);
			float newValue = EditorGUILayout.Slider((string)parameter.name, value, parameter.minimum, parameter.maximum);
			if (!Mathf.Approximately(newValue, value)) RuntimeManager.StudioSystem.setParameterByID(parameter.id, newValue);
		}

		EditorGUILayout.EndScrollView();
	}

	// Settings tab

	private void DrawSettingsTab()
	{
		SoundLibrary library = SoundEditorUtils.FindLibrary();
		m_settingsScroll = EditorGUILayout.BeginScrollView(m_settingsScroll);
		EditorGUILayout.Space(4f);

		if (library == null)
		{
			EditorGUILayout.HelpBox("No Sound Library asset yet (gets made when you first tweak a sound)", MessageType.Info);
			if (GUILayout.Button("Create Sound Library")) SoundEditorUtils.FindOrCreateLibrary();
			EditorGUILayout.EndScrollView();
			return;
		}

		if (!AssetDatabase.GetAssetPath(library).Contains("/Resources/"))
			EditorGUILayout.HelpBox("Sound Library has to be in a Resources folder or it won't load in game", MessageType.Error);

		if (m_librarySerialized == null || m_librarySerialized.targetObject != library) m_librarySerialized = new SerializedObject(library);
		m_librarySerialized.Update();

		Section("Pausing");
		EditorGUILayout.LabelField(
			$"bus:/ is master (pauses menu sounds too). Our other buses are {SONIC_AUDIO_BUS.SFX}, {SONIC_AUDIO_BUS.Music} and {SONIC_AUDIO_BUS.Ambience}",
			EditorStyles.wordWrappedMiniLabel);
		EditorGUILayout.PropertyField(m_librarySerialized.FindProperty("pauseBuses"), true);
		EditorGUILayout.PropertyField(m_librarySerialized.FindProperty("pauseSnapshot"));

		Section("Scene changes");
		EditorGUILayout.PropertyField(m_librarySerialized.FindProperty("stopMusicInScenesWithoutLevelAudio"));

		Section("Debugging");
		EditorGUILayout.PropertyField(m_librarySerialized.FindProperty("warnAboutMissingSounds"));
		EditorGUILayout.PropertyField(m_librarySerialized.FindProperty("logEverySound"));

		if (m_librarySerialized.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(library);

		Section("Listener");
		EditorGUILayout.LabelField("AudioManager adds one on the main camera if the scene doesn't have one", EditorStyles.wordWrappedMiniLabel);
		if (GUILayout.Button("Add Sound Listener 2D to Main Camera", GUILayout.Width(340f))) SoundMenuItems.AddListenerToMainCamera();

		Section("Library asset");
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(library), EditorStyles.miniLabel);
			if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60f))) EditorGUIUtility.PingObject(library);
		}
		EditorGUILayout.LabelField($"{library.sounds.Count} tweaked", EditorStyles.miniLabel);

		EditorGUILayout.EndScrollView();
	}
}
