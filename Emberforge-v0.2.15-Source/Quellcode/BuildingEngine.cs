using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Emberforge {
 public sealed class BuildingCatalogItem {
  public string Name {get;set;} public string Id {get;set;} public string Mesh {get;set;}
  public override string ToString(){return Name;}
 }
 internal sealed class BuildingTarget {
  internal BuildingCatalogItem Item;internal long Entity;internal int EntityId;internal bool HasPosition;internal double X,Y,Z;
 }
 internal sealed class BuildingSnapshot {
  internal int TerrainIndex,BlockIndex,MaterialId,TargetStatus,VoxelTarget;
  internal uint TargetCalls,MaterialCaptures,InspectorCalls,AimedCalls,PlaceCalls,PreviewCalls,HeldCalls;
  internal bool BlocksEnabled,PropsEnabled,FixedSource,NoConsumption,FreeBuild,NoCraftConsumption,NoDurabilityLoss;
  internal int OriginalMaterial,ReplacementMaterial;
  internal long AimedEntity,World,EntityValues,Occupied,Capacity,PickingOwner,HeldDefinition;internal int PickingActor,HeldActor;internal bool HeldPickaxe;
  internal byte[] OriginalProp,OriginalMesh,ReplacementProp,ReplacementMesh;
 }
 // This component owns no process handle or injection lifecycle. All native hooks
 // are part of GameSession's single validated install, rollback and watchdog receipt.
 internal sealed class BuildingEngine {
  internal const int RelativeData=0x1000;
  readonly GameSession session;
  internal BuildingCatalogItem[] Catalog {get;private set;}
  Dictionary<string,BuildingCatalogItem> byId;
  internal GameCatalogData Imported {get;private set;}
  internal BuildingEngine(GameSession game){
   session=game;Catalog=Resources.Json.Deserialize<BuildingCatalogItem[]>(Resources.Text("BuildingCatalog.json"));
   byId=Catalog.GroupBy(i=>i.Id,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
  }
  internal void Import(GameCatalogData data){if(!GameCatalog.Valid(data))throw new InvalidOperationException("Ungültiger Spielkatalog.");Catalog=data.Items;byId=Catalog.ToDictionary(i=>i.Id,StringComparer.OrdinalIgnoreCase);Imported=data;}
  internal BuildingCatalogItem CurrentItem(BuildingCatalogItem item){BuildingCatalogItem current;if(item!=null&&byId.TryGetValue(item.Id,out current))return current;return Imported==null?item:null;}
  internal bool IsReady {get{return session.IsConnected&&session.Manifest.DataOffset==0x4000&&session.Manifest.AllocationSize>=0x6000&&session.Manifest.Hooks.Any(h=>h.Name=="BuildingInspector");}}
  void Ready(){if(!IsReady)throw new InvalidOperationException("Zuerst die Version mit Baufunktionen mit Enshrouded verbinden.");}
  long Data {get{Ready();return session.Receipt.Data+RelativeData;}}
  byte[] Read(long address,int length){return GameSession.Read(session.Handle,address,length);}
  void Write(int offset,byte[] data){GameSession.Write(session.Handle,Data+offset,data);}
  void Integer(int offset,int value){Write(offset,BitConverter.GetBytes(value));}
  static int Int(byte[] b,int p){return BitConverter.ToInt32(b,p);}
  static uint Uint(byte[] b,int p){return BitConverter.ToUInt32(b,p);}
  static long Long(byte[] b,int p){return BitConverter.ToInt64(b,p);}
  static byte[] Part(byte[] b,int p,int n){return b.Skip(p).Take(n).ToArray();}
  static bool Pointer(long p){return p>=0x10000&&p<=0x7fffffffffff;}
  static bool Nonzero(byte[] b){return b.Any(v=>v!=0);}
  internal BuildingSnapshot Sample(){
   byte[] b=Read(Data,0x60c);
   byte[] free=Read(Data+0x610,4);
   return new BuildingSnapshot{BlocksEnabled=Int(b,0)!=0,PropsEnabled=Int(b,4)!=0,FixedSource=Int(b,0x4c0)!=0,NoConsumption=Int(b,0x600)!=0,FreeBuild=Int(free,0)!=0,NoCraftConsumption=Int(b,0x604)!=0,NoDurabilityLoss=Int(Read(session.Receipt.Data+0x7c,4),0)!=0,
    OriginalMaterial=Int(b,0x20),ReplacementMaterial=Int(b,0x24),TerrainIndex=Int(b,0x80),BlockIndex=Int(b,0x88),
    TargetCalls=Uint(b,0x90),TargetStatus=Int(b,0x94),MaterialId=Int(b,0x98),MaterialCaptures=Uint(b,0x9c),VoxelTarget=Int(b,0xa0),
    World=Long(b,0x100),AimedEntity=Long(b,0x108),InspectorCalls=Uint(b,0x110),AimedCalls=Uint(b,0x114),
    EntityValues=Long(b,0x128),Capacity=Long(b,0x130),Occupied=Long(b,0x138),PickingOwner=Long(b,0x140),PickingActor=Int(b,0x148),HeldDefinition=Long(b,0x150),HeldCalls=Uint(b,0x158),HeldActor=Int(b,0x160),HeldPickaxe=Int(b,0x164)==1,
    PlaceCalls=Uint(b,0x400),PreviewCalls=Uint(b,0x404),OriginalProp=Part(b,0x430,16),OriginalMesh=Part(b,0x440,16),
    ReplacementProp=Part(b,0x480,16),ReplacementMesh=Part(b,0x490,16)};
  }
  internal void SetMaterialTargets(int terrainIndex,int blockIndex,bool enabled){
   if(terrainIndex!=-1&&(terrainIndex<1||terrainIndex>127))throw new ArgumentOutOfRangeException("terrainIndex","Terrain-ID muss zwischen 1 und 127 liegen.");
   if(blockIndex<-1||blockIndex>127)throw new ArgumentOutOfRangeException("blockIndex","Baublock-ID muss zwischen 0 und 127 liegen.");
   if(enabled&&terrainIndex==-1&&blockIndex==-1)throw new InvalidOperationException("Zuerst ein Ersatzmaterial wählen.");
   Integer(0,0);Write(8,BitConverter.GetBytes(terrainIndex).Concat(BitConverter.GetBytes(blockIndex)).ToArray());
   if(enabled)Integer(0,1);
  }
  internal void EnableBlocks(bool enabled){
   if(enabled){var b=Read(Data+8,8);int t=Int(b,0),block=Int(b,4);if((t<1||t>127)&&(block<0||block>127))throw new InvalidOperationException("Zuerst ein Ersatzmaterial wählen.");}
   Integer(0,enabled?1:0);
  }
  internal static bool ValidMaterialTarget(BuildingSnapshot s){return IsPickaxe(s)&&s.TargetStatus==0&&s.VoxelTarget==1&&s.MaterialCaptures!=0&&s.MaterialId>=1&&s.MaterialId<=255;}
  internal int CaptureMaterial(){
   var s=Sample();if(!ValidMaterialTarget(s))throw new InvalidOperationException("Mit der Spitzhacke einen Boden oder Baublock direkt im Fadenkreuz anvisieren.");
   var after=Sample();if(!ValidMaterialTarget(after)||after.MaterialId!=s.MaterialId||after.HeldActor!=s.HeldActor||after.HeldDefinition!=s.HeldDefinition)throw new InvalidOperationException("Das Materialziel hat sich geändert. Erneut anvisieren und den Hotkey drücken.");
   return s.MaterialId;
  }
  internal static bool IsCurrentPickingObject(int actor,int heldActor,int targetId,int entityId,int afterId){return actor!=0&&actor==heldActor&&targetId!=0&&targetId==entityId&&targetId==afterId;}
  internal static bool IsPickaxe(BuildingSnapshot s){return s.HeldPickaxe&&Pointer(s.HeldDefinition)&&s.HeldActor!=0;}
  internal BuildingTarget CaptureAimedObject(){
   var s=Sample();if(!IsPickaxe(s))throw new InvalidOperationException("Zum Übernehmen die Spitzhacke in die Hand nehmen.");
   if(!Pointer(s.PickingOwner)||s.PickingActor!=s.HeldActor||s.AimedCalls==0)throw new InvalidOperationException("Mit der Spitzhacke ein Objekt direkt im Fadenkreuz anvisieren.");
   int current=Int(Read(s.PickingOwner+0x5b4,4),0);if(current==0)throw new InvalidOperationException("Kein aktuelles Objekt im Fadenkreuz.");
   var target=Describe(s.AimedEntity);var after=Sample();int afterId=Int(Read(s.PickingOwner+0x5b4,4),0);
   if(target==null||!IsCurrentPickingObject(s.PickingActor,s.HeldActor,current,target.EntityId,afterId)||!IsPickaxe(after)||after.HeldActor!=s.HeldActor||after.HeldDefinition!=s.HeldDefinition||after.PickingOwner!=s.PickingOwner||after.AimedEntity!=s.AimedEntity)throw new InvalidOperationException("Das Ziel hat sich geändert. Erneut direkt anvisieren und den Hotkey drücken.");
   return target;
  }
  internal BuildingTarget Describe(long entity){return Describe(entity,session.Handle);}
  BuildingTarget Describe(long entity,IntPtr handle){
   if(!Pointer(entity))return null;
   try{
    byte[] e=GameSession.Read(handle,entity+0x10,0x28);long def=Long(e,0x18);if(!Pointer(def))return null;
    byte[] d=GameSession.Read(handle,def,0x20),uuid=Part(d,0,16);if(!Nonzero(uuid))return null;
    string id=Resources.Hex(uuid);BuildingCatalogItem item;
    if(!byId.TryGetValue(id,out item)){
     long name=Long(d,0x10),count=Long(d,0x18);if(!Pointer(name)||count<1||count>=512)return null;
     string text=Encoding.UTF8.GetString(GameSession.Read(handle,name,(int)count)).TrimEnd('\0');
     if(text.Length==0||text.Any(char.IsControl))return null;
     item=new BuildingCatalogItem{Name=text,Id=id,Mesh=new string('0',32)};
    }
    var target=new BuildingTarget{Item=item,Entity=entity,EntityId=Int(e,0)};double x,y,z;if(TryPosition(handle,entity,out x,out y,out z)){target.HasPosition=true;target.X=x;target.Y=y;target.Z=z;}return target;
   }catch{return null;}
  }
  static bool TryPosition(IntPtr handle,long entity,out double x,out double y,out double z){x=y=z=0;try{byte[] e=GameSession.Read(handle,entity+0x18,0x20);long meta=Long(e,0),start=Long(e,8);int row=Int(e,0x18);if(!Pointer(meta)||!Pointer(start)||row<0||row>1000000)return false;int index=125;byte[] bit=GameSession.Read(handle,meta+(index>>3),1);if((bit[0]&(1<<(index&7)))==0)return false;byte[] entry=GameSession.Read(handle,meta+0x84+index*2,2);byte[] sizeBytes=GameSession.Read(handle,meta+0xA84+index*2,2);int off=BitConverter.ToUInt16(entry,0),size=BitConverter.ToUInt16(sizeBytes,0);if(size<24||size>4096)return false;byte[] p=GameSession.Read(handle,start+off+(long)row*size,24);x=BitConverter.ToInt64(p,0)/4294967296.0;y=BitConverter.ToInt64(p,8)/4294967296.0;z=BitConverter.ToInt64(p,16)/4294967296.0;return Math.Abs(x)<1000000&&Math.Abs(y)<1000000&&Math.Abs(z)<1000000;}catch{return false;}}
  internal static BuildingTarget[] FilterNearby(BuildingTarget[] objects,double radius){if(objects==null||radius<=0)return new BuildingTarget[0];var player=objects.FirstOrDefault(o=>o!=null&&o.Item!=null&&o.Item.Name.StartsWith("1_Player",StringComparison.OrdinalIgnoreCase)&&o.HasPosition);if(player==null)return new BuildingTarget[0];return objects.Where(o=>o!=null&&o.Item!=null&&!o.Item.Name.StartsWith("1_Player",StringComparison.OrdinalIgnoreCase)&&o.HasPosition&&Math.Sqrt((o.X-player.X)*(o.X-player.X)+(o.Y-player.Y)*(o.Y-player.Y)+(o.Z-player.Z)*(o.Z-player.Z))<=radius).GroupBy(o=>o.Item.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(o=>o.Item.Name,StringComparer.OrdinalIgnoreCase).ToArray();}
  internal void SetPropTarget(BuildingCatalogItem item,bool enabled){
   if(item==null)throw new InvalidOperationException("Zuerst ein Ersatzobjekt wählen.");
   byte[] id=Resources.Hex(item.Id),mesh=Resources.Hex(item.Mesh??new string('0',32));
   if(id.Length!=16||mesh.Length!=16||!Nonzero(id))throw new InvalidOperationException("Ungültige Objekt-ID.");
   Integer(4,0);Write(0x480,id.Concat(mesh).ToArray());if(enabled)Integer(4,1);
  }
  internal void EnableProps(bool enabled){if(enabled&&!Nonzero(Read(Data+0x480,16)))throw new InvalidOperationException("Zuerst ein Ersatzobjekt wählen.");Integer(4,enabled?1:0);}
  internal void UseCurrentPlaceable(){
   bool enabled=Sample().PropsEnabled;Integer(4,0);Integer(0x4c0,0);if(enabled)Integer(4,1);
  }
  internal void SetFixedSource(BuildingCatalogItem item){
   if(item==null)throw new InvalidOperationException("Zuerst ein Ausgangsobjekt wählen.");
   byte[] prop=Resources.Hex(item.Id),mesh=Resources.Hex(item.Mesh??new string('0',32));
   if(prop.Length!=16||mesh.Length!=16||!Nonzero(prop)||!Nonzero(mesh))throw new InvalidOperationException("Für eine feste Quelle werden Objekt-ID und Vorschau-ID benötigt. Eine bekannte Quelle aus dem Katalog wählen.");
   bool enabled=Sample().PropsEnabled;Integer(4,0);Write(0x4a0,prop.Concat(mesh).ToArray());Integer(0x4c0,1);if(enabled)Integer(4,1);
  }
  internal BuildingCatalogItem CaptureLastPlacedSource(){
   var s=Sample();if(s.PlaceCalls==0||!Nonzero(s.OriginalProp))throw new InvalidOperationException("Ersetzen ausschalten und das gewünschte Ausgangsobjekt einmal normal platzieren.");
   string id=Resources.Hex(s.OriginalProp);BuildingCatalogItem item;
   if(!byId.TryGetValue(id,out item))throw new InvalidOperationException("Ausgangsobjekt ist noch nicht im Katalog. Eine bekannte Quelle auswählen.");
   return item;
  }
  internal BuildingTarget[] LoadedObjects(Func<bool> cancelled=null){
   IntPtr handle=session.Handle;var s=Sample();return ReadLoadedObjects(s,(address,length)=>GameSession.Read(handle,address,length),entity=>Describe(entity,handle),cancelled);
  }
  internal static BuildingTarget[] ReadLoadedObjects(BuildingSnapshot s,Func<long,int,byte[]> read,Func<long,BuildingTarget> describe,Func<bool> cancelled=null){
   if(s.Capacity<1||s.Capacity>262144||!Pointer(s.Occupied)||!Pointer(s.EntityValues))throw new InvalidOperationException("Objektliste noch nicht verfügbar. Kurz umsehen und erneut laden.");
   if(cancelled!=null&&cancelled())throw new OperationCanceledException();
   int count=(int)s.Capacity;var mask=read(s.Occupied,(count+7)/8);var pointers=read(s.EntityValues,checked(count*8));
   var result=new List<BuildingTarget>();
   for(int i=0;i<count;i++){if((i&31)==0&&cancelled!=null&&cancelled())throw new OperationCanceledException();if((mask[i/8]&(1<<(i%8)))!=0){var obj=describe(Long(pointers,i*8));if(obj!=null)result.Add(obj);}}
   return result.GroupBy(o=>o.Item.Id,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).OrderBy(o=>o.Item.Name,StringComparer.OrdinalIgnoreCase).ToArray();
  }
  internal void EnableNoConsumption(bool enabled){Ready();Integer(0x600,enabled?1:0);}
  internal void EnableFreeBuild(bool enabled){Ready();Integer(0x610,enabled?1:0);}
  internal void EnableNoCraftConsumption(bool enabled){Ready();Integer(0x604,enabled?1:0);}
  internal void EnableDurability(bool enabled){Ready();GameSession.Write(session.Handle,session.Receipt.Data+0x7c,BitConverter.GetBytes(enabled?1:0));}
  internal void AllOff(){if(IsReady){Write(0,new byte[8]);Write(0x600,new byte[8]);Integer(0x610,0);EnableDurability(false);}}
 }
}
