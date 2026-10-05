using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Reflection;
using System.Web.Script.Serialization;

namespace Emberforge {
 internal static class Native {
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr length,out UIntPtr read);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] buffer,UIntPtr length,out UIntPtr written);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,UIntPtr size,uint type,uint protection);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool VirtualProtectEx(IntPtr process,IntPtr address,UIntPtr size,uint protection,out uint old);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool FlushInstructionCache(IntPtr process,IntPtr address,UIntPtr size);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern IntPtr OpenThread(uint access,bool inherit,int id);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern uint SuspendThread(IntPtr thread);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool GetThreadContext(IntPtr thread,IntPtr context);
  [DllImport("kernel32.dll",SetLastError=true)] internal static extern uint ResumeThread(IntPtr thread);
  [DllImport("kernel32.dll")] internal static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint type,uint protection);
  [DllImport("kernel32.dll")] internal static extern bool VirtualFree(IntPtr address,UIntPtr size,uint type);
  [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
  [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window,int id);
  internal static void Check(bool success,string operation) { if(!success) throw new Win32Exception(Marshal.GetLastWin32Error(),operation); }
 }
 internal sealed class PausedThreads : IDisposable {
  readonly List<IntPtr> handles=new List<IntPtr>();
  internal PausedThreads(Process process) {
   var seen=new HashSet<int>();
   try {
    for(int pass=0;pass<4;pass++) {
     process.Refresh();bool added=false;
     foreach(ProcessThread thread in process.Threads) {
      if(seen.Contains(thread.Id)) continue;
      IntPtr handle=Native.OpenThread(0x0a,false,thread.Id);
      if(handle==IntPtr.Zero) { if(Marshal.GetLastWin32Error()==87) continue;throw new Win32Exception(Marshal.GetLastWin32Error(),"Spiel-Thread konnte nicht angehalten werden."); }
      if(Native.SuspendThread(handle)==uint.MaxValue) {
       int error=Marshal.GetLastWin32Error();Native.CloseHandle(handle);
       if(error==87)continue;throw new Win32Exception(error,"Spiel-Thread konnte nicht angehalten werden.");
      }
      handles.Add(handle);seen.Add(thread.Id);added=true;
     }
     if(!added) return;
    }
    throw new InvalidOperationException("Spiel-Threads ändern sich gerade. Bitte erneut versuchen.");
   } catch { Dispose();throw; }
  }
  internal void EnsureOutside(ReceiptPatch[] patches) {
   IntPtr raw=Marshal.AllocHGlobal(1250);
   try {
    IntPtr context=new IntPtr((raw.ToInt64()+15)&~15L);
    foreach(var thread in handles){Marshal.Copy(new byte[1232],0,context,1232);Marshal.WriteInt32(context,48,0x100001);Native.Check(Native.GetThreadContext(thread,context),"Spiel-Thread konnte nicht geprüft werden.");long ip=Marshal.ReadInt64(context,248);
     if(patches.Any(p=>ip>=p.Address&&ip<p.Address+Resources.Hex(p.Original).Length))throw new InvalidOperationException("Das Spiel verwendet die Funktion gerade. Bitte erneut versuchen.");
    }
   }finally{Marshal.FreeHGlobal(raw);}
  }
  public void Dispose() { for(int i=handles.Count-1;i>=0;i--) {Native.ResumeThread(handles[i]);Native.CloseHandle(handles[i]);}handles.Clear(); }
 }
 public sealed class Relocation { public int Offset{get;set;} public int Rva{get;set;} public string Kind{get;set;} }
 public sealed class HookSpec {
  public string Name{get;set;} public int Rva{get;set;} public int CodeOffset{get;set;}
  public string Original{get;set;} public string Code{get;set;} public string Context{get;set;}
  public Relocation[] Relocations{get;set;}
 }
 public sealed class HookManifest {
  public string Build{get;set;} public string Sha256{get;set;} public int AllocationSize{get;set;} public int DataOffset{get;set;} public HookSpec[] Hooks{get;set;}
 }
 public sealed class ReceiptPatch { public long Address{get;set;} public string Original{get;set;} public string Patched{get;set;} }
 public sealed class ResourcePatch {public long Address{get;set;}public uint Item{get;set;}public string Original{get;set;}public string Patched{get;set;}public string Previous{get;set;}}
 public sealed class Receipt {
  public int GamePid{get;set;} public long GameStart{get;set;} public int ParentPid{get;set;} public long ParentStart{get;set;}
  public long Data{get;set;} public int[] FlagOffsets{get;set;} public ReceiptPatch[] Patches{get;set;}
  public ResourcePatch[] ResourcePatches{get;set;}
 }
 internal static class Resources {
  internal static byte[] Read(string name) {using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(s==null)throw new InvalidOperationException("Fehlende Programmdatei: "+name);using(var m=new MemoryStream()){s.CopyTo(m);return m.ToArray();}}}
  internal static string Text(string name){return Encoding.UTF8.GetString(Read(name)).TrimStart('\uFEFF');}
  internal static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  internal static HookManifest Manifest(){return Json.Deserialize<HookManifest>(Text("CharacterHooks.json"));}
  internal static byte[] Hex(string text){return Enumerable.Range(0,text.Length/2).Select(i=>Convert.ToByte(text.Substring(i*2,2),16)).ToArray();}
  internal static string Hex(byte[] bytes){return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}
 }
 internal static class Files {
  internal static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Emberforge");
  internal static string PathOf(string name){Directory.CreateDirectory(Root);return Path.Combine(Root,name);}
  internal static void Log(string message){try{File.AppendAllText(PathOf("Emberforge.log"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+message+Environment.NewLine);}catch{}}
 }
 internal sealed class GameSession : IDisposable {
  internal static readonly int[] FlagOffsets={0,4,8,0x70,0x74,0x78};
  internal static readonly int[] AllFlagOffsets={0,4,8,0x70,0x74,0x78,0x7c,0x1000,0x1004,0x1600,0x1604,0x1610,0x1700,0x1704,0x180,0x184,0x188};
  internal Process Game;internal IntPtr Handle;internal long Base;internal long Allocation;internal HookManifest Manifest;
  internal Receipt Receipt;string journal;bool installed;
  internal bool IsAlive{get{try{return Game!=null&&!Game.HasExited;}catch{return false;}}}
  internal bool IsConnected{get{return installed&&IsAlive;}}
  internal static byte[] Read(IntPtr handle,long address,int count) {var bytes=new byte[count];UIntPtr n;Native.Check(Native.ReadProcessMemory(handle,new IntPtr(address),bytes,(UIntPtr)count,out n),"Spielwerte konnten nicht gelesen werden.");if(n.ToUInt64()!=(ulong)count)throw new IOException("Unvollständiger Speicherzugriff.");return bytes;}
  internal static void Write(IntPtr handle,long address,byte[] bytes,bool code=false) {
   uint old=0;bool changed=false;
   try {
    if(code){Native.Check(Native.VirtualProtectEx(handle,new IntPtr(address),(UIntPtr)bytes.Length,0x40,out old),"Spielcode konnte nicht freigegeben werden.");changed=true;}
    UIntPtr n;Native.Check(Native.WriteProcessMemory(handle,new IntPtr(address),bytes,(UIntPtr)bytes.Length,out n),"Spielwerte konnten nicht geschrieben werden.");
    if(n.ToUInt64()!=(ulong)bytes.Length)throw new IOException("Unvollständiger Schreibzugriff.");
    if(code)Native.Check(Native.FlushInstructionCache(handle,new IntPtr(address),(UIntPtr)bytes.Length),"Code-Cache konnte nicht aktualisiert werden.");
   } finally {if(changed){uint unused;Native.Check(Native.VirtualProtectEx(handle,new IntPtr(address),(UIntPtr)bytes.Length,old,out unused),"Speicherschutz konnte nicht wiederhergestellt werden.");}}
  }
  internal static byte[] Jump(long source,long target,int length) {
   if(length<5)throw new ArgumentException("Hook zu kurz.");long delta=target-source-5;
   if(delta<int.MinValue||delta>int.MaxValue)throw new InvalidOperationException("Sprungziel außerhalb der Reichweite.");
   var bytes=Enumerable.Repeat((byte)0x90,length).ToArray();bytes[0]=0xe9;Buffer.BlockCopy(BitConverter.GetBytes((int)delta),0,bytes,1,4);return bytes;
  }
  internal static byte[] Image(HookManifest manifest,long module,long allocation) {
   var image=new byte[manifest.AllocationSize];
   foreach(var hook in manifest.Hooks) {
    byte[] code=Resources.Hex(hook.Code);
    foreach(var fix in hook.Relocations) {
     int local=fix.Offset-hook.CodeOffset;
     long delta=module+fix.Rva-(allocation+fix.Offset+4);
     if(delta<int.MinValue||delta>int.MaxValue)throw new InvalidOperationException("Hook außerhalb der Reichweite.");
     Buffer.BlockCopy(BitConverter.GetBytes((int)delta),0,code,local,4);
    }
    Buffer.BlockCopy(code,0,image,hook.CodeOffset,code.Length);
   }
   if(manifest.DataOffset==0x4000&&manifest.Hooks.Any(h=>h.Name=="BuildingInspector"))foreach(int offset in new[]{0x1008,0x100c,0x1080,0x1088})Buffer.BlockCopy(BitConverter.GetBytes(-1),0,image,manifest.DataOffset+offset,4);
   return image;
  }
  long AllocateNear() {
   // One allocation within range of every checked hook; no DLL loading.
   for(long gap=0x10000000;gap<0x70000000;gap+=0x10000) {
    foreach(long candidate in new[]{(Base+gap)&~0xffffL,(Base-gap)&~0xffffL}) {
     if(candidate<0x10000)continue;
     IntPtr p=Native.VirtualAllocEx(Handle,new IntPtr(candidate),(UIntPtr)Manifest.AllocationSize,0x3000,4);
     if(p!=IntPtr.Zero)return p.ToInt64();
    }
   }
   throw new InvalidOperationException("Kein passender Speicherbereich verfügbar. Spiel neu starten.");
  }
  internal void Connect() {
   if(IsConnected)return;
   Receipt=null;journal=null;Allocation=Base=0;
   Manifest=Resources.Manifest();
   var games=Process.GetProcessesByName("enshrouded");
   if(games.Length!=1)throw new InvalidOperationException(games.Length==0?"Enshrouded zuerst starten und deine Welt laden.":"Mehrere Spielprozesse gefunden. Bitte nur eine Instanz öffnen.");
   Game=games[0];
   try {
    Handle=Native.OpenProcess(0x0438,false,Game.Id);
    if(Handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"Kein Zugriff auf Enshrouded.");
    var module=Game.MainModule;Base=module.BaseAddress.ToInt64();
    string hash;using(var file=File.OpenRead(module.FileName))using(var sha=SHA256.Create())hash=Resources.Hex(sha.ComputeHash(file));
    if(hash!=Manifest.Sha256)throw new InvalidOperationException("Diese Spielversion ist noch nicht unterstützt. Emberforge benötigt eine Anpassung.");
    foreach(var hook in Manifest.Hooks) {
     byte[] context=Resources.Hex(hook.Context);
     if(!Read(Handle,Base+hook.Rva,context.Length).SequenceEqual(context))throw new InvalidOperationException("Die Funktion "+hook.Name+" wurde bereits verändert. Andere Trainer ausschalten und erneut verbinden.");
    }
    Allocation=AllocateNear();byte[] image=Image(Manifest,Base,Allocation);Write(Handle,Allocation,image);
    uint old;Native.Check(Native.VirtualProtectEx(Handle,new IntPtr(Allocation),(UIntPtr)Manifest.DataOffset,0x20,out old),"Hook-Code konnte nicht geschützt werden.");
    Native.Check(Native.FlushInstructionCache(Handle,new IntPtr(Allocation),(UIntPtr)Manifest.DataOffset),"Hook-Code konnte nicht aktualisiert werden.");
    Receipt=new Receipt{GamePid=Game.Id,GameStart=Game.StartTime.ToUniversalTime().Ticks,ParentPid=Process.GetCurrentProcess().Id,ParentStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,Data=Allocation+Manifest.DataOffset,FlagOffsets=AllFlagOffsets,
     Patches=Manifest.Hooks.Select(h=>new ReceiptPatch{Address=Base+h.Rva,Original=h.Original,Patched=Resources.Hex(Jump(Base+h.Rva,Allocation+h.CodeOffset,Resources.Hex(h.Original).Length))}).ToArray()};
    journal=Files.PathOf("session-"+Receipt.ParentPid+"-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(journal,Resources.Json.Serialize(Receipt));
    var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--watchdog \""+journal+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
    using(var watchdog=Process.Start(start)){if(watchdog==null)throw new InvalidOperationException("Wiederherstellungsdienst konnte nicht starten.");}
    using(var paused=new PausedThreads(Game)) {
     paused.EnsureOutside(Receipt.Patches);
     foreach(var hook in Manifest.Hooks)if(!Read(Handle,Base+hook.Rva,Resources.Hex(hook.Context).Length).SequenceEqual(Resources.Hex(hook.Context)))throw new InvalidOperationException("Spielcode wurde während der Verbindung verändert.");
     foreach(var patch in Receipt.Patches)Write(Handle,patch.Address,Resources.Hex(patch.Patched),true);
    }
    installed=true;Files.Log("Verbunden PID="+Game.Id+" Build="+Manifest.Build+"; alle Schalter aus.");
   } catch {
    if(Receipt!=null&&IsAlive)try{Restore(Receipt,Game,Handle);}catch(Exception error){Files.Log("ROLLBACK: "+error);}
    if(Handle!=IntPtr.Zero){Native.CloseHandle(Handle);Handle=IntPtr.Zero;}
    Game=null;installed=false;throw;
   }
  }
  internal byte[] Sample(){if(!IsConnected)return null;return Read(Handle,Receipt.Data,0x120);}
  internal void Set(int index,bool on){if(!IsConnected)throw new InvalidOperationException("Zuerst mit Enshrouded verbinden.");if(index<0||index>=FlagOffsets.Length)throw new ArgumentOutOfRangeException("index");Write(Handle,Receipt.Data+FlagOffsets[index],BitConverter.GetBytes(on?1:0));Files.Log("Funktion "+index+"="+on);}
  void SaveReceipt(){if(journal==null)return;string temporary=journal+".tmp";File.WriteAllText(temporary,Resources.Json.Serialize(Receipt));if(File.Exists(journal))File.Replace(temporary,journal,null);else File.Move(temporary,journal);}
  internal int StackLimitOriginal(long definition,int current){var patch=(Receipt.ResourcePatches??new ResourcePatch[0]).FirstOrDefault(p=>p.Address==definition+0x14);return patch==null?current:BitConverter.ToUInt16(Resources.Hex(patch.Original),0);}
  internal int StackLimitCount{get{return IsConnected?(Receipt.ResourcePatches??new ResourcePatch[0]).Length:0;}}
  internal void SetStackLimit(long definition,uint item,int limit){
   if(!IsConnected)throw new InvalidOperationException("Not connected");if(limit<2||limit>50000)throw new ArgumentOutOfRangeException("limit");
   using(var paused=new PausedThreads(Game)){
    paused.EnsureOutside(Receipt.Patches);byte[] header=Read(Handle,definition,24);if(BitConverter.ToUInt32(header,0)!=item)throw new InvalidOperationException("Item changed");
    int current=BitConverter.ToUInt16(header,0x14),original=StackLimitOriginal(definition,current);if(original<=1||limit<original)throw new ArgumentOutOfRangeException("limit");
    if(limit==original){RestoreResources(Receipt,Handle,definition+0x14);SaveReceipt();return;}
    var patches=(Receipt.ResourcePatches??new ResourcePatch[0]).ToList();var patch=patches.FirstOrDefault(p=>p.Address==definition+0x14);
    if(patch!=null&&current!=BitConverter.ToUInt16(Resources.Hex(patch.Patched),0))throw new InvalidOperationException("Stack limit changed externally");
    var updated=new ResourcePatch{Address=definition+0x14,Item=item,Original=patch==null?Resources.Hex(BitConverter.GetBytes((ushort)original)):patch.Original,Previous=patch==null?null:patch.Patched,Patched=Resources.Hex(BitConverter.GetBytes((ushort)limit))};
    if(patch!=null)patches.Remove(patch);patches.Add(updated);Receipt.ResourcePatches=patches.ToArray();SaveReceipt();Write(Handle,updated.Address,Resources.Hex(updated.Patched),true);
   }
  }
  internal void ResetStackLimit(long definition){if(!IsConnected)return;using(var paused=new PausedThreads(Game)){RestoreResources(Receipt,Handle,definition+0x14);SaveReceipt();}}
  internal static void RestoreResources(Receipt receipt,IntPtr handle,long only=0){
   var keep=new List<ResourcePatch>();foreach(var patch in receipt.ResourcePatches??new ResourcePatch[0]){
    if(only!=0&&patch.Address!=only){keep.Add(patch);continue;}
    byte[] header;try{header=Read(handle,patch.Address-0x14,24);}catch(Win32Exception){continue;}
    if(BitConverter.ToUInt32(header,0)!=patch.Item)continue;
    byte[] current=header.Skip(0x14).Take(2).ToArray();if(current.SequenceEqual(Resources.Hex(patch.Patched))||(patch.Previous!=null&&current.SequenceEqual(Resources.Hex(patch.Previous))))Write(handle,patch.Address,Resources.Hex(patch.Original),true);
    else if(!current.SequenceEqual(Resources.Hex(patch.Original)))Files.Log("Stapelgrenze extern geändert; fremden Wert beibehalten.");
   }receipt.ResourcePatches=keep.ToArray();
  }
  internal void AllOff(){if(IsConnected){using(var paused=new PausedThreads(Game)){ClearFlags(Handle,Receipt);RestoreResources(Receipt,Handle);}SaveReceipt();}}
  static void ClearFlags(IntPtr handle,Receipt receipt){foreach(int offset in receipt.FlagOffsets??new[]{0,4,8})Write(handle,receipt.Data+offset,new byte[4]);}
  internal static void Restore(Receipt receipt,Process game,IntPtr handle) {
   if(game.HasExited)return;
   // Retain the tiny allocation: a thread paused inside a hook can finish safely.
   // It becomes unreachable after restoration and Windows reclaims it on game exit.
   using(var paused=new PausedThreads(game)) {
    paused.EnsureOutside(receipt.Patches);
    ClearFlags(handle,receipt);
    RestoreResources(receipt,handle);
    foreach(var patch in receipt.Patches) {
     byte[] current=Read(handle,patch.Address,Resources.Hex(patch.Patched).Length);
     if(current.SequenceEqual(Resources.Hex(patch.Patched)))Write(handle,patch.Address,Resources.Hex(patch.Original),true);
     else if(!current.SequenceEqual(Resources.Hex(patch.Original)))throw new InvalidOperationException("Eine andere Anwendung hat den Spielcode verändert. Wiederherstellung angehalten.");
    }
   }
  }
  public void Dispose() {
   if(Handle!=IntPtr.Zero) {
    if(IsAlive&&Receipt!=null)Restore(Receipt,Game,Handle);
    Native.CloseHandle(Handle);Handle=IntPtr.Zero;
   }
   installed=false;Game=null;Receipt=null;Allocation=Base=0;
   if(journal!=null&&File.Exists(journal))File.Delete(journal);
   journal=null;
   Files.Log("Verbindung getrennt; ursprünglicher Spielcode wiederhergestellt.");
  }
  internal static void Watchdog(string path) {
   try {
    if(!File.Exists(path))return;var receipt=Resources.Json.Deserialize<Receipt>(File.ReadAllText(path));
    try {using(var parent=Process.GetProcessById(receipt.ParentPid)){if(parent.StartTime.ToUniversalTime().Ticks==receipt.ParentStart)parent.WaitForExit();}}catch(ArgumentException){}
    if(!File.Exists(path))return;
    var latest=Resources.Json.Deserialize<Receipt>(File.ReadAllText(path));
    if(latest.GamePid!=receipt.GamePid||latest.GameStart!=receipt.GameStart||latest.ParentPid!=receipt.ParentPid||latest.ParentStart!=receipt.ParentStart||latest.Data!=receipt.Data)throw new InvalidOperationException("Recovery receipt identity changed.");receipt=latest;
    using(var game=Process.GetProcessById(receipt.GamePid)) {
     if(game.StartTime.ToUniversalTime().Ticks!=receipt.GameStart)return;
     IntPtr handle=Native.OpenProcess(0x0438,false,game.Id);
     if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
     try {Restore(receipt,game,handle);Files.Log("Wiederherstellungsdienst: Originalcode wiederhergestellt.");}finally{Native.CloseHandle(handle);}
    }
    File.Delete(path);
   }catch(ArgumentException){try{File.Delete(path);}catch{}}catch(Exception ex){Files.Log("Wiederherstellungsdienst: "+ex);}
  }
 }
}
