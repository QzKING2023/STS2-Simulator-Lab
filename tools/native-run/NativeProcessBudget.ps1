function Wait-NativeProcessBudget {
 param([Parameter(Mandatory)][Diagnostics.Process]$Process,
       [Parameter(Mandatory)][Diagnostics.Stopwatch]$Clock,
       [ValidateRange(1,120)][int]$Seconds=120)
 while (!$Process.WaitForExit(50)) {
  if ($Clock.Elapsed.TotalSeconds -ge $Seconds) {
   # Only this launched process and its descendants belong to this execution.
   if (!$Process.HasExited) { $Process.Kill($true) }
   if (!$Process.WaitForExit(5000)) { throw 'Owned process tree did not terminate after timeout' }
   return $true
  }
 }
 return $false
}

# Explicit full-run envelope. The legacy 120-second function stays unchanged.
function Wait-NativeFullRunProcessBudget {
 param([Parameter(Mandatory)][Diagnostics.Process]$Process,
       [Parameter(Mandatory)][Diagnostics.Stopwatch]$Clock,
       [ValidateRange(1,480)][int]$Seconds,
       [ValidateRange(1,4294967296)][long]$MaximumWorkingSetBytes=4294967296)
 while (!$Process.WaitForExit(50)) {
  $Process.Refresh()
  $reason=if($Clock.Elapsed.TotalSeconds -ge $Seconds){'time'}elseif($Process.WorkingSet64 -gt $MaximumWorkingSetBytes){'memory'}else{''}
  if($reason){
   if(!$Process.HasExited){$Process.Kill($true)}
   if(!$Process.WaitForExit(5000)){throw 'Owned full-run process tree did not terminate'}
   return @{Expired=$true;Reason=$reason}
  }
 }
 return @{Expired=$false;Reason=''}
}
