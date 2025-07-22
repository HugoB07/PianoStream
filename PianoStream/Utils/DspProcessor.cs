namespace PianoStream.Utils
{
    public class DspProcessor
    {
        private float previousLeftLPF = 0.0f;
        private float previousRightLPF = 0.0f;

        private readonly float lowPassAlpha;
        private readonly float limitThreshold;

        public DspProcessor(
            float lowPassAlpha = 0.2f,
            float limitThreshold = 0.98f)
        {
            this.lowPassAlpha = lowPassAlpha;
            this.limitThreshold = limitThreshold;
        }

        public void Process(float[] left, float[] right, int frameCount)
        {
            for (int i = 0; i < frameCount; i++)
            {
                float l = left[i];
                float r = right[i];

                // Low-pass filter (removes hiss)
                l = LowPassFilter(l, ref previousLeftLPF);
                r = LowPassFilter(r, ref previousRightLPF);

                // Limiter (prevents digital saturation)
                left[i] = SoftLimit(l);
                right[i] = SoftLimit(r);
            }
        }

        private float LowPassFilter(float input, ref float previous)
        {
            float output = lowPassAlpha * input + (1 - lowPassAlpha) * previous;
            previous = output;
            return output;
        }

        private float SoftLimit(float sample)
        {
            // Soft clipper
            if (sample > limitThreshold)
                return limitThreshold + (sample - limitThreshold) * 0.3f;
            if (sample < -limitThreshold)
                return -limitThreshold + (sample + limitThreshold) * 0.3f;
            return sample;
        }
    }
}
