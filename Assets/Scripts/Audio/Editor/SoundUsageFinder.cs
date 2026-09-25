using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FMODUnity;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans scripts, scenes, prefabs and animations for sound usages + problems for the Sound Library window
/// </summary>
public static class SoundUsageFinder
{
	public enum Source { Script, Scene, Prefab, Asset, Animation }

	public class Usage
	{
		public Source source;
		public string assetPath;
		public int line;           // scripts only
		public string method;      // RAudio method or animation event function name
		public string soundText;   // name/path exactly as written
		public string detail;

		public string Location => line > 0 ? $"{assetPath}:{line}" : assetPath;
	}

	public class Problem
	{
		public MessageType severity;
		public string message;
		public Usage usage;
		public string soundPath;
		public string fixLabel;
		public Action fix;
	}

	public class ScanResult
	{
		public readonly List<Usage> usages = new List<Usage>();
		public readonly List<Problem> problems = new List<Problem>();
		public DateTime scannedAt;

		public List<Usage> UsagesOf(string path) => usages.Where(u => Resolves(u, path)).ToList();

		private static bool Resolves(Usage usage, string path)
		{
			if (string.Equals(usage.soundText, path, StringComparison.OrdinalIgnoreCase)) return true;
			if (usage.soundText.Contains(":/")) return false;
			return string.Equals(usage.soundText, SoundEditorUtils.NameOf(path), StringComparison.OrdinalIgnoreCase) &&
			       path.StartsWith(SnapshotMethods.Contains(usage.method) ? "snapshot:/" : "event:/");
		}
	}

	// RAudio methods where the first arg is a sound name
	private static readonly HashSet<string> EventMethods = new HashSet<string>
	{
		"PlayOneShot", "Play", "Stop", "IsPlaying", "SetLabeledParam", "SetValueParam", "GetValueParam",
		"PlayMusic", "PlayAmbience", "CreateInstance",
	};

	private static readonly HashSet<string> SnapshotMethods = new HashSet<string> { "StartSnapshot", "StopSnapshot", "IsSnapshotActive" };

	private static readonly HashSet<string> AnimationFunctions = new HashSet<string> { "PlaySound", "PlaySoundEvent", "StartLoop", "StopLoop" };

	private static readonly Regex ScriptCall = new Regex(@"RAudio\s*\.\s*(?<method>\w+)\s*\(\s*""(?<name>[^""]*)""", RegexOptions.Compiled);
	private static readonly Regex YamlEventPath = new Regex(@"^\s*Path: (?<path>(?:event|snapshot):/.*?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

	public static ScanResult Scan()
	{
		ScanResult result = new ScanResult { scannedAt = DateTime.Now };

		try
		{
			EditorUtility.DisplayProgressBar("Sound Library", "Scanning scripts...", 0.1f);
			ScanScripts(result.usages);

			EditorUtility.DisplayProgressBar("Sound Library", "Scanning scenes and prefabs...", 0.4f);
			ScanSerializedFiles(result.usages);

			EditorUtility.DisplayProgressBar("Sound Library", "Scanning animations...", 0.8f);
			ScanAnimations(result.usages);

			FindProblems(result);
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}

		return result;
	}

	private static IEnumerable<string> ProjectFiles(params string[] extensions)
	{
		return Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories)
			.Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
			.Select(f => f.Replace('\\', '/'))
			.Where(f => !f.StartsWith("Assets/Plugins/FMOD/"));
	}

	private static void ScanScripts(List<Usage> usages)
	{
		foreach (string file in ProjectFiles(".cs"))
		{
			if (file.StartsWith("Assets/Scripts/Audio/")) continue;

			string[] lines = File.ReadAllLines(file);
			for (int i = 0; i < lines.Length; i++)
			{
				foreach (Match match in ScriptCall.Matches(lines[i]))
				{
					string method = match.Groups["method"].Value;
					if (!EventMethods.Contains(method) && !SnapshotMethods.Contains(method)) continue;
					if (lines[i].TrimStart().StartsWith("//")) continue;

					usages.Add(new Usage
					{
						source = Source.Script,
						assetPath = file,
						line = i + 1,
						method = method,
						soundText = match.Groups["name"].Value,
						detail = lines[i].Trim(),
					});
				}
			}
		}
	}

