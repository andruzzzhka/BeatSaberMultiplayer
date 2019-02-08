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
    }
}