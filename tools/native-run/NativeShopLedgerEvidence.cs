using System.Text.Json;
namespace OfflineSearchHarness;
internal static class NativeShopLedgerEvidence
{
 internal static object Review(IEnumerable<JsonElement> rows){
  var contract=new NativeThirdShopContract();int buys=0,writes=0;
  foreach(var p in rows){
   string kind=p.GetProperty("kind").GetString()!;var d=p.GetProperty("detail");
   JsonElement? state=d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("state",out var s)?s:null;
   if(NativeThirdShopContract.Kinds.Contains(kind)||NativeShopLedgerContract.Kinds.Contains(kind)){
    NativeBoundaryContract.Require(state is {} value&&value.ValueKind==JsonValueKind.Object,"Required shop complete state");
    NativeTraceContract.CheckState(state!.Value);
   }
   contract.Accept(p,state);
   if(kind=="third-shop-buy-return")buys++;
   if(kind=="third-shop-resource-return")writes++;
  }
  // Failures may end within a transaction; only a confirmed ThirdShop requires
  // all purchases/hooks/completion. Never turn that partial root into success.
  return new{buys,writes};
 }
}
