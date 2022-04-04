using System.Runtime.InteropServices;
using Mono.Options;
using Tpm2Lib;

class Program
{
    /// <summary>
    /// Defines the argument to use to have this program use a Linux TPM device
    /// file or TPM access broker to communicate with a TPM 2.0 device.
    ///
    /// NOTE: Use this device in order to communicate to the EFLOW VM
    /// </summary>
    private const string DeviceLinux = "tpm0";

    /// <summary>
    /// Defines the argument to use to have this program use a TCP connection
    /// to communicate with a TPM 2.0 simulator.
    /// </summary>
    private const string DeviceSimulator = "tcp";

    /// <summary>
    /// Defines the argument to use to have this program use the Windows TBS
    /// API to communicate with a TPM 2.0 device.
    /// Use this device if you are testing the TPM Read functionality on the Windows host.
    /// </summary>
    private const string DeviceWinTbs = "tbs";


    /// <summary>
    /// The default connection to use for communication with the TPM.
    /// </summary>
    private const string DefaultDevice = DeviceLinux;

    /// <summary>
    /// If using a TCP connection, the default DNS name/IP address for the
    /// simulator.
    /// </summary>
    private const string DefaultSimulatorName = "127.0.0.1";

    /// <summary>
    /// If using a TCP connection, the default TCP port of the simulator.
    /// </summary>
    private const int DefaultSimulatorPort = 2321;

    static int Main(string[] args)
    {
        string? device = null;
        int? index = null;
        string? path = null;
        bool read = false;
        bool help = false;
        var options = new OptionSet {
            { "d|device=", GetDeviceOptions(), d => device = d },
            { "i|index=", "Required: The index in TPM memory to read from or write to.", (int i) => index = i },
            { "w|write=", "Fully qualified path to the file containing data to write to the TPM device.", w => path = w },
            { "r|read", "Whether to read from the TPM device.", r => read = true },
            { "h|help", h => help = true },
        };

        List<string> extra;
        try
        {
            // parse the command line
            extra = options.Parse(args);

            if (help)
            {
                PrintUsage(options);
                return 0;
            }

            if (device == null)
            {
                throw new InvalidOperationException("Missing required option -d");
            }

            if (index == null)
            {
                throw new InvalidOperationException("Missing required option -i");
            }

            Tpm2Device tpmDevice;
            switch (device)
            {
                case DeviceSimulator:
                    tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
                    break;
                case DeviceWinTbs:
                    tpmDevice = new TbsDevice();
                    break;
                case DeviceLinux:
                    tpmDevice = new LinuxTpmDevice();
                    break;
                default:
                    throw new InvalidOperationException("Invalid device option -d.\n" + GetDeviceOptions());
            }

            tpmDevice.Connect();

            using (var tpm = new Tpm2(tpmDevice))
            {
                if (tpmDevice is TcpTpmDevice)
                {
                    tpmDevice.PowerCycle();
                    tpm.Startup(Su.Clear);
                }

                if (path != null)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        throw new InvalidOperationException("Writing to TPM is currently unsupported under this platform.");
                    }
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Could not find file to write to TPM.", write);
                    }

                    NVWrite(path, tpm);
                }

                if (read)
                {
                    NVRead(tpm);
                }
            }
        }
        catch (OptionException)
        {
            PrintUsage(options);
            return 1;
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception occurred: {0}", e.Message);
        }
        return 0;
    }

    private static void NVWrite(string path, Tpm2 tpm)
    {
        throw new NotImplementedException();
    }

    private static void NVRead(Tpm2 tpm)
    {
        throw new NotImplementedException();
    }

    private static string GetDeviceOptions()
    {
        return String.Format("Required: Can be '{0}' or '{1}' or '{2}'. Defaults to '{3}'." +
            " If <device> is '{0}', the program will connect to the TPM via the TPM2 Access Broker on the EFLOW VM." +
            " If <device> is '{1}', the program will connect to a simulator listening on a TCP port." +
            " If <device> is '{2}', the program will use the Windows TBS interface to talk to the TPM device (for use on testing within the Windows Host).",
        DeviceLinux, DeviceSimulator, DeviceWinTbs, DefaultDevice);
    }

    private static void PrintUsage(OptionSet options)
    {
        Console.WriteLine(@"This program reads or writes to a TPM 2.0 device.
After parsing the arguments for the TPM device, the program reads or writes arbitrary data to the provided NV index.

  Usage: nv [OPTIONS]");
        options.WriteOptionDescriptions(Console.Out);
    }


}