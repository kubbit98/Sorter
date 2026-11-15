# sorter app
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

## Description

This app is a simple web program which is aimed at simplifying the process of sorting various files via simple control panel. Additional features of the sorter app is generation of thumbnails of photos, so that their loading during sorting is much faster than the classic several MB files, and simultaneous support for multiple sessions which do not collide with each other.

Ready-to-run packages are available for Windows (x64) and Linux (x64). macOS isn't packaged, because the app has to be compiled on macOS itself for Apple reasons (https://github.com/dotnet/runtime/issues/49091) and I don't have a Mac to test it. It can be built from source there (see [Building from source](#building-from-source)), but HEIC photos are not supported on macOS.

The application supports keyboard shortcuts. Once the application is initialized, the a-z and 0-9 keys can be dynamically assigned to a folder in the settings, the spacebar opens or closes the photo zoom mode, the right and left arrows go forward and backward, and the F2 function key focuses the field to rename a file (you can then exit with ESC, or save with Enter).

## Supported media types
- Photos: jpg, jpeg, jfif, jpe, png, gif, webp, apng, avif, bmp, heic, heif, hif and svg. HEIC/HEIF and BMP are converted for display (to JPG and PNG), because browsers can't show HEIF and only some BMP variants.
- Videos: formats your browser can play, e.g. mp4, webm, mov, ogv
- Audio recordings: mp3, wav, ogg
- Text documents: txt, csv, log, md, html, js, css
- PDF documents

Other files are listed too and can be sorted, they just aren't previewed.

## Presentation
You can watch the presentation here:

[![Watch the presentation](https://img.youtube.com/vi/D4gXkqrmgU8/hqdefault.jpg)](https://youtu.be/D4gXkqrmgU8)

## Installation

To run the application, simply download the latest version appropriate for your operating system from the [releases](https://github.com/kubbit98/Sorter/releases), extract it and run `Sorter` (`Sorter.exe` on Windows). Nothing else needs to be installed.

### Building from source

Building requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```
cd src/Sorter
dotnet run
```

## Configuration

The basic and most important configuration is done inside the application, it is persistent and saved in the config.json and keybinds.json files next to the application.

## Advanced configuration
You can look at some deeper settings in [appsettings.json](https://github.com/kubbit98/Sorter/blob/master/src/Sorter/appsettings.json) file:
- Logging

  (object)

  Settings of logging in console, the default show some information about the operation of the server itself, but also information about moved files or problems with the application. For more information, see the official documentation https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/
- Kestrel
 
  (object) 

  Settings of kestrel web server, the settings are many and there is no reason for me to list them all, some of them can be found in the [link below](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)
  - Endpoints.Http.Url

    (string) Default: http://localhost:5000
 
    By default, the application starts in http mode and is only accessible as localhost, you can change this for example to "http://0.0.0.0:5005" to make the application available on the local network on port 5005.
- Password:

  (string) Default: "" (empty)
 
  The application can be run on a local network (see Kestrel settings above), by this we may want to hide some of the configuration from unauthorized people. After entering the password in this file, the behavior of the program will change a bit:
  - Entering the configuration will require a password.
  - Resetting and shutting down the application (before reaching the final screen) will require a password.
  - In the application's configuration, it will be possible to choose whether users without the specified password will be able to rename files.
- UseThumbnails:
  
  (boolean) Default: true
  
  The application can speed up the loading of subsequent files with thumbnails it can generate moments before a file is used. The acceleration involves sending tiny files to the browser instead of full multi-megabyte images, only to view them for a few seconds. However, this behavior minimally increases CPU usage (we have to generate the thumbnail) and saves the thumbnail in the appropriate folder.
- TempPath

  (string/path) Default: "Temp"

  If the application has thumbnail generation enabled, it needs to save them somewhere. So it saves them in the folder indicated in the above parameter, by default it creates a Temp folder in the application folder. For example, you can set it to "c:\users\tempFiles". **Everything inside this folder is deleted every time the application starts**, so never point it at a folder containing anything else. If you don't know if you need to change it, you probably shouldn't change it.

## Roadmap

This is an early version of the project. Therefore, some unknowns changes are expected. I intend to improve the interface, make it comfortable in use, increase the number of supported files and I want to make sure that the program works on multiple platforms.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

The app lives in `src/Sorter`, automated tests (xUnit) in `tests/Sorter.Tests`. Run them with `dotnet test`, they also run in CI on Ubuntu and Windows for every pull request.

### Test files

`src/Sorter/TestFolder/generate_test_files.py` prepares a manual test environment: it fills `src/Sorter/TestFolder/src` with sample files of every supported type (including edge cases like broken files or special characters in names) and recreates empty destination folders `src/Sorter/TestFolder/dst/A`–`E`. It requires Python with `pillow` and `pillow-heif`, and `ffmpeg` (LibreOffice is optional).

```
pip install pillow pillow-heif
python src/Sorter/TestFolder/generate_test_files.py --clean
```

Then, when running in the Development environment, use "Load default config for testing" in the configuration dialog to point the app at these folders.

### Releasing

Releases are built by GitHub Actions. The version comes only from the git tag, local builds are versioned `0.0.0-dev`.

```
git tag v0.13.0-beta
git push origin v0.13.0-beta
```

The workflow builds and tests the app, publishes the Windows and Linux packages using the profiles in `src/Sorter/Properties/PublishProfiles` and creates a draft release with generated notes. Review the notes and publish the draft. The same packages can be built locally with `dotnet publish src/Sorter/Sorter.csproj -c Release -p:PublishProfile=win-x64` (or `linux-x64`).

## License

[GPL-3.0](https://choosealicense.com/licenses/gpl-3.0/)
