# For Documentation Example @see https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-content?view=powershell-7.3

# Remove all the NON Standard Data Streams from a File
function Remove-ADS { param( [string]$FilePath, [Switch]$ConfirmPrompt)

    if ($ConfirmPrompt) {

        $YesNo = Read-Host "Remove all ADS's from " $Path " [Y/N]"
        if("Y" -inotin $YesNo) { return }
    }

    if( Test-Path -Path $FilePath) {
        Get-Item -Path $FilePath  -Stream * |
            ForEach-Object {
                if($_.Stream -notlike "*DATA") {
                    Remove-Item -Path $FilePath -Stream $_.Stream
                }
            }
    }
}

# Display all the NON Standard Data Streams in a File
function Get-ADS { param( $FilePath )

    if( Test-Path -Path $FilePath) {
        Get-Item -Path $FilePath  -Stream * |
            ForEach-Object {
                if($_.Stream -notlike "*DATA") {
                    Get-Content -Path $FilePath -Stream $_.Stream
                }
            }
    }
}

Clear-Host

# Dispaly  Alternative Data Streams (:OZ for General PlugIn Processing Messages)
$Path = "S:\Metal Products\MainOffice\PROGRAMS\PlugIns\TaskManagerDB.accdb"
$FilePath = Get-Item $Path
Get-ADS -FilePath $FilePath

# Dispaly Alternative Data Streams (Plugin Specific Messages)
#$Path2 = "S:\Metal Products\MainOffice\PROGRAMS\PlugIns\67677392495355544850\TMDB.accdb"
#$Path2 = "S:\Metal Products\MainOffice\PROGRAMS\PlugIns\67677392495456525348\TMDB.accdb"
$Path2 = "S:\Metal Products\MainOffice\PROGRAMS\PlugIns\67687967921009710911111599104101\TMDB.accdb"
Get-ADS -FilePath $Path2

# Remove all the NON Standard Data Streams from $Path
if( Test-Path -Path $Path) {
    Remove-ADS -FilePath $Path -ConfirmPrompt
    Get-Item -Path $Path -Stream *
}

if( Test-Path -Path $Path2) {
    Remove-ADS -FilePath $Path2 -ConfirmPrompt
    Get-Item -Path $Path2 -Stream *
}