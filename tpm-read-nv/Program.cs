/*
* Copyright (c) 2021  Microsoft Corporation
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Tpm2Lib;

class Program
{
    /// <summary>
    /// Defines the argument to use to have this program use a Linux TPM device
    /// file or TPM access broker to communicate with a TPM 2.0 device.
    ///
    /// NOTE: Use this device in order to communicate to the EFLOW VM
    /// </summary>
    private const string DeviceLinux = "-tpm0";

    /// <summary>
    /// Defines the argument to use to have this program use a TCP connection
    /// to communicate with a TPM 2.0 simulator.
    /// </summary>
    private const string DeviceSimulator = "-tcp";

    /// <summary>
    /// Defines the argument to use to have this program use the Windows TBS
    /// API to communicate with a TPM 2.0 device.
    /// Use this device if you are testing the TPM Read functionality on the Windows host.
    /// </summary>
    private const string DeviceWinTbs = "-tbs";


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

    /// <summary>
    /// Prints instructions for usage of this progsram.
    /// </summary>
    static void WriteUsage()
    {
    }  

    /// <summary>
    /// Parse the arguments of the program and return the selected values.
    /// </summary>
    /// <param name="args">The arguments of the program.</param>
    /// <param name="tpmDeviceName">The name of the selected TPM connection created.</param>
    /// <returns>True if the arguments could be parsed. False if an unknown argument or malformed
    /// argument was present.</returns>
    static bool ParseArguments(IEnumerable<string> args, out string tpmDeviceName)
    {
        tpmDeviceName = "";
        return true;
    }

    /// <summary>
    /// After parsing the arguments for the TPM device, the program executes a read
    /// of the prior initialized NV index (3001). The program then outputs the 8 bytes
    /// previously stored at that index.
    /// </summary>
    /// <param name="args">Arguments to this program.</param>
    static void Main(string[] args)
    {
        //
        // Parse the program arguments. If the wrong arguments are given or
        // are malformed, then instructions for usage are displayed and 
        // the program terminates.
        // 
        string tpmDeviceName;
        tpmDeviceName = DeviceLinux;
        if (!ParseArguments(args, out tpmDeviceName))
        {
            //WriteUsage();
            //return;                                                                                                                                                                   
        }

        try
        {
            Tpm2Device tpmDevice;
                    tpmDevice = new LinuxTpmDevice();
/*
            switch (tpmDeviceName)
            {
                case DeviceSimulator:
                    tpmDevice = new TcpTpmDevice(DefaultSimulatorName, DefaultSimulatorPort);
                    break;
                case DeviceWinTbs:

                    break;
                case DeviceLinux:
                    tpmDevice = new LinuxTpmDevice();
                    break;
                default:
                    throw new Exception("Unknown device selected.");
            }
*/
            tpmDevice.Connect();

            var tpm = new Tpm2(tpmDevice);
            if (tpmDevice is TcpTpmDevice)
            {
                //
                // If we are using the simulator, we have to do a few things the
                // firmware would usually do. These actions have to occur after
                // the connection has been established.
                // 
                tpmDevice.PowerCycle();
                tpm.Startup(Su.Clear);
            }


		int index = args[0].Length > 0 ? Int32.Parse(args[0]) : 31337;
		string fileName = args[1];
		int lenght = Int32.Parse(args[2]);
		   
            NVReadOnly(tpm, index, fileName, length);

            // TPM clean up procedure 
            tpm.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception occurred: {0}", e.Message);
        }
    }


    /// <summary>
    /// This sample demonstrates the creation and use of TPM NV memory storage.
    /// NOTE: Only reads from the TPM NV Memory are supported on the EFLOW VM.
    /// In order to properly run through this sample, you must have previously
    /// setup and written to NV Index 3001 on the Windows Host. See README.md
    /// for details.
    /// </summary>
    /// <param name="tpm">Reference to TPM object.</param>
    static void NVReadOnly(Tpm2 tpm, int index, string fileName, int length)
    {
        //
        // AuthValue encapsulates an authorization value: essentially a byte-array.
        // OwnerAuth is the owner authorization value of the TPM-under-test.  We
        // assume that it (and other) auths are set to the default (null) value.
        // If running on a real TPM, which has been provisioned by Windows, this
        // value will be different. An administrator can retrieve the owner
        // authorization value from the registry.
        //

        int nvIndex = index; // Arbitrarily Chosen
        ushort dataLength = length; // Length from the data stored into the TPM

        TpmHandle nvHandle = TpmHandle.NV(nvIndex);
        AuthValue nvAuth = new AuthValue(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        byte[] nvRead = tpm[nvAuth].NvRead(nvHandle, nvHandle, dataLength, 0);
	  File.WriteAllBytes(fileName, nvRead);
        Console.WriteLine("Read Bytes: {0}", nvRead.Length);
    }
}