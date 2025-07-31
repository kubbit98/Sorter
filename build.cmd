SET name=%1
IF [%name%] == [] (GOTO Prompt) ELSE (GOTO NEXT)
:Prompt
SET /p "name=Enter name of deploy: "

:Next
SET dirr=bin\Prod\net8.0\publish

REM windows 64
dotnet publish sorter.sln -p:PublishProfile=Win64 -c Release
IF EXIST %dirr%\%name%-win64 rmdir /S /Q %dirr%\%name%-win64
ren %dirr%\win64 %name%-win64
IF EXIST %dirr%\%name%-win64\config.json DEL /F %dirr%\%name%-win64\config.json
IF EXIST %dirr%\%name%-win64\keybinds.json DEL /F %dirr%\%name%-win64\keybinds.json
IF EXIST %dirr%\%name%-win64.zip DEL /F %dirr%\%name%-win64.zip
"C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-win64.zip .\%dirr%\%name%-win64\*

REM linux 64
dotnet publish sorter.sln -p:PublishProfile=Linux64 -c Release
IF EXIST %dirr%\%name%-linux64 rmdir /S /Q %dirr%\%name%-linux64
ren %dirr%\linux64 %name%-linux64
IF EXIST %dirr%\%name%-linux64\config.json DEL /F %dirr%\%name%-linux64\config.json
IF EXIST %dirr%\%name%-linux64\keybinds.json DEL /F %dirr%\%name%-linux64\keybinds.json
IF EXIST %dirr%\%name%-linux64.tar DEL /F %dirr%\%name%-linux64.tar
"C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-linux64.tar .\%dirr%\%name%-linux64\*

REM macos arm
REM dotnet publish sorter.sln -p:PublishProfile=OSX_ARM64 -c Release
REM IF EXIST %dirr%\%name%-osx-arm64 rmdir /S /Q %dirr%\%name%-osx-arm64
REM ren %dirr%\osx-arm64 %name%-osx-arm64
REM IF EXIST %dirr%\%name%-osx-arm64\config.json DEL /F %dirr%\%name%-osx-arm64\config.json
REM IF EXIST %dirr%\%name%-osx-arm64\keybinds.json DEL /F %dirr%\%name%-osx-arm64\keybinds.json
REM IF EXIST %dirr%\%name%-osx-arm64.tar DEL /F %dirr%\%name%-osx-arm64.tar
REM "C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-osx-arm64.tar .\%dirr%\%name%-osx-arm64\*