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
			}
			else{
				if (AudioManager.Instance != this) Destroy(gameObject);
			}
		}

		private void Start()
		{
			SceneManager.activeSceneChanged += (n, o) =>
			{
				DestroyInstances();
				Regenerate();
			};
			Regenerate();
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
			AudioBinding bind = m_bindings.Find(s => s.id == id);
			if (!bind.generateFromPath) PlayOneShot(bind.reference, Vector3.zero);
			else RuntimeManager.PlayOneShot(bind.path, Vector3.zero);
		}

		public void Play(string id)
		{
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
	}