using NAudio.Wave;
using NFluidsynth;

namespace PianoStream.Utils
{
    public class FluidSynthWaveProvider : IWaveProvider
    {
        public bool EnableNoiseCancellation { get; set; } = false;

        private readonly Synth synth;
        private readonly DspProcessor dsp;

        private WaveFormat waveFormat;
        private float[] leftBuffer;
        private float[] rightBuffer;

        public FluidSynthWaveProvider(Synth synth, bool enableNoiseCancellation = false)
        {
            this.synth = synth;
            this.dsp = new DspProcessor(); // Initialize DSP processor
            this.EnableNoiseCancellation = enableNoiseCancellation;

            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); 
            leftBuffer = new float[2048];
            rightBuffer = new float[2048];
        }

        public WaveFormat WaveFormat => waveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int totalSamples = count / 4;          
            int frameCount = totalSamples / 2;      

            if (leftBuffer.Length < frameCount)
            {
                leftBuffer = new float[frameCount];
                rightBuffer = new float[frameCount];
            }

            Span<float> leftSpan = new Span<float>(leftBuffer, 0, frameCount);
            Span<float> rightSpan = new Span<float>(rightBuffer, 0, frameCount);

            synth.WriteSampleFloat(frameCount, leftSpan, 0, 1, rightSpan, 0, 1);

            if(EnableNoiseCancellation)
            {
                dsp.Process(leftBuffer, rightBuffer, frameCount);
            }

            float gain = 8.0f; 
            for (int i = 0; i < frameCount; i++)
            {
                leftBuffer[i] *= gain;
                rightBuffer[i] *= gain;
            }

            int byteIndex = offset;
            for (int i = 0; i < frameCount; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(leftBuffer[i]), 0, buffer, byteIndex, 4);
                byteIndex += 4;
                Buffer.BlockCopy(BitConverter.GetBytes(rightBuffer[i]), 0, buffer, byteIndex, 4);
                byteIndex += 4;
            }

            return frameCount * 2 * 4; 
        }
    }
}
