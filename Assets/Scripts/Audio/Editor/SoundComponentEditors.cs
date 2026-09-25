using System.Collections.Generic;
using System.Linq;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// base for the sound component inspectors, draws the info foldout at the top
public abstract class SoundInspector : Editor
{
	protected abstract string HelpText { get; }

	protected SerializedProperty P(string name) => serializedObject.FindProperty(name);

	protected void Field(string name, string label = null)
	{
		if (label == null) EditorGUILayout.PropertyField(P(name), true);
		else EditorGUILayout.PropertyField(P(name), new GUIContent(label, P(name).tooltip), true);
	}

	protected static void Hint(string text) => EditorGUILayout.LabelField(text, SoundEditorUtils.WrappedMini);

	public sealed override void OnInspectorGUI()
	{
		serializedObject.Update();
		SoundEditorUtils.HowItWorks(GetType().Name, HelpText);
		DrawInspector();
		serializedObject.ApplyModifiedProperties();
	}

	protected abstract void DrawInspector();

	public override bool RequiresConstantRepaint() => Application.isPlaying;
}

// Sound Emitter

[CustomEditor(typeof(SoundEmitter)), CanEditMultipleObjects]
public class SoundEmitterEditor : SoundInspector
{
	protected override string HelpText =>
		"Plays an event from this object. Set Play When to Manually to call Play() from a UnityEvent or animation event instead.\n\nOne shots can overlap, loops only play once at a time.";

	protected override void DrawInspector()
	{
		EditorEventRef sound = SoundEditorUtils.SoundField(P("sound"));
		bool isLoop = sound != null && !sound.IsOneShot;

		EditorGUILayout.Space(4f);
		Field("playWhen");
		Field("stopWhen");
		if (sound != null && sound.IsOneShot) Hint("One shot - only matters if you want to cut it off early");

		SoundEmitter.PlayWhen play = (SoundEmitter.PlayWhen)P("playWhen").enumValueIndex;
		SoundEmitter.StopWhen stop = (SoundEmitter.StopWhen)P("stopWhen").enumValueIndex;

		bool usesTrigger = play == SoundEmitter.PlayWhen.SomethingEntersTrigger || play == SoundEmitter.PlayWhen.SomethingLeavesTrigger ||
		                   stop == SoundEmitter.StopWhen.EverythingLeavesTrigger;
		bool usesCollision = play == SoundEmitter.PlayWhen.SomethingCollides;

		if (usesTrigger || usesCollision)
		{
			SoundEditorUtils.TagField(P("onlyReactToTag"), new GUIContent("Only React To Tag", P("onlyReactToTag").tooltip));
			if (!serializedObject.isEditingMultipleObjects)
				SoundEditorUtils.RequireCollider2D((Component)target, usesTrigger, usesTrigger ? "A trigger" : "A collision");
		}

		EditorGUILayout.Space(4f);
		Field("position");
		if (sound != null && !sound.Is3D && P("position").enumValueIndex != (int)SoundEmitter.Position.NoPosition)
			Hint("Event is 2D so position doesn't do anything");

		Field("fadeOutWhenStopped");
		Field("playOnlyOnce");
		if (sound == null || isLoop) Field("restartIfAlreadyPlaying");

		if (isLoop && stop == SoundEmitter.StopWhen.Manually && play != SoundEmitter.PlayWhen.Manually)
			EditorGUILayout.HelpBox("Loop with Stop When set to Manually - needs something to call Stop()", MessageType.Warning);

		if (isLoop && (play == SoundEmitter.PlayWhen.ObjectDestroyed || play == SoundEmitter.PlayWhen.ObjectDisabled))
			EditorGUILayout.HelpBox("Nothing will stop this loop once the object is gone", MessageType.Warning);

		if (play == SoundEmitter.PlayWhen.ObjectDisabled && stop == SoundEmitter.StopWhen.ObjectDisabled && isLoop)
			EditorGUILayout.HelpBox("Play When and Stop When are both Object Disabled", MessageType.Warning);

		EditorGUILayout.Space(4f);
		SoundEditorUtils.ParameterList(P("parameters"), sound, new GUIContent("Parameters"));

		if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
		{
			SoundEmitter emitter = (SoundEmitter)target;
			EditorGUILayout.Space(6f);
			using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField(emitter.IsPlaying ? "Playing" : "Stopped", EditorStyles.boldLabel);
				if (GUILayout.Button("Play", GUILayout.Width(60f))) emitter.Play();
				if (GUILayout.Button("Stop", GUILayout.Width(60f))) emitter.Stop();
			}
		}
	}
}

