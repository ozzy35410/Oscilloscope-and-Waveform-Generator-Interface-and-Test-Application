using System;
using System.Linq;
using System.Threading.Tasks;
using Oscilloscope.Communication;
using System.Globalization; // ensure '.' decimal parsing/formatting


namespace Oscilloscope.Controllers
{
    public class OscilloscopeController
    {
        private readonly VisaCommunication _communication;
        private bool _disposed = false; // Flag to track disposal

        public OscilloscopeController(VisaCommunication communication)
        {
            _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        }

        public async Task InitializeAsync()
        {
            await _communication.WriteAsync("*RST");
            await Task.Delay(1000);
            await _communication.WriteAsync("*CLS");
            await _communication.WriteAsync(":SYSTem:HEADer ON");
        }

        // ---------------------------
        // Horizontal (Timebase)
        // ---------------------------
        public async Task SetTimebaseScaleAsync(double secondsPerDivision)
        {
            if (secondsPerDivision < 500e-12 || secondsPerDivision > 100)
                throw new ArgumentException("Timebase scale must be between 500ps and 100s", nameof(secondsPerDivision));

            string cmd = $":TIMebase:SCALe {secondsPerDivision.ToString(CultureInfo.InvariantCulture)}";
            await _communication.WriteAsync(cmd);

            // Verify
            string response = await _communication.QueryAsync(":TIMebase:SCALe?");
            double actualScale = double.Parse(response, CultureInfo.InvariantCulture);
            if (Math.Abs(actualScale - secondsPerDivision) > secondsPerDivision * 0.01)
                throw new Exception($"Failed to set timebase scale. Requested: {secondsPerDivision}, Actual: {actualScale}");
        }

