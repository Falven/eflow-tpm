#Requires -RunAsAdministrator
Write-Host "Verifying EFLOW installation..." -ForegroundColor "green"
if (-Not (Verify-EflowVm)) {
    Write-Host "IoT Edge for Linux on Windows virtual machine was not created succesfully." -ForegroundColor "red"
    exit 1
}
Write-Host "EFLOW installed. Building solution..." -ForegroundColor "green"
dotnet build "tpm-read-nv.sln" -c "Release" -p:DeployOnBuild=true -p:MyRuntimeIdentifier=linux-x64
if($LASTEXITCODE -ne 0)
{
    Write-Host "Error building solution." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Tar'ing binaries..." -ForegroundColor "green"
tar -C .\tpm-read-nv\bin\Release\net6.0 -cvf tpm-read-nv.tar linux-x64
if($LASTEXITCODE -ne 0)
{
    Write-Host "Error tar'ing binaries." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Copying binaries to EFLOW..." -ForegroundColor "green"
Copy-EflowVmFile -fromFile tpm-read-nv.tar -toFile ~/tpm-read-nv.tar -pushFile
if($LASTEXITCODE -ne 0)
{
    Write-Host "Error copying binaries to EFLOW." -ForegroundColor "red"
    exit $LASTEXITCODE
}
Write-Host "Success." -ForegroundColor "green"