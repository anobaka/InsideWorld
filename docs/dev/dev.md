# Summary <!-- {docsify-ignore} -->

There are still works to do to optimize the dev procedure.

For now, you can start to develop this project by following instructions. 

Make sure you are working on `Windows`. `OS X` and `Linux` are not supported yet.

## Help wanted

If you are interested in this project, please feel free to contribute. 
Currently, we are expecting help on the following topics:
1. docs
2. more enhancers(such as nfo parser from jellyfin, etc.).
3. more downloaders.

## Setup

### 1. Get sources

Clone repositories below and put them into same folder.

+ [Current repo](https://github.com/anobaka/InsideWorld)
+ [LazyMortal](https://github.com/anobaka/LazyMortal) on `main` branch (by default)
+ [Bakabase.Infrastructures](https://github.com/anobaka/bakabase.infrastructures) on `main` branch (by default)

### 2. Install SDKs & Tools

+ [.NET 6.0](https://dot.net)
+ [Node.js v19.8.1](https://nodejs.org/) 
+ [Yarn v1.22.19](https://yarnpkg.com/getting-started/install)
+ Microsoft Edge

*Warning: Other versions have not been tested*

### 3. Install project dependencies

1. Restore all .NET project dependencies
2. Install node modules in `src/ClientApp` by `yarn install` (not `npm`)

> **For versions before v1.9.0**
> 1. If `Microsoft Edge` is not installed on your system, download `Microsoft.WebView2.FixedVersionRuntime.101.0.1210.53.x64` then put its contents into `Bakabase.InsideWorld.App.Wpf`, and make sure you get something like
    ```
    {this repo}/src/Bakabase.InsideWorld.App.Wpf/Microsoft.WebView2.FixedVersionRuntime.101.0.1210.53.x64/msedgewebview2.exe
    ```
    *You can get this package from [offical site](https://developer.microsoft.com/en-us/microsoft-edge/webview2) or the latest offline package of [InsideWorld](https://github.com/anobaka/InsideWorld/releases)*
> 2. Restore all .NET project dependencies
> 3. Install node modules in `src/Bakabase.InsideWorld.App.Core/ClientApp` by `yarn install` (not `npm`)

## Start this app

1. Run `yarn run start` in `src/ClientApp`
1. Set `Bakabase` as startup .NET project and run it

> **For versions before v1.9.0**
> 1. Run `yarn run start` in `src/Bakabase.InsideWorld.App.Core/ClientApp`
> 1. Set `Bakabase.InsideWorld.App.Wpf` as startup .NET project and run it