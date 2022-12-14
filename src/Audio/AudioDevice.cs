using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MoonWorks.Audio
{
	public class AudioDevice : IDisposable
	{
		public IntPtr Handle { get; }
		public byte[] Handle3D { get; }
		public IntPtr MasteringVoice { get; }
		public FAudio.FAudioDeviceDetails DeviceDetails { get; }
		public IntPtr ReverbVoice { get; }

		public float CurveDistanceScalar = 1f;
		public float DopplerScale = 1f;
		public float SpeedOfSound = 343.5f;

		private float masteringVolume = 1f;
		public float MasteringVolume
		{
			get => masteringVolume;
			set
			{
				masteringVolume = value;
				FAudio.FAudioVoice_SetVolume(MasteringVoice, masteringVolume, 0);
			}
		}

		internal FAudio.FAudioVoiceSends ReverbSends;

		private readonly List<WeakReference<AudioResource>> resources = new List<WeakReference<AudioResource>>();
		private readonly List<WeakReference<StreamingSound>> streamingSounds = new List<WeakReference<StreamingSound>>();

		private bool IsDisposed;

		public unsafe AudioDevice()
		{
			FAudio.FAudioCreate(out var handle, 0, FAudio.FAUDIO_DEFAULT_PROCESSOR);
			Handle = handle;

			/* Find a suitable device */

			FAudio.FAudio_GetDeviceCount(Handle, out var devices);

			if (devices == 0)
			{
				Logger.LogError("No audio devices found!");
				Handle = IntPtr.Zero;
				FAudio.FAudio_Release(Handle);
				return;
			}

			FAudio.FAudioDeviceDetails deviceDetails;

			uint i = 0;
			for (i = 0; i < devices; i++)
			{
				FAudio.FAudio_GetDeviceDetails(
					Handle,
					i,
					out deviceDetails
				);
				if ((deviceDetails.Role & FAudio.FAudioDeviceRole.FAudioDefaultGameDevice) == FAudio.FAudioDeviceRole.FAudioDefaultGameDevice)
				{
					DeviceDetails = deviceDetails;
					break;
				}
			}

			if (i == devices)
			{
				i = 0; /* whatever we'll just use the first one I guess */
				FAudio.FAudio_GetDeviceDetails(
					Handle,
					i,
					out deviceDetails
				);
				DeviceDetails = deviceDetails;
			}

			/* Init Mastering Voice */
			IntPtr masteringVoice;

			if (FAudio.FAudio_CreateMasteringVoice(
				Handle,
				out masteringVoice,
				FAudio.FAUDIO_DEFAULT_CHANNELS,
				FAudio.FAUDIO_DEFAULT_SAMPLERATE,
				0,
				i,
				IntPtr.Zero
			) != 0)
			{
				Logger.LogError("No mastering voice found!");
				Handle = IntPtr.Zero;
				FAudio.FAudio_Release(Handle);
				return;
			}

			MasteringVoice = masteringVoice;

			/* Init 3D Audio */

			Handle3D = new byte[FAudio.F3DAUDIO_HANDLE_BYTESIZE];
			FAudio.F3DAudioInitialize(
				DeviceDetails.OutputFormat.dwChannelMask,
				SpeedOfSound,
				Handle3D
			);

			/* Init reverb */

			IntPtr reverbVoice;

			IntPtr reverb;
			FAudio.FAudioCreateReverb(out reverb, 0);

			IntPtr chainPtr;
			chainPtr = Marshal.AllocHGlobal(
				sizeof(FAudio.FAudioEffectChain)
			);

			FAudio.FAudioEffectChain* reverbChain = (FAudio.FAudioEffectChain*) chainPtr;
			reverbChain->EffectCount = 1;
			reverbChain->pEffectDescriptors = Marshal.AllocHGlobal(
				sizeof(FAudio.FAudioEffectDescriptor)
			);

			FAudio.FAudioEffectDescriptor* reverbDescriptor =
				(FAudio.FAudioEffectDescriptor*) reverbChain->pEffectDescriptors;

			reverbDescriptor->InitialState = 1;
			reverbDescriptor->OutputChannels = (uint) (
				(DeviceDetails.OutputFormat.Format.nChannels == 6) ? 6 : 1
			);
			reverbDescriptor->pEffect = reverb;

			FAudio.FAudio_CreateSubmixVoice(
				Handle,
				out reverbVoice,
				1, /* omnidirectional reverb */
				DeviceDetails.OutputFormat.Format.nSamplesPerSec,
				0,
				0,
				IntPtr.Zero,
				chainPtr
			);
			FAudio.FAPOBase_Release(reverb);

			Marshal.FreeHGlobal(reverbChain->pEffectDescriptors);
			Marshal.FreeHGlobal(chainPtr);

			ReverbVoice = reverbVoice;

			/* Init reverb params */
			// Defaults based on FAUDIOFX_I3DL2_PRESET_GENERIC

			IntPtr reverbParamsPtr = Marshal.AllocHGlobal(
				sizeof(FAudio.FAudioFXReverbParameters)
			);

			FAudio.FAudioFXReverbParameters* reverbParams = (FAudio.FAudioFXReverbParameters*) reverbParamsPtr;
			reverbParams->WetDryMix = 100.0f;
			reverbParams->ReflectionsDelay = 7;
			reverbParams->ReverbDelay = 11;
			reverbParams->RearDelay = FAudio.FAUDIOFX_REVERB_DEFAULT_REAR_DELAY;
			reverbParams->PositionLeft = FAudio.FAUDIOFX_REVERB_DEFAULT_POSITION;
			reverbParams->PositionRight = FAudio.FAUDIOFX_REVERB_DEFAULT_POSITION;
			reverbParams->PositionMatrixLeft = FAudio.FAUDIOFX_REVERB_DEFAULT_POSITION_MATRIX;
			reverbParams->PositionMatrixRight = FAudio.FAUDIOFX_REVERB_DEFAULT_POSITION_MATRIX;
			reverbParams->EarlyDiffusion = 15;
			reverbParams->LateDiffusion = 15;
			reverbParams->LowEQGain = 8;
			reverbParams->LowEQCutoff = 4;
			reverbParams->HighEQGain = 8;
			reverbParams->HighEQCutoff = 6;
			reverbParams->RoomFilterFreq = 5000f;
			reverbParams->RoomFilterMain = -10f;
			reverbParams->RoomFilterHF = -1f;
			reverbParams->ReflectionsGain = -26.0200005f;
			reverbParams->ReverbGain = 10.0f;
			reverbParams->DecayTime = 1.49000001f;
			reverbParams->Density = 100.0f;
			reverbParams->RoomSize = FAudio.FAUDIOFX_REVERB_DEFAULT_ROOM_SIZE;
			FAudio.FAudioVoice_SetEffectParameters(
				ReverbVoice,
				0,
				reverbParamsPtr,
				(uint) sizeof(FAudio.FAudioFXReverbParameters),
				0
			);
			Marshal.FreeHGlobal(reverbParamsPtr);

			/* Init reverb sends */

			ReverbSends = new FAudio.FAudioVoiceSends
			{
				SendCount = 2,
				pSends = Marshal.AllocHGlobal(
					2 * sizeof(FAudio.FAudioSendDescriptor)
				)
			};
			FAudio.FAudioSendDescriptor* sendDesc = (FAudio.FAudioSendDescriptor*) ReverbSends.pSends;
			sendDesc[0].Flags = 0;
			sendDesc[0].pOutputVoice = MasteringVoice;
			sendDesc[1].Flags = 0;
			sendDesc[1].pOutputVoice = ReverbVoice;
		}

		internal void Update()
		{
			for (var i = streamingSounds.Count - 1; i >= 0; i--)
			{
				var weakReference = streamingSounds[i];
				if (weakReference.TryGetTarget(out var streamingSound))
				{
					streamingSound.Update();
				}
				else
				{
					streamingSounds.RemoveAt(i);
				}
			}
		}

		internal void AddDynamicSoundInstance(StreamingSound instance)
		{
			streamingSounds.Add(new WeakReference<StreamingSound>(instance));
		}

		internal void AddResourceReference(WeakReference<AudioResource> resourceReference)
		{
			lock (resources)
			{
				resources.Add(resourceReference);
			}
		}

		internal void RemoveResourceReference(WeakReference<AudioResource> resourceReference)
		{
			lock (resources)
			{
				resources.Remove(resourceReference);
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!IsDisposed)
			{
				if (disposing)
				{
					for (var i = resources.Count - 1; i >= 0; i--)
					{
						var weakReference = resources[i];

						if (weakReference.TryGetTarget(out var resource))
						{
							resource.Dispose();
						}
					}
					resources.Clear();
				}

				FAudio.FAudioVoice_DestroyVoice(ReverbVoice);
				FAudio.FAudioVoice_DestroyVoice(MasteringVoice);
				FAudio.FAudio_Release(Handle);

				IsDisposed = true;
			}
		}

		// TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		~AudioDevice()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: false);
		}

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