// Sound Zone

[CustomEditor(typeof(SoundZone)), CanEditMultipleObjects]
public class SoundZoneEditor : SoundInspector
{
	protected override string HelpText =>
		"Needs a trigger collider. While the player is inside it plays the loop, turns on the snapshot and sets the global param (reset on exit).\n\nWorks with multiple players, stays active until the last one leaves.";

	protected override void DrawInspector()
	{
		SoundEditorUtils.TagField(P("onlyReactToTag"), new GUIContent("Only React To Tag", P("onlyReactToTag").tooltip));
		if (!serializedObject.isEditingMultipleObjects) SoundEditorUtils.RequireCollider2D((Component)target, true, "A Sound Zone");

		EditorEventRef loop = SoundEditorUtils.SoundField(P("loopWhileInside"));
		if (loop != null && loop.IsOneShot)
			EditorGUILayout.HelpBox("This event is a one shot so it won't loop (use Play On Enter instead?)", MessageType.Warning);
		if (loop != null) Field("loopPosition");

		EditorEventRef snapshot = SoundEditorUtils.SoundField(P("snapshotWhileInside"));
		if (snapshot != null && !snapshot.Path.StartsWith("snapshot:/"))
			EditorGUILayout.HelpBox("That's an event not a snapshot", MessageType.Error);

		EditorParamRef parameter = SoundEditorUtils.GlobalParameterField(P("globalParameter"), new GUIContent("Global Parameter", P("globalParameter").tooltip));
		if (!string.IsNullOrEmpty(P("globalParameter").stringValue))
		{
			EditorGUI.indentLevel++;
			SoundEditorUtils.ParameterValueField(P("valueInside"), new GUIContent("Value Inside"), parameter);
			SoundEditorUtils.ParameterValueField(P("valueOutside"), new GUIContent("Value Outside"), parameter);
			EditorGUI.indentLevel--;
		}

		EditorEventRef enter = SoundEditorUtils.SoundField(P("playOnEnter"));
		EditorEventRef exit = SoundEditorUtils.SoundField(P("playOnExit"));
		if ((enter != null && !enter.IsOneShot) || (exit != null && !exit.IsOneShot))
			EditorGUILayout.HelpBox("Loops in Play On Enter/Exit never stop, use Loop While Inside", MessageType.Warning);

		Field("fadeOut");
		Field("onEnter");
		Field("onExit");

		if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
			EditorGUILayout.HelpBox(((SoundZone)target).IsOccupied ? "Occupied" : "Empty", MessageType.None);
	}
}

// Level Audio

[CustomEditor(typeof(LevelAudio))]
public class LevelAudioEditor : SoundInspector
{
	protected override string HelpText =>
		"One per scene. Music/ambience fade out if the next scene uses a different track and carry over if it's the same one.\n\nScenes without Level Audio fade music out (can be changed in Sound Library settings).";

