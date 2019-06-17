using NSpeex;
using System;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/SpeexCodex.cs
    public class SpeexCodex
    {
        //Encoding.
        private short[] encodingBuffer;
        private byte[] encodedBuffer;
        private readonly NSpeex.SpeexEncoder encoder;

        //Decoding.
        private short[] decodeBuffer;
        private float[] decodedBuffer;
        private readonly NSpeex.SpeexDecoder decoder;

        //The size of the data frame.
        public int dataSize { get; set; }

        //The mode.
        public BandMode mode { get; set; }

        //The SpeedxMode
        private NSpeex.BandMode SpeedxMode
        {
            get
            {
                if (mode == BandMode.UltraWide) return NSpeex.BandMode.UltraWide;
                if (mode == BandMode.Wide) return NSpeex.BandMode.Wide;
                return NSpeex.BandMode.Narrow;
            }
        }

        public static SpeexCodex Create( BandMode mode )
        {
            return new SpeexCodex(mode);
        }

        public SpeexCodex( BandMode mode )
        {
            this.mode = mode;

            encoder = new NSpeex.SpeexEncoder(SpeedxMode);
            decoder = new NSpeex.SpeexDecoder(SpeedxMode, false);

            encoder.VBR = true;
            encoder.Quality = 4;

            dataSize = encoder.FrameSize * ( mode == BandMode.Narrow ? 8 : mode == BandMode.Wide ? 8 : 2 );

            encodingBuffer = new short[dataSize];
            encodedBuffer = new byte[dataSize];
            decodeBuffer = new short[dataSize];
            decodedBuffer = new float[dataSize];

            int frequency = AudioUtils.GetFrequency(mode);
            var time = frequency / (float)dataSize;
            UnityEngine.Debug.Log("SpeedCodex created mode:" + mode + " dataSize:" + dataSize+ " time:"+ time);
        }

        public float[] Decode( byte[] data )
        {
            var decoded = decoder.Decode(data, 0, data.Length, decodeBuffer, 0, false);
            Convert(decodeBuffer, ref decodedBuffer);
            return decodedBuffer;
        }

        public byte[] Encode( float[] data )
        {
            //Convert 
            Convert(data, ref encodingBuffer);
            int length = encoder.Encode(encodingBuffer, 0, encodingBuffer.Length, encodedBuffer, 0, encodedBuffer.Length);
            //Copy to temp.
            var tmp = new byte[length];
            Buffer.BlockCopy(encodedBuffer, 0, tmp, 0, tmp.Length);
            //Return encoded data.
            return tmp;
        }

        private static void Convert(short[] data, ref float[] target)
        {
            for (int i = 0; i < data.Length; i++)
            {
                target[i] = data[i] / (float)short.MaxValue;
            }
        }

        private static void Convert(float[] data, ref short[] target)
        {
            for (int i = 0; i < data.Length; i++)
            {
                target[i] = (short)(data[i] * short.MaxValue);
            }
        }
    }
}
