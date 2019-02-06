using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BeatSaberMultiplayer.Misc
{   //https://github.com/Phylliida/UnityVOIP/blob/master/VoiceChat/Assets/UnityVOIP/WritableAudioPlayer.cs
    public class WritableAudioPlayer : MonoBehaviour
    {
        Dictionary<ulong, WasapiOut> outputs;
        Dictionary<ulong, WritablePureDataSource> sources;

        public int inputSampleRate = 16000;

        bool cleanedUp = false;
        object cleanupLock = new object();

        private void Start()
        {
            outputs = new Dictionary<ulong, WasapiOut>();
            sources = new Dictionary<ulong, WritablePureDataSource>();
        }

        void MakeOutput(out WasapiOut output, out WritablePureDataSource outSource)
        {
            var ayy = new MMDeviceEnumerator();

            output = new WasapiOut(false, AudioClientShareMode.Shared, 100);
            output.Device = ayy.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            outSource = new WritablePureDataSource(new WaveFormat(inputSampleRate, 16, 1), new WaveFormat(output.Device.DeviceFormat.SampleRate, 16, output.Device.DeviceFormat.Channels));
            
            output.Initialize(outSource.ToWaveSource());
            
            output.Volume = Mathf.Clamp01(Config.Instance.VoiceChatVolume);

            output.Play();
        }

        public void SetVolume(float volume)
        {
            foreach(var output in outputs)
            {
                output.Value.Volume = Mathf.Clamp01(volume);
            }
        }

        public float TimeUntilDone(ulong id)
        {
            if (outputs.ContainsKey(id))
            {
                return sources[id].numUnprocessed / (float)sources[id].waveFormatIn.SampleRate;
            }
            else
            {
                throw new ArgumentException("Id " + id + " does not have an output yet");
            }
        }


        public void PlayAudio(float[] audio, int offset, int len, ulong audioId)
        {
            lock (cleanupLock)
            {
                if (!cleanedUp)
                {
                    if (outputs.ContainsKey(audioId))
                    {
                        sources[audioId].Write(audio, offset, len);
                    }
                    else
                    {
                        WasapiOut output;
                        WritablePureDataSource source;
                        MakeOutput(out output, out source);
                        sources[audioId] = source;
                        outputs[audioId] = output;
                        source.Write(audio, offset, len);
                    }
                }
            }
        }
        public void PlayAudio(float[] audio, ulong audioId)
        {
            PlayAudio(audio, 0, audio.Length, audioId);
        }

        void Cleanup()
        {
            lock (cleanupLock)
            {
                if (!cleanedUp)
                {
                    cleanedUp = true;

                    foreach (KeyValuePair<ulong, WasapiOut> keyOutput in outputs)
                    {
                        WasapiOut output = keyOutput.Value;
                        if (output.PlaybackState == PlaybackState.Paused)
                        {
                            output.Stop();
                        }
                        while (output.PlaybackState == PlaybackState.Playing)
                        {
                            output.Stop();
                        }
                        output.Dispose();
                    }
                }
            }
        }

        void OnDestroy()
        {
            Cleanup();
        }
        void OnApplicationQuit()
        {
            Cleanup();
        }
    }


    public enum ConverterQuality
    {
        SRC_SINC_FASTEST = 2,
        SRC_SINC_MEDIUM_QUALITY = 1,
        SRC_SINC_BEST_QUALITY = 0
    };

    public class WritablePureDataSource : ISampleSource
    {

        [DllImport("samplerate", EntryPoint = "src_simple_plain")]
        public static extern int src_simple_plain(float[] data_in, float[] data_out, int input_frames, int output_frames, float src_ratio, ConverterQuality converter_type, int channels);


        public long Length
        {
            get
            {
                return 0;
            }
        }

        public long Position
        {
            get
            {
                return 0;
            }

            set
            {
                throw new NotImplementedException();
            }
        }
        private WaveFormat _WaveFormat;
        public WaveFormat WaveFormat
        {
            get
            {
                return _WaveFormat;
            }
        }

        private int _Patch = 0;
        public int Patch
        {
            get { return _Patch; }
        }

        public bool CanSeek
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public WaveFormat waveFormatIn;
        public WaveFormat waveFormatOut;

        public WritablePureDataSource(WaveFormat waveFormatIn, WaveFormat waveFormatOut)
        {
            this.waveFormatIn = waveFormatIn;
            _WaveFormat = waveFormatOut;
            this.waveFormatOut = waveFormatOut;

            dats = new Queue<float[]>();
            offsets = new Queue<int>();
            lens = new Queue<int>();
        }

        
        float[] tempBuffer1 = new float[80000 * 20];
        float[] tempBuffer2 = new float[80000 * 20];
        float[] tempBuffer3 = new float[80000 * 20];

        float[] unprocessedAudio = new float[80000 * 20];
        public int numUnprocessed = 0;

        Queue<float[]> dats;
        Queue<int> offsets;
        Queue<int> lens;

        private void AddToUnprocessedData(float[] data, int offset, int numBytes, int inChannels)
        {
            if (inChannels == 1 && waveFormatOut.Channels == 2)
            {
                int pos = 0;
                for (int i = 0; i < numBytes && pos < tempBuffer3.Length - 2; i++)
                {
                    tempBuffer3[pos++] = data[i + offset];
                    tempBuffer3[pos++] = data[i + offset];
                }
                AddToUnprocessedData(tempBuffer3, 0, numBytes * 2, 2);
                return;
            }

            if (numUnprocessed + numBytes > unprocessedAudio.Length)
            {
                numBytes = unprocessedAudio.Length - numUnprocessed;
            }
            if (numBytes == 0)
            {
                return;
            }

            Buffer.BlockCopy(data, offset * sizeof(float), unprocessedAudio, numUnprocessed * sizeof(float), numBytes * sizeof(float));

            numUnprocessed += numBytes;

            while (numUnprocessed > 320 * 5)
            {
                Buffer.BlockCopy(data, 320 * sizeof(float), data, 0, numUnprocessed - 320);
                numUnprocessed -= 320;
            }
        }

        public void Write(float[] buffer, int offset, int count)
        {
            lock (unprocessedAudio)
            {
                AddToUnprocessedData(buffer, offset, count, waveFormatIn.Channels);
            }
        }

        public int ReadUnprocessedData(float[] buffer, int offset, int count)
        {
            lock (unprocessedAudio)
            {
                int countUsing = Mathf.Min(count, numUnprocessed);

                if (countUsing <= 0)
                {
                    if (count > 0)
                    {
                        Array.Clear(buffer, offset, count);
                        return count;
                    }
                    else
                    {
                        return 0;
                    }
                }
                Buffer.BlockCopy(unprocessedAudio, 0, buffer, offset * sizeof(float), countUsing * sizeof(float)); // size is in bytes

                int numLeft = numUnprocessed - countUsing;
                if (numUnprocessed > 0)
                {
                    Buffer.BlockCopy(unprocessedAudio, countUsing * sizeof(float), unprocessedAudio, 0, numLeft * sizeof(float));
                }
                numUnprocessed = numLeft;


                return countUsing;
            }
        }




        public ConverterQuality quality;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            int sourceSampleRate = waveFormatIn.SampleRate;
            int mySampleRate = waveFormatOut.SampleRate;

            int numTheirSamples = (int)Mathf.Floor((count * (float)sourceSampleRate / mySampleRate));

            int res = 100;
            try
            {
                res = ReadUnprocessedData(tempBuffer1, 0, numTheirSamples);
                if (res == 0)
                {
                    if (count == 0)
                    {
                        return 0;
                    }
                    else
                    {
                        return count;
                    }
                }
                int numOurSamples = (int)Mathf.Round((res * (float)mySampleRate / sourceSampleRate));
                res = src_simple_plain(tempBuffer1, tempBuffer2, res, numOurSamples, (float)mySampleRate / sourceSampleRate, ConverterQuality.SRC_SINC_MEDIUM_QUALITY, waveFormatOut.Channels);
                if (res > count)
                {
                    res = count;
                }
                Buffer.BlockCopy(tempBuffer2, 0, buffer, offset * sizeof(float), res * sizeof(float));
            }
            catch (Exception e)
            {
                Debug.Log("failed read: " + e);
                return count;
            }
            int fadeSize = 100;
            fadeSize = Mathf.Min(fadeSize, res / 2 - 1);
            if (fadeSize > 1)
            {
                int startInd = offset + res - fadeSize;
                for (int i = 0; i < fadeSize; i++)
                {
                    float fade = 1 - (i / (float)(fadeSize - 1));
                    buffer[startInd + i] *= fade * fade;
                    float startFade = 1 - fade;
                    buffer[offset + i] *= startFade * startFade;
                }
            }
            if (res < count)
            {
                for (int i = res; i < count; i++)
                {
                    buffer[offset + i] = 0;
                }
            }
            return count;
        }

        public void Dispose()
        {

        }
    }
}
