param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$VcpkgRoot = $env:VCPKG_ROOT,
    [string]$ValhallaSource,
    [switch]$ManagedOnly
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if ($ManagedOnly) {
        dotnet build ValhallaSharp.sln -c $Configuration -p:BuildNative=false --configfile NuGet.Config
    } else {
        if (!$VcpkgRoot -or !(Test-Path (Join-Path $VcpkgRoot 'scripts/buildsystems/vcpkg.cmake'))) {
            throw 'Provide -VcpkgRoot pointing to a bootstrapped vcpkg checkout (or set VCPKG_ROOT). See README.md.'
        }
        $cmakeCommand = Get-Command cmake -ErrorAction SilentlyContinue
        $cmakePath = if ($cmakeCommand) { $cmakeCommand.Source } else {
            Get-ChildItem 'C:\Program Files\Microsoft Visual Studio' -Filter cmake.exe -Recurse -ErrorAction SilentlyContinue |
                Select-Object -First 1 -ExpandProperty FullName
        }
        if (!$cmakePath) { throw 'Install CMake or the Visual Studio C++ CMake tools.' }
        $toolchain = (Join-Path $VcpkgRoot 'scripts/buildsystems/vcpkg.cmake').Replace('\', '/')
        $buildArguments = @('build', 'ValhallaSharp.sln', '-c', $Configuration, '--configfile', 'NuGet.Config', "-p:CMakeExecutable=$cmakePath", "-p:NativeToolchainFile=$toolchain")
        if ($ValhallaSource) {
            $sourcePath = (Resolve-Path $ValhallaSource).Path.Replace('\', '/')
            $buildArguments += "-p:NativeValhallaSource=$sourcePath"
        }
        dotnet @buildArguments
    }
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }
} finally { Pop-Location }
