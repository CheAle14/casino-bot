$casinofiles = "D:\Documents\_CasinoDocs\CasinoBotRepo\src"
$localFiles = "D:\_GitHub\casino-bot\src"



function mklink { cmd /c mklink $args }

function Move-Files {
    [CmdletBinding(
    SupportsShouldProcess = $true
    #ConfirmImpact = "Medium"
    )]
    Param (
        [Parameter(Mandatory=$true, Position=0)]
        [string] $from,
        [Parameter(Mandatory=$true, Position=1)]
        [string] $to
    )
    Write-Host "From: $from"
    Write-Host "To: $to"
    $nl = [Environment]::NewLine
    if($PSCmdLet.ShouldProcess("$from -> $to ")) {
        Write-Host "Doing!"
        $dirPath = [System.IO.Path]::GetDirectoryName($to)
        if([System.IO.Directory]::Exists($dirPath)) {
        } else {
            New-Item -ItemType Directory -Force -Path $dirPath
        }
        $command = "mklink /H ""$to"" ""$from"""
        Write-Host $command
        cmd /c $command
    } else {
        Write-Host "Aborted!"
    }
}


function Symlink-Files {
    $file = Read-Host "What file do you want to add?"
    $fromFile = "$casinofiles\$file"
    $toFile = "$localFiles\$file"
    if([System.IO.File]::Exists($fromFile)) {
        Move-Files $fromFile $toFile -Confirm
    } else {
        Write-Host "File does not exist"
    }
}

$continue = "y"
while($continue -eq "y") {
    Symlink-Files
    $continue = Read-Host "Do you want to do it again? Enter [y] exactly, any other char will exit"
}