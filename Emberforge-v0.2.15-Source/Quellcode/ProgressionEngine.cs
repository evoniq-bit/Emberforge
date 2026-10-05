using System;
using System.Text;
namespace Emberforge {
 internal sealed class ProgressionSnapshot {
  internal long Player,Selection,Registry,Stock,InventoryEntity,Definitions,Definition;
  internal int Experience,Level,QuickSlot,Slot,Inventory,Amount,Instance,Bonus,BaseSkills,TotalSkills,Command,Result,NormalStackLimit,OriginalStackLimit,ObservedQuickSlot;
  internal uint Item,Calls,SkillCalls;internal string Name;
  internal bool Stackable {get{return Stock!=0&&Item!=0&&Item!=0x41a792dc&&Amount>0&&Instance==0&&NormalStackLimit>1;}}
 }
 internal sealed class ProgressionEngine {
  readonly GameSession session;internal const int RelativeData=0x1700;
  internal ProgressionEngine(GameSession game){session=game;}
  long Data {get{if(!session.IsConnected)throw new InvalidOperationException("Not connected");return session.Receipt.Data+RelativeData;}}
  byte[] Read(long address,int length){return GameSession.Read(session.Handle,address,length);}
  static long Q(byte[] b,int p){return BitConverter.ToInt64(b,p);}static int I(byte[] b,int p){return BitConverter.ToInt32(b,p);}
  internal static bool Pointer(long p){return p>=0x10000&&p<=0x7fffffffffff;}
  internal long Component(long entity,int index,int expectedSize){
   if(!Pointer(entity))return 0;var e=Read(entity+0x18,0x20);long meta=Q(e,0),start=Q(e,8);int row=I(e,0x18);
   if(!Pointer(meta)||!Pointer(start)||row<0||row>1000000)return 0;
   if((Read(meta+(index>>3),1)[0]&(1<<(index&7)))==0)return 0;
   int size=BitConverter.ToUInt16(Read(meta+0xa84+index*2,2),0);if(size!=expectedSize)return 0;
   int offset=BitConverter.ToUInt16(Read(meta+0x84+index*2,2),0);return start+offset+(long)row*size;
  }
  long Entity(long registry,int id){
   if(!Pointer(registry))return 0;byte[] table=Read(registry+0x148,0x48);
   long mask=Q(table,8),capacity=Q(table,0x10),keys=Q(table,0x28),values=Q(table,0x40);
   if(capacity<1||capacity>262144||(capacity&(capacity-1))!=0||!Pointer(mask)||!Pointer(keys)||!Pointer(values))return 0;
   uint hash=(uint)id;unchecked{hash=((hash>>16)^hash)*0x45d9f3b;hash=((hash>>16)^hash)*0x45d9f3b;hash=(hash>>16)^hash;}long slot=hash&(capacity-1);
   for(int n=0;n<capacity;n++,slot=(slot+1)&(capacity-1)){
    if((Read(mask+(slot>>3),1)[0]&(1<<(int)(slot&7)))==0)return 0;
    if(I(Read(keys+slot*4,4),0)==id){long result=Q(Read(values+slot*8,8),0);return Pointer(result)&&I(Read(result+0x10,4),0)==id?result:0;}
   }return 0;
  }
  internal ProgressionSnapshot Sample(){return Sample(-1);}
  internal ProgressionSnapshot Sample(int requestedSlot){
   if(requestedSlot< -1||requestedSlot>7)throw new ArgumentOutOfRangeException("requestedSlot");
   byte[] b=Read(Data,0x80);var s=new ProgressionSnapshot{Player=Q(b,0x10),Selection=Q(b,0x18),Registry=Q(b,0x20),Definitions=Q(b,0x68),Calls=BitConverter.ToUInt32(b,8),Bonus=I(b,4),BaseSkills=I(b,0x70),TotalSkills=I(b,0x78),SkillCalls=BitConverter.ToUInt32(b,0x74),Command=I(b,0),Result=I(b,0xc)};
   if(!Pointer(s.Player)||I(Read(s.Player+0x10,4),0)!=1)return s;
   long xp=Component(s.Player,191,48),level=Component(s.Player,290,44);
   if(xp!=0){byte[] a=Read(xp,48);if(I(a,0)==unchecked((int)0x8dcddaf4)&&I(a,8)==0x24&&I(a,0xc)==3)s.Experience=I(a,0x24);}
   if(level!=0)s.Level=I(Read(level+0x24,4),0);
   try {
   if(!Pointer(s.Selection)||!Pointer(s.Registry))return s;
   s.ObservedQuickSlot=Read(s.Selection+0xf8,1)[0];s.QuickSlot=requestedSlot<0?s.ObservedQuickSlot:requestedSlot;if(s.QuickSlot>=16)return s;
   s.Slot=s.QuickSlot%8;s.Inventory=I(Read(s.Selection+0xd8+(s.QuickSlot/8)*4,4),0);
   long containers=Component(s.Player,268,84);if(containers==0)return s;
   byte[] owned=Read(containers,36);bool owner=false;for(int i=0;i<9;i++)if(I(owned,i*4)==s.Inventory)owner=true;if(!owner)return s;
   s.InventoryEntity=Entity(s.Registry,s.Inventory);s.Stock=Component(s.InventoryEntity,265,96);if(s.Stock==0)return s;
   byte[] slot=Read(s.Stock+s.Slot*12,12);s.Item=BitConverter.ToUInt32(slot,0);s.Amount=I(slot,4);s.Instance=I(slot,8);
   ItemDetails(s);
   }catch(System.ComponentModel.Win32Exception){s.Stock=0;}catch(System.IO.IOException){s.Stock=0;}return s;
  }
  void ItemDetails(ProgressionSnapshot s){
   if(!Pointer(s.Definitions)||s.Item==0)return;
   try {
    byte[] t=Read(s.Definitions+0x38,0x58);long mask=Q(t,8),cap=Q(t,0x10),keys=Q(t,0x28),values=Q(t,0x40);
    if(cap<1||cap>262144||(cap&(cap-1))!=0||!Pointer(mask)||!Pointer(keys)||!Pointer(values))return;long slot=s.Item&(cap-1);
    for(int n=0;n<cap;n++,slot=(slot+1)&(cap-1)){
     if((Read(mask+(slot>>3),1)[0]&(1<<(int)(slot&7)))==0)return;
     if(BitConverter.ToUInt32(Read(keys+slot*4,4),0)!=s.Item)continue;
     long def=Q(Read(values+slot*8,8),0);if(!Pointer(def))return;byte[] header=Read(def,24);
     if(BitConverter.ToUInt32(header,0)!=s.Item)return;
     // ItemResource: stack limit is UInt16 at 14; name uses a relative UTF-8 span at 790.
     s.Definition=def;s.NormalStackLimit=BitConverter.ToUInt16(header,0x14);s.OriginalStackLimit=session.StackLimitOriginal(def,s.NormalStackLimit);
     byte[] span=Read(def+0x790,8);uint offset=BitConverter.ToUInt32(span,0),length=BitConverter.ToUInt32(span,4);
     if(offset<8||offset>1048576||length<1||length>240)return;
     string text=new UTF8Encoding(false,true).GetString(Read(def+0x790+offset,(int)length)).TrimEnd('\0');
     foreach(char c in text)if(char.IsControl(c))return;
     s.Name=text.Replace('_',' ');return;
    }
   }catch{}return;
  }
  internal void SetLimit(ProgressionSnapshot s,int amount){if(!s.Stackable||s.Definition==0)throw new InvalidOperationException("No stack");session.SetStackLimit(s.Definition,s.Item,amount);}
  internal void ResetLimit(ProgressionSnapshot s){if(s.Definition!=0)session.ResetStackLimit(s.Definition);}
  internal static string ClampStackInput(string text){
   string digits=(text??"").Trim();if(digits.StartsWith("+",StringComparison.Ordinal))digits=digits.Substring(1);
   foreach(char c in digits)if(c<'0'||c>'9')return text;
   digits=digits.TrimStart('0');if(digits.Length>5||(digits.Length==5&&string.CompareOrdinal(digits,"50000")>0))return "50000";
   return text;
  }
  internal static int ClampStack(long value){return (int)Math.Max(1,Math.Min(50000,value));}
  internal void AddSkills(int amount){if(amount<1||amount>50000)throw new ArgumentOutOfRangeException("amount");var s=Sample();if(s.SkillCalls==0)throw new InvalidOperationException("Skills not ready");int bonus=checked(s.Bonus+amount);if(bonus>50000)throw new ArgumentOutOfRangeException("amount");GameSession.Write(session.Handle,Data+4,BitConverter.GetBytes(bonus));}
  internal void ClearSkills(){GameSession.Write(session.Handle,Data+4,new byte[4]);}
  internal void Request(int command,int amount,ProgressionSnapshot s){
   if(command!=1&&command!=3)throw new ArgumentOutOfRangeException("command");
   if(!Pointer(s.Player)||s.Calls==0||s.Command!=0)throw new InvalidOperationException("Not ready");
   if(command==1&&(amount<1||amount>1000000))throw new ArgumentOutOfRangeException("amount");
   if(command==3&&(!s.Stackable||amount<1||amount>50000||amount>s.NormalStackLimit))throw new ArgumentOutOfRangeException("amount");
   byte[] b=new byte[0x34];Array.Copy(BitConverter.GetBytes(s.Player),0,b,0,8);Array.Copy(BitConverter.GetBytes(s.InventoryEntity),0,b,8,8);Array.Copy(BitConverter.GetBytes(s.Stock),0,b,16,8);
   Array.Copy(BitConverter.GetBytes(amount),0,b,24,4);Array.Copy(BitConverter.GetBytes(s.Inventory),0,b,28,4);Array.Copy(BitConverter.GetBytes(s.Slot),0,b,32,4);Array.Copy(BitConverter.GetBytes(s.Item),0,b,36,4);Array.Copy(BitConverter.GetBytes(s.Amount),0,b,40,4);Array.Copy(BitConverter.GetBytes(s.ObservedQuickSlot),0,b,44,4);Array.Copy(BitConverter.GetBytes(s.QuickSlot),0,b,48,4);
   GameSession.Write(session.Handle,Data+0xc,new byte[4]);GameSession.Write(session.Handle,Data+0x28,b);GameSession.Write(session.Handle,Data,BitConverter.GetBytes(command));
  }
 }
}
