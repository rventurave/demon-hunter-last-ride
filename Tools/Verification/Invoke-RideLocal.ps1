param(
    [Parameter(Mandatory=$true)][string]$Action,
    [string]$File,
    [string]$Entry,
    [int]$TimeoutSec=45
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$requestPath=Join-Path $projectRoot 'Library/RideLocalRequest.json'
$replyPath=Join-Path $projectRoot 'Library/RideLocalReply.json'
$requestId=[Guid]::NewGuid().ToString()
$request=@{id=$requestId;action=$Action;file=$File;entry=$Entry;expiresUtcTicks=[DateTime]::UtcNow.AddSeconds($TimeoutSec+30).Ticks}
$request|ConvertTo-Json|Set-Content -LiteralPath $requestPath -Encoding UTF8
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSec)
while([DateTime]::UtcNow -lt $deadline){
    if(Test-Path -LiteralPath $replyPath){
        try{$reply=Get-Content -LiteralPath $replyPath -Raw|ConvertFrom-Json}catch{$reply=$null}
        if($reply -and $reply.id -eq $requestId){
            if(-not $reply.passed){throw $reply.error}
            if($Action -eq 'verification'){
                $result=$reply.details|ConvertFrom-Json
                if(-not $result.success){throw ($reply.details)}
            }
            $reply|ConvertTo-Json -Depth 20
            exit 0
        }
    }
    Start-Sleep -Milliseconds 250
}
throw "Editor did not respond to $Action before the timeout."
