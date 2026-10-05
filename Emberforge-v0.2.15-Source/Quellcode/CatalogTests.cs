using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;
namespace Emberforge {
 internal static class CatalogTests {
  internal static string Run(string path){
   var report=new StringBuilder();Action<bool,string> check=(ok,label)=>{report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(report.ToString());};
   Action<Action,string> rejects=(action,label)=>{bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}check(rejected,label);};
   var data=new byte[56];Action<int,int> put=(offset,value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,data,offset,4);
   put(0,20);put(4,7);put(8,9);put(12,16);put(16,1);Encoding.UTF8.GetBytes("Prop_A\0").CopyTo(data,20);put(28,422260440);put(32,8);put(36,16);for(int i=40;i<56;i++)data[i]=1;
   string mesh=Resources.Hex(data.Skip(40).ToArray()),id=new string('a',32);var resources=new HashSet<string>{mesh};
   var item=GameCatalog.ParseTemplate(id,data,resources);check(item.Name=="Prop_A"&&item.Mesh==mesh&&item.Id==id,"fixture extracts name, definition ID and model ID");
   rejects(()=>GameCatalog.ParseTemplate(id,data,new HashSet<string>()),"reject model not present in resource index");
   put(32,int.MaxValue);rejects(()=>GameCatalog.ParseTemplate(id,data,resources),"reject overflowing component offset");put(32,8);
   put(0,500);rejects(()=>GameCatalog.ParseTemplate(id,data,resources),"reject name outside resource");put(0,20);
   put(16,100);rejects(()=>GameCatalog.ParseTemplate(id,data,resources),"reject truncated component table");put(16,1);
   rejects(()=>Zstd.Decode(new byte[]{1,2,3},64),"reject malformed compressed frame");
   var real=GameCatalog.Read(path,null);check(GameCatalog.Valid(real),"real game archive produces valid complete catalog");
   check(real.Items.Length==10745&&real.Resources==77810,"current game directory: all 10745 templates out of 77810 resources");
   var old=Resources.Json.Deserialize<BuildingCatalogItem[]>(Resources.Text("BuildingCatalog.json"));var byId=real.Items.ToDictionary(i=>i.Id);var matching=old.Where(i=>byId.ContainsKey(i.Id)).ToArray();
   check(matching.Length==3877,"3877 legacy IDs confirmed in current resource directory");
   check(matching.Count(i=>i.Mesh==byId[i.Id].Mesh)>=3822,"at least 3822 model IDs independently confirmed against legacy catalog");
   var restored=GameCatalog.Json.Deserialize<GameCatalogData>(GameCatalog.Json.Serialize(real));check(GameCatalog.Valid(restored)&&restored.Items.Length==real.Items.Length,"full catalog round trips through JSON");
   var engine=new BuildingEngine(new GameSession());engine.Import(restored);check(engine.Catalog.Length==10745&&engine.CurrentItem(old[0])!=null,"building engine uses imported definitions for capture lookup");
   check(engine.CurrentItem(old.First(i=>!byId.ContainsKey(i.Id)))==null,"removed legacy IDs do not reappear through favorites");
   check(real.Items.Any(i=>!BuildingView.Technical(i))&&real.Items.Any(BuildingView.Technical),"technical definitions can be separated without discarding them");
   report.AppendLine("Game archive read only. No running game memory accessed. No user catalog cache replaced by this test.");return report.ToString();
  }
 }
}
