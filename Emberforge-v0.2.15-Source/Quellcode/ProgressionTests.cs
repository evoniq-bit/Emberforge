using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class ProgressionTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate void Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("PROGRESSION TEST FAILED: "+name);lines.Add("PASS "+name);};
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Test allocation failed");
   try {
    long origin=memory.ToInt64();var hooks=Resources.Manifest().Hooks.Where(h=>h.Name=="ProgressionActions"||h.Name=="AdditionalSkillPoints").Select(h=>Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(h))).ToArray();
    foreach(var hook in hooks)foreach(var relocation in hook.Relocations)relocation.Rva=0x1000;
    byte[] image=GameSession.Image(new HookManifest{AllocationSize=0x6000,DataOffset=0x4000,Hooks=hooks},origin,origin);Marshal.Copy(image,0,memory,image.Length);Marshal.WriteByte(memory,0x1000,0xc3);
    Action<int,int> integer=(p,n)=>Marshal.WriteInt32(memory,p,n);Action<int,long> pointer=(p,n)=>Marshal.WriteInt64(memory,p,origin+n);Func<int,int> value=p=>Marshal.ReadInt32(memory,p);
    pointer(0xc000,0xc100);pointer(0xc130,0xc200);pointer(0x8000,0x8100);integer(0x9010,1);pointer(0x9018,0xa000);pointer(0x9020,0xb000);integer(0x9030,0);
    // Component 191: three integer experience attributes, with pending XP at +28.
    Marshal.WriteByte(memory,0xa000+191/8,(byte)(1<<(191%8)));Marshal.WriteInt16(memory,0xa000+0x84+191*2,0);Marshal.WriteInt16(memory,0xa000+0xa84+191*2,48);
    integer(0xb000,unchecked((int)0x8dcddaf4));integer(0xb008,0x24);integer(0xb00c,3);integer(0xb010,1);integer(0xb028,7);
    integer(0x8118+0xe0,4);integer(0x8100+0xd8,123); // Selected quickbar index is at F8.
    pointer(0x5710,0x9000);pointer(0x5728,0x9000);integer(0x5740,100);
    var stub=new List<byte>();Action<string> add=s=>stub.AddRange(Resources.Hex(s));Action<long> imm=n=>stub.AddRange(BitConverter.GetBytes(n));
    add("535641574883ec6048b8");imm(origin+0x8000);add("48bb");imm(origin+0x9000);add("49bf");imm(origin+0xc000);add("4989db4885db49bb");imm(origin+0x1a00);add("41ffd34883c460415f5e5bc3");
    Marshal.Copy(stub.ToArray(),0,new IntPtr(origin+0x11000),stub.Count);var call=(Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(origin+0x11000),typeof(Call));
    integer(0x5700,1);call();check(value(0xb028)==107&&value(0x5700)==0&&value(0x570c)==1,"XP request is applied once to pending XP");call();check(value(0xb028)==107,"XP is not repeated next frame");
    integer(0x5700,1);pointer(0x5728,0x9050);call();check(value(0xb028)==107&&value(0x570c)==2,"XP rejects stale player identity");pointer(0x5728,0x9000);
    integer(0xb028,int.MaxValue-10);integer(0x5700,1);call();check(value(0xb028)==int.MaxValue-10&&value(0x570c)==2,"XP overflow is rejected");
    integer(0xb024,int.MaxValue);integer(0xb028,0);integer(0x5700,1);call();check(value(0xb028)==0&&value(0x570c)==2,"XP total plus pending reward cannot overflow");integer(0xb024,0);
    integer(0x9310,123);pointer(0x9318,0xd000);pointer(0x9320,0xf000);integer(0x9330,0);Marshal.WriteByte(memory,0xd000+265/8,(byte)(1<<(265%8)));Marshal.WriteInt16(memory,0xd000+0xa84+265*2,96);
    pointer(0x5730,0x9300);pointer(0x5738,0xf000);integer(0x5744,123);integer(0x5748,4);integer(0x574c,123456);integer(0x5750,75);integer(0x5754,4);integer(0x5758,4);integer(0xf030,123456);integer(0xf034,75);integer(0xf038,0);
    integer(0x5740,5000);integer(0x5700,3);call();check(value(0xf034)==5000&&value(0x570c)==1,"Selected stack changes to requested amount");
    integer(0x5750,5000);integer(0x5740,50001);integer(0x5700,3);call();check(value(0xf034)==5000&&value(0x570c)==2,"Native stack limit rejects amounts above 50000");
    integer(0x5740,100);integer(0x5754,3);integer(0x5700,3);call();check(value(0xf034)==5000&&value(0x570c)==2,"Changed quickbar selection is rejected");integer(0x5754,4);
    integer(0xf038,111);integer(0x5700,3);call();check(value(0xf034)==5000&&value(0x570c)==2,"Individual item instances are excluded");integer(0xf038,0);
    integer(0x5750,4999);integer(0x5700,3);call();check(value(0xf034)==5000&&value(0x570c)==2,"Changed quantity is rejected");
    for(int slot=0;slot<8;slot++){
     integer(0xf000+slot*12,123456);integer(0xf004+slot*12,75);integer(0xf008+slot*12,0);integer(0x5748,slot);integer(0x5758,slot);integer(0x5750,75);integer(0x5740,100);integer(0x5700,3);call();
     check(value(0xf004+slot*12)==100&&value(0x570c)==1,"Manual quickbar slot "+(slot+1)+" updates without changing selected slot");
    }
    check(Marshal.ReadInt64(memory,0x5720)==origin+0xc200,"Capture retains persistent entity registry instead of transient job wrapper");
    check(ProgressionEngine.ClampStack(50001)==50000&&ProgressionEngine.ClampStack(long.MaxValue)==50000&&ProgressionEngine.ClampStack(-1)==1,"Input clamping respects 1 to 50000");
    check(ProgressionEngine.ClampStackInput(new string('9',100))=="50000"&&ProgressionEngine.ClampStackInput("000050001")=="50000"&&ProgressionEngine.ClampStackInput("50000")=="50000"&&ProgressionEngine.ClampStackInput("37")=="37","Oversized pasted numbers clamp immediately, including numbers larger than Decimal");
    check(!new ProgressionSnapshot{Stock=1,Item=123,Amount=1,NormalStackLimit=1}.Stackable&&new ProgressionSnapshot{Stock=1,Item=123,Amount=75,NormalStackLimit=1000}.Stackable,"Single items are excluded using the game's normal stack limit");
    stub.Clear();add("534883ec7048bb");imm(origin+0x12000);add("c744245801000000b931000000ba6400000049bb");imm(origin+0x1e00);add("41ffd34883c4705bc3");Marshal.Copy(stub.ToArray(),0,new IntPtr(origin+0x11000),stub.Count);
    integer(0x5704,10);call();check(value(0x12004)==159&&value(0x5770)==149,"Skill bonus adds to the game's derived base points");integer(0x5704,0);call();check(value(0x12004)==149,"Clearing skill bonus restores the derived base");
    check(GameSession.AllFlagOffsets.Contains(0x1700)&&GameSession.AllFlagOffsets.Contains(0x1704),"All off and recovery clear pending actions and skill bonus");
    // An unreadable inventory registry must not suppress otherwise valid XP/skill samples.
    integer(0xb024,17);Marshal.WriteByte(memory,0xa000+268/8,(byte)(1<<(268%8)));Marshal.WriteInt16(memory,0xa000+0x84+268*2,48);Marshal.WriteInt16(memory,0xa000+0xa84+268*2,84);integer(0xb030,123);
    Marshal.WriteByte(memory,0xa000+290/8,(byte)(1<<(290%8)));Marshal.WriteInt16(memory,0xa000+0x84+290*2,256);Marshal.WriteInt16(memory,0xa000+0xa84+290*2,44);integer(0xb124,35);Marshal.WriteInt64(memory,0x5720,0x10000);
    var testSession=new GameSession{Game=System.Diagnostics.Process.GetCurrentProcess(),Handle=new IntPtr(-1),Receipt=new Receipt{Data=origin+0x4000}};
    typeof(GameSession).GetField("installed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(testSession,true);
    var sampled=new ProgressionEngine(testSession).Sample();check(sampled.Experience==17&&sampled.Level==35&&sampled.BaseSkills==149&&sampled.Stock==0,"Inventory read failure preserves valid character progression values");
    Marshal.WriteInt64(memory,0x5720,0);sampled=new ProgressionEngine(testSession).Sample();check(sampled.Experience==17&&sampled.Level==35&&sampled.Stock==0,"Missing inventory registry preserves valid XP and level");
    var view=new MainView(true);try{
     var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var controller=(ProgressionView)typeof(MainView).GetField("progression",flags).GetValue(view);
     typeof(ProgressionView).GetField("session",flags).SetValue(controller,testSession);typeof(ProgressionView).GetField("engine",flags).SetValue(controller,new ProgressionEngine(testSession));typeof(ProgressionView).GetField("preview",flags).SetValue(controller,false);
     controller.Poll(true);check(view.Find<System.Windows.Controls.Button>("XpApply").IsEnabled&&view.Find<System.Windows.Controls.Button>("SkillApply").IsEnabled,"Character action buttons stay enabled when inventory is unavailable");
    }finally{view.Window.Close();}
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
   return string.Join("\r\n",lines);
  }
 }
}
