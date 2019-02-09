using NSpeex;
using System;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/VoipListener.cs
    public class VoipListener : VoipBehaviour
    {
        private string lastDevice;
        private AudioClip recording;
        private float[] recordingBuffer;

        private SpeexCodex encoder;

        private int lastPos = 0;
        private int index;

        public event Action<VoipFragment> OnAudioGenerated;

        [Range(0.001f,1f)]
        public float minAmplitude = 0.001f;

        public BandMode max = BandMode.Wide;
        public BandMode inputMode;

        public bool IsListening
        {
            get { return recording != null; }
        }

        void Awake()
        {
           
        }

        void Setup()
        {
            if (Microphone.devices.Length == 0) return;

            inputMode = BandMode.Wide;
            encoder = SpeexCodex.Create(inputMode);

            recordingBuffer = new float[encoder.dataSize];
            
            lastDevice = Config.Instance.InputDevice;
            if (!Microphone.devices.Contains(lastDevice))
            {
                lastDevice = Microphone.devices.First();
                Config.Instance.InputDevice = lastDevice;
            }

            recording = Microphone.Start(lastDevice, true, 10, AudioUtils.GetFrequency(inputMode));
        }

        public void ChangeDevice(string newDevice)
        {
            Microphone.End(lastDevice);

            lastDevice = newDevice;
            if (!Microphone.devices.Contains(lastDevice))
            {
                lastDevice = Microphone.devices.First();
                Config.Instance.InputDevice = lastDevice;
            }
            recording = Microphone.Start(lastDevice, true, 10, AudioUtils.GetFrequency(inputMode));
        }

        void Update()
        {
            if ( recording == null )
            {
                Setup();
                return;
            }

            var now = Microphone.GetPosition(null);
            var length = now - lastPos;

            if (now < lastPos)
            {
                lastPos = 0;
                length = now;
            }
            
            while (length >= recordingBuffer.Length)
            {
                if (recording.GetData(recordingBuffer, lastPos))
                {
                    //Send..
                    var amplitude = AudioUtils.GetMaxAmplitude(recordingBuffer);
                    if (amplitude >= minAmplitude )
                    {
                        index++;
                        if (OnAudioGenerated != null )
                        {
                            chunkCount++;
                            
                            var data = encoder.Encode(recordingBuffer);
                            bytes += data.Length + 11;
                            OnAudioGenerated( new VoipFragment(0, index, data, encoder.mode) );
                        }
                    }
                }
                length -= recordingBuffer.Length;
                lastPos += recordingBuffer.Length;
            }

            UpdateStats();
            
        }
    }
}