using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Keysight33500BApp
{
    public class FunctionGeneratorController
    {
        private readonly LanCommunication _comm;

        // Track which waveform shape is currently set on each channel, e.g. "SIN", "PULS", ...
        // This helps us skip invalid commands for DC or NOIS, etc.
        private readonly Dictionary<string, string> _channelShape;

        public FunctionGeneratorController(LanCommunication communication)
        {
            _comm = communication ?? throw new ArgumentNullException(nameof(communication));

            // Default both channels to SIN, or unknown, etc.
            _channelShape = new Dictionary<string, string>
            {
                { "CH1", "SIN" },
                { "CH2", "SIN" }
            };
        }

        public async Task InitializeAsync()
        {
            await _comm.WriteAsync("*RST");
            await Task.Delay(1000);
            await _comm.WriteAsync("*CLS");
        }

        public async Task<string> IdentifyAsync()
        {
            return await _comm.QueryAsync("*IDN?");
        }

        // ─────────────────────────────────────────
        //         HELPER: IsCommandValid?
        // ─────────────────────────────────────────
        // We check if the current shape supports phase, pulse edges, etc.
        private bool SupportsPhase(string shape) 
            => shape == "SIN" || shape == "SQU" || shape == "RAMP" || shape == "PULS";

        private bool SupportsPulseEdges(string shape) 
            => shape == "PULS"; // Only pulses can set leading/trailing edge times

        private bool SupportsSquareDuty(string shape)
            => shape == "SQU";

        private bool SupportsRampSymmetry(string shape)
            => shape == "RAMP";

        // ─────────────────────────────────────────
        //  BASIC PARAMETERS (Shared)
        // ─────────────────────────────────────────
        public async Task SetWaveformAsync(string channel, string shape)
        {
            shape = shape.ToUpperInvariant();
            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion {shape}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion {shape}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }

            // Remember which shape we set for this channel
            _channelShape[channel] = shape;
        }

        public async Task SetFrequencyAsync(string channel, double frequency)
        {
            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FREQuency {frequency.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FREQuency {frequency.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetAmplitudeAsync(string channel, double amplitude)
        {
            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:VOLTage {amplitude.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:VOLTage {amplitude.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetOffsetAsync(string channel, double offset)
        {
            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:VOLTage:OFFSet {offset.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:VOLTage:OFFSet {offset.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        // ─────────────────────────────────────────
        //  EXTENDED PARAMETERS
        // ─────────────────────────────────────────
        public async Task SetPhaseAsync(string channel, double phase)
        {
            string shape = _channelShape[channel]; // Check current shape
            if (!SupportsPhase(shape)) 
            {
                // We skip this command to avoid instrument error
                return;
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:PHASe {phase.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:PHASe {phase.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetSquareDutyCycleAsync(string channel, double dutyCycle)
        {
            string shape = _channelShape[channel];
            if (!SupportsSquareDuty(shape))
            {
                return; // skip if shape isn't SQU
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:SQUare:DCYCle {dutyCycle.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:SQUare:DCYCle {dutyCycle.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetRampSymmetryAsync(string channel, double symmetry)
        {
            string shape = _channelShape[channel];
            if (!SupportsRampSymmetry(shape))
            {
                return; // skip if shape isn't RAMP
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:RAMP:SYMMetry {symmetry.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:RAMP:SYMMetry {symmetry.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetPulseWidthAsync(string channel, double width)
        {
            string shape = _channelShape[channel];
            if (!SupportsPulseEdges(shape))
            {
                return; // skip if shape isn't PULS
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:PULSe:WIDTh {width.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:PULSe:WIDTh {width.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetPulseLeadingEdgeAsync(string channel, double leadEdge)
        {
            string shape = _channelShape[channel];
            if (!SupportsPulseEdges(shape))
            {
                return;
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:PULSe:TRANsition:LEADing {leadEdge.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:PULSe:TRANsition:LEADing {leadEdge.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetPulseTrailingEdgeAsync(string channel, double trailEdge)
        {
            string shape = _channelShape[channel];
            if (!SupportsPulseEdges(shape))
            {
                return;
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:PULSe:TRANsition:TRAiling {trailEdge.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:PULSe:TRANsition:TRAiling {trailEdge.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        public async Task SetNoiseBandwidthAsync(string channel, double bandwidth)
        {
            string shape = _channelShape[channel];
            // It's valid for shape == "NOIS". If shape != NOIS, skip to avoid SCPI error.
            if (!shape.StartsWith("NOIS")) 
            {
                return;
            }

            if (channel == "CH1")
            {
                await _comm.WriteAsync($":SOURce1:FUNCtion:NOISe:BWIDth {bandwidth.ToString(CultureInfo.InvariantCulture)}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":SOURce2:FUNCtion:NOISe:BWIDth {bandwidth.ToString(CultureInfo.InvariantCulture)}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        // ─────────────────────────────────────────
        //  DISPOSE
        // ─────────────────────────────────────────
        public async Task DisposeAsync()
        {
            // Turn off both channels before disconnecting if you want
            await _comm.WriteAsync(":OUTPut1:STATe OFF");
            await _comm.WriteAsync(":OUTPut2:STATe OFF");

            _comm.Dispose();  
        }

        // ─────────────────────────────────────────
        //  OPTIONAL: SetChannelAsync
        // ─────────────────────────────────────────
        public async Task SetChannelAsync(string channel)
        {
            // Some 33500B firmware might accept :INSTrument:SELect 1 or 2
            // But if it triggers an error, you can skip calling this entirely
            // since all your commands are direct to :SOURce1: or :SOURce2: anyway.
            if (channel == "CH1")
            {
                await _comm.WriteAsync(":INSTrument:SELect 1");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync(":INSTrument:SELect 2");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        // ─────────────────────────────────────────
        //  Enable/Disable Output
        // ─────────────────────────────────────────
        public async Task EnableChannelAsync(string channel, bool enable = true)
        {
            // CH1 => :OUTPut1:STATe ON|OFF
            // CH2 => :OUTPut2:STATe ON|OFF
            string onOff = enable ? "ON" : "OFF";
            if (channel == "CH1")
            {
                await _comm.WriteAsync($":OUTPut1:STATe {onOff}");
            }
            else if (channel == "CH2")
            {
                await _comm.WriteAsync($":OUTPut2:STATe {onOff}");
            }
            else
            {
                throw new ArgumentException("Invalid channel. Use 'CH1' or 'CH2'.");
            }
        }

        /// <summary>
        /// If you want a simple “set output ON/OFF” for the *currently selected* channel only,
        /// you can do:
        /// </summary>
        public async Task SetOutputStateAsync(bool enable)
        {
            string onOff = enable ? "ON" : "OFF";
            await _comm.WriteAsync($":OUTPut:STATe {onOff}");
        }


        // ─────────────────────────────────────────
        //  (Optional) Utility to Force a Trigger
        // ─────────────────────────────────────────
        public async Task SendSoftwareTriggerAsync()
        {
            // e.g. :TRIGger:IMMediate
            await _comm.WriteAsync(":TRIGger:IMMediate");
        }
    }
}
