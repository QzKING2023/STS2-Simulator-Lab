param([Parameter(Mandatory)][string]$HarnessRoot,[Parameter(Mandatory)][string]$Case,[Parameter(Mandatory)][string]$OutputDirectory,[int]$Nodes=1000)
$ErrorActionPreference='Stop'
$labRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$harness=[IO.Path]::GetFullPath($HarnessRoot)
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(-not $out.StartsWith((Join-Path $labRoot '.build/runs')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay under Lab runs.'}
if(Test-Path -LiteralPath $out){throw 'Use a fresh case output directory.'}
New-Item -ItemType Directory -Path $out|Out-Null
$pin=Join-Path $labRoot '.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix'
$lock=Get-Content (Join-Path $pin 'input-lock.json') -Raw|ConvertFrom-Json
foreach($f in $lock.files){if((Get-FileHash (Join-Path $pin $f.relativePath)).Hash-ne$f.sha256){throw 'Pinned input changed.'}}
$dll=Join-Path $harness 'bin/Release/net9.0/OfflineSearchHarness.dll'
$runtime=Get-Content (Join-Path (Split-Path $dll) 'OfflineSearchHarness.runtimeconfig.json') -Raw|ConvertFrom-Json
if((Get-FileHash (Join-Path $runtime.runtimeOptions.configProperties.Sts2DataDir 'sts2.dll')).Hash-ne'0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'){throw 'Wrong game input.'}
$variables=@{
SIMULATORLAB_ELEANOR_MOD='1';SIMULATORLAB_ELEANOR_MOD_DLL=(Join-Path $pin 'MyMod.dll');SIMULATORLAB_ELEANOR_BASELIB_DLL=(Join-Path $pin 'dependencies/BaseLib.dll');
SIMULATORLAB_ELEANOR_MANIFEST=(Join-Path $pin 'MyMod.json');SIMULATORLAB_ELEANOR_MOD_VERSION='v0.4.1-forms-cardpool.fix';SIMULATORLAB_ELEANOR_V040_PROBE='1';SIMULATORLAB_ELEANOR_V040_FORM='Reverse';
SIMULATORLAB_NATIVE_PROFILE=(Join-Path $labRoot '.build/pinned-inputs/eleanor-v041-native-bootstrap');SIMULATORLAB_NATIVE_LOOP_STAGE='Loop';SIMULATORLAB_NATIVE_SECOND_BATTLE='0';
SIMULATORLAB_SHIELD_FIXTURE='0';SIMULATORLAB_NATIVE_RECORD_FIXTURE='0';SIMULATORLAB_STAGE_ONE_CASE=$Case
}
$previous=@{};foreach($key in $variables.Keys){$previous[$key]=[Environment]::GetEnvironmentVariable($key);[Environment]::SetEnvironmentVariable($key,$variables[$key])}
try{
 & dotnet $dll --character MyMod --encounter FUZZY_WURM_CRAWLER_WEAK --seed LAB_V0111_ELEANOR_V040_FORM_01 --profile Low --beam 8 --nodes $Nodes --dop 1 --budget-ms 5000 --search-mode Evaluate --milestone M2 --label $Case --out $out *> (Join-Path $out 'console.log')
 $code=$LASTEXITCODE
 [ordered]@{case=$Case;exit=$code;harnessSha256=(Get-FileHash $dll).Hash;nodes=$Nodes}|ConvertTo-Json|Set-Content (Join-Path $out 'process.json') -Encoding utf8
 Write-Output "$Case exit=$code evidence=$out"
 if($code-ne0){Get-Content (Join-Path $out 'console.log') -Tail 15}
 exit $code
}finally{foreach($key in $previous.Keys){[Environment]::SetEnvironmentVariable($key,$previous[$key])}}
