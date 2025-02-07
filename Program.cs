using System;
using System.Windows.Forms;
using Oscilloscope.Forms;
using Oscilloscope.Communication;

namespace Oscilloscope
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Uncomment this section to run the SCPI test instead of launching the GUI
            /*
            RunScpiTest();
            return;
            */

            // Launch the GUI
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fatal error: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static async Task Main(string[] args)
        {
            string visaAddress = "USB0::0x2A8D::0x1770::MY58491960::0::INSTR"; // VISA adresinizi girin
            var visaCommunication = new VisaCommunication(visaAddress);

            try
            {
                Console.WriteLine("Connecting to oscilloscope...");
                if (await visaCommunication.ConnectAsync())
                {
                    Console.WriteLine("Connected successfully!");

                    string idnResponse = await visaCommunication.QueryAsync("*IDN?");
                    Console.WriteLine("Oscilloscope Response: " + idnResponse);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            finally
            {
                visaCommunication.Dispose();
            }
        }


    }
}
