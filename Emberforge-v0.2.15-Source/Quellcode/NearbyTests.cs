using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace Emberforge {
 internal static class NearbyTests {
  internal static string Run(){
   var report=new StringBuilder();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Nearby test failed: "+label);report.AppendLine("PASS "+label);};
   var sample=new BuildingSnapshot{Capacity=8,Occupied=0x10000,EntityValues=0x20000};
   var mask=new byte[]{0x5f};var pointers=Enumerable.Range(1,8).SelectMany(i=>BitConverter.GetBytes((long)i)).ToArray();var described=new List<long>();int reads=0;
   Func<long,int,byte[]> read=(address,length)=>{reads++;var bytes=address==sample.Occupied?mask:pointers;if(length!=bytes.Length)throw new Exception("Incorrect scan read size");return bytes;};
   Func<long,BuildingTarget> describe=entity=>{described.Add(entity);if(entity==4)return null;return new BuildingTarget{Entity=entity,Item=new BuildingCatalogItem{Id=entity==2?"A":entity.ToString(),Name=entity==3?"1_PlayerCharacter":entity==2?"Tree":entity==1?"Fence":"Vase"}};};
   var targets=BuildingEngine.ReadLoadedObjects(sample,read,describe);
   check(described.SequenceEqual(new long[]{1,2,3,4,5,7}),"nearby scan visits occupied slots and skips empty slots");
   check(targets.Any(t=>t.Entity==3)&&targets.All(t=>t.Entity!=4),"player remains available as the distance reference and unreadable entities are excluded");
   check(reads==2,"scene bitset and pointer table use two bounded reads");
   pointers=BitConverter.GetBytes(1L).Concat(BitConverter.GetBytes(1L)).Concat(pointers.Skip(16)).ToArray();described.Clear();targets=BuildingEngine.ReadLoadedObjects(sample,read,describe);
   check(targets.Count(t=>t.Item.Id=="1")==1,"duplicate instances collapse into one reusable object template");
   check(targets.Select(t=>t.Item.Name).SequenceEqual(targets.Select(t=>t.Item.Name).OrderBy(n=>n,StringComparer.OrdinalIgnoreCase)),"scene results have stable alphabetical ordering");
   int before=reads;bool cancelled=false;try{BuildingEngine.ReadLoadedObjects(sample,read,describe,()=>true);}catch(OperationCanceledException){cancelled=true;}
   check(cancelled&&reads==before,"disconnect cancellation stops scan before accessing scene tables");
   bool invalid=false;sample.Capacity=262145;try{BuildingEngine.ReadLoadedObjects(sample,read,describe);}catch(InvalidOperationException){invalid=true;}
   check(invalid&&reads==before,"corrupt capacity is rejected before any memory read");
   sample.Capacity=8;sample.Occupied=0;invalid=false;try{BuildingEngine.ReadLoadedObjects(sample,read,describe);}catch(InvalidOperationException){invalid=true;}
   check(invalid&&reads==before,"invalid scene pointers are rejected before any memory read");
   var player=new BuildingTarget{Item=new BuildingCatalogItem{Name="1_PlayerCharacter",Id="P"},HasPosition=true,X=0,Y=0,Z=0};var near=new BuildingTarget{Item=new BuildingCatalogItem{Name="Fence",Id="F"},HasPosition=true,X=3,Y=4,Z=0};var far=new BuildingTarget{Item=new BuildingCatalogItem{Name="Tree",Id="T"},HasPosition=true,X=11,Y=0,Z=0};
   var within=BuildingEngine.FilterNearby(new[]{player,near,far},5);check(within.Length==1&&within[0].Item.Id=="F","nearby radius includes objects at or inside the selected distance");check(BuildingEngine.FilterNearby(new[]{player,near,far},4.9).Length==0,"nearby radius excludes objects outside the selected distance");
   check(BuildingEngine.IsCurrentPickingObject(5,5,9,9,9),"current local picking object is accepted");
   check(!BuildingEngine.IsCurrentPickingObject(5,5,0,9,0),"looking into empty space rejects an old captured entity");
   check(!BuildingEngine.IsCurrentPickingObject(5,6,9,9,9),"another player's target cannot be captured");
   check(!BuildingEngine.IsCurrentPickingObject(5,5,9,9,10),"ray changing during capture rejects the stale replacement");
   check(!BuildingEngine.IsCurrentPickingObject(5,5,10,9,10),"current ray ID must match resolved target entity");
   var material=new BuildingSnapshot{HeldPickaxe=true,HeldDefinition=0x10000,HeldActor=5,TargetStatus=0,VoxelTarget=1,MaterialCaptures=1,MaterialId=129};
   check(BuildingEngine.ValidMaterialTarget(material),"valid current pickaxe material is accepted");material.VoxelTarget=0;
   check(!BuildingEngine.ValidMaterialTarget(material),"object-only hit cannot be used as voxel material");material.VoxelTarget=1;material.HeldPickaxe=false;
   check(!BuildingEngine.ValidMaterialTarget(material),"held weapon cannot capture a previously valid material");material.HeldPickaxe=true;material.MaterialId=-1;
   check(!BuildingEngine.ValidMaterialTarget(material),"cleared ray invalidates old raw material");material.MaterialId=0;
   check(!BuildingEngine.ValidMaterialTarget(material),"air is excluded from material replacement");
   return report.ToString();
  }
 }
}
