using System;
using System.Threading.Tasks;
using Ivi.Visa.Interop;
using System.Globalization;

namespace Oscilloscope.Communication
{
    public class VisaCommunication : IDisposable
    {
        private readonly string visaAddress;
        private ResourceManager? resourceManager;
        private FormattedIO488? session;
        private bool isConnected;
        private bool disposed;

        public VisaCommunication(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("VISA address cannot be empty", nameof(address));

            visaAddress = address;
            isConnected = false;
            disposed = false;
        }

        public async Task<bool> ConnectAsync()
        {
            ThrowIfDisposed();

            try
            {
                await Task.Run(() =>
                {
                    Console.WriteLine("Creating VISA ResourceManager...");
                    resourceManager = new ResourceManager();
                    session = new FormattedIO488
                    {
                        IO = (IMessage)resourceManager.Open(visaAddress)
                    };

                    if (session.IO == null)
                        throw new Exception("Failed to open session with VISA address: " + visaAddress);

                    Console.WriteLine("Session opened successfully.");
                    session.IO.Timeout = 5000; // 5-second timeout

                    // Basic initialization
                    session.WriteString("*RST", true);
                    session.WriteString("*CLS", true);

                    isConnected = true;
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection failed: " + ex.Message);
                Dispose();
                throw new Exception($"Failed to connect to oscilloscope: {ex.Message}", ex);
            }
        }

        public async Task<string> QueryAsync(string command)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            try
            {
                return await Task.Run(() =>
                {
                    Console.WriteLine($"Sending command: {command}");
                    session!.WriteString(command, true);
                    return session.ReadString();
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to execute query '{command}': {ex.Message}", ex);
            }
        }

        public async Task WriteAsync(string command)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            try
            {
                await Task.Run(() =>
                {
                    Console.WriteLine($"Writing command: {command}");
                    session!.WriteString(command, true);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to write command '{command}': {ex.Message}", ex);
            }
        }

        public async Task SendCommandAsync(string command)
        {
            await WriteAsync(command);
        }

        public async Task<byte[]> ReadBinaryDataAsync()
        {
            ThrowIfDisposed();
            EnsureConnected();

            try
            {
                return await Task.Run(() =>
                {
                    session!.WriteString(":DISP:DATA? PNG", true);
                    return (byte[])session.ReadIEEEBlock(IEEEBinaryType.BinaryType_UI1, false, true);
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read binary data from oscilloscope: {ex.Message}", ex);
            }
        }

        public async Task<bool> VerifyConnectionAsync()
        {
            if (disposed || !isConnected || session == null)
                return false;

            try
            {
                string response = await QueryAsync("*IDN?");
                Console.WriteLine("Connection verified. Device ID: " + response);
                return true;
            }
            catch
            {
                isConnected = false;
                return false;
            }
        }

        private void EnsureConnected()
        {
            if (!isConnected || session == null)
                throw new InvalidOperationException("Not connected to oscilloscope");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(VisaCommunication));
        }

        public void Dispose()
        {
            if (disposed) return;

            try
            {
                session?.IO.Close();
                session = null;
                resourceManager = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during session cleanup: " + ex.Message);
            }
            finally
            {
                isConnected = false;
                disposed = true;
            }
        }

        public bool IsConnected => isConnected;

        public async Task<string> ReadResponseAsync()
        {
            ThrowIfDisposed();
            EnsureConnected();

            try
            {
                return await Task.Run(() =>
                {
                    return session!.ReadString();
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to read response from oscilloscope: {ex.Message}", ex);
            }
        }

    }
}
