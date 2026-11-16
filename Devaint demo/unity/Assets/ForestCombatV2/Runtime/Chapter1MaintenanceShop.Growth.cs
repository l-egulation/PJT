using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Run;
namespace ForestCombatV2
{
 public sealed partial class Chapter1MaintenanceShop
 {
  readonly Random growthRandom=new Random(45109);
  public WeaponGrowth Growth=>binding?binding.Run.Growth:null;
  public static string ColorName(string color)=>color switch{"red"=>"빨강","yellow"=>"노랑","green"=>"초록","blue"=>"파랑","purple"=>"보라",_=>"무색"};
  public Guid GrowthWeapon(int slot)=>world.arena.Player.weapons[slot].InstanceId;
  public bool GrowthTransaction(Action action,out string error)
  {error=null;if(!IsOpen||!world||!world.Ready||binding.ShowingResult||world.arena.Player.Dead){error="정비소를 연 상태에서만 가능합니다.";return false;}try{action();return true;}catch(InvalidOperationException e){error=e.Message;return false;}}
  public bool TryUpgradeMvpAttack(out string error)=>Growth==null?Service.TryUpgradeStat(out error):GrowthTransaction(()=>Growth.UpgradeAttack(),out error);
 }
}
