using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Keysight33500BApp
{
    public class LanCommunication : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly string _ipAddress;
        private readonly int _port;
        private bool _connected;

        

        public LanCommunication(string ipAddress, int port = 5025)
        {
            _ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            _port = port;
            _connected = false;
        }

        public async Task ConnectAsync()
        {
            if (_connected) return;

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ipAddress, _port);
                _stream = _tcpClient.GetStream();
                _stream.ReadTimeout  = 3000; // 3s timeout
                _stream.WriteTimeout = 3000;
                _connected = true;
            }
            catch (Exception ex)
            {
                Dispose();
                throw new Exception($"Could not connect to {_ipAddress}:{_port}. {ex.Message}", ex);
            }
        }

        public async Task WriteAsync(string command)
        {
            if (!_connected || _stream == null)
                throw new InvalidOperationException("Not connected to instrument.");

            if (string.IsNullOrEmpty(command))
                throw new ArgumentException("Command cannot be null or empty.");

            try
            {
                // SCPI commands typically end with "\n"
                string cmd = command.EndsWith("\n") ? command : command + "\n";
                byte[] data = Encoding.ASCII.GetBytes(cmd);
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to write command '{command}': {ex.Message}", ex);
            }
        }

        public async Task<string> QueryAsync(string command)
        {
            await WriteAsync(command);

            // Read the response line from the instrument
            // 33500B typically terminates with "\n"
            return await ReadLineAsync();
        }

        private async Task<string> ReadLineAsync()
        {
            if (_stream == null)
                throw new InvalidOperationException("Stream is not open.");

            using var ms = new MemoryStream();
            byte[] buffer = new byte[1];

            // Read until we hit '\n'
            while (true)
            {
                int read = await _stream.ReadAsync(buffer, 0, 1);
                if (read < 1)
                    throw new IOException("Socket closed unexpectedly.");

                if (buffer[0] == (byte)'\n')
                    break;

                ms.Write(buffer, 0, read);
            }

            return Encoding.ASCII.GetString(ms.ToArray()).Trim();
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _tcpClient?.Close();
            _tcpClient = null;
            _stream = null;
            _connected = false;
        }
    }
}
