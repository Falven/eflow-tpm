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
    /// Defines the argument to use to have this program use the Windows TBS
    /// API to communicate with a TPM 2.0 device.
    /// Use this device if you are testing the TPM Read functionality on the Windows host.
    /// </summary>
    private const string DeviceWinTbs = "tbs";

    /// <summary>
    /// The default connection to use for communication with the TPM.
    /// </summary>
    private const string DefaultDevice = DeviceLinux;

    private const int DefaultNVIndex = 3001;

    private static bool verbose = false;

    static int Main(string[] args)
    {
        Guid? authValue = null;
        string? device = DefaultDevice;
        int index = DefaultNVIndex;
        string? path = null;
        bool read = false;
        bool help = false;
        var options = new OptionSet {
            { "a|authvalue=", "Authorization GUID used for accessing TPM device memory.", a => authValue = Guid.Parse(a) },
            { "d|device:", GetDeviceOptions(), d => device = d },
            { "i|index:", String.Format("The index in authorized TPM memory to read from or write to (Defaults to {0}).", DefaultNVIndex), (int i) => index = i },
            { "w|write:", "Fully qualified path to a file containing data to write to TPM device memory.", w => path = w },
            { "r|read", "Whether to read from the TPM device.", r => read = true },
            { "v|verbose", v => verbose = true },
            { "h|help", h => help = true },
        };

        List<string> extra;
        try
        {
            if (args.Length == 0)
            {
                PrintUsage(options);
                return 0;
            }

            // parse the command line
            extra = options.Parse(args);

            if (help)
            {
                PrintUsage(options);
                return 0;
            }

            if (authValue == null)
            {
                throw new InvalidOperationException("Invalid authvalue option -a.");
            }

            Tpm2Device tpmDevice;
            switch (device)
            {
                case DeviceWinTbs:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        throw new InvalidOperationException(String.Format("Device {0} is not supported for this platform.", device));
                    }
                    tpmDevice = new TbsDevice();
                    break;
                case DeviceLinux:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        throw new InvalidOperationException(String.Format("Device {0} is not supported for this platform.", device));
                    }
                    tpmDevice = new LinuxTpmDevice();
                    break;
                default:
                    throw new InvalidOperationException("Invalid device option -d.\n" + GetDeviceOptions());
            }

            tpmDevice.Connect();

            using (var tpm = new Tpm2(tpmDevice))
            {
                if (path != null)
                {
                    if (tpm._GetUnderlyingDevice().GetType() != typeof(TbsDevice))
                    {
                        throw new InvalidOperationException("Writing to this device type is currently not supported.");
                    }

                    if (!File.Exists(path))
                    {
                        throw new FileNotFoundException("Could not find file to write to TPM.", path);
                    }

                    NVAuthWrite(tpm, (Guid)authValue, index, path);
                }

                if (read)
                {
                    NVRead(tpm, (Guid)authValue, index);
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

    private static void NVAuth(Tpm2 tpm, TpmHandle nvHandle, AuthValue nvAuth, int nvIndex)
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.WriteLine("Running as Administrator.");

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
        }
    }

    private static void NVAuthWrite(Tpm2 tpm, Guid authValue, int nvIndex, string path)
    {
        var nvHandle = TpmHandle.NV(nvIndex);
        var nvAuth = new AuthValue(authValue.ToByteArray());
        var nvData = File.ReadAllBytes(path);
        var nvDataLength = (ushort)nvData.Length;
        var nvDataLengthBytes = BitConverter.GetBytes((ushort)nvDataLength);
        var nvDataLengthBytesLength = (ushort)nvDataLengthBytes.Length;
        NVAuth(tpm, nvHandle, nvAuth, nvIndex);
        tpm._AllowErrors().NvUndefineSpace(TpmHandle.RhOwner, nvHandle);
        tpm.NvDefineSpace(
            TpmHandle.RhOwner,
            nvAuth,
            new NvPublic(
                nvHandle,
                TpmAlgId.Sha1,
                NvAttr.Authread | NvAttr.Authwrite,
                new byte[0],
                (ushort)(nvDataLength + nvDataLengthBytesLength)
            )
        );
        LogLine(String.Format("Writing NVIndex {0}.", nvIndex));
        tpm[nvAuth].NvWrite(nvHandle, nvHandle, nvDataLengthBytes, 0);
        LogLine(String.Format("Wrote nvData length: {0}", nvDataLength));
        tpm[nvAuth].NvWrite(nvHandle, nvHandle, nvData, nvDataLengthBytesLength);
        LogLine(String.Format("Wrote nvData: {0}", BitConverter.ToString(nvData)));
    }

    private static void NVRead(Tpm2 tpm, Guid authValue, int nvIndex)
    {
        var nvHandle = TpmHandle.NV(nvIndex);
        var nvAuth = new AuthValue(authValue.ToByteArray());
        LogLine(String.Format("Reading NVIndex {0}.", nvIndex));
        var nvDataLengthBytes = tpm[nvAuth].NvRead(nvHandle, nvHandle, (ushort)sizeof(ushort), 0);
        var nvDataLength = BitConverter.ToUInt16(nvDataLengthBytes);
        LogLine(String.Format("Read Data length: {0}", nvDataLength));
        var nvData = tpm[nvAuth].NvRead(nvHandle, nvHandle, nvDataLength, (ushort)sizeof(ushort));
        LogLine(String.Format("Read Bytes: {0}", BitConverter.ToString(nvData)));
    }

    private static void LogLine(string message)
    {
        if (verbose)
        {
            Console.WriteLine(message);
        }
    }

    private static string GetDeviceOptions()
    {
        return String.Format("Required: Can be '{0}' or '{1}' (Defaults to '{2}')." +
            " If <device> is '{0}', the program will connect to the TPM via the TPM2 Access Broker on the EFLOW VM." +
            " If <device> is '{1}', the program will use the Windows TBS interface to talk to the TPM device (for use on testing within the Windows Host).",
        DeviceLinux, DeviceWinTbs, DefaultDevice);
    }

    private static void PrintUsage(OptionSet options)
    {
        Console.WriteLine(@"This program reads or writes to a TPM 2.0 device.
After parsing the arguments for the TPM device, the program reads or writes arbitrary data to the provided NV index.

  Usage: nv [OPTIONS]");
        options.WriteOptionDescriptions(Console.Out);
        Console.WriteLine("\n  Example: nv -d=tpm0 -i=3001 -r");
    }
}