param([Parameter(Mandatory)][string]$Barrier)
[IO.File]::WriteAllText($Barrier,[string]$PID)
while($true){Wait-Event -Timeout 1|Out-Null}
