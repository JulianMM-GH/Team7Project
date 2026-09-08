using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.SceneManagement;

	/// <summary>
	/// Global audio engine
	/// </summary>
	public static class RAudio
	{
		public static void PlayOneShot(string ID) => AudioManager.Manager.PlayOneShot(ID);
		public static void Play(string ID) => AudioManager.Manager.Play(ID);
		public static void Stop(string ID) => AudioManager.Manager.Stop(ID);
		public static void Stop(string ID, FMOD.Studio.STOP_MODE mode) => AudioManager.Manager.Stop(ID, mode);
		public static void StopAllFromBanks(FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.IMMEDIATE, params string[] banks) => AudioManager.Manager.StopAllFromBanks(mode, banks);
        
		public static bool IsPlaying(string ID) => AudioManager.Manager.IsPlaying(ID);

		//Param settings for local parameters

		public static void SetLabeledParam(string ID, string Parameter, string value)
		{
			AudioManager.Manager.SetLabeledParam(ID, Parameter, value);
		}
		public static void SetValueParam(string ID, string Parameter, float value)
		{
			AudioManager.Manager.SetValueParam(ID, Parameter, value);
		}

		//Param settings for global parameters
        
		public static void GSetLabeledParam(string Parameter, string value)
		{
			AudioManager.Manager.GSetLabeledParam(Parameter, value);
		}
		public static void GSetValueParam(string Parameter, float value)
		{
			AudioManager.Manager.GSetValueParam(Parameter, value);
		}
		public static float GGetValueParam(string Parameter)
		{
			return AudioManager.Manager.GGetParam(Parameter);
		}

		// Bus volume (options menu sliders) - persists across scenes and sessions via PlayerPrefs

		public static void SetMasterVolume(float level) => AudioManager.Manager.SetMasterVolume(level);
		public static void SetSFXVolume(float level) => AudioManager.Manager.SetSFXVolume(level);
		public static void SetMusicVolume(float level) => AudioManager.Manager.SetMusicVolume(level);
		public static void SetAmbienceVolume(float level) => AudioManager.Manager.SetAmbienceVolume(level);

		public static float GetMasterVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Master, 1f);
		public static float GetSFXVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.SFX, 1f);
		public static float GetMusicVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Music, 1f);
		public static float GetAmbienceVolume() => PlayerPrefs.GetFloat(SONIC_VOLUME_PREFS.Ambience, 1f);

		// Pauses/resumes every FMOD sound in one call (routes through the master bus) - used when the game is paused
		public static void SetGlobalPause(bool paused) => AudioManager.Manager.SetGlobalPause(paused);
	}

	/// <summary>
	/// FMOD mixer bus paths for the options menu volume sliders
	/// </summary>
	public static class SONIC_AUDIO_BUS
	{
		public const string Master = "bus:/";
		public const string SFX = "bus:/SFX";
		public const string Music = "bus:/MSC";
		public const string Ambience = "bus:/AMBNC";
	}

	/// <summary>
	/// PlayerPrefs keys the saved slider volumes are stored under
	/// </summary>
	public static class SONIC_VOLUME_PREFS
	{
		public const string Master = "Volume_Master";
		public const string SFX = "Volume_SFX";
		public const string Music = "Volume_Music";
		public const string Ambience = "Volume_Ambience";
	}
    
	[Serializable]
	public class AudioBinding
	{
		public string id;
		public EventReference reference;

		public string path;
		public string bank;
		[HideInInspector] public bool generateFromPath;
	}
    
	[Serializable]
	public class EventInstanceBinding
	{
		public string id;
		public EventInstance Inst;
	}
	
	/// <summary>
	/// Contains common use audio banks. I mostly use these when I want to stop all audio on a dime
	/// </summary>
	public class SONIC_AUDIO_BANK{
		public readonly static string SFX = "Bank_SFX";
	}
    
	public class AudioManager : MonoBehaviour
	{
		private static AudioManager Instance { get; set; }
		public static AudioManager Manager
		{
			get
			{
				if (AudioManager.Instance != null) return AudioManager.Instance;
				else
				{
					UnityEngine.Debug.Log("HZ Audio - Audio Manager is not set up. Cannot play FMOD audio");
					return null;
				}
			}
		}

		[FMODUnity.BankRef]
		public string[] banksToIgnore;

		private List<AudioBinding> m_bindings;
		private List<EventInstanceBinding> m_eventBindings;
		private bool m_pendingInit;

        void Regenerate()
        {
            m_bindings = new List<AudioBinding>();
            RuntimeManager.StudioSystem.getBankList(out Bank[] loadedBanks);
            Array.ForEach(loadedBanks, bank =>
            {
                bank.getPath(out string bankPath);
                if (!banksToIgnore.Contains(bankPath.Split("/")[^1]))
                {
                    bank.getEventList(out EventDescription[] desc);
                    for (int i = 0; i < desc.Length; i++)
                    {
                        AudioBinding newBinding = new AudioBinding();
                        newBinding.generateFromPath = true;
                        desc[i].getPath(out string path);
                        newBinding.path = path;
                        newBinding.bank = bankPath.Split("/")[^1];
                        newBinding.id = path.Split("/")[^1];
                        m_bindings.Add(newBinding);
                    }
                }
            });
            GenerateInstances();
        }

        private void Awake()
		{
			if (AudioManager.Instance == null){
				transform.SetParent(null);
				AudioManager.Instance = this;
				DontDestroyOnLoad(this);

				// Bindings are generated here (not Start) so they exist before any
				// other object's Start() runs and calls RAudio.Play() this frame -
				// Unity doesn't guarantee Start() order across GameObjects.
				SceneManager.activeSceneChanged += (n, o) =>
				{
					DestroyInstances();
					Regenerate();
				};
				InitializeAudio();
			}
			else{
				if (AudioManager.Instance != this) Destroy(gameObject);
			}
		}

		// FMOD's master banks are loaded synchronously, but if this Awake() runs
		// before RuntimeManager has actually indexed them (RuntimeManager.
		// HaveMasterBanksLoaded can read true before StudioSystem.getBankList()
		// has anything to return), GetBus()/getBankList() come back empty and
		// throw, aborting Awake() before Regenerate() runs - leaving
		// m_eventBindings null for the rest of the session. Retry on Update()
		// instead of letting that happen.
		private void InitializeAudio()
		{
			try
			{
				ApplySavedVolumes();
				Regenerate();
				m_pendingInit = false;
			}
			catch (Exception)
			{
				m_pendingInit = true;
			}
		}

		private void Update()
		{
			if (m_pendingInit)
			{
				InitializeAudio();
			}
		}

		private void OnDestroy()
		{
			DestroyInstances();
		}

		void DestroyInstances()
		{
			if (m_eventBindings == null) return;
            
			foreach (EventInstanceBinding binding in m_eventBindings)
			{
				binding.Inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
				binding.Inst.release();
			}
		}
		
		public bool IsPlaying(string id)
		{
			m_eventBindings.Find(b => b.id == id).Inst.getPlaybackState(out var thing);
			//print($"FMOD Audio Playback state for {id}: {thing}");
			return thing != PLAYBACK_STATE.STOPPED;
		}
        
		//Set Events

		public void SetLabeledParam(string ID, string Parameter, string value)
		{
			m_eventBindings.Find(s => s.id == ID).Inst.setParameterByNameWithLabel(Parameter, value);
		}
		public void GSetLabeledParam(string Parameter, string value)
		{
			RuntimeManager.StudioSystem.setParameterByNameWithLabel(Parameter, value);
		}
		//----------------------//
		public void SetValueParam(string ID, string Parameter, float value)
		{
			m_eventBindings.Find(s => s.id == ID).Inst.setParameterByName(Parameter, value);
		}
		public void GSetValueParam(string Parameter, float value)
		{
			RuntimeManager.StudioSystem.setParameterByName(Parameter, value);
		}
        
		//Get Events
		public float GetParam(string ID, string Parameter)
		{
			float f = 0;
			m_eventBindings.Find(s => s.id == ID).Inst.getParameterByName(Parameter, out f);
			return f;
		}	    
	    
		public float GGetParam(string Parameter)
		{
			float f = 0;
			RuntimeManager.StudioSystem.getParameterByName(Parameter, out f);
			return f;
		}

		private void GenerateInstances()
		{
			m_eventBindings = new List<EventInstanceBinding>();
            
			foreach (var bind in m_bindings)
			{
				var eventInst = bind.generateFromPath ? RuntimeManager.CreateInstance(bind.path) : RuntimeManager.CreateInstance(bind.reference);
				m_eventBindings.Add(new EventInstanceBinding
				{
					Inst = eventInst,
					id = bind.id
                });
			}
		}

		// ReSharper disable Unity.PerformanceAnalysis
		public void PlayOneShot(string id)
		{
			if (m_bindings == null)
			{
				Debug.LogWarning($"AudioManager not ready yet, dropped PlayOneShot(\"{id}\")");
				return;
			}

			AudioBinding bind = m_bindings.Find(s => s.id == id);
			if (bind == null)
			{
				Debug.LogWarning("Sound effect was not found in the bank");
				return;
			}

			if (!bind.generateFromPath) PlayOneShot(bind.reference, Vector3.zero);
			else RuntimeManager.PlayOneShot(bind.path, Vector3.zero);
		}

		public void Play(string id)
		{
			if (m_eventBindings == null)
			{
				Debug.LogWarning($"AudioManager not ready yet, dropped Play(\"{id}\")");
				return;
			}

			EventInstanceBinding EIB = m_eventBindings.Find(b => b.id == id);
			if(EIB == null)
            {
				Debug.LogWarning("Sound effect was not found in the bank");
            }
            else
			{
				EIB.Inst.start();
			}
		}
        
		public void Stop(string id, FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.IMMEDIATE)
		{
			m_eventBindings.Find(b => b.id == id).Inst.stop(mode);
		}
		
		public void StopAllFromBanks(FMOD.Studio.STOP_MODE mode = FMOD.Studio.STOP_MODE.IMMEDIATE, params string[] banks)
		{
			foreach(string bank in banks){
				m_bindings.ForEach((b) => {
					if (banks.ToList().Contains(b.bank)) {
						m_eventBindings.Find(s=>s.id == b.id).Inst.stop(mode);
					}
				});
			}
		}

		private void PlayOneShot(EventReference sound, Vector3 pos)
		{
			RuntimeManager.PlayOneShot(sound, pos);
		}

		// Bus volume (options menu sliders)

		public void SetMasterVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Master, SONIC_VOLUME_PREFS.Master, level);
		public void SetSFXVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.SFX, SONIC_VOLUME_PREFS.SFX, level);
		public void SetMusicVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Music, SONIC_VOLUME_PREFS.Music, level);
		public void SetAmbienceVolume(float level) => SetBusVolume(SONIC_AUDIO_BUS.Ambience, SONIC_VOLUME_PREFS.Ambience, level);

		private void SetBusVolume(string busPath, string prefKey, float level)
		{
			ResolveBus(busPath).setVolume(level);
			PlayerPrefs.SetFloat(prefKey, level);
			PlayerPrefs.Save();
		}

		// Re-applies the saved slider levels to the FMOD buses - called once when this singleton is created,
		// so the saved settings take effect immediately on boot rather than only once a slider is next touched.
		private void ApplySavedVolumes()
		{
			ResolveBus(SONIC_AUDIO_BUS.Master).setVolume(RAudio.GetMasterVolume());
			ResolveBus(SONIC_AUDIO_BUS.SFX).setVolume(RAudio.GetSFXVolume());
			ResolveBus(SONIC_AUDIO_BUS.Music).setVolume(RAudio.GetMusicVolume());
			ResolveBus(SONIC_AUDIO_BUS.Ambience).setVolume(RAudio.GetAmbienceVolume());
		}

		public void SetGlobalPause(bool paused)
		{
			ResolveBus(SONIC_AUDIO_BUS.Master).setPaused(paused);
		}

		private Dictionary<string, Bus> m_busesByPath;

		// RuntimeManager.GetBus(path) does a string lookup through FMOD's internal
		// hash table, which has proven unreliable here even for buses that are
		// definitely compiled into the bank (confirmed by inspecting the raw
		// strings bank data). Walking each loaded bank's own bus list and
		// indexing by path sidesteps that lookup entirely.
		private int m_diagLogsRemaining = 3;

		private Bus ResolveBus(string busPath)
		{
			// Don't permanently cache an empty result - if the bank scan came up
			// empty because banks weren't enumerable yet, retry it next call
			// instead of falling back to the broken GetBus() lookup forever.
			if (m_busesByPath == null || m_busesByPath.Count == 0)
			{
				bool logThisAttempt = m_diagLogsRemaining > 0;
				if (logThisAttempt) m_diagLogsRemaining--;

				m_busesByPath = new Dictionary<string, Bus>();
				FMOD.RESULT bankListResult = RuntimeManager.StudioSystem.getBankList(out Bank[] banks);
				if (logThisAttempt) Debug.Log($"AudioManager: getBankList -> {bankListResult}, {banks?.Length ?? 0} bank(s)");

				foreach (Bank bank in banks)
				{
					bank.getPath(out string bankPath);
					FMOD.RESULT busListResult = bank.getBusList(out Bus[] buses);
					if (logThisAttempt) Debug.Log($"AudioManager: bank '{bankPath}' getBusList -> {busListResult}, {buses?.Length ?? 0} bus(es)");
					if (busListResult != FMOD.RESULT.OK) continue;

					foreach (Bus bus in buses)
					{
						FMOD.RESULT pathResult = bus.getPath(out string path);
						if (logThisAttempt) Debug.Log($"AudioManager:   bus.getPath -> {pathResult}, path='{path}'");
						if (pathResult != FMOD.RESULT.OK) continue;

						// FMOD's root/master bus sometimes reports an empty path
						// rather than "bus:/" - normalize so it's still keyed
						// the same way SONIC_AUDIO_BUS.Master looks it up.
						if (string.IsNullOrEmpty(path)) path = SONIC_AUDIO_BUS.Master;
						m_busesByPath[path] = bus;
					}
				}

				if (logThisAttempt) Debug.Log($"AudioManager: resolved buses [{string.Join(", ", m_busesByPath.Keys)}]");
			}

			if (m_busesByPath.TryGetValue(busPath, out Bus found)) return found;

			// Fall back for anything the bank scan didn't turn up.
			return RuntimeManager.GetBus(busPath);
		}
	}