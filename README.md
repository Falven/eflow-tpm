# nv cli

A cli that reads or writes to a TPM 2.0 device.
After parsing the arguments for the TPM device, the program reads or writes arbitrary data to or from the provided NVIndex.
As of 1.0, this program supports both writing and reading from Windows and only reading from EFLOW/Linux.

## Requirements

.NET 6 SDK for building
Optionally, an EFLOW VM to take advantage of the CLI from within EFLOW.

### .NET Core 6

Follow well documented steps for setting up .NET 6.
https://dotnet.microsoft.com/en-us/download/dotnet/6.0

### EFLOW

Follow the section: 'Install IoT Edge' and optionally all others to set up an EFLOW VM.

https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-on-windows-x509?view=iotedge-2020-11&tabs=azure-portal%2Cpowershell#install-iot-edge

After setting up EFLOW the following article lists all available powershell functions for EFLOW.

https://docs.microsoft.com/en-us/azure/iot-edge/reference-iot-edge-for-linux-on-windows-functions?view=iotedge-2020-11#start-eflowvm

To finish setup, run EnableEFLOWTPM.ps1 from an elevated Powershell For Windows session to enable the TPM on the vm.

### Building

Run the BuildAndDeployNV.ps1 script from an elevated Powershell For Windows session to build and deploy the cli for both Windows and EFLOW.
The cli will be available from Windows in `C:\path\to\your\repo\eflow-tpm\nv\bin\Release\net6.0\win-x64` and the cli will be available from the EFLOW VM's home directory in a tar.

### Running

Usage statement (`-h -help`):

```text
This program reads or writes to a TPM 2.0 device.
After parsing the arguments for the TPM device, the program reads or writes arbitrary data to or from the provided NV index. As of 1.0, this program only supports writing and reading from Windows and only writing from EFLOW/Linux.

  Usage: nv [OPTIONS]
  -a, --authvalue=VALUE      Authorization GUID used for accessing TPM device
                               memory.
  -i, --index[=VALUE]        The index in authorized TPM memory to read from or
                               write to (Defaults to 3001).
  -w, --write[=VALUE]        Fully qualified path to a file containing data to
                               write to TPM device memory.
  -r, --read                 Whether to read from the TPM device.
  -v, --verbose
  -h, --help

  Example: nv -a=1be4e78e-01fb-4935-ac07-9128cfb18ba1 -r -v
```

#### Examples

```powershell
PS C:\> .\nv.exe -a=1be4e78e-01fb-4935-ac07-9128cfb18ba1 -w=C:\\source\\repos\\eflow-tpm\\test-key -v
Running as Administrator.
Writing NVIndex 3001.
Wrote NVData length: 8
Wrote NVData: 31-32-33-34-35-36-37-38
PS C:\> .\nv.exe -a=1be4e78e-01fb-4935-ac07-9128cfb18ba1 -r -v
Reading NVIndex 3001.
Read NVData length: 8
Read NVData: 31-32-33-34-35-36-37-38
12345678
```

### Known issues

Does not work from Powershell Core.

Weird internal print statements on Linux.