	private static void ScanSerializedFiles(List<Usage> usages)
	{
		string libraryPath = SoundEditorUtils.FindLibrary() is SoundLibrary library ? AssetDatabase.GetAssetPath(library) : null;
		string levelAudioGuid = AssetDatabase.FindAssets("LevelAudio t:MonoScript")
			.FirstOrDefault(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/LevelAudio.cs"));

		foreach (string file in ProjectFiles(".unity", ".prefab", ".asset"))
		{
			if (file == libraryPath) continue;

			// skip binary assets (terrain data etc) without reading the whole file
			string text;
			try
			{
				if (!IsYaml(file)) continue;
				text = File.ReadAllText(file);
			}
			catch (IOException) { continue; }
			catch (UnauthorizedAccessException) { continue; }

			Source source = file.EndsWith(".unity") ? Source.Scene : file.EndsWith(".prefab") ? Source.Prefab : Source.Asset;
			if (levelAudioGuid != null && text.Contains(levelAudioGuid)) AddLegacyLevelAudio(usages, source, file, text, levelAudioGuid);

			foreach (IGrouping<string, Match> group in YamlEventPath.Matches(text).Cast<Match>().GroupBy(m => m.Groups["path"].Value))
			{
				int count = group.Count();
				usages.Add(new Usage
				{
					source = source,
					assetPath = file,
					method = "Component",
					soundText = group.Key,
					detail = count == 1 ? "1 component field" : $"{count} component fields",
				});
			}
		}
	}

	// old Level Audio tick boxes play sounds by name so count those too
	private static void AddLegacyLevelAudio(List<Usage> usages, Source source, string file, string text, string levelAudioGuid)
	{
		foreach (string document in text.Split(new[] { "\n--- " }, StringSplitOptions.None).Where(d => d.Contains("guid: " + levelAudioGuid)))
		{
			AddIfTicked("playMusic", LevelAudio.LegacyMusic);
			AddIfTicked("playUIMusic", LevelAudio.LegacyUIMusic);
			AddIfTicked("playAmbience", LevelAudio.LegacyAmbience);

			void AddIfTicked(string field, string soundName)
			{
				if (!Regex.IsMatch(document, $@"^\s*{field}: 1\s*$", RegexOptions.Multiline)) return;
				usages.Add(new Usage
				{
					source = source,
					assetPath = file,
					method = "LevelAudio",
					soundText = soundName,
					detail = $"Level Audio (old \"{field}\" tick box)",
				});
			}
		}
	}

	private static bool IsYaml(string file)
	{
		byte[] header = new byte[5];
		using (FileStream stream = File.OpenRead(file))
		{
			return stream.Read(header, 0, header.Length) == header.Length && System.Text.Encoding.ASCII.GetString(header) == "%YAML";
		}
	}

	private static void ScanAnimations(List<Usage> usages)
	{
		foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
			{
				foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
				{
					if (!AnimationFunctions.Contains(animationEvent.functionName)) continue;
					usages.Add(new Usage
					{
						source = Source.Animation,
						assetPath = path,
						method = animationEvent.functionName,
						soundText = animationEvent.stringParameter ?? "",
						detail = $"{clip.name} at {animationEvent.time:0.00}s",
					});
				}
			}
		}
	}

	private static void FindProblems(ScanResult result)
	{
		HashSet<string> knownPaths = new HashSet<string>(EventManager.Events.Where(e => e != null).Select(e => e.Path), StringComparer.OrdinalIgnoreCase);

		foreach (Usage usage in result.usages)
		{
			bool snapshot = SnapshotMethods.Contains(usage.method);
			string where = usage.source == Source.Script ? $"{Path.GetFileName(usage.assetPath)} line {usage.line}" : $"{usage.assetPath} ({usage.detail})";

			// components store the full path
			if (usage.method == "Component")
			{
				if (!knownPaths.Contains(usage.soundText))
				{
					result.problems.Add(new Problem
					{
						severity = MessageType.Warning,
						usage = usage,
						message = $"{where} uses {usage.soundText} which doesn't exist anymore (renamed/deleted in FMOD?)",
						fixLabel = "Update Event References",
						fix = () => EditorApplication.ExecuteMenuItem("FMOD/Update Event References"),
					});
				}
				continue;
			}

			if (string.IsNullOrWhiteSpace(usage.soundText))
			{
				result.problems.Add(new Problem { severity = MessageType.Error, usage = usage, message = $"{where}: {usage.method} with an empty name" });
				continue;
			}

			EditorEventRef sound = SoundEditorUtils.FindByName(usage.soundText, out List<EditorEventRef> matches, snapshot);
			if (sound == null)
			{
				string suggestion = usage.soundText.Contains(":/") ? null : SoundEditorUtils.SuggestName(usage.soundText, snapshot);
				result.problems.Add(new Problem
				{
					severity = MessageType.Error,
					usage = usage,
					message = $"{where}: no {(snapshot ? "snapshot" : "event")} called \"{usage.soundText}\"" +
					          (suggestion != null ? $" (did you mean \"{suggestion}\"?)" : ""),
				});
				continue;
			}

			if (matches.Count > 1)
			{
				result.problems.Add(new Problem
				{
					severity = MessageType.Warning,
					usage = usage,
					soundPath = sound.Path,
					message = $"{where}: \"{usage.soundText}\" matches {matches.Count} events ({string.Join(", ", matches.Select(m => m.Path))}), use the full path",
				});
			}

			bool playedAsOneShot = usage.method == "PlayOneShot" || usage.method == "PlaySound" || usage.method == "PlaySoundEvent";
			if (playedAsOneShot && !sound.IsOneShot)
			{
				result.problems.Add(new Problem
				{
					severity = MessageType.Warning,
					usage = usage,
					soundPath = sound.Path,
					message = $"{where}: {NameOrPath(sound)} loops but is played as a one shot so it'll never stop" +
					          (usage.source == Source.Animation ? " (use StartLoop/StopLoop)" : " (use RAudio.Play/Stop)"),
				});
			}
		}

		SoundLibrary library = SoundEditorUtils.FindLibrary();
		if (library != null)
		{
			foreach (SoundSettings entry in library.sounds.ToList())
			{
				if (entry == null || SoundEditorUtils.Find(entry.sound) != null) continue;
				result.problems.Add(new Problem
				{
					severity = MessageType.Info,
					message = $"Sound Library has tweaks for {entry.sound.Path} which doesn't exist anymore",
					fixLabel = "Remove tweaks",
					fix = () =>
					{
						Undo.RecordObject(library, "Remove sound tweaks");
						library.sounds.Remove(entry);
						library.InvalidateLookup();
						EditorUtility.SetDirty(library);
						AssetDatabase.SaveAssetIfDirty(library);
					},
				});
			}
		}

		List<string> unused = SoundEditorUtils.Sounds.Where(s => result.UsagesOf(s.Path).Count == 0).Select(s => SoundEditorUtils.NameOf(s.Path)).ToList();
		if (unused.Count > 0)
		{
			result.problems.Add(new Problem
			{
				severity = MessageType.Info,
				message = $"Unused: {string.Join(", ", unused)}",
			});
		}

		result.problems.Sort((a, b) => Rank(a.severity).CompareTo(Rank(b.severity)));
	}

	private static string NameOrPath(EditorEventRef sound) => $"\"{SoundEditorUtils.NameOf(sound.Path)}\"";

	private static int Rank(MessageType type) => type == MessageType.Error ? 0 : type == MessageType.Warning ? 1 : 2;

	public static void Open(Usage usage)
	{
		UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(usage.assetPath);
		if (asset == null) return;

		if (usage.source == Source.Script)
		{
			AssetDatabase.OpenAsset(asset, usage.line);
			return;
		}

		EditorGUIUtility.PingObject(asset);
		Selection.activeObject = asset;
	}
}
