using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class MovementNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,n)=>{if(!ok)throw new Exception("MOVEMENT TEST FAILED: "+n);lines.Add("PASS "+n);};
   var original=Resources.Manifest().Hooks.Single(h=>h.Name=="PlayerBreathConsumption");var hook=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(original));foreach(var r in hook.Relocations)r.Rva=0x9000;
   IntPtr mem=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(mem==IntPtr.Zero)throw new Exception("Test allocation failed.");
   try{
    long baseAt=mem.ToInt64(),health=baseAt+0x12000,oxygen=baseAt+0x13000,values=baseAt+0x14000,record=baseAt+0x15000;
    var image=GameSession.Image(new HookManifest{DataOffset=0x4000,AllocationSize=0x6000,Hooks=new[]{hook}},baseAt,baseAt);Marshal.Copy(image,0,mem,image.Length);
    Marshal.WriteByte(new IntPtr(baseAt+0x9000),0xc3);Marshal.WriteInt64(new IntPtr(baseAt+0x4010),health);
    Action<bool,bool> invoke=(on,local)=>{
     Marshal.WriteInt32(new IntPtr(baseAt+0x4180),on?1:0);Marshal.WriteInt32(new IntPtr(values+12),120);
     var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));
     add("534883EC6848BB");imm(oxygen);add("49B8");imm(values);add("48B8");imm(local?health:health+64);add("4889442440B903000000B80500000049BB");imm(baseAt+hook.CodeOffset);add("41FFD348B8");imm(record);add("4889189C598948084883C4685BC3");
     Marshal.Copy(b.ToArray(),0,new IntPtr(baseAt+0x8000),b.Count);Native.FlushInstructionCache(new IntPtr(-1),mem,(UIntPtr)0x20000);((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(baseAt+0x8000),typeof(Call)))();
    };
    invoke(false,true);check(Marshal.ReadInt32(new IntPtr(values+12))==115,"Breath off retains ordinary oxygen subtraction");
    invoke(true,true);check(Marshal.ReadInt32(new IntPtr(values+12))==120,"Breath on suppresses local-player oxygen subtraction");
    check(Marshal.ReadInt64(new IntPtr(baseAt+0x41b0))==oxygen,"Oxygen pointer belongs to matching player's health context");
    check(Marshal.ReadInt32(new IntPtr(baseAt+0x4194))==1,"Only protected oxygen consumption advances protection counter");
    invoke(true,false);check(Marshal.ReadInt32(new IntPtr(values+12))==115,"Another entity still consumes oxygen while player protection is on");
    check(Marshal.ReadInt64(new IntPtr(record))==oxygen,"Breath hook preserves original RBX");
    invoke(false,true);check(Marshal.ReadInt32(new IntPtr(values+12))==115,"Disabling breath resumes consumption");
    check(GameSession.AllFlagOffsets.Contains(0x180),"Breath flag is included in normal and emergency restoration");
    return "Player breath native validation\r\n"+string.Join("\r\n",lines)+"\r\n"+FlightTests()+DismantleTests()+"\r\nSynthetic memory only; diving, flying and dismantling still need in-game tests.\r\n";
   }finally{Native.VirtualFree(mem,UIntPtr.Zero,0x8000);}
  }
  static string FlightTests(){
   var lines=new List<string>();Action<bool,string> check=(ok,n)=>{if(!ok)throw new Exception("TURK FLIGHT TEST FAILED: "+n);lines.Add("PASS "+n);};
   var hook=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(Resources.Manifest().Hooks.Single(h=>h.Name=="TurkGliderFlightPitch")));foreach(var r in hook.Relocations)r.Rva=r.Rva==0x1400330?0x16000:0x9000;
   IntPtr mem=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(mem==IntPtr.Zero)throw new Exception("Test allocation failed");
   try{long at=mem.ToInt64(),fixture=at+0x12000,actor=at+0x13000,record=at+0x15000;var image=GameSession.Image(new HookManifest{DataOffset=0x4000,AllocationSize=0x6000,Hooks=new[]{hook}},at,at);Marshal.Copy(image,0,mem,image.Length);Marshal.WriteByte(new IntPtr(at+0x9000),0xc3);Marshal.WriteInt64(new IntPtr(at+0x41a0),actor);Marshal.WriteInt32(new IntPtr(at+0x16000),BitConverter.ToInt32(BitConverter.GetBytes(0.08726646f),0));
    Action<bool,bool> invoke=(on,local)=>{Marshal.WriteInt32(new IntPtr(at+0x4184),on?1:0);Marshal.WriteInt64(new IntPtr(fixture+0x20),local?actor:actor+0x1000);
     var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));add("554883EC2048BD");imm(fixture);add("B8AA5500003BC049BB");imm(at+hook.CodeOffset);add("41FFD349BA");imm(record);add("66410F7E02498942089C5941894A104883C4205DC3");Marshal.Copy(b.ToArray(),0,new IntPtr(at+0x8000),b.Count);Native.FlushInstructionCache(new IntPtr(-1),mem,(UIntPtr)0x20000);((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(at+0x8000),typeof(Call)))();};
    Func<float> pitch=()=>BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(new IntPtr(record))),0);
    invoke(false,true);check(pitch()==0.08726646f,"Disabled Turk adaptation loads original game pitch bound");
    invoke(true,true);check(pitch()==-1.57f,"Enabled local flight uses Turk's exact -1.57 pitch bound");check(Marshal.ReadInt64(new IntPtr(record+8))==0x55aa,"Turk adaptation preserves original RAX");check((Marshal.ReadInt32(new IntPtr(record+0x10))&0x41)==0x40,"Turk adaptation preserves branch-relevant zero and carry flags");check(Marshal.ReadInt32(new IntPtr(at+0x41f0))==1,"Pitch protection counter advances only for local enabled glider");
    invoke(true,false);check(pitch()==0.08726646f,"Another actor keeps normal glider pitch bound");invoke(false,true);check(pitch()==0.08726646f,"Disabling flight resumes normal pitch bound");
    check(!Resources.Manifest().Hooks.Any(h=>h.Name=="PlayerGliderVerticalVelocity"||h.Name=="PlayerGliderLocomotionVelocity"),"Old vertical velocity overrides and altitude-lock hooks are absent");
    check(GameSession.AllFlagOffsets.Contains(0x184),"Turk pitch toggle participates in normal and emergency restoration");
    return "Turk Glider Flight adaptation native validation\r\n"+string.Join("\r\n",lines)+"\r\n";
   }finally{Native.VirtualFree(mem,UIntPtr.Zero,0x8000);}
  }
  static string DismantleTests(){
   var lines=new List<string>();Action<bool,string> check=(ok,n)=>{if(!ok)throw new Exception("DISMANTLE TEST FAILED: "+n);lines.Add("PASS "+n);};
   var hook=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(Resources.Manifest().Hooks.Single(h=>h.Name=="OverriddenPropRemovalOrigin")));foreach(var r in hook.Relocations)r.Rva=0x9000;
   IntPtr mem=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(mem==IntPtr.Zero)throw new Exception("Test allocation failed");
   try{long at=mem.ToInt64(),placement=at+0x12000,item=at+0x14000,record=at+0x15000;var image=GameSession.Image(new HookManifest{DataOffset=0x4000,AllocationSize=0x6000,Hooks=new[]{hook}},at,at);Marshal.Copy(image,0,mem,image.Length);Marshal.WriteByte(new IntPtr(at+0x9000),0xc3);Marshal.WriteInt32(new IntPtr(item),123456);Marshal.WriteInt64(new IntPtr(at+0x5480),0x123456789);Marshal.WriteInt64(new IntPtr(at+0x5488),0x23456789a);
    Action<bool,bool,int> invoke=(on,overrides,mismatch)=>{Marshal.WriteInt32(new IntPtr(at+0x4188),on?1:0);Marshal.WriteInt32(new IntPtr(at+0x5004),overrides?1:0);Marshal.WriteInt64(new IntPtr(placement),mismatch==1?42:0x123456789);Marshal.WriteInt64(new IntPtr(placement+8),mismatch==2?42:0x23456789a);
     var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));add("55574883EC2848BD");imm(placement-0xd0);add("48BF");imm(item);add("49BB");imm(at+hook.CodeOffset);add("41FFD348B8");imm(record);add("448900488950084883C4285F5DC3");Marshal.Copy(b.ToArray(),0,new IntPtr(at+0x8000),b.Count);Native.FlushInstructionCache(new IntPtr(-1),mem,(UIntPtr)0x20000);((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(at+0x8000),typeof(Call)))();};
    invoke(false,true,0);check(Marshal.ReadInt32(new IntPtr(record))==123456,"Removal origin is unchanged when dismantle is off");
    invoke(true,true,0);check(Marshal.ReadInt32(new IntPtr(record))==0,"Matching overridden placement selects alternate removal origin");check(Marshal.ReadInt64(new IntPtr(record+8))==placement,"Removal hook preserves placement record pointer");check(Marshal.ReadInt32(new IntPtr(at+0x41d0))==123456,"Original source item is captured for removal diagnostics");check(Marshal.ReadInt32(new IntPtr(item))==123456,"Removal option never rewrites inventory item definitions");
    invoke(true,false,0);check(Marshal.ReadInt32(new IntPtr(record))==123456,"Ordinary placement remains unchanged without object override");
    invoke(true,true,1);check(Marshal.ReadInt32(new IntPtr(record))==123456,"Unmatched first UUID half excludes unrelated placements");invoke(true,true,2);check(Marshal.ReadInt32(new IntPtr(record))==123456,"Unmatched second UUID half excludes unrelated placements");
    check(Marshal.ReadInt32(new IntPtr(at+0x41d4))==1,"Only exact replacement placement advances removal counter");check(GameSession.AllFlagOffsets.Contains(0x188),"Removal option participates in normal and emergency restoration");return "Overridden prop removal native validation\r\n"+string.Join("\r\n",lines)+"\r\n";
   }finally{Native.VirtualFree(mem,UIntPtr.Zero,0x8000);}
  }
 }
}

