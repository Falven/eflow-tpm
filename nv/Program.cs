using System.Runtime.InteropServices;
using System.Security.Principal;
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

    private const int DefaultAuthValueSize = 32;

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
                        throw new InvalidOperationException("Writing to TPM is currently not supported under this platform.");
                    }
                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Could not find file to write to TPM.", path);
                    }

                    NVWrite(tpm, (int)index, path);
                }

                if (read)
                {
                    NVRead(tpm, (int)index);
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

    internal class TbsWrapper
    {
        public class NativeMethods
        {
            [DllImport("tbs.dll", CharSet = CharSet.Unicode)]
            internal static extern TBS_RESULT
            Tbsi_Context_Create(
                ref TBS_CONTEXT_PARAMS ContextParams,
                ref UIntPtr Context);

            [DllImport("tbs.dll", CharSet = CharSet.Unicode)]
            internal static extern TBS_RESULT
            Tbsip_Context_Close(
                UIntPtr Context);

            [DllImport("tbs.dll", CharSet = CharSet.Unicode)]
            internal static extern TBS_RESULT
                Tbsi_Get_OwnerAuth(
                UIntPtr Context,
                [System.Runtime.InteropServices.MarshalAs(UnmanagedType.U4), In]
                TBS_OWNERAUTH_TYPE OwnerAuthType,
                [System.Runtime.InteropServices.MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3), In, Out]
                 byte[] OutBuffer,
                ref uint OutBufferSize);
        }

        public enum TBS_RESULT : uint
        {
            TBS_SUCCESS = 0,
            TBS_E_BLOCKED = 0x80280400,
            TBS_E_INTERNAL_ERROR = 0x80284001,
            TBS_E_BAD_PARAMETER = 0x80284002,
            TBS_E_INSUFFICIENT_BUFFER = 0x80284005,
            TBS_E_COMMAND_CANCELED = 0x8028400D,
            TBS_E_OWNERAUTH_NOT_FOUND = 0x80284015
        }

        public enum TBS_OWNERAUTH_TYPE : uint
        {
            TBS_OWNERAUTH_TYPE_FULL = 1,
            TBS_OWNERAUTH_TYPE_ADMIN = 2,
            TBS_OWNERAUTH_TYPE_USER = 3,
            TBS_OWNERAUTH_TYPE_ENDORSEMENT = 4,
            TBS_OWNERAUTH_TYPE_ENDORSEMENT_20 = 12,
            TBS_OWNERAUTH_TYPE_STORAGE_20 = 13
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TBS_CONTEXT_PARAMS
        {
            public TBS_CONTEXT_VERSION Version;
            public TBS_CONTEXT_CREATE_FLAGS Flags;
        }

        public enum TBS_CONTEXT_VERSION : uint
        {
            ONE = 1,
            TWO = 2
        }

        public enum TBS_CONTEXT_CREATE_FLAGS : uint
        {
            RequestRaw = 0x00000001,
            IncludeTpm12 = 0x00000002,
            IncludeTpm20 = 0x00000004,
        }
    }

    static bool GetOwnerAuthFromOS(out byte[] ownerAuth)
    {
        ownerAuth = new byte[0];

        TbsWrapper.TBS_CONTEXT_PARAMS contextParams;
        var tbsContext = UIntPtr.Zero;
        contextParams.Version = TbsWrapper.TBS_CONTEXT_VERSION.TWO;
        contextParams.Flags = TbsWrapper.TBS_CONTEXT_CREATE_FLAGS.IncludeTpm20;
        var result = TbsWrapper.NativeMethods.Tbsi_Context_Create(ref contextParams, ref tbsContext);

        if (result != TbsWrapper.TBS_RESULT.TBS_SUCCESS)
        {
            return false;
        }
        if (tbsContext == UIntPtr.Zero)
        {
            return false;
        }

        uint ownerAuthSize = 0;
        TbsWrapper.TBS_OWNERAUTH_TYPE ownerType = TbsWrapper.TBS_OWNERAUTH_TYPE.TBS_OWNERAUTH_TYPE_STORAGE_20;
        result = TbsWrapper.NativeMethods.Tbsi_Get_OwnerAuth(tbsContext, ownerType, ownerAuth, ref ownerAuthSize);
        if (result != TbsWrapper.TBS_RESULT.TBS_SUCCESS &&
            result != TbsWrapper.TBS_RESULT.TBS_E_INSUFFICIENT_BUFFER)
        {
            ownerType = TbsWrapper.TBS_OWNERAUTH_TYPE.TBS_OWNERAUTH_TYPE_FULL;
            result = TbsWrapper.NativeMethods.Tbsi_Get_OwnerAuth(tbsContext, ownerType, ownerAuth, ref ownerAuthSize);
            if (result != TbsWrapper.TBS_RESULT.TBS_SUCCESS &&
                result != TbsWrapper.TBS_RESULT.TBS_E_INSUFFICIENT_BUFFER)
            {
                Console.WriteLine(Globs.GetResourceString("Failed to get ownerAuth."));
                return false;
            }
        }

        ownerAuth = new byte[ownerAuthSize];
        result = TbsWrapper.NativeMethods.Tbsi_Get_OwnerAuth(tbsContext, ownerType, ownerAuth, ref ownerAuthSize);
        if (result != TbsWrapper.TBS_RESULT.TBS_SUCCESS)
        {
            Console.WriteLine(Globs.GetResourceString("Failed to get ownerAuth."));
            return false;
        }

        TbsWrapper.NativeMethods.Tbsip_Context_Close(tbsContext);

        return true;
    }

    private static void NVWrite(Tpm2 tpm, int nvIndex, string path)
    {
        if (tpm._GetUnderlyingDevice().GetType() != typeof(TbsDevice))
        {
            return;
        }

        TpmHandle nvHandle = TpmHandle.NV(nvIndex);
        //
        // The NV auth value is required to read and write the NV slot after it has been
        // created. Because this test is supposed to be used in different conditions:
        // first as Administrator to create the NV slot, and then as Standard User to read
        // it, the test uses a well defined authorization value.
        //
        // In a real world scenario, tha authorization value should be bigger and random,
        // or include a policy with better policy. For example, the next line could be
        // substitued with:
        // AuthValue nvAuth = AuthValue.FromRandom(32);
        // which requires storage of the authorization value for reads.
        //
        AuthValue nvAuth = AuthValue.FromRandom(DefaultAuthValueSize);

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.WriteLine("Running as Administrator. Deleting and re-creating NV entry.");

            //
            // AuthValue encapsulates an authorization value: essentially a byte-array.
            // OwnerAuth is the owner authorization value of the TPM-under-test.  We
            // assume that it (and other) auths are set to the default (null) value.
            // If running on a real TPM, which has been provisioned by Windows, this
            // value will be different. An administrator can retrieve the owner
            // authorization value from the registry.
            //
            byte[] ownerAuth;
            if (GetOwnerAuthFromOS(out ownerAuth))
            {
                tpm.OwnerAuth = ownerAuth;
            }
            else
            {
                Console.WriteLine("Could not retrieve owner auth from registry. Trying empty auth.");
            }

            bool failed;
            do
            {
                failed = false;
                //
                // Clean up any slot that was left over from an earlier run.
                // Only clean up the nvIndex if data from a possible previous invocation
                // should be deleted.
                //
                // Another approach could be to invoke NvDefineSpace, check if the call
                // returns TpmRc.NvDefined, then try a read with the known/stored
                // NV authorization value. If that succeeds, the likelyhood that this
                // NV index already contains valid data is high.
                // 
                tpm._AllowErrors().NvUndefineSpace(TpmHandle.RhOwner, nvHandle);

                //
                // Define the NV slot. The authorization passed in as nvAuth will be
                // needed for future NvRead and NvWrite access. (Attribute Authread
                // specifies that authorization is required to read. Attribute Authwrite
                // specifies that authorization is required to write.)
                // 
                try
                {
                    tpm.NvDefineSpace(TpmHandle.RhOwner, nvAuth,
                                             new NvPublic(nvHandle, TpmAlgId.Sha1,
                                                          NvAttr.Authread | NvAttr.Authwrite,
                                                          new byte[0], 32));
                }
                catch (TpmException e)
                {
                    if (e.RawResponse == TpmRc.NvDefined)
                    {
                        nvIndex++;
                        nvHandle = TpmHandle.NV(nvIndex);
                        Console.WriteLine("NV index already taken, trying next.");
                        failed = true;
                    }
                    else
                    {
                        Console.WriteLine("Exception {0}\n{1}", e.Message, e.StackTrace);
                        return;
                    }
                }

                //
                // Store successful nvIndex and nvAuth, so next invocation as client
                // knows which index to read. For instance in registry. Storage of 
                // nvAuth is only required if attributes of NvDefineSpace include 
                // NvAttr.Authread.
                // 
            } while (failed);

            var nvData = File.ReadAllBytes(path);

            //
            // Now that NvDefineSpace succeeded, write some random data (nvData) to
            // nvIndex. Note that NvDefineSpace defined the NV slot to be 32 bytes,
            // so a NvWrite (nor NvRead) should try to write more than that.
            // If more data has to be written to the NV slot, NvDefineSpace should
            // be adjusted accordingly.
            // 
            Console.WriteLine("Writing NVIndex {0}.", nvIndex);
            var nvLengthBytes = BitConverter.GetBytes(nvData.Length);
            tpm[nvAuth].NvWrite(nvHandle, nvHandle, nvLengthBytes, 0);
            Console.WriteLine("Wrote nvData length: {0}", BitConverter.ToString(nvData));
            tpm[nvAuth].NvWrite(nvHandle, nvHandle, nvData, (ushort)(nvLengthBytes.Length - 1));
            Console.WriteLine("Wrote nvData: {0}", BitConverter.ToString(nvData));
        }

        Console.WriteLine("NV access complete.");
    }

    private static void NVRead(Tpm2 tpm, int index)
    {
        var nvHandle = TpmHandle.NV(index);
        var nvAuth = AuthValue.FromRandom(DefaultAuthValueSize);
        Console.WriteLine("Reading NVIndex {0}.", index);
        var nvLengthBytes = tpm[nvAuth].NvRead(nvHandle, nvHandle, (ushort)sizeof(ushort), 0);
        var nvLength = BitConverter.ToUInt16(nvLengthBytes);
        Console.WriteLine("Read Data length: {0}\nReading key.", nvLength);
        var nvData = tpm[nvAuth].NvRead(nvHandle, nvHandle, ushort.MaxValue, (ushort)(nvLength - 1));
        Console.WriteLine("Read Bytes: {0}", BitConverter.ToString(nvData));
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