using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class HeldPickaxeNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate ulong Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,n)=>{if(!ok)throw new Exception("HELD PICKAXE TEST FAILED: "+n);lines.Add("PASS "+n);};
   var hooks=Resources.Manifest().Hooks.Where(h=>h.Name.StartsWith("HeldPickaxe")).ToArray();check(hooks.Length==2,"Exactly two local cursor hooks");
   var copied=Resources.Json.Deserialize<HookSpec[]>(Resources.Json.Serialize(hooks));foreach(var h in copied)foreach(var r in h.Relocations)r.Rva=0x12100;
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Held test allocation failed.");
   try{
    long origin=memory.ToInt64(),record=origin+0x10000,source=origin+0x16000,tool=origin+0x18000;
    byte[] img=GameSession.Image(new HookManifest{AllocationSize=0x6000,DataOffset=0x4000,Hooks=copied},origin,origin);Marshal.Copy(img,0,memory,img.Length);
    Marshal.WriteInt32(new IntPtr(source+0x108),12345);Marshal.WriteInt64(new IntPtr(source+0x1c0),0x1122334455667788L);Marshal.WriteInt64(new IntPtr(source+0x1d0),0x2233445566778899L);
    byte[] continuation=Resources.Hex("4889024C897A089C588942104883C428415F5D31C0C3");Marshal.Copy(continuation,0,new IntPtr(origin+0x12100),continuation.Length);
    Action<string,long> invoke=(name,definition)=>{
     var h=copied.Single(x=>x.Name==name);var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));
     add("5541574883EC2848BD");imm(source);add("48B8");imm(definition);add("48BA");imm(record);add("49BF");imm(0x33445566778899aaL);add("4C8D15");int at=b.Count;b.AddRange(new byte[4]);add("4839C041FFE2");byte[] wrapper=b.ToArray();Array.Copy(BitConverter.GetBytes(h.CodeOffset-(0x11000+at+4)),0,wrapper,at,4);Marshal.Copy(wrapper,0,new IntPtr(origin+0x11000),wrapper.Length);Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(origin+0x11000),typeof(Call)))();
    };
    Func<int,int> get=o=>Marshal.ReadInt32(new IntPtr(origin+o));Func<int,long> q=o=>Marshal.ReadInt64(new IntPtr(origin+o));
    Action<int,int> setTool=(flags,mode)=>{Marshal.WriteInt16(new IntPtr(tool+0x56e),(short)flags);Marshal.WriteByte(new IntPtr(tool+0x4d8),(byte)mode);};
    setTool(0x40,0);invoke("HeldPickaxeCapture",tool);check(q(0x5150)==tool&&get(0x5164)==1,"Terraformer Remove tool is recognized as held pickaxe");
    check(get(0x5160)==12345&&get(0x5158)==1,"Local cursor entity ID and change counter are captured");
    check(Marshal.ReadInt64(new IntPtr(record))==0x2233445566778899L&&Marshal.ReadInt64(new IntPtr(record+8))==tool,"Capture replays original RAX load and R15 assignment");
    check((Marshal.ReadInt32(new IntPtr(record+0x10))&0x41)==0x40,"Capture preserves incoming condition flags");
    setTool(0,0);invoke("HeldPickaxeCapture",tool);check(get(0x5164)==0,"Ordinary weapon is rejected even when default mode is Remove");
    setTool(0x40,2);invoke("HeldPickaxeCapture",tool);check(get(0x5164)==0,"Rake Flatten mode is rejected");
    setTool(0x40,1);invoke("HeldPickaxeCapture",tool);check(get(0x5164)==0,"Terraformer Add mode is rejected");
    setTool(0x40,7);invoke("HeldPickaxeCapture",tool);check(get(0x5164)==0,"Decay remover is rejected");
    setTool(0xffff,0);invoke("HeldPickaxeCapture",tool);check(get(0x5164)==1,"Other unrelated item flags do not reject pickaxe");
    invoke("HeldPickaxeCapture",0);check(q(0x5150)==0&&get(0x5164)==0,"Empty active item safely clears pointer and pickaxe state");
    setTool(0x40,0);invoke("HeldPickaxeCapture",tool);invoke("HeldPickaxeClear",0);
    check(q(0x5150)==0&&get(0x5160)==0&&get(0x5164)==0,"Local cursor iteration clears stale definition and identity");
    check(Marshal.ReadInt64(new IntPtr(record))==0x1122334455667788L&&Marshal.ReadInt64(new IntPtr(record+8))==0x33445566778899aaL,"Clear replays original RAX load and preserves R15");
    check((Marshal.ReadInt32(new IntPtr(record+0x10))&0x41)==0x40,"Clear preserves incoming condition flags");
    check(get(0x5158)==9,"Every publish or clear advances held-tool refresh counter");
    invoke("HeldPickaxeClear",0);check(Marshal.ReadInt32(new IntPtr(origin+0x5094))==1&&Marshal.ReadInt32(new IntPtr(origin+0x50a0))==0&&Marshal.ReadInt32(new IntPtr(origin+0x5098))==-1,"every local cursor iteration invalidates previous ray even when next query is skipped");
    return "Held pickaxe native validation\r\n"+string.Join("\r\n",lines)+"\r\nOwned synthetic memory only; no live game writes.\r\n";
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}
