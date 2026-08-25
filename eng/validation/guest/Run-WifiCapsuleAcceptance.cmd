@echo off
setlocal

set "ROOT=%~1"
set "INCLUDE_X64=%~2"
set "RESULTS=%ROOT%\results"

if not exist "%RESULTS%" mkdir "%RESULTS%"

"%ROOT%\x86\Ytec.WifiCapsule.Tests.exe" > "%RESULTS%\tests-x86.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail

"%ROOT%\x86\Ytec.WifiCapsule.Tests.exe" --probe-native-wifi > "%RESULTS%\native-wifi-x86.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail

"%ROOT%\x86\YtecWifiCapsule.exe" > "%RESULTS%\ui-x86.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail
if not exist "%ROOT%\x86\captures\main-backup.png" goto fail
if not exist "%ROOT%\x86\captures\main-restore.png" goto fail

if not "%INCLUDE_X64%"=="1" goto pass

"%ROOT%\x64\Ytec.WifiCapsule.Tests.exe" > "%RESULTS%\tests-x64.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail

"%ROOT%\x64\Ytec.WifiCapsule.Tests.exe" --probe-native-wifi > "%RESULTS%\native-wifi-x64.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail

"%ROOT%\x64\YtecWifiCapsule.exe" > "%RESULTS%\ui-x64.txt" 2>&1
if not "%ERRORLEVEL%"=="0" goto fail
if not exist "%ROOT%\x64\captures\main-backup.png" goto fail
if not exist "%ROOT%\x64\captures\main-restore.png" goto fail

:pass
> "%RESULTS%\done.txt" echo PASS
exit /b 0

:fail
> "%RESULTS%\done.txt" echo FAIL
exit /b 1
