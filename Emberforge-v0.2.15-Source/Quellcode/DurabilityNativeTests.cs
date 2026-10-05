using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class DurabilityNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("DURABILITY TEST FAILED: "+name);lines.Add("PASS "+name);};
   var originals=Resources.Manifest().Hooks.Where(h=>h.Name.StartsWith("Durability")).OrderBy(h=>h.CodeOffset).ToArray();
   check(originals.Length==3,"Exactly three durability CALL sites are scoped");
   foreach(var actual in originals){
    var hook=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(actual));
    foreach(var fix in hook.Relocations)fix.Rva=fix.Rva==0x3319b0?0x12000:0x12100;
    var manifest=new HookManifest{AllocationSize=0x6000,DataOffset=0x4000,Hooks=new[]{hook}};
    IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Durability test allocation failed.");
    try{
     long origin=memory.ToInt64(),record=origin+0x10000;byte[] image=GameSession.Image(manifest,origin,origin);Marshal.Copy(image,0,memory,image.Length);
     // Owned synthetic durability helper: count calls, preserve invalid components,
     // add signed delta and emit a break event only when crossing zero.
     byte[] payment=Resources.Hex("4885c90f8425000000ff4104488b44242848894150f64110010f840f0000004401018339000f8f03000000ff4130c3");Marshal.Copy(payment,0,new IntPtr(origin+0x12000),payment.Length);
     // Continuation records register/flag state and returns with caller's stack restored.
     byte[] continuation=Resources.Hex("488941409C58894128488951184C8959204C8941084C89491031C04883C428C3");Marshal.Copy(continuation,0,new IntPtr(origin+0x12100),continuation.Length);
     var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));
     add("4883EC2848B9");imm(record);add("48BA");imm(0x1122334455667788L);add("49B9");imm(0x2233445566778899L);add("49BB");imm(0x33445566778899aaL);add("48B8");imm(0x445566778899aa11L);add("4889442420");
     add("41B8");int deltaAt=b.Count;b.AddRange(new byte[4]);add("48B8");imm(0x5566778899aa1122L);add("4C8D15");int displacementAt=b.Count;b.AddRange(new byte[4]);add("4539C041FFE2");
     byte[] wrapper=b.ToArray();Array.Copy(BitConverter.GetBytes(hook.CodeOffset-(0x11000+displacementAt+4)),0,wrapper,displacementAt,4);Marshal.Copy(wrapper,0,new IntPtr(origin+0x11000),wrapper.Length);
     Func<int,int,int,int,bool,ulong> invoke=(enabled,amount,delta,valid,isNull)=>{
      Marshal.Copy(new byte[0xc0],0,new IntPtr(record),0xc0);Marshal.WriteInt32(new IntPtr(record),amount);Marshal.WriteByte(new IntPtr(record+0x10),(byte)valid);Marshal.WriteInt32(new IntPtr(record+0x38),delta);Marshal.WriteInt32(new IntPtr(origin+0x407c),enabled);
      // Switch only the wrapper's own RCX immediate; no process or game memory is accessed.
      Marshal.WriteInt64(new IntPtr(origin+0x11000+6),isNull?0:record);Marshal.WriteInt32(new IntPtr(origin+0x11000+deltaAt),delta);
      if(isNull){ // Separate safe continuation for null-component case.
       Marshal.Copy(Resources.Hex("31C04883C428C3"),0,new IntPtr(origin+0x12100),7);
      }else Marshal.Copy(continuation,0,new IntPtr(origin+0x12100),continuation.Length);
      Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);
      return ((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(origin+0x11000),typeof(Call)))();
     };
     Func<int,int> get=offset=>Marshal.ReadInt32(new IntPtr(record+offset));string prefix=actual.Name+": ";
     invoke(0,37,-1,1,false);check(get(0)==36&&get(4)==1,prefix+"disabled mode preserves normal wear");
     check(Marshal.ReadInt64(new IntPtr(record+0x50))==0x445566778899aa11L,prefix+"original helper receives unchanged spilled argument and aligned caller stack");
     invoke(0,1,-2,1,false);check(get(0)==-1&&get(0x30)==1,prefix+"disabled wear crossing zero preserves break event");
     invoke(1,37,-3,1,false);check(get(0)==37&&get(4)==0&&get(0x30)==0,prefix+"enabled wear keeps durability and avoids break event");
     check(Marshal.ReadInt64(new IntPtr(record+8))==unchecked((long)(uint)-3)&&Marshal.ReadInt64(new IntPtr(record+0x10))==0x2233445566778899L,prefix+"enabled path preserves R8 and R9");
     check(Marshal.ReadInt64(new IntPtr(record+0x18))==0x1122334455667788L&&Marshal.ReadInt64(new IntPtr(record+0x20))==0x33445566778899aaL,prefix+"enabled path preserves RDX and R11");
     check(Marshal.ReadInt64(new IntPtr(record+0x40))==0x5566778899aa1122L&&(get(0x28)&0x41)==0x40,prefix+"enabled path preserves RAX and incoming ZF/CF");
     invoke(1,37,10,1,false);check(get(0)==47&&get(4)==1,prefix+"repairs and positive durability changes remain active");
     invoke(1,37,0,1,false);check(get(0)==37&&get(4)==1,prefix+"zero delta stays on original helper path");
     invoke(1,37,-3,0,false);check(get(0)==37&&get(4)==1,prefix+"inactive component stays on original helper path");
     check(invoke(1,37,-3,1,true)==0,prefix+"null component safely retains original helper behavior");
     invoke(1,0,-1,1,false);check(get(0)==0&&get(4)==0,prefix+"already broken item is not repaired implicitly");
     invoke(1,50000,int.MinValue,1,false);check(get(0)==50000&&get(4)==0,prefix+"extreme negative wear cannot underflow while enabled");
     invoke(0,37,-1,1,false);check(get(0)==36&&get(4)==1,prefix+"turning function off restores wear immediately");
     check(actual.Original.Length==10&&actual.Context.StartsWith(actual.Original),prefix+"one complete CALL and exact binary context verified");
    }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
   }
   return "Durability native validation\r\n"+string.Join("\r\n",lines)+"\r\nSynthetic memory only; weapon attacks, tool hits and repairs need user's in-game confirmation.\r\n";
  }
 }
}

