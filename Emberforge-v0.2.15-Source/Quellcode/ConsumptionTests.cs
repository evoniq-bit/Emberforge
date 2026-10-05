using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class ConsumptionTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("CONSUMPTION TEST FAILED: "+name);lines.Add("PASS "+name);};
   HookSpec actual=Resources.Manifest().Hooks.Single(h=>h.Name=="BuildingNoConsumption");
   var copy=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(actual));
   int[] sites={0x280e72,0x282b6c,0x282bc9};
   foreach(var fix in copy.Relocations){int index=Array.IndexOf(sites,fix.Rva);fix.Rva=index>=0?0x10000+index*0x100+35:0x11000;}
   var manifest=new HookManifest{AllocationSize=0x6000,DataOffset=0x4000,Hooks=new[]{copy}};
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Consumption test memory unavailable.");
   try{
    long origin=memory.ToInt64();byte[] image=GameSession.Image(manifest,origin,origin);Marshal.Copy(image,0,memory,image.Length);
    // This stands in for the original deduction function after its seven-byte prologue.
    byte[] fallback=Resources.Hex("B02A4883C4785E5DC3");Marshal.Copy(fallback,0,new IntPtr(origin+0x11000),fallback.Length);
    Func<int,int,ulong> invoke=(site,deduct)=>{
     var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));
     add("4883EC28");add("41B8");b.AddRange(BitConverter.GetBytes(deduct));
     add("49BB");b.AddRange(BitConverter.GetBytes(0x5566778899aabbccL));
     add("48B8");b.AddRange(BitConverter.GetBytes(0x1122334400000000L));
     add("E8");b.AddRange(BitConverter.GetBytes(0x3000-(0x10000+site*0x100+35)));
     // Success exposes preserved upper RAX; caller/callee frames must also balance.
     add("48B9");b.AddRange(BitConverter.GetBytes(0x5566778899aabbccL));add("4939CB7402B0FF4883C428C3");long address=origin+0x10000+site*0x100;Marshal.Copy(b.ToArray(),0,new IntPtr(address),b.Count);
     Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);
     return ((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(address),typeof(Call)))();
    };
    Func<ulong,int> low=v=>(int)(v&255);
    for(int i=0;i<3;i++){
     Marshal.WriteInt32(new IntPtr(origin+0x5600),0);check(low(invoke(i,1))==42,"Disabled option retains original placement payment at site "+i);
     Marshal.WriteInt32(new IntPtr(origin+0x5600),1);ulong value=invoke(i,1);check(low(value)==1&&value>>32==0x11223344,"Enabled placement bypass returns bool success and preserves upper RAX at site "+i);
     check(low(invoke(i,0))==42,"Refund path remains original at site "+i);
     Marshal.WriteInt32(new IntPtr(origin+0x5600),0);Marshal.WriteInt32(new IntPtr(origin+0x5610),1);value=invoke(i,1);check(low(value)==1&&value>>32==0x11223344,"Free-build flag bypasses placement payment at site "+i);check(low(invoke(i,0))==42,"Free-build flag leaves refund path original at site "+i);
     Marshal.WriteInt32(new IntPtr(origin+0x5610),0);
    }
    check(low(invoke(3,1))==42,"Unrelated caller still executes original payment");
    check(Marshal.ReadInt32(new IntPtr(origin+0x5608))==6,"Only the six scoped placement tests counted");
    check(GameSession.AllFlagOffsets.Contains(0x1600),"New switch participates in AllOff and recovery receipt");
    check(Resources.Text("BuildingPages.xaml").Contains("PreserveToggle"),"Preserve material option lives in Baublöcke category");
    HookSpec cursor=Resources.Manifest().Hooks.Single(h=>h.Name=="BuildingFreeCursor");check(cursor.Rva==0x2504d4&&cursor.Code.Contains("418127"),"Free-build cursor hook clears only the missing-material bit");check(Resources.Text("BuildingPages.xaml").Contains("FreeBuildToggle"),"Free-build option has its own control");
    return string.Join("\r\n",lines);
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}