	protected override void DrawInspector()
	{
		LevelAudio levelAudio = (LevelAudio)target;

		if (levelAudio.UsesLegacySetup)
		{
			EditorGUILayout.HelpBox("Using the old tick boxes, these still work but can be upgraded", MessageType.Info);
			EditorGUI.indentLevel++;
			Field("playMusic", "Play \"Music\" (old)");
			Field("playUIMusic", "Play \"UI Music\" (old)");
			Field("playAmbience", "Play \"Ambience\" (old)");
			EditorGUI.indentLevel--;
			if (GUILayout.Button("Upgrade")) Upgrade();
			EditorGUILayout.Space(6f);
		}

		EditorEventRef music = SoundEditorUtils.SoundField(P("music"));
		if (music != null && music.IsOneShot) EditorGUILayout.HelpBox("Music event doesn't loop", MessageType.Warning);

		EditorEventRef ambience = SoundEditorUtils.SoundField(P("ambience"));
		if (ambience != null && ambience.IsOneShot) EditorGUILayout.HelpBox("Ambience event doesn't loop", MessageType.Warning);

		Field("restartIfAlreadyPlaying");
		Field("keepPreviousSceneAudioIfEmpty");

		SoundEditorUtils.ParameterList(P("musicParameters"), music, new GUIContent("Music Parameters"));

		if (levelAudio.gameObject.scene.IsValid() &&
		    FindObjectsByType<LevelAudio>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(l => l.gameObject.scene == levelAudio.gameObject.scene) > 1)
		{
			EditorGUILayout.HelpBox("More than one Level Audio in this scene", MessageType.Warning);
		}
	}

	private void Upgrade()
	{
		bool playMusic = P("playMusic").boolValue;
		bool playUIMusic = P("playUIMusic").boolValue;
		bool playAmbience = P("playAmbience").boolValue;

		if (playMusic && playUIMusic && !EditorUtility.DisplayDialog("Upgrade Level Audio",
			    "Music and UI Music are both ticked but there's only one music slot, UI Music will be kept", "Upgrade", "Cancel"))
			return;

		string musicName = playUIMusic ? LevelAudio.LegacyUIMusic : playMusic ? LevelAudio.LegacyMusic : null;
		if (!TryAssign("music", musicName) || !TryAssign("ambience", playAmbience ? LevelAudio.LegacyAmbience : null)) return;

		P("playMusic").boolValue = false;
		P("playUIMusic").boolValue = false;
		P("playAmbience").boolValue = false;
	}

	private bool TryAssign(string field, string soundName)
	{
		if (soundName == null) return true;

		EditorEventRef sound = SoundEditorUtils.FindByName(soundName, out _);
		if (sound == null)
		{
			EditorUtility.DisplayDialog("Upgrade Level Audio", $"Couldn't find an event called \"{soundName}\"", "OK");
			return false;
		}

		P(field).SetEventReference(sound.Guid, sound.Path);
		return true;
	}
}

// Footstep Sounds

[CustomEditor(typeof(FootstepSounds)), CanEditMultipleObjects]
public class FootstepSoundsEditor : SoundInspector
{
	protected override string HelpText =>
		"Call Footstep() from an animation event on the frames the feet land, or use Step Automatically.\n\nPut Sound Surface on ground objects then either add a sound per surface below, or use a labeled parameter on the default sound.";

	protected override void DrawInspector()
	{
		EditorEventRef defaultSound = SoundEditorUtils.SoundField(P("defaultSound"));

		List<EditorParamRef> labeled = defaultSound?.Parameters?.Where(p => p != null && !p.IsGlobal && p.Type == ParameterType.Labeled).ToList() ?? new List<EditorParamRef>();
		SerializedProperty surfaceParameter = P("surfaceParameter");

		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.PropertyField(surfaceParameter);
			using (new EditorGUI.DisabledScope(labeled.Count == 0))
			{
				if (GUILayout.Button("Pick", EditorStyles.miniButton, GUILayout.Width(40f)))
				{
					GenericMenu menu = new GenericMenu();
					menu.AddItem(new GUIContent("(none - use the list)"), string.IsNullOrEmpty(surfaceParameter.stringValue), () => SetString(surfaceParameter, ""));
					foreach (EditorParamRef parameter in labeled)
						menu.AddItem(new GUIContent(parameter.Name), parameter.Name == surfaceParameter.stringValue, () => SetString(surfaceParameter, parameter.Name));
					menu.ShowAsContext();
				}
			}
		}

		HashSet<string> knownSurfaces = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
		if (!string.IsNullOrEmpty(surfaceParameter.stringValue))
		{
			EditorParamRef parameter = labeled.Find(p => p.Name == surfaceParameter.stringValue);
			if (parameter == null && defaultSound != null)
				EditorGUILayout.HelpBox($"Default sound has no labeled parameter called \"{surfaceParameter.stringValue}\"", MessageType.Warning);
			else if (parameter != null)
			{
				Hint($"Surfaces: {string.Join(", ", parameter.Labels)}");
				knownSurfaces.UnionWith(parameter.Labels);
			}
		}
		else
		{
			Field("surfaceSounds");
			SerializedProperty list = P("surfaceSounds");
			for (int i = 0; i < list.arraySize; i++) knownSurfaces.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("surface").stringValue);
		}

		Field("feet");
		Field("groundCheckDistance");
		Field("groundLayers");
		Field("onlyWhenGrounded");
		Field("stepAutomatically");
		if (P("stepAutomatically").boolValue || P("stepAutomatically").hasMultipleDifferentValues)
		{
			EditorGUI.indentLevel++;
			Field("secondsBetweenSteps");
			Field("minimumSpeed");
			Field("body");
			EditorGUI.indentLevel--;
		}

		List<string> unmatched = FindObjectsByType<SoundSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)
			.Select(s => s.surface).Where(s => !string.IsNullOrEmpty(s) && !knownSurfaces.Contains(s)).Distinct().ToList();
		if (unmatched.Count > 0 && !serializedObject.isEditingMultipleObjects)
			EditorGUILayout.HelpBox($"Surfaces in the scene with no sound set (will use default): {string.Join(", ", unmatched)}", MessageType.Info);

		if (Application.isPlaying && !serializedObject.isEditingMultipleObjects)
		{
			FootstepSounds footsteps = (FootstepSounds)target;
			using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
			{
				EditorGUILayout.LabelField($"Last surface: {(string.IsNullOrEmpty(footsteps.CurrentSurface) ? "(default)" : footsteps.CurrentSurface)}");
				if (GUILayout.Button("Test step", GUILayout.Width(90f))) footsteps.Footstep();
			}
		}
	}

	private static void SetString(SerializedProperty property, string value)
	{
		property.serializedObject.Update();
		property.stringValue = value;
		property.serializedObject.ApplyModifiedProperties();
	}
}

// Sound Surface

[CustomEditor(typeof(SoundSurface)), CanEditMultipleObjects]
public class SoundSurfaceEditor : SoundInspector
{
	protected override string HelpText =>
		"Goes on the object with the ground collider (or a parent of it). Tilemaps need a separate tilemap per surface.";

	protected override void DrawInspector()
	{
		SerializedProperty surface = P("surface");
		using (new EditorGUILayout.HorizontalScope())
		{
			EditorGUILayout.PropertyField(surface);
			if (GUILayout.Button("Pick", EditorStyles.miniButton, GUILayout.Width(40f)))
			{
				GenericMenu menu = new GenericMenu();
				foreach (string name in KnownSurfaceNames())
				{
					menu.AddItem(new GUIContent(name), name == surface.stringValue, () =>
					{
						serializedObject.Update();
						surface.stringValue = name;
						serializedObject.ApplyModifiedProperties();
					});
				}
				if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No surfaces found"));
				menu.ShowAsContext();
			}
		}

		if (!serializedObject.isEditingMultipleObjects && ((Component)target).GetComponentInChildren<Collider2D>() == null)
			EditorGUILayout.HelpBox("No 2D collider on this object or its children", MessageType.Warning);
	}

	private static IEnumerable<string> KnownSurfaceNames()
	{
		HashSet<string> names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

		foreach (FootstepSounds footsteps in FindObjectsByType<FootstepSounds>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			SerializedObject serialized = new SerializedObject(footsteps);
			SerializedProperty list = serialized.FindProperty("surfaceSounds");
			for (int i = 0; i < list.arraySize; i++) names.Add(list.GetArrayElementAtIndex(i).FindPropertyRelative("surface").stringValue);

			string parameterName = serialized.FindProperty("surfaceParameter").stringValue;
			EditorEventRef sound = SoundEditorUtils.Find(serialized.FindProperty("defaultSound").GetEventReference());
			EditorParamRef parameter = sound?.Parameters?.Find(p => p != null && p.Name == parameterName);
			if (parameter != null) names.UnionWith(parameter.Labels);
		}

		// "Footsteps Wood" etc
		foreach (EditorEventRef sound in SoundEditorUtils.Sounds)
		{
			string name = SoundEditorUtils.NameOf(sound.Path);
			if (name.StartsWith("Footsteps ")) names.Add(name.Substring("Footsteps ".Length));
		}

		names.Remove("");
		return names.OrderBy(n => n);
	}
}

// Animation Sounds

[CustomEditor(typeof(AnimationSounds))]
public class AnimationSoundsEditor : SoundInspector
{
	private static readonly HashSet<string> Functions = new HashSet<string> { "PlaySound", "PlaySoundEvent", "StartLoop", "StopLoop", "StopAllLoops" };
	private bool m_showNames;

	protected override string HelpText =>
		"Has to be on the object with the Animator. Add an animation event, pick PlaySound / PlaySoundEvent / StartLoop / StopLoop and type the sound name in the String field.\n\nPlaySoundEvent ignores events from clips that are blending out, use it if sounds are doubling up.";

	protected override void DrawInspector()
	{
		Field("followThisObject");

		AnimationSounds component = (AnimationSounds)target;
		Animator animator = component.GetComponent<Animator>();

		if (animator == null)
		{
			EditorGUILayout.HelpBox("No Animator on this object, animation events won't reach this", MessageType.Warning);
		}
		else if (animator.runtimeAnimatorController != null)
		{
			EditorGUILayout.Space(6f);
			EditorGUILayout.LabelField("Animation events", EditorStyles.boldLabel);
			int count = 0;

			foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct())
			{
				foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
				{
					if (!Functions.Contains(animationEvent.functionName)) continue;
					count++;
					DrawEventRow(clip, animationEvent);
				}
			}

			if (count == 0) Hint("None");
		}

		EditorGUILayout.Space(4f);
		m_showNames = EditorGUILayout.Foldout(m_showNames, "Sound names", true);
		if (m_showNames)
		{
			foreach (EditorEventRef sound in SoundEditorUtils.Sounds)
			{
				using (new EditorGUILayout.HorizontalScope())
				{
					EditorGUILayout.LabelField(SoundEditorUtils.CodeName(sound));
					SoundEditorUtils.DrawBadges(sound, false);
					if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(44f))) EditorGUIUtility.systemCopyBuffer = SoundEditorUtils.CodeName(sound);
				}
			}
		}
	}

	private static void DrawEventRow(AnimationClip clip, AnimationEvent animationEvent)
	{
		string status = "OK";
		MessageType type = MessageType.None;

		if (animationEvent.functionName != "StopAllLoops")
		{
			EditorEventRef sound = SoundEditorUtils.FindByName(animationEvent.stringParameter, out _);
			if (sound == null)
			{
				string suggestion = SoundEditorUtils.SuggestName(animationEvent.stringParameter);
				status = string.IsNullOrEmpty(animationEvent.stringParameter)
					? "no sound name set"
					: $"no sound called \"{animationEvent.stringParameter}\"" + (suggestion != null ? $" - did you mean \"{suggestion}\"?" : "");
				type = MessageType.Error;
			}
			else if (!sound.IsOneShot && animationEvent.functionName.StartsWith("PlaySound"))
			{
				status = "event loops, use StartLoop/StopLoop";
				type = MessageType.Warning;
			}
		}

		string text = $"{clip.name} @ {animationEvent.time:0.00}s   {animationEvent.functionName}(\"{animationEvent.stringParameter}\")";
		if (type == MessageType.None) EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
		else EditorGUILayout.HelpBox($"{text}\n{status}", type);
	}
}

// UI Sounds

[CustomEditor(typeof(UISounds)), CanEditMultipleObjects]
public class UISoundsEditor : SoundInspector
{
	protected override string HelpText =>
		"Hover/click sounds, works with mouse and controller. If they don't play in the pause menu, the pause buses in Sound Library settings are pausing them.";

	protected override void DrawInspector()
	{
		SoundEditorUtils.SoundField(P("hover"));
		SoundEditorUtils.SoundField(P("click"));
		SoundEditorUtils.SoundField(P("clickWhileDisabled"));
		Field("skipHoverWhenMenuOpens");

		if (!serializedObject.isEditingMultipleObjects)
		{
			Component component = (Component)target;
			Graphic graphic = component.GetComponent<Graphic>();
			if (component.GetComponent<Selectable>() == null && (graphic == null || !graphic.raycastTarget))
				EditorGUILayout.HelpBox("Needs a Selectable or a raycast target graphic to get UI events", MessageType.Warning);
		}
	}
}

// Sound Listener 2D

[CustomEditor(typeof(SoundListener2D))]
public class SoundListener2DEditor : SoundInspector
{
	protected override string HelpText =>
		"Optional, the AudioManager already makes one on the main camera. Uses the camera's x/y but the level's z so 3D sounds aren't quieter because the camera is 10 units back.";

	protected override void DrawInspector()
	{
		Field("flattenDepth");
		if (P("flattenDepth").boolValue) Field("levelDepth");

		SoundListener2D listener = (SoundListener2D)target;
		List<StudioListener> others = FindObjectsByType<StudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
			.Where(l => l.GetComponentInParent<SoundListener2D>() == null).ToList();
		int listeners2D = FindObjectsByType<SoundListener2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Count(l => l.gameObject.scene == listener.gameObject.scene);

		if (others.Count > 0)
			EditorGUILayout.HelpBox($"There's also a Studio Listener on \"{others[0].name}\", should only have one", MessageType.Warning);
		if (listeners2D > 1)
			EditorGUILayout.HelpBox("More than one Sound Listener 2D in this scene", MessageType.Warning);
	}
}

// Audio Manager

[CustomEditor(typeof(AudioManager))]
public class AudioManagerEditor : SoundInspector
{
	protected override string HelpText =>
		"Doesn't need to be placed in scenes, it creates itself when the first sound plays. Settings are in Audio > Sound Library > Settings.";

	protected override void DrawInspector()
	{
		Field("banksToIgnore");

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Open Sound Library")) SoundLibraryWindow.Open(null);
			if (GUILayout.Button("Open Live Debugger")) SoundLibraryWindow.Open(null, SoundLibraryWindow.Tab.Live);
		}

		if (Application.isPlaying && target == AudioManager.Current)
			EditorGUILayout.HelpBox(((AudioManager)target).IsReady ? "Running" : "Waiting for banks to load", MessageType.None);
	}
}

// Sound Library

[CustomEditor(typeof(SoundLibrary))]
public class SoundLibraryEditor : Editor
{
	private bool m_showRaw;

	public override void OnInspectorGUI()
	{
		EditorGUILayout.HelpBox("Edit this through the Sound Library window", MessageType.Info);
		if (GUILayout.Button("Open Sound Library", GUILayout.Height(28f))) SoundLibraryWindow.Open(null);

		m_showRaw = EditorGUILayout.Foldout(m_showRaw, "Raw data", true);
		if (m_showRaw) DrawDefaultInspector();
	}
}
