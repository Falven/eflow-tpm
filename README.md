## Requirements

EFLOW and .NET 6 SDK

### EFLOW

https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-on-windows-x509?view=iotedge-2020-11&tabs=azure-portal%2Cpowershell#install-iot-edge

Modify EFLOW ps1 to allow TPM2

Add the `-IncludeHidden` flag to all of the `Get-VMNetworkAdapter` and `Get-NetAdapter` commandlets in:
C:\Program Files\WindowsPowerShell\Modules\AzureEFLOW\AzureEFLOW.psm1:3202-3228

### .NET Core 6

https://dotnet.microsoft.com/en-us/download/dotnet/6.0

### Running

Run the DeployTPMEFLOW.ps1 script from an elevated Powershell For Windows session.

### Known issues

Does not work from Powershell Core.
