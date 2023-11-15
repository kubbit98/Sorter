# sorter app
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

## Description

This app is a simple web program which is aimed at simplifying the process of sorting various files via simple control panel. Additional features of the sorter app is generation of thumbnails of photos, so that their loading during sorting is much faster than the classic several MB files, and simultaneous support for multiple sessions which do not collide with each other (disabled by default).

The application has been tested on Windows 11, Linux (Fedora 38) and macOS (Sonoma). For macOS, however, it had to be compiled under macOS itself, for apple reasons (https://github.com/dotnet/runtime/issues/49091), so although nothing needs to be changed in the code, there will not be a downloadable executable package available in releases, since I don't have any macOS to package the app.

## Presentation

[![Watch the presentation](https://img.youtube.com/vi/h57BWJZT7WI/hqdefault.jpg)](https://youtu.be/h57BWJZT7WI)

## Installation

To run the application, simply download the latest version appropriate for your operating system from the [releases](https://github.com/kubbit98/Sorter/releases). 

## Configuration

The basic and most important configuration is done inside the application, it is persistent and saved in the config.json file

## Advanced configuration
You can look at some deeper settings in [appsettings.json](https://github.com/kubbit98/Sorter/blob/master/appsettings.json) file:
- Logging

  (object)

  Settings of logging in console, the default show some information about the operation of the server itself, but also information about moved files or problems with the application. For more information, see the official documentation https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0
- Kestrel
 
  (object) 

  Settings of kestrel web server, the settings are many and there is no reason for me to list them all, some of them can be found in the [link below](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-6.0)
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

  If the application has thumbnail generation enabled, it needs to save them somewhere. So it saves them in the folder indicated in the above parameter, by default it creates a Temp folder in the application folder. For example, you can set it to "c:\users\tempFiles". This folder is cleaned up every time the application is launched, so setting it wrong can cause the entire computer to crash irreversibly. If you don't know if you need to change it, you probably shouldn't change it.

## Roadmap

This is an early version of the project. Therefore, some unknowns changes are expected. I intend to improve the interface, make it comfortable in use, increase the number of supported files and I want to make sure that the program works on multiple platforms.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

[GPL-3.0](https://choosealicense.com/licenses/gpl-3.0/)
