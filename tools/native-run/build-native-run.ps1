param(
 [Parameter(Mandatory)][string]$GamePath,
 [Parameter(Mandatory)][string]$HostSource,
 [string]$OutputRoot='',
 [string]$SolverSnapshot='',
 [string]$ForeseerSnapshot=''
)
$ErrorActionPreference='Stop'
$labRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$hostRoot=[IO.Path]::GetFullPath($HostSource)
$data=Join-Path $GamePath 'data_sts2_windows_x86_64'
$expected='0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9'
if((Get-FileHash (Join-Path $data 'sts2.dll')).Hash -ne $expected){throw 'Pinned game assembly mismatch'}
$release=Get-Content (Join-Path $GamePath 'release_info.json') -Raw | ConvertFrom-Json
if($release.version -ne 'v0.111.0' -or $release.commit -ne '41cef1ea'){throw 'Pinned release mismatch'}
if(!$OutputRoot){$OutputRoot=Join-Path $labRoot ('.build/native-build/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))}
$out=[IO.Path]::GetFullPath($OutputRoot)
if(!$out.StartsWith($labRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build output must stay in Lab'}
if(Test-Path $out){throw 'Use a fresh immutable build directory'}
New-Item -ItemType Directory -Path $out | Out-Null
$shared=@('GameBootstrap.cs','MemorySaveStore.cs','MainLoopContext.cs','OfflineLocalization.cs')
$rows=@()
if($ForeseerSnapshot){
 $rf=[IO.Path]::GetFullPath($ForeseerSnapshot)
 if(!$rf.StartsWith((Join-Path $labRoot '.build')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Foreseer snapshot must stay in Lab'}
 $rfLock=Get-Content -LiteralPath (Join-Path $rf 'input-lock.json') -Raw|ConvertFrom-Json
 if($rfLock.schema-ne'foreseer-source-reuse/v1'-or$rfLock.version-ne'0.13.19'){throw 'Unsupported Foreseer source lock'}
 foreach($r in $rfLock.inputs){$p=Join-Path $rf $r.path;if((Get-FileHash -LiteralPath $p).Hash-ne$r.sha256){throw 'Foreseer input changed'};$rows+=@{path=$p;sha256=$r.sha256}}
 $rows+=@{path=(Join-Path $rf 'input-lock.json');sha256=(Get-FileHash -LiteralPath (Join-Path $rf 'input-lock.json')).Hash}
 Copy-Item -LiteralPath (Join-Path $rf 'input-lock.json') -Destination (Join-Path $out 'foreseer-lock.json')
 Copy-Item -LiteralPath (Join-Path $rf 'LICENSE') -Destination (Join-Path $out 'RANDOM-FORESEER-LICENSE')
}
foreach($name in $shared){
 $path=Join-Path $hostRoot $name
 $rows+=@{path=$path;sha256=(Get-FileHash $path).Hash}
 Copy-Item -LiteralPath $path -Destination $out
}
$license=[IO.Path]::GetFullPath((Join-Path $hostRoot '../../../LICENSE'))
if(!(Test-Path $license)){throw 'Upstream MIT license required'}
Copy-Item $license (Join-Path $out 'UPSTREAM-LICENSE')
$rows+=@{path=$license;sha256=(Get-FileHash $license).Hash}
$bootstrapPath=Join-Path $out 'GameBootstrap.cs'
$text=[IO.File]::ReadAllText($bootstrapPath)
$start=$text.IndexOf('        if (!NativeModRunLoop.Enabled) {')
$end=$text.IndexOf('        Trace("mod_manager_state");',$start)
if($start -lt 0 -or $end -lt 0){throw 'Reviewed upstream bootstrap anchor changed'}
$text=$text.Remove($start,$end-$start).Insert($start,@'
        AccessTools.Field(typeof(ReflectionHelper), "_modTypes").SetValue(null, Array.Empty<Type>());
        AccessTools.PropertySetter(typeof(ModManager), nameof(ModManager.State))!.Invoke(null, [ModManagerState.Initialized]);
'@+[Environment]::NewLine)
$text=$text -replace '(?m)^.*CombatSolver\.SolverController\.DisplayServerNameProvider =.*\r?\n',''
$text=$text -replace '(?m)^.*_bypasses\.Add\("SolverController.DisplayServerNameProvider.*\r?\n',''
$text=$text.Replace('        if (NativeModRunLoop.Enabled) NativeModRunLoop.Initialize();','')
$text=$text.Replace('ModelDb.Init(EleanorModBootstrap.IncludeModels(AbstractModelSubtypes.All.ToArray()));','ModelDb.Init(AbstractModelSubtypes.All.ToArray());')
$text=$text.Replace('combatsolver.offline.harness','simulatorlab.native.host')
if($text -cmatch 'EleanorModBootstrap\.|NativeModRunLoop\.|CombatSolver\.'){throw 'Native bootstrap still depends on Mod/solver'}
[IO.File]::WriteAllText($bootstrapPath,$text)
Copy-Item (Join-Path $PSScriptRoot '*.cs') $out
$core=(Join-Path $labRoot 'src/SimulatorLab.Core/SimulatorLab.Core.csproj')
$solverProperties='';$solverReferences='';$assemblyName='SimulatorLab.NativeRun'
if($SolverSnapshot){
 $SolverSnapshot=[IO.Path]::GetFullPath($SolverSnapshot)
 if(!$SolverSnapshot.StartsWith((Join-Path $labRoot '.build')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Solver snapshot must be in Lab'}
 $solverLock=Get-Content (Join-Path $SolverSnapshot 'solver-lock.json') -Raw|ConvertFrom-Json
 if($solverLock.schema-ne'solver-compute-inputs/v1' -or $solverLock.version-ne'0.50.1'){throw 'Unexpected solver lock'}
 foreach($r in $solverLock.assemblies){$f=Join-Path $SolverSnapshot $r.file;if((Get-FileHash $f).Hash-ne$r.sha256){throw 'Solver input changed'};$solverReferences+="<Reference Include=`"$($r.name)`" HintPath=`"$f`" Private=`"false`"/>";$rows+=@{path=$f;sha256=$r.sha256}}
 foreach($r in $solverLock.source){$f=Join-Path $SolverSnapshot ('source/'+$r.path);if((Get-FileHash $f).Hash-ne$r.sha256){throw 'Solver snapshot source changed'};$rows+=@{path=$f;sha256=$r.sha256}}
 $rows+=@{path=(Join-Path $SolverSnapshot 'solver-lock.json');sha256=(Get-FileHash (Join-Path $SolverSnapshot 'solver-lock.json')).Hash}
 $assemblyName='OfflineSearchHarness';$solverProperties='<DefineConstants>$(DefineConstants);SOLVER_GATE</DefineConstants>'
}
$project=@"
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net9.0</TargetFramework><LangVersion>13.0</LangVersion><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AssemblyName>$assemblyName</AssemblyName>$solverProperties</PropertyGroup>
 <ItemGroup><PackageReference Include="Krafs.Publicizer" Version="2.3.0" PrivateAssets="all"/><Publicize Include="sts2" IncludeVirtualMembers="false" IncludeCompilerGeneratedMembers="false"/></ItemGroup>
 <ItemGroup><Reference Include="sts2" HintPath="$data/sts2.dll" Private="false"/><Reference Include="GodotSharp" HintPath="$data/GodotSharp.dll" Private="false"/><Reference Include="0Harmony" HintPath="$data/0Harmony.dll" Private="false"/><ProjectReference Include="$core"/>$solverReferences</ItemGroup>
 <ItemGroup><RuntimeHostConfigurationOption Include="Sts2DataDir" Value="$data"/><RuntimeHostConfigurationOption Include="LabRoot" Value="$labRoot"/></ItemGroup>
</Project>
"@
[IO.File]::WriteAllText((Join-Path $out 'NativeRun.csproj'),$project)
& dotnet build (Join-Path $out 'NativeRun.csproj') -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false
if($LASTEXITCODE -ne 0){throw 'Native build failed'}
if($SolverSnapshot){
 foreach($r in $solverLock.assemblies){Copy-Item (Join-Path $SolverSnapshot $r.file) (Join-Path $out 'bin/Release/net9.0')}
 Copy-Item (Join-Path $SolverSnapshot 'solver-lock.json') (Join-Path $out 'bin/Release/net9.0')
 Copy-Item (Join-Path $SolverSnapshot 'LICENSE') (Join-Path $out 'SOLVER-LICENSE')
 Copy-Item (Join-Path $SolverSnapshot 'CombatSolver.json') (Join-Path $out 'bin/Release/net9.0')
}
if($ForeseerSnapshot){Copy-Item -LiteralPath (Join-Path $rf 'input-lock.json') -Destination (Join-Path $out 'bin/Release/net9.0/foreseer-lock.json')}
$rows+=@(Get-ChildItem $PSScriptRoot -File | ForEach-Object {@{path=$_.FullName;sha256=(Get-FileHash $_.FullName).Hash}})
$rows+=@(Get-ChildItem (Join-Path $labRoot 'src/SimulatorLab.Core') -Filter '*.cs' | ForEach-Object {@{path=$_.FullName;sha256=(Get-FileHash $_.FullName).Hash}})
$rows+=@(Get-Item (Join-Path $labRoot 'src/SimulatorLab.Core/SimulatorLab.Core.csproj'),(Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/Program.cs'),(Join-Path $labRoot 'tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj') | ForEach-Object {@{path=$_.FullName;sha256=(Get-FileHash $_.FullName).Hash}})
$inputs=@('sts2.dll','GodotSharp.dll','0Harmony.dll') | ForEach-Object {@{path=(Join-Path $data $_);sha256=(Get-FileHash (Join-Path $data $_)).Hash}}
$compiled=@(Get-ChildItem $out -File | Where-Object {$_.Extension-in@('.cs','.csproj') -or $_.Name-eq'UPSTREAM-LICENSE'} | ForEach-Object {@{path=$_.Name;sha256=(Get-FileHash $_.FullName).Hash}})
$artifacts=@(Get-ChildItem (Join-Path $out 'bin/Release/net9.0') -File | ForEach-Object {@{path=[IO.Path]::GetRelativePath($out,$_.FullName);sha256=(Get-FileHash $_.FullName).Hash}})
$payload=@{schema='native-build/v1';profile=if($SolverSnapshot){'solver-gate/v1'}else{'native-only/v1'};executable=($assemblyName+'.dll');head=(git -C $labRoot rev-parse HEAD);source=$rows;inputs=@($inputs);compiled=$compiled;artifacts=$artifacts;toolchain=(& dotnet --version)}
$canonical=$payload | ConvertTo-Json -Depth 8 -Compress
$id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical)))
@{buildId=$id;manifest=$payload} | ConvertTo-Json -Depth 9 | Set-Content (Join-Path $out 'build-manifest.json') -Encoding utf8
Write-Output "NATIVE_BUILD=$out"
