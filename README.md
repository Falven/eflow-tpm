## Requirements

EFLOW and .NET 6 SDK

### EFLOW

https://docs.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-on-windows-x509?view=iotedge-2020-11&tabs=azure-portal%2Cpowershell#install-iot-edge

Modify EFLOW ps1 to allow TPM2

Add the `-IncludeHidden` flag to all of the `Get-NetAdapter` commandlets in:
C:\Program Files\WindowsPowerShell\Modules\AzureEFLOW\AzureEFLOW.psm1:3202-3228

Remove-Module AzureEFLOW
Import-Module AzureEFLOW
Start-EflowVm
Set-EflowVmFeature -feature 'DpsTpm' -Enable

https://github.com/microsoft/TSS.MSR/tree/master/TSS.NET/Samples/NV%20(Windows)
openssh -> generate key
use cryptsetup to encrypt partition create filsystem...
use encryption key to decrypt partition

cryptsetup -c aes-xts-plain64 --key-size 512 --hash sha512 --time 5000 --use-urandom /dev/sdb1
cryptsetup open /dev/sdb1 encrypted

cryptsetup luksFormat --type luks2 /dev/sda10
cryptsetup luksOpen /dev/sda10 datadrive

echo -e "n\n10\n\n\nw" | sudo fdisk /dev/sda
sudo cryptsetup luksFormat /dev/sda10

1. Take key we set up partition with cryptsetup.
2. Write that key from windows with NV R/W tool.
3. Read that key from the TPM from linux and use it to decrypt partition.

### .NET Core 6

https://dotnet.microsoft.com/en-us/download/dotnet/6.0

### Running

Run the DeployTPMEFLOW.ps1 script from an elevated Powershell For Windows session.

### Known issues

Does not work from Powershell Core.
