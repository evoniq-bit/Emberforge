using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

namespace Emberforge {
 internal static class SelfTest {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong TestCall();
  static readonly List<string> results=new List<string>();
  static void Assert(bool condition,string name){if(!condition)throw new Exception("TEST FAILED: "+name);results.Add("PASS "+name);}
  static void Put(long address,int value){Marshal.WriteInt32(new IntPtr(address),value);}
  static int Get(long address){return Marshal.ReadInt32(new IntPtr(address));}
  static void Q(long address,long value){Marshal.WriteInt64(new IntPtr(address),value);}
  internal static string Run(){
   results.Clear();var manifest=Resources.Manifest();
   Assert(manifest.Hooks.Length>=12,"Twelve complete character hooks");
   Assert(manifest.Sha256=="af2f5a1227911d8aa06b3908d6bd0211838211cae14ea91099cb57d0df990781","Explicit supported executable fingerprint");
   long module=0x7ff600000000,remote=module+0x10000000;var image=GameSession.Image(manifest,module,remote);
   Assert(GameSession.AllFlagOffsets.All(offset=>BitConverter.ToInt32(image,manifest.DataOffset+offset)==0),"All character and building functions start disabled");
   foreach(var hook in manifest.Hooks) {
    byte[] original=Resources.Hex(hook.Original),jump=GameSession.Jump(module+hook.Rva,remote+hook.CodeOffset,original.Length);
    Assert(jump[0]==0xe9&&module+hook.Rva+5+BitConverter.ToInt32(jump,1)==remote+hook.CodeOffset,"Patch reaches "+hook.Name);
    foreach(var fix in hook.Relocations)Assert(remote+fix.Offset+4+BitConverter.ToInt32(image,fix.Offset)==module+fix.Rva,"Return reaches original "+hook.Name);
   }
   bool rejected=false;try{GameSession.Jump(0x10000,0x700000000000,7);}catch(InvalidOperationException){rejected=true;}
   Assert(rejected,"Out-of-range detours refused");
   NativeHooks(manifest);
   RemoteRestoration();
   Assert(Resources.Read("Emberforge-Logo.png").Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),"Approved generated logo embedded");
   string xaml=Resources.Text("MainWindow.xaml"),de=Resources.Text("Deutsch.xaml"),en=Resources.Text("English.xaml");
   Assert(xaml.Contains("FuturePage")&&de.Contains("Terrain im Bereich austauschen"),"Terrain is in Zukunft");
   Assert(!xaml.Contains("NavFavorites"),"Favorites stay inside their feature area");
   Assert(!xaml.Contains("x:Name=\"Search\"")&&!xaml.Contains("Text=\"06\""),"Requested search field and sidebar counter removed");
   Assert(!de.Contains("Englisch Â· spÃ¤ter")&&de.Contains(">Englisch</sys:String>")&&en.Contains("English")&&xaml.Contains("LanguageChoice")&&xaml.Contains("DynamicResource"),"German and English resources can be switched from Settings");
   var defaults=Preferences.Default();Assert(defaults.Language=="de"&&defaults.Keys[6]==3&&defaults.Keys.Distinct().Count()==7,"German is the default language and F4 remains AllOff");
   var migrated=Preferences.Normalize(new Preferences{Keys=new[]{8,7,6,5},Favorites=new[]{true,false,true},Global=true});Assert(migrated.Keys.Take(3).SequenceEqual(new[]{8,7,6})&&migrated.Keys[6]==5&&migrated.Keys.Distinct().Count()==7&&migrated.Favorites.Take(3).SequenceEqual(new[]{true,false,true}),"Custom v0.1 shortcuts and favorites survive migration without collisions");
   Assert(xaml.Contains("x:Name=\"Hero\" Stretch=\"UniformToFill\"")&&!xaml.Contains("Binding Source,ElementName=Hero"),"One continuous full-width banner");
   return "Emberforge 0.2.15 - validation\r\n"+string.Join("\r\n",results)+"\r\n\r\n"+BuildingNativeTests.Run()+"\r\n"+ConsumptionTests.Run()+"\r\n"+CraftingNativeTests.Run()+NearbyTests.Run()+DurabilityNativeTests.Run()+HeldPickaxeNativeTests.Run()+CrosshairMaterialNativeTests.Run()+"\r\n"+ProgressionTests.Run()+MovementNativeTests.Run()+"\r\nNative hooks executed against synthetic memory in this test process only.\r\nNo running game's memory was written by this self-test.\r\nNew progression actions still require an in-game test.\r\n";
  }
  internal static void MemoryHost(string path){
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)16384,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Test host allocation failed.");
   long start=memory.ToInt64();var me=Process.GetCurrentProcess();
   var receipt=new Receipt{GamePid=me.Id,GameStart=me.StartTime.ToUniversalTime().Ticks,Data=start+8192,FlagOffsets=GameSession.AllFlagOffsets,Patches=Enumerable.Range(0,Resources.Manifest().Hooks.Length).Select(i=>new ReceiptPatch{Address=start+i*32,Original="b800000000c390",Patched="b801000000c390"}).ToArray()};
   foreach(var patch in receipt.Patches){byte[] original=Resources.Hex(patch.Original);Marshal.Copy(original,0,new IntPtr(patch.Address),original.Length);}
   File.WriteAllText(path,Resources.Json.Serialize(receipt));while(true)Thread.Sleep(100);
  }
  static Process Host(string path){
   var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--memory-test-host \""+path+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};var process=Process.Start(start);
   var watch=Stopwatch.StartNew();while(!File.Exists(path)&&!process.HasExited&&watch.ElapsedMilliseconds<5000)Thread.Sleep(20);
   if(!File.Exists(path)){if(!process.HasExited)process.Kill();throw new Exception("Test host did not become ready.");}return process;
  }
  static void RemoteRestoration(){
   string root=Path.Combine(Path.GetTempPath(),"Emberforge-Test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   Process host=null,parent=null,watchdog=null;IntPtr handle=IntPtr.Zero;
   try{
    string info=Path.Combine(root,"host.json");host=Host(info);var receipt=Resources.Json.Deserialize<Receipt>(File.ReadAllText(info));handle=Native.OpenProcess(0x0438,false,host.Id);Assert(handle!=IntPtr.Zero,"Own test process can be opened for controlled restoration test");
    long definition=receipt.Data-4096;GameSession.Write(handle,definition,BitConverter.GetBytes((uint)123456));GameSession.Write(handle,definition+0x14,BitConverter.GetBytes((ushort)250));
    uint oldProtection;Native.Check(Native.VirtualProtectEx(handle,new IntPtr(definition),(UIntPtr)4096,2,out oldProtection),"Read-only resource fixture");
    Action install=()=>{using(var paused=new PausedThreads(host)){paused.EnsureOutside(receipt.Patches);foreach(var patch in receipt.Patches)GameSession.Write(handle,patch.Address,Resources.Hex(patch.Patched),true);foreach(int offset in receipt.FlagOffsets)GameSession.Write(handle,receipt.Data+offset,BitConverter.GetBytes(1));receipt.ResourcePatches=new[]{new ResourcePatch{Address=definition+0x14,Item=123456,Original="fa00",Patched="8813"}};GameSession.Write(handle,definition+0x14,Resources.Hex("8813"),true);}};
    Func<bool> clean=()=>receipt.Patches.All(p=>GameSession.Read(handle,p.Address,7).SequenceEqual(Resources.Hex(p.Original)))&&receipt.FlagOffsets.All(offset=>BitConverter.ToInt32(GameSession.Read(handle,receipt.Data+offset,4),0)==0)&&BitConverter.ToUInt16(GameSession.Read(handle,definition+0x14,2),0)==250;
    install();GameSession.Restore(receipt,host,handle);Assert(clean(),"Disconnect restores every original code span and disables all feature flags in another process");
    install();byte[] foreign=Resources.Hex("b802000000c390");GameSession.Write(handle,receipt.Patches[0].Address,foreign,true);bool refused=false;try{GameSession.Restore(receipt,host,handle);}catch(InvalidOperationException){refused=true;}
    Assert(refused&&GameSession.Read(handle,receipt.Patches[0].Address,7).SequenceEqual(foreign),"Foreign modifications are preserved instead of overwritten during disconnect");
    GameSession.Write(handle,receipt.Patches[0].Address,Resources.Hex(receipt.Patches[0].Patched),true);GameSession.Restore(receipt,host,handle);Assert(clean(),"Restoration can finish after a conflicting modification is removed");
    var dataSession=new GameSession{Game=host,Handle=Native.OpenProcess(0x0438,false,host.Id),Receipt=Resources.Json.Deserialize<Receipt>(Resources.Json.Serialize(receipt))};
    typeof(GameSession).GetField("installed",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(dataSession,true);
    try{
     dataSession.SetStackLimit(definition,123456,5000);Assert(BitConverter.ToUInt16(GameSession.Read(handle,definition+0x14,2),0)==5000&&dataSession.StackLimitCount==1&&dataSession.StackLimitOriginal(definition,5000)==250,"Stack limit changes a read-only resource and records original value");
     bool tooLarge=false;try{dataSession.SetStackLimit(definition,123456,50001);}catch(ArgumentOutOfRangeException){tooLarge=true;}Assert(tooLarge,"Resource stack limit refuses more than 50000");
     bool single=false;try{dataSession.SetStackLimit(definition,123456,1);}catch(ArgumentOutOfRangeException){single=true;}Assert(single,"Stack limit refuses invalid single-item limit");
     dataSession.SetStackLimit(definition,123456,8000);GameSession.Write(handle,definition+0x14,Resources.Hex("8813"),true);
     dataSession.ResetStackLimit(definition);Assert(BitConverter.ToUInt16(GameSession.Read(handle,definition+0x14,2),0)==250,"Restoration handles a journal updated before the new resource write");
     dataSession.ResetStackLimit(definition);Assert(BitConverter.ToUInt16(GameSession.Read(handle,definition+0x14,2),0)==250&&dataSession.StackLimitCount==0,"Per-item reset restores original stack limit");
     dataSession.SetStackLimit(definition,123456,5000);dataSession.AllOff();Assert(BitConverter.ToUInt16(GameSession.Read(handle,definition+0x14,2),0)==250&&dataSession.StackLimitCount==0,"All off restores stack resources as well as feature flags");
    }finally{dataSession.Dispose();}
    parent=Host(Path.Combine(root,"parent.json"));receipt.ParentPid=parent.Id;receipt.ParentStart=parent.StartTime.ToUniversalTime().Ticks;install();string journal=Path.Combine(root,"receipt.json");var earlyReceipt=Resources.Json.Deserialize<Receipt>(Resources.Json.Serialize(receipt));earlyReceipt.ResourcePatches=null;File.WriteAllText(journal,Resources.Json.Serialize(earlyReceipt));
    watchdog=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--watchdog \""+journal+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
    Thread.Sleep(250);File.WriteAllText(journal,Resources.Json.Serialize(receipt));parent.Kill();parent.WaitForExit();Assert(watchdog.WaitForExit(5000),"Recovery worker finishes after its synthetic parent is terminated");Assert(clean()&&!File.Exists(journal),"Recovery worker reloads latest receipt and restores code, flags and later stack-limit changes");
   }finally{
    if(handle!=IntPtr.Zero)Native.CloseHandle(handle);foreach(var p in new[]{watchdog,parent,host})if(p!=null){try{if(!p.HasExited){p.Kill();p.WaitForExit(3000);}}finally{p.Dispose();}}
    foreach(string path in Directory.GetFiles(root))File.Delete(path);Directory.Delete(root);
   }
  }
  static void NativeHooks(HookManifest manifest){
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x10000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Native test allocation failed.");
   try {
    long origin=memory.ToInt64(),data=origin+manifest.DataOffset;
    byte[] image=GameSession.Image(manifest,origin,origin);
    foreach(var hook in manifest.Hooks)foreach(var fix in hook.Relocations)if(fix.Kind!="Address"){image[fix.Offset-1]=0xc3;for(int i=0;i<4;i++)image[fix.Offset+i]=0x90;}
    HookSpec commit=manifest.Hooks.Single(h=>h.Name=="AttributeCommit");int end=commit.CodeOffset+Resources.Hex(commit.Code).Length;
    image[end-6]=0xc3;for(int i=1;i<6;i++)image[end-6+i]=0x90;
    Marshal.Copy(image,0,memory,image.Length);
    long hblob=origin+0x6000,sblob=origin+0x6400,other=origin+0x6800,mblob=origin+0x6c00,cblob=origin+0x7000,fblob=origin+0x7400,queryOutput=origin+0x7800;
    foreach(long blob in new[]{hblob,sblob,other,mblob,cblob,fblob}){Put(blob+8,0x40);Put(blob+12,16);Put(blob+16,1);}
    long hv=hblob+0x40,sv=sblob+0x40,ov=other+0x40;
    Action<string,long,long,int,int> invoke=(name,blob,values,index,amount)=>{
     var bytes=new List<byte>();Action<string> add=s=>bytes.AddRange(Resources.Hex(s));Action<long> imm=v=>bytes.AddRange(BitConverter.GetBytes(v));
     add("5355565741544155415641574881ec88000000"); // preserve all nonvolatile general registers; aligned scratch/shadow space
     add("48bb");imm(name=="ShroudQuery"?queryOutput:blob);add("48bf");imm(blob);
     if(name=="FallDamage"||name=="ColdDamage"){add("b9");bytes.AddRange(BitConverter.GetBytes(index));}else{add("48b9");imm(values);}add("ba");bytes.AddRange(BitConverter.GetBytes(index));
     add("48be");imm(blob);add("49b8");imm(values);add("41b9");bytes.AddRange(BitConverter.GetBytes(amount));
     if(name=="ShroudQuery"){add("48b8");imm(origin+(amount==1?0x2346b0:amount==2?0x2346ce:0x2346f0));add("4889442430");}
     add("b8");bytes.AddRange(BitConverter.GetBytes(amount));
     add("49bb");imm(origin+manifest.Hooks.Single(h=>h.Name==name).CodeOffset);add("41ffd34881c488000000415f415e415d415c5f5e5d5bc3");
     long wrapper=origin+0x8000;Marshal.Copy(bytes.ToArray(),0,new IntPtr(wrapper),bytes.Count);
     Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x10000);
     var call=(TestCall)Marshal.GetDelegateForFunctionPointer(new IntPtr(wrapper),typeof(TestCall));call();
    };
    Put(hv+2*4,100);Put(hv+7*4,250);
    invoke("HealthCapture",hblob,hv,2,0);invoke("HealthMaximum",hblob,hv,7,0);
    Assert(Get(hv+2*4)==100,"Disabled health captures without changing health");
    Assert(Marshal.ReadInt64(new IntPtr(data+0x18))==hv+8,"Health capture retains exact character address");
    Put(data,1);invoke("HealthMaximum",hblob,hv,7,0);
    Assert(Get(hv+2*4)==250,"Health fills to actual maximum index 7 (no adjacent-field assumption)");
    Put(hv+2*4,1);invoke("AttributeCommit",hblob,hv,2,0);
    Assert(Get(hv+2*4)==250,"Health restored immediately after attribute updates");
    Put(ov+2*4,17);Put(ov+7*4,999);invoke("AttributeCommit",other,ov,2,0);
    Assert(Get(ov+2*4)==17,"Health does not change an unrelated attribute blob");
    Put(data,0);Put(hv+2*4,12);invoke("AttributeCommit",hblob,hv,2,0);
    Assert(Get(hv+2*4)==12,"Turning health off restores normal attribute behavior");
    Put(sv+1*4,40);Put(sv+6*4,140);invoke("StaminaCapture",sblob,sv,1,0);invoke("StaminaMaximum",sblob,sv,6,0);
    Assert(Get(sv+4)==40,"Disabled stamina does not refill");
    Put(data+8,1);invoke("StaminaMaximum",sblob,sv,6,0);
    Assert(Get(sv+4)==140,"Stamina fills to actual maximum index 6");
    Put(sv+4,2);invoke("AttributeCommit",sblob,sv,1,0);
    Assert(Get(sv+4)==140,"Stamina restored after consumption update");
    Put(data+8,0);Put(sv+4,33);invoke("AttributeCommit",sblob,sv,1,0);
    Assert(Get(sv+4)==33,"Turning stamina off restores normal consumption");
    Put(data+0x184,1);invoke("StaminaMaximum",sblob,sv,6,0);Assert(Get(sv+4)==140,"Flight retains local stamina independently of stamina toggle");
    Put(sv+4,1);invoke("AttributeCommit",sblob,sv,1,0);Assert(Get(sv+4)==140,"Flight retains stamina after an attribute commit");
    Put(ov+4,19);invoke("AttributeCommit",other,ov,1,0);Assert(Get(ov+4)==19,"Flight stamina preservation excludes other actors");
    Put(data+0x184,0);Put(sv+4,20);invoke("AttributeCommit",sblob,sv,1,0);Assert(Get(sv+4)==20,"Disabling flight restores independent ordinary stamina behavior");
    Put(hv+8,250);Put(data+4,0);invoke("FallDamage",hblob,hv,2,25);
    Assert(Get(hv+8)==25,"Disabled fall protection executes the original health write");
    Put(hv+8,250);Put(data+4,1);invoke("FallDamage",hblob,hv,2,25);
    Assert(Get(hv+8)==250,"Fall protection blocks the captured character's health loss");
    invoke("FallDamage",other,ov,2,25);Assert(Get(ov+8)==25,"Fall protection preserves other entities' original behavior");
    Put(data,1);Put(data+4,0);Put(hv+8,250);invoke("FallDamage",hblob,hv,2,0);
    Assert(Get(hv+8)==250,"Health protection also preserves health during a lethal fall");
    Put(hv+7*4,0);Put(hv+8,37);invoke("AttributeCommit",hblob,hv,2,0);
    Assert(Get(hv+8)==37,"Invalid zero maximum is refused");
    Put(hv+7*4,20000000);invoke("AttributeCommit",hblob,hv,2,0);
    Assert(Get(hv+8)==37,"Implausible maximum is refused");
    Put(hblob+12,2);invoke("AttributeCommit",hblob,hv,2,0);
    Assert(Get(hv+8)==37,"Out-of-bounds attribute indexes are refused");
    Put(data,0);Put(hblob+12,16);
    long mv=mblob+0x40,cv=cblob+0x40,fv=fblob+0x40;
    foreach(var spec in new[]{new[]{"Mana","112","128","160"},new[]{"Cold","116","176","208"}}){
     string name=spec[0];int flag=int.Parse(spec[1]),captureData=int.Parse(spec[2]),counter=int.Parse(spec[3]);long blob=name=="Mana"?mblob:cblob,values=blob+0x40;
     Put(values+12,25);Put(values+36,360);invoke(name+"Capture",blob,values,3,0);invoke(name+"Maximum",blob,values,9,0);
     Assert(Get(values+12)==25,name+" disabled captures without refilling");
     Assert(Get(data+captureData+0x10)==3&&Get(data+captureData+0x14)==9,name+" stores actual separate current/max indexes");
     Put(data+flag,1);invoke(name+"Maximum",blob,values,9,0);Assert(Get(values+12)==360,name+" maximum hook refills");
     Put(values+12,-5);invoke("AttributeCommit",blob,values,3,0);Assert(Get(values+12)==360,name+" restores immediately after consumption, independently of health");
     Put(ov+12,19);invoke("AttributeCommit",other,ov,3,0);Assert(Get(ov+12)==19,name+" ignores unrelated attributes");
     Put(values+36,0);Put(values+12,14);invoke("AttributeCommit",blob,values,3,0);Assert(Get(values+12)==14,name+" rejects invalid maximum");
     Put(values+36,360);Put(data+flag,0);invoke("AttributeCommit",blob,values,3,0);Assert(Get(values+12)==14,name+" off restores consumption");
    }
    Put(hv+8,200);Put(data+0x74,1);invoke("ColdDamage",hblob,hv,2,35);Assert(Get(hv+8)==200,"Cold damage blocked while life switch is off");
    Put(ov+8,200);invoke("ColdDamage",other,ov,2,35);Assert(Get(ov+8)==165,"Cold damage retains subtraction for another entity");
    Put(data+0x74,0);invoke("ColdDamage",hblob,hv,2,35);Assert(Get(hv+8)==165,"Cold damage resumes after frost switch is off");
    Put(fv+4,20);Put(fv+32,540);Put(data+0x78,0);
    invoke("ShroudQuery",fblob,fv,1,1);invoke("ShroudQuery",fblob,fv,8,2);
    Assert(Get(fv+4)==20&&Get(queryOutput+4)==540,"Shroud current/max query preserves getter output while off");
    Assert(Get(data+0xf0)==1&&Get(data+0xf4)==8&&Get(data+0x100)==1,"Shroud records exact indexes only from matching player caller");
    Put(data+0x78,1);invoke("ShroudQuery",fblob,fv,8,2);Assert(Get(fv+4)==540,"Shroud maximum query fills remaining timer");
    Put(fv+4,-1);invoke("AttributeCommit",fblob,fv,1,0);Assert(Get(fv+4)==540,"Shroud depleted time restored before subsequent danger checks, with health off");
    invoke("ShroudQuery",other,ov,4,0);Assert(Marshal.ReadInt64(new IntPtr(data+0xe0))==fblob,"Unrelated generic query caller cannot replace Shroud capture");
    Put(ov+4,22);invoke("ShroudQuery",other,ov,8,2);Assert(Get(ov+4)==22,"Shroud max caller with unrelated blob does not refill it");
    Put(data+0x78,0);Put(fv+4,21);invoke("AttributeCommit",fblob,fv,1,0);Assert(Get(fv+4)==21,"Shroud off restores timer depletion");
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}
