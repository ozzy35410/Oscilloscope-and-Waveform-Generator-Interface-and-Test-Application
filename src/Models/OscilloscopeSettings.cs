using System;
using System.Linq;

namespace Oscilloscope.Models
{
    public class OscilloscopeSettings
    {
        // Horizontal (Timebase) Settings
        public double TimebaseScale { get; set; } = 1e-3; // Default: 1ms/div
        public string TimebaseReference { get; set; } = "CENTER";

        // Vertical Settings per Channel
        public class ChannelSettings
        {
            public bool Enabled { get; set; } = false;
            public double VerticalScale { get; set; } = 1.0; // Default: 1V/div
            public double Offset { get; set; } = 0.0;
        }

        // Array of channel settings (MSOX3104T has 4 analog channels)
        public ChannelSettings[] Channels { get; private set; }

        // Waveform Generator Settings
        public class WaveformSettings
        {
            public string WaveformType { get; set; } = "SINusoid";
            public double Frequency { get; set; } = 1000.0; // Default: 1kHz
            public double Amplitude { get; set; } = 1.0; // Default: 1Vpp
            public double Offset { get; set; } = 0.0;
        }

        public WaveformSettings WaveformGenerator { get; set; }

        public OscilloscopeSettings()
        {
            // Initialize channel settings
            Channels = new ChannelSettings[4];
            for (int i = 0; i < 4; i++)
            {
                Channels[i] = new ChannelSettings();
            }

            // Initialize waveform generator settings
            WaveformGenerator = new WaveformSettings();
        }

        public void ValidateSettings()
        {
            if (TimebaseScale <= 0)
                throw new ArgumentException("Timebase scale must be positive");

            var validReferences = new[] { "LEFT", "CENTER", "RIGHT" };
            if (string.IsNullOrEmpty(TimebaseReference) || !validReferences.Contains(TimebaseReference.ToUpper()))
                throw new ArgumentException($"Invalid timebase reference point. Use one of: {string.Join(", ", validReferences)}");

            for (int i = 0; i < Channels.Length; i++)
            {
                if (Channels[i].VerticalScale <= 0)
                    throw new ArgumentException($"Vertical scale for channel {i + 1} must be positive");
            }

            if (WaveformGenerator.Frequency <= 0)
                throw new ArgumentException("Waveform frequency must be positive");

            if (WaveformGenerator.Amplitude <= 0)
                throw new ArgumentException("Waveform amplitude must be positive");
        }
    }
}