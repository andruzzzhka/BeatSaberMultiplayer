using UnityEngine;

namespace BeatSaberMultiplayer.VOIP
{
    //https://github.com/DwayneBull/UnityVOIP/blob/master/VoipBehaviour.cs
    public class VoipBehaviour : MonoBehaviour
    {
        public int chunkCount { get; protected set; }
        public int bytes { get; protected set; }
        public float bytesPerSecond { get; protected set; }

        private int bps = 0;
        private float bpst = 0f;

        protected void UpdateStats()
        {
            bpst += Time.unscaledDeltaTime;
            if (bpst > 0.2f)
            {
                bpst = bpst % 0.2f;
                bytesPerSecond = Mathf.Lerp(bytesPerSecond, (bytes - bps), 0.5f);
                bps = bytes;
            }
        }
    }
}