        public async Task<double> GetTimebaseScaleAsync()
        {
            string response = await _communication.QueryAsync(":TIMebase:SCALe?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task SetTimebaseReferenceAsync(string reference)
        {
            if (string.IsNullOrEmpty(reference))
                throw new ArgumentException("Reference point cannot be empty", nameof(reference));

            reference = reference.ToUpper();
            var validReferences = new[] { "LEFT", "CENTER", "RIGHT" };
            if (!validReferences.Contains(reference))
                throw new ArgumentException($"Invalid reference point. Use one of: {string.Join(", ", validReferences)}", nameof(reference));

            await _communication.WriteAsync($":TIMebase:REFerence {reference}");
        }

        // ---------------------------
        // Vertical (Voltage)
        // ---------------------------
        public async Task SetVerticalScaleAsync(int channel, double voltsPerDivision)
        {
            ValidateChannel(channel);
            if (voltsPerDivision < 1e-3 || voltsPerDivision > 10)
                throw new ArgumentException("Voltage scale must be between 1mV and 10V", nameof(voltsPerDivision));

            string cmd = $":CHANnel{channel}:SCALe {voltsPerDivision.ToString(CultureInfo.InvariantCulture)}";
            await _communication.WriteAsync(cmd);

            // Verify
            string response = await _communication.QueryAsync($":CHANnel{channel}:SCALe?");
            double actualScale = double.Parse(response, CultureInfo.InvariantCulture);
            if (Math.Abs(actualScale - voltsPerDivision) > voltsPerDivision * 0.01)
                throw new Exception($"Failed to set vertical scale. Requested: {voltsPerDivision}, Actual: {actualScale}");
        }

        public async Task<double> GetVerticalScaleAsync(int channel)
        {
            ValidateChannel(channel);
            string response = await _communication.QueryAsync($":CHANnel{channel}:SCALe?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task SetVerticalOffsetAsync(int channel, double offsetVolts)
        {
            ValidateChannel(channel);
            string scaleResponse = await _communication.QueryAsync($":CHANnel{channel}:SCALe?");
            double currentScale = double.Parse(scaleResponse, CultureInfo.InvariantCulture);
            double maxOffset = currentScale * 40;

            if (Math.Abs(offsetVolts) > maxOffset)
                throw new ArgumentException($"Offset must be between {-maxOffset} and {maxOffset}", nameof(offsetVolts));

            string cmd = $":CHANnel{channel}:OFFSet {offsetVolts.ToString(CultureInfo.InvariantCulture)}";
            await _communication.WriteAsync(cmd);
        }

        public async Task SendCommandAsync(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));

            await _communication.WriteAsync(command);
        }

        public async Task SetChannelStateAsync(int channel, bool enabled)
        {
            ValidateChannel(channel);
            string state = enabled ? "ON" : "OFF";
            await _communication.WriteAsync($":CHANnel{channel}:DISPlay {state}");

            // Verify
            string response = await _communication.QueryAsync($":CHANnel{channel}:DISPlay?");
            bool actualState = response.Trim() == "1";
            if (actualState != enabled)
                throw new Exception($"Failed to set channel {channel} state to {enabled}");
        }

        // ---------------------------
        // Waveform Generator
        // ---------------------------
        public async Task ConfigureWaveformGeneratorAsync(
            string waveformType,
            double frequency,
            double amplitude,
            double offset = 0,
            double dutyCycle = 50,
            double symmetry = 50,
            double widthNs = 0)
        {
            if (string.IsNullOrEmpty(waveformType))
                throw new ArgumentException("Waveform type cannot be empty", nameof(waveformType));

            waveformType = waveformType.ToUpper();
            var validWaveforms = new[] { "SINUSOID", "SQUARE", "RAMP", "PULSE", "NOISE", "DC" };
            if (!validWaveforms.Contains(waveformType))
                throw new ArgumentException($"Invalid waveform type. Must be one of: {string.Join(", ", validWaveforms)}", nameof(waveformType));

            string freqStr   = frequency.ToString(CultureInfo.InvariantCulture);
            string ampStr    = amplitude.ToString(CultureInfo.InvariantCulture);
            string offsetStr = offset.ToString(CultureInfo.InvariantCulture);
            string dutyStr   = dutyCycle.ToString(CultureInfo.InvariantCulture);
            string symStr    = symmetry.ToString(CultureInfo.InvariantCulture);
            double widthSec  = widthNs * 1e-9; 
            string widthSecStr = widthSec.ToString(CultureInfo.InvariantCulture);

            await _communication.WriteAsync($":WGEN:FUNCtion {waveformType}");

            if (frequency > 0)
                await _communication.WriteAsync($":WGEN:FREQuency {freqStr}");

            if (amplitude > 0)
                await _communication.WriteAsync($":WGEN:VOLTage {ampStr}");

            await _communication.WriteAsync($":WGEN:VOLTage:OFFSet {offsetStr}");

            switch (waveformType)
            {
                case "SQUARE":
                    await _communication.WriteAsync($":WGEN:FUNCtion:SQUare:DCYCle {dutyStr}");
                    break;

                case "RAMP":
                    await _communication.WriteAsync($":WGEN:FUNCtion:RAMP:SYMMetry {symStr}");
                    break;

                case "PULSE":
                    // Adjust or remove if you need < 20 ns
                    await _communication.WriteAsync($":WGEN:FUNCtion:PULSe:WIDTh {widthSecStr}");
                    break;

                case "NOISE":
                    // no special command beyond amplitude/offset
                    break;

                case "DC":
                    // only offset matters
                    break;
            }

            await _communication.WriteAsync(":WGEN:OUTPut ON");

            // Verify
            string actualType = await _communication.QueryAsync(":WGEN:FUNCtion?");
            if (!actualType.Trim().Equals(waveformType, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Failed to configure waveform generator. Expected: {waveformType}, got: {actualType}");
        }

        // ---------------------------
        // Measurements
        // ---------------------------
        public async Task<double> MeasureVppAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:VPP?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureVrmsAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:VRMS?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureFrequencyAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:FREQuency?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasurePeriodAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:PERiod?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureAmplitudeAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:VAMPlitude?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureMeanVoltageAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:VAVerage?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasurePhaseAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:PHASe?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureDutyCycleAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:DUTYcycle?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasurePulseWidthAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:PWIDth?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureRiseTimeAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:RISetime?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureFallTimeAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:FALLtime?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureOvershootAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:OVERshoot?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasurePreshootAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:PREShoot?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureSlewRateAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:SLEWrate?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }



        



        private void ValidateChannel(int channel)
        {
            if (channel < 1 || channel > 4)
                throw new ArgumentException("Invalid channel number. Must be 1 to 4", nameof(channel));
        }

        // ─────────────────────────────────────────────
        // Trigger Methods
        // ─────────────────────────────────────────────
        public async Task SetTriggerSweepModeAsync(string mode)
        {
            // This sets the scope’s sweep to AUTO|NORMal|SINGle
            // (not the same as :TRIGger:MODE)
            mode = mode.ToUpper();
            switch (mode)
            {
                case "AUTO":
                    mode = "AUTO";
                    break;
                case "NORM":
                    mode = "NORMal";
                    break;
                case "SING":
                    mode = "SINGle";
                    break;
                default:
                    throw new ArgumentException($"Unsupported trigger sweep mode: {mode}");
            }
            await _communication.WriteAsync($":TRIGger:SWEep {mode}");
        }


        public async Task SetTriggerEdgeSourceAsync(string source)
        {
            // For the MSOX3104T, valid sources might be: CHAN1..CHAN4, LINE, WGEN, etc.
            // We'll trust the caller to pass a valid string, or you can add validation
            await _communication.WriteAsync($":TRIGger:EDGE:SOURce {source.ToUpper()}");
        }

        public async Task SetTriggerEdgeSlopeAsync(string slope)
        {
            // Typically POSitive|NEGative
            slope = slope.ToUpper();
            if (slope == "POS") slope = "POSitive";
            if (slope == "NEG") slope = "NEGative";
            await _communication.WriteAsync($":TRIGger:EDGE:SLOPe {slope}");
        }

        public async Task SetTriggerLevelAsync(double level)
        {
            // If you want to set a trigger level specifically for a certain channel,
            // you can do: :TRIGger:EDGE:LEVel <level>,(@<channel>)
            // But for InfiniiVision, usually :TRIGger:EDGE:LEVel <level> sets level for the active source.
            await _communication.WriteAsync($":TRIGger:EDGE:LEVel {level.ToString("G", CultureInfo.InvariantCulture)}");

        }

        public async Task SetTriggerTypeAsync(string triggerType)
        {
            // For Keysight InfiniiVision, advanced triggers might be:
            // EDGE, GLITch, PATTern, PULSe, RUNT, etc.
            triggerType = triggerType.ToUpper();
            var validTypes = new[] { "EDGE", "PULSE", "PATTERN" };
            if (!validTypes.Contains(triggerType))
                throw new ArgumentException($"Unsupported trigger type: {triggerType}");

            // SCPI: :TRIGger:MODE EDGE|PULSe|PATTern
            await _communication.WriteAsync($":TRIGger:MODE {triggerType}");
        }

        public async Task<byte[]> GetScreenCaptureAsync()
        {
            if (_communication == null) throw new InvalidOperationException("Oscilloscope not connected!");

            // SCPI command to capture screen (depends on your oscilloscope model)
            await _communication.SendCommandAsync(":DISP:DATA? PNG");

            // Read the binary data response from oscilloscope
            byte[] imageData = await _communication.ReadBinaryDataAsync();
            
            return imageData;
        }

        // Add this method inside OscilloscopeController
        public async Task<string> QueryAsync(string command)
        {
            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            return await _communication.QueryAsync(command);
        }

        // Add this method inside OscilloscopeController
        public async Task<string> ReadResponseAsync()
        {
            return await _communication.ReadResponseAsync();
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                if (_communication is IDisposable disposableCommunication)
                {
                    disposableCommunication.Dispose();  // ✅ Try disposing if available
                }

                _disposed = true;
            }
        }


        public async Task<double> MeasureSignalDelayAsync(int channel, double frequency)
        {
            ValidateChannel(channel);

            // 1. Capture the waveform from the oscilloscope
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            
            // 2. Measure the period of the waveform
            string periodResponse = await _communication.QueryAsync(":MEASure:PERiod?");
            double period = double.Parse(periodResponse, CultureInfo.InvariantCulture);

            // 3. Measure the time when the first zero-crossing happens (estimated as delay)
            string delayResponse = await _communication.QueryAsync(":MEASure:RISetime?");
            double measuredDelay = double.Parse(delayResponse, CultureInfo.InvariantCulture);

            // 4. Calculate the theoretical delay based on frequency
            double expectedDelay = (1 / frequency) * (360.0 / 360.0);  // Full cycle = 360°

            return measuredDelay - expectedDelay;
        }


        public async Task<double> MeasureSymmetryAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string riseTimeResponse = await _communication.QueryAsync(":MEASure:RISetime?");
            string periodResponse = await _communication.QueryAsync(":MEASure:PERiod?");

            double riseTime = double.Parse(riseTimeResponse, CultureInfo.InvariantCulture);
            double period = double.Parse(periodResponse, CultureInfo.InvariantCulture);

            return (riseTime / period) * 100.0; // Convert to percentage
        }

        public async Task<double> MeasureLeadEdgeTimeAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:TRANsition?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureTrailEdgeTimeAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:TRANsition?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        public async Task<double> MeasureBandwidthAsync(int channel)
        {
            ValidateChannel(channel);
            await _communication.WriteAsync($":MEASure:SOURce CHANnel{channel}");
            string response = await _communication.QueryAsync(":MEASure:BANDwidth?");
            return double.Parse(response, CultureInfo.InvariantCulture);
        }

        

        public async Task ApplyAutoScaleAsync()
        {
            try
            {
                await QueryAsync(":AUTOSCALE"); // Send the Auto Scale command to the oscilloscope
                await Task.Delay(200); // Small delay to allow the oscilloscope to adjust
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying auto scale: {ex.Message}");
            }
        }

    }
}
