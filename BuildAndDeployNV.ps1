#
# 'BuildAndDeployNV'
#
# Generated by: Fran Aguilera
#
# Generated on: 04/01/2022
#
#Requires -RunAsAdministrator
Import-Module AzureEFLOW
Write-Host "Verifying EFLOW installation..." -ForegroundColor "green"
if (-Not (Verify-EflowVm)) {
    Write-Host "IoT Edge for Linux on Windows virtual machine was not created succesfully." -ForegroundColor "red"
    exit 1
}
Write-Host "EFLOW installed. Building solution..." -ForegroundColor "green"
dotnet build ".\nv\nv.csproj" -c "Release" -p:DeployOnBuild=true -p:MyRuntimeIdentifier=linux-x64
dotnet build ".\nv\nv.csproj" -c "Release" -p:DeployOnBuild=true -p:MyRuntimeIdentifier=win-x64
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error building solution." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Tar'ing binaries..." -ForegroundColor "green"
cd ".\nv\bin\Release\net6.0"
tar -cf "nv.tar" "linux-x64"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error tar'ing binaries." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Copying binaries to EFLOW home directory..." -ForegroundColor "green"
Copy-EflowVmFile -fromFile "nv.tar" -toFile "~/nv.tar" -pushFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error copying binaries to EFLOW." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Success." -ForegroundColor "green"