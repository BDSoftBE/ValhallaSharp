# ValhallaSharp

.NET 10 bindings for in-process Valhalla routing on Windows x64. `src/Valhalla.Bindings`
contains the C# library; `src/Valhalla.Native` builds a C ABI DLL that statically links
Valhalla and its third-party dependencies. No routing HTTP service is required.

## Build

Install Visual Studio 2022 or newer with **Desktop development with C++**, a modern
Windows SDK, CMake, Git, and a bootstrapped [vcpkg](https://github.com/microsoft/vcpkg)
checkout. The first native build needs internet access to fetch Valhalla, its
submodules, and dependency packages. The source revision is pinned in CMakeLists.txt.

```powershell
./build.ps1 -VcpkgRoot C:/dev/vcpkg
```

The manifest installs Boost, Protobuf, zlib, and pkgconf using
`x64-windows-static-md`. Valhalla tools, tile builders, HTTP services, Python,
GeoTIFF and LZ4 are disabled: build your tiles separately. Deployment needs the
Microsoft Visual C++ x64 runtime. The native build outputs `valhalla_sharp.dll`
under `artifacts/native/win-x64/Release`; MSBuild copies it beside the managed
assembly and into consuming project/publish output. `dotnet pack` includes an
existing built DLL under `runtimes/win-x64/native`.

An existing recursive checkout can be supplied with
`./build.ps1 -VcpkgRoot C:/dev/vcpkg -ValhallaSource C:/dev/valhalla`.
Use the pinned revision for compatibility. CMake can also be invoked directly;
the C# project exposes `CMakeExecutable` and `NativeCMakeArguments` properties.
Keep the native and managed build configuration consistent.

To compile only C# without native prerequisites:

```powershell
./build.ps1 -ManagedOnly
```

That mode does not produce a runnable routing engine. A native DLL is needed when
constructing an actor. Applications should target x64 (or run on an x64 .NET host).

## Routing with your tiles

Generate a **full Valhalla configuration** using `valhalla_build_config` from the
same Valhalla revision used to build your tiles. Set `mjolnir.tile_extract` to your
Valhalla tile tar extract, or `mjolnir.tile_dir` to a directory of graph tiles.
An arbitrary archive or OSM PBF is not a routing tile extract. Keep the service
limits and other generated settings in the configuration.

```csharp
using Valhalla.Bindings;

using var actor = ValhallaActor.FromConfigurationFile(
    "data/valhalla.json", tileExtractPath: "data/valhalla_tiles.tar");

string responseJson = actor.Route("""
    {
      "locations": [
        { "lat": 50.8503, "lon": 4.3517 },
        { "lat": 51.2194, "lon": 4.4025 }
      ],
      "costing": "auto",
      "units": "kilometers"
    }
    """);
Console.WriteLine(responseJson);
```

Configuration-file tile paths resolve relative to the configuration file. The
optional extract override resolves relative to the application's working directory.
You can also pass full configuration JSON to `new ValhallaActor(json)`; paths then
follow Valhalla's normal working-directory rules.

Route, locate, matrix, optimized route, isochrone, trace route, trace attributes,
and status accept Valhalla's JSON request schemas and return its text response.
Protobuf output is unsupported. Failures throw `ValhallaException`; malformed
JSON fails before entering native code. Each actor serializes requests and dispose,
owns its native handle, and cleans worker state after each action. For concurrent
routing use multiple actor instances. Requests are synchronous.

Valhalla and third-party license obligations apply when redistributing the native
DLL; retain the upstream licenses from the fetched sources and dependencies.

## Verify

```powershell
dotnet run --project tests/Valhalla.Bindings.SmokeTests -p:BuildNative=false
dotnet run --project tests/Valhalla.Bindings.SmokeTests -c Release -p:BuildNative=false -- --native
dotnet run --project tests/Valhalla.Bindings.SmokeTests -c Release -p:BuildNative=false -- data/valhalla.json data/route-request.json
```

The first command verifies managed validation. The second checks native loading
and configuration error propagation without tiles. The third needs a built Release
native DLL and your tiles; it checks an actual route, native error recovery and
disposal. Use a request with locations covered by your tiles.

## Tag releases

Push a version tag such as `v1.2.3` or `v1.2.3-beta.1` to trigger
`.github/workflows/publish-nuget.yml`. The workflow builds and smoke-tests the
Windows x64 native DLL, uses `dotnetCampus.TagToVersion` to update
`build/Version.props`, packs `Valhalla.Bindings`, verifies the DLL is included,
and publishes to NuGet.org with the repository secret `NUGET_API_KEY`.
The generated version change stays in the workflow checkout. Local builds default
to the version committed in `build/Version.props`.

Native build jobs and packaging are separate so macOS and Linux builds can be
added later. Adding a platform will also require its native build support,
artifact download, and runtime assets in the package. Currently the package
contains only `win-x64`; the Linux publish job simply uploads the finished package.

API and build references: [actor interface](https://github.com/valhalla/valhalla/blob/53e00619f642c32896dd77afa3ee2f792c6f75d3/valhalla/tyr/actor.h),
[Valhalla CMake](https://github.com/valhalla/valhalla/blob/53e00619f642c32896dd77afa3ee2f792c6f75d3/CMakeLists.txt).
