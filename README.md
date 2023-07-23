# The Sorter
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

## Description

The Sorter is a simple web program which is aimed at simplifying the process of sorting various files via simple control panel. Additional features of the Sorter is generation of thumbnails of photos, so that their loading during sorting is much faster than the classic several MB files, and simultaneous support for multiple sessions which do not collide with each other (disabled by default). In the future, cross-platform support is planned.

## Installation

To run the application, simply download the latest version appropriate for your operating system from the [releases](https://github.com/kubbit98/Sorter/releases). 

## Configuration

The basic and most important configuration is done inside the application, is persistent and saved in the config.json file

## Advanced configuration
You can look at some deeper settings in [appsettings.json](https://github.com/kubbit98/Sorter/blob/master/appsettings.json) file:
- Kestrel
 
  (object) 

  Settings of kestrel web server, the settings are many and there is no reason for me to list them all, some of them can be found in the [link below](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-6.0)
  - Endpoints.Http.Url

    (string) Default: http://localhost:5000
 
    By default, the application starts in http mode and is only accessible as localhost, you can change this for example to "http://0.0.0.0:5005" to make the application available on the local network on port 5005.
- UseThumbnails:
  
  (boolean) Default: true
  
  The application can speed up the loading of subsequent files with thumbnails it can generate moments before a file is used. The acceleration involves sending tiny files to the browser instead of full multi-megabyte images, only to view them for a few seconds. However, this behavior minimally increases CPU usage (we have to generate the thumbnail) and saves the thumbnail in the appropriate folder.
- TempPath

  (string/path) Default: Temp

  If the application has thumbnail generation enabled, it needs to save them somewhere. So it saves them in the folder indicated in the above parameter, by default it creates a Temp folder in the application folder. For example, you can set it to "c:\users\tempFiles".

## Roadmap

This is an early version of the project. Therefore, some unknowns changes are expected. I intend to improve the interface, make it comfortable in use, increase the number of supported files and I want to make sure that the program works on multiple platforms.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

[GPL-3.0](https://choosealicense.com/licenses/gpl-3.0/)