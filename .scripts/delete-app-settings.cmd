@echo off
title Delete Application Settings and Data
echo --------------------------------------------------------------------------------------------------
echo DELETING  %DATE%:%TIME% Application Settings and Data

rd %LOCALAPPDATA%\OmniZenNotes /s /q
if %ERRORLEVEL% GEQ 1 goto ERROR

echo SUCCESS %DATE%:%TIME% Application Settings and Data Deleted OK
echo --------------------------------------------------------------------------------------------------
goto :EOF

:ERROR 
echo --------------------------------------------------------------------------------------------------
echo ERROR %DATE%:%TIME% Application Settings and Data Delete Problems encounterred
echo --------------------------------------------------------------------------------------------------
pause
