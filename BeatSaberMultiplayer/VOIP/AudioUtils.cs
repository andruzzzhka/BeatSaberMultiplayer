using NSpeex;
using UnityEngine;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/AudioUtils.cs
    public static class AudioUtils
    {
        public static void ApplyGain(float[] samples, float gain)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        public static float GetMaxAmplitude(float[] samples)
        {
            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                max = Mathf.Max(max, Mathf.Abs(samples[i]));
            }
            return max;
        }

        public static int GetFrequency( BandMode mode )
        {
            switch (mode)
            {
                case BandMode.Narrow:
                    return 8000;
                case BandMode.Wide:
                    return 16000;
                case BandMode.UltraWide:
                    return 32000;
                default:
                    return 8000;
            }
        }

        public static void Downsample(float[] source, float[] target)
        {
            int ratio = source.Length / target.Length;
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = source[i * ratio];
            }
        }

        public static int GetFreqForMic(string deviceName = null)
        {
            int minFreq;
            int maxFreq;

            Microphone.GetDeviceCaps(deviceName, out minFreq, out maxFreq);
            
            if((minFreq <= 16000 && maxFreq >= 16000) || (minFreq == 0 && maxFreq == 0))
            {
                return 16000;
            }
            else if(minFreq > 16000)
            {
                if(FindClosestFreq(minFreq, maxFreq) != 0)
                {
                    return FindClosestFreq(minFreq, maxFreq);
                }
                else
                {
                    return minFreq;
                }
            }
            else
            {
                return maxFreq;
            }
        }

        public static int FindClosestFreq(int minFreq, int maxFreq)
        {
            for (int i = (int)Mathf.Round(minFreq / 1000); i < Mathf.Round(maxFreq / 1000); i++)
            {
                if (i % 16 == 0)
                {
                    return i * 1000;
                }
            }
            return 0;
        }
    }
}