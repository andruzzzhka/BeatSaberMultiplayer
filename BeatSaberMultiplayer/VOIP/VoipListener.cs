using NSpeex;
using System;
using System.Linq;
using UnityEngine;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/VoipListener.cs
    public class VoipListener : MonoBehaviour
    {

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
        public int inputFreq;

        public bool isListening
        {
            get
            {
                return _isListening;
            }
            set
            {
                if (!_isListening && value)
                {
                    index += 3;
                    lastPos = Math.Max(Microphone.GetPosition(null) - recordingBuffer.Length, 0);
                }
                _isListening = value;
            }
        }

        private bool _isListening;

        void Awake()
        {
           
        }

        public void StartRecording()
        {
            if (Microphone.devices.Length == 0) return;

            inputFreq = AudioUtils.GetFreqForMic();
            
            encoder = SpeexCodex.Create(BandMode.Wide);

            var ratio = inputFreq / (float)AudioUtils.GetFrequency(encoder.mode);
            int sizeRequired = (int)(ratio * encoder.dataSize);
            recordingBuffer = new float[sizeRequired];
            resampleBuffer = new float[encoder.dataSize];
            
            if (AudioUtils.GetFrequency(encoder.mode) == inputFreq)
            {
                recordingBuffer = resampleBuffer;
            }            

            recording = Microphone.Start(null, true, 20, inputFreq);
            Plugin.log.Info("Used mic sample rate: "+inputFreq+"Hz");
        }

        public void StopRecording()
        {
            Microphone.End(null);
            Destroy(recording);
            recording = null;
        }

        void Update()
        {
            if ( recording == null)
            {
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
                if (_isListening && recording.GetData(recordingBuffer, lastPos))
                {
                    //Send..
                    var amplitude = AudioUtils.GetMaxAmplitude(recordingBuffer);
                    if (amplitude >= minAmplitude )
                    {
                        index++;
                        if (OnAudioGenerated != null )
                        {
                            //Downsample if needed.
                            if (recordingBuffer != resampleBuffer)
                            {
                                AudioUtils.Resample(recordingBuffer, resampleBuffer, inputFreq, AudioUtils.GetFrequency(encoder.mode));
                            }

                            var data = encoder.Encode(resampleBuffer);

                            VoipFragment frag = new VoipFragment(0, index, data, encoder.mode);

                            OnAudioGenerated?.Invoke(frag);
                        }
                    }
                }
                length -= recordingBuffer.Length;
                lastPos += recordingBuffer.Length;
            }            
        }

        void OnDestroy()
        {
            StopRecording();
        }
    }
}