using HarmonyLib;
using SimulatorLab.Core;
using MegaCrit.Sts2.Core.Events.Custom.CrystalSphereEvent;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
namespace OfflineSearchHarness;
// Replace only the screen entry. Original CellClicked, completion Task and rewards run unchanged.
internal static class NativeCrystalSphere {
 internal static void Install(){if(NativeFullRun.Active||NativeEventFixtures.Active)NativeHost.Register(AccessTools.DeclaredMethod(typeof(NCrystalSphereScreen),nameof(NCrystalSphereScreen.ShowScreen)),nameof(Show),typeof(NativeCrystalSphere));}
 static Task? driver;internal static int Clicks;
 internal static void EnsureCompleted(){if(driver==null||!driver.IsCompleted)throw new NativeDataException("Crystal driver did not finish");driver.GetAwaiter().GetResult();NativeHost.Ensure();}
 static bool Show(CrystalSphereMinigame __0){if(driver is {IsCompleted:false})throw new NativeDataException("Concurrent Crystal driver");Clicks=0;driver=Drive(__0);return false;}
 static object Board(CrystalSphereMinigame game)=>new{remaining=game.DivinationCount,tool=game.CrystalSphereTool.ToString(),rng=game.Rng.ToSerializable(),cells=game.cells.Cast<CrystalSphereCell>().Select(c=>new{c.X,c.Y,c.IsHidden}).ToArray(),oracleItems=game.Items.Select(x=>x.ToSerializable()).ToArray()};
 static async Task Drive(CrystalSphereMinigame game){try{
  if(!game.PlacedAllItems||game.DivinationCount is <1 or >6)throw new NotSupportedException("Crystal board initialization outside reviewed profile");
  Program.Observe("crystal-decisions-start",new{board=Board(game),policy="first hidden coordinate; original Big tool; item placement is Oracle-only"});
  while(!game.IsFinished){
   var cells=game.cells.Cast<CrystalSphereCell>().Where(c=>c.IsHidden).OrderBy(c=>c.X).ThenBy(c=>c.Y).ToArray();
   var options=cells.Select((c,i)=>new NativeRewards.Option("o"+i,"GridCell",NativeIdentity.Ref(c),c.X+","+c.Y)).ToArray();
   var op=NativeRewards.Begin("crystal-cell",game,options,0);int index=Array.FindIndex(options,x=>x.Handle==op.Handle);if(index<0)throw new NativeActionRejectedException("Invalid crystal cell");
   var cell=cells[index];int before=game.DivinationCount;Program.Observe("crystal-click-start",new{id=op.Id,cell=NativeIdentity.Ref(cell),cell.X,cell.Y,board=Board(game)});
   await game.CellClicked(cell);Clicks++;
   if(game.DivinationCount!=before-1||cell.IsHidden)throw new NativeDataException("Original crystal click did not complete");
   Program.Observe("crystal-click-return",new{id=op.Id,cell=NativeIdentity.Ref(cell),cell.X,cell.Y,board=Board(game)});NativeRewards.End(op,new{remaining=game.DivinationCount});
  }
  Program.Observe("crystal-decisions-complete",new{board=Board(game)});
 }catch(Exception ex){NativeHost.Faults.Add(new InvalidOperationException("Crystal decision task failed",ex));game.ForceMinigameEnd();throw;}}
}
