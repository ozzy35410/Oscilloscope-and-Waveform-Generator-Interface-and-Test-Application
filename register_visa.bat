@echo off
echo Registering VISA COM components...

cd /d "C:\Program Files (x86)\IVI Foundation\VISA\WinNT\Bin"
regsvr32 /s VisaCom.dll
regsvr32 /s VisaComC.dll

echo Registration complete.
pause