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
        private float[] resampleBuffer;

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

            var mode = inputMode;
            if ( inputMode > max )
            {
                mode = max;
            }
            encoder = SpeexCodex.Create(mode);

            var ratio = AudioUtils.GetFrequency(inputMode) / AudioUtils.GetFrequency(encoder.mode);
            int sizeRequired = ratio * encoder.dataSize;
            recordingBuffer = new float[sizeRequired];
            resampleBuffer = new float[encoder.dataSize];


            BeatSaberMultiplayer.Misc.Logger.Info("inputMode:" + inputMode + " encodingMode:" + encoder.mode + " ratio:" + ratio + " record:" + recordingBuffer.Length + " resample:" + resampleBuffer.Length);
            if ( encoder.mode == inputMode )
            {
                recordingBuffer = resampleBuffer;
            }

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

                            //Downsample if needed.
                            if (recordingBuffer != resampleBuffer)
                            {
                                AudioUtils.Downsample(recordingBuffer, resampleBuffer);
                            }

                            var data = encoder.Encode(resampleBuffer);
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