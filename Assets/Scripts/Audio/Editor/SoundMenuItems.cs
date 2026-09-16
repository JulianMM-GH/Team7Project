using FMODUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// hierarchy right click > Audio (FMOD) + some Audio menu items
public static class SoundMenuItems
{
	[MenuItem("GameObject/Audio (FMOD)/Sound Emitter", false, 10)]
	private static void CreateEmitter(MenuCommand command)
	{
		GameObject go = CreateObject("Sound Emitter", command);
		Undo.AddComponent<SoundEmitter>(go);
	}

	[MenuItem("GameObject/Audio (FMOD)/Sound Zone", false, 11)]
	private static void CreateZone(MenuCommand command)
	{
		GameObject go = CreateObject("Sound Zone", command);
		BoxCollider2D area = Undo.AddComponent<BoxCollider2D>(go);
		area.isTrigger = true;
		area.size = new Vector2(6f, 4f);
		Undo.AddComponent<SoundZone>(go);
	}

	[MenuItem("GameObject/Audio (FMOD)/Level Audio (music and ambience)", false, 12)]
	private static void CreateLevelAudio(MenuCommand command)
	{
		LevelAudio existing = Object.FindAnyObjectByType<LevelAudio>(FindObjectsInactive.Include);
		if (existing != null)
		{
			EditorUtility.DisplayDialog("Level Audio", $"Scene already has one on \"{existing.name}\"", "OK");
			Selection.activeGameObject = existing.gameObject;
			EditorGUIUtility.PingObject(existing);
			return;
		}

		GameObject go = CreateObject("Level Audio", command);
		Undo.AddComponent<LevelAudio>(go);
	}

	[MenuItem("Audio/Add Sound Listener 2D to Main Camera", priority = 40)]
	public static void AddListenerToMainCamera()
	{
		Camera camera = Camera.main;
		if (camera == null)
		{
			EditorUtility.DisplayDialog("Sound Listener 2D", "No camera tagged MainCamera", "OK");
			return;
		}

		SoundListener2D listener = camera.GetComponent<SoundListener2D>();
		if (listener == null) listener = Undo.AddComponent<SoundListener2D>(camera.gameObject);

		StudioListener fmodListener = camera.GetComponent<StudioListener>();
		if (fmodListener != null && EditorUtility.DisplayDialog("Sound Listener 2D", "Camera also has a Studio Listener, remove it?", "Remove", "Keep"))
		{
			Undo.DestroyObjectImmediate(fmodListener);
		}

		Selection.activeGameObject = camera.gameObject;
		EditorGUIUtility.PingObject(listener);
	}

	[MenuItem("Audio/Open FMOD Studio Project", priority = 41)]
	private static void OpenFmodProject() => SoundEditorUtils.OpenFmodProject();

	private static GameObject CreateObject(string name, MenuCommand command)
	{
		GameObject go = new GameObject(name);
		GameObject parent = command.context as GameObject;
		GameObjectUtility.SetParentAndAlign(go, parent);

		if (parent == null && SceneView.lastActiveSceneView != null)
		{
			Vector3 centre = SceneView.lastActiveSceneView.pivot;
			centre.z = 0f;
			go.transform.position = centre;
		}

		Undo.RegisterCreatedObjectUndo(go, "Create " + name);
		Selection.activeGameObject = go;
		return go;
	}

	// used by the Sound Library "Add to scene" menu

	public static void AddEmitter(GameObject target, EditorEventRef sound)
	{
		SoundEmitter emitter = Undo.AddComponent<SoundEmitter>(target);
		SerializedObject serialized = new SerializedObject(emitter);
		serialized.FindProperty("sound").SetEventReference(sound.Guid, sound.Path);
		serialized.FindProperty("playWhen").enumValueIndex = (int)(sound.IsOneShot ? SoundEmitter.PlayWhen.Manually : SoundEmitter.PlayWhen.SceneStarts);
		serialized.ApplyModifiedProperties();
		Selection.activeGameObject = target;
	}

	public static void AddZone(GameObject target, EditorEventRef sound)
	{
		if (target.GetComponent<Collider2D>() == null)
		{
			BoxCollider2D area = Undo.AddComponent<BoxCollider2D>(target);
			area.isTrigger = true;
		}

		SoundZone zone = target.GetComponent<SoundZone>();
		if (zone == null) zone = Undo.AddComponent<SoundZone>(target);

		SerializedObject serialized = new SerializedObject(zone);
		serialized.FindProperty("loopWhileInside").SetEventReference(sound.Guid, sound.Path);
		serialized.ApplyModifiedProperties();
		Selection.activeGameObject = target;
	}

	// field is "music" or "ambience"
	public static void SetLevelAudio(EditorEventRef sound, string field)
	{
		LevelAudio levelAudio = Object.FindAnyObjectByType<LevelAudio>(FindObjectsInactive.Include);
		if (levelAudio == null)
		{
			GameObject go = new GameObject("Level Audio");
			Undo.RegisterCreatedObjectUndo(go, "Create Level Audio");
			levelAudio = Undo.AddComponent<LevelAudio>(go);
		}

		SerializedObject serialized = new SerializedObject(levelAudio);
		serialized.FindProperty(field).SetEventReference(sound.Guid, sound.Path);
		serialized.ApplyModifiedProperties();

		EditorSceneManager.MarkSceneDirty(levelAudio.gameObject.scene);
		Selection.activeGameObject = levelAudio.gameObject;
	}
}
