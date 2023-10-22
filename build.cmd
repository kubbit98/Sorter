SET name=%1
IF [%name%] == [] (GOTO Prompt) ELSE (GOTO NEXT)
:Prompt
SET /p "name=Enter name of deploy: "

:Next
SET dirr=bin\Prod\net6.0\publish

dotnet publish sorter.sln -p:PublishProfile=Win64 -c Release
dotnet publish sorter.sln -p:PublishProfile=Linux64 -c Release
dotnet publish sorter.sln -p:PublishProfile=OSX_ARM64 -c Release

IF EXIST %dirr%\%name%-win64 rmdir /S /Q %dirr%\%name%-win64
ren %dirr%\win64 %name%-win64
IF EXIST %dirr%\%name%-linux64 rmdir /S /Q %dirr%\%name%-linux64
ren %dirr%\linux64 %name%-linux64
IF EXIST %dirr%\%name%-osx-arm64 rmdir /S /Q %dirr%\%name%-osx-arm64
ren %dirr%\osx-arm64 %name%-osx-arm64

IF EXIST %dirr%\%name%-win64\config.json DEL /F %dirr%\%name%-win64\config.json
IF EXIST %dirr%\%name%-linux64\config.json DEL /F %dirr%\%name%-linux64\config.json
IF EXIST %dirr%\%name%-osx-arm64\config.json DEL /F %dirr%\%name%-osx-arm64\config.json

IF EXIST %dirr%\%name%-win64.zip DEL /F %dirr%\%name%-win64.zip
IF EXIST %dirr%\%name%-linux64.tar DEL /F %dirr%\%name%-linux64.tar
IF EXIST %dirr%\%name%-osx-arm64.tar DEL /F %dirr%\%name%-osx-arm64.tar
"C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-win64.zip .\%dirr%\%name%-win64\*
"C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-linux64.tar .\%dirr%\%name%-linux64\*
"C:\Program Files\7-Zip\7z.exe" a %dirr%\%name%-osx-arm64.tar .\%dirr%\%name%-osx-arm64\*