using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Text.RegularExpressions;

namespace Emberforge {
 public sealed class BuildingMaterialFavorite {public string Name{get;set;}public int Id{get;set;}}
 public sealed class BuildingPreferences {
  public bool CraftFavorite{get;set;}public bool DurabilityFavorite{get;set;}public BuildingMaterialFavorite[] Materials{get;set;}public BuildingCatalogItem[] Objects{get;set;}
  internal static BuildingPreferences Load(){
   try{var p=Resources.Json.Deserialize<BuildingPreferences>(File.ReadAllText(Files.PathOf("Baufavoriten.json")));
    if(p.Materials==null||p.Objects==null)return Empty();
    p.Materials=p.Materials.Where(i=>i!=null&&i.Id>=1&&i.Id<=255&&!string.IsNullOrWhiteSpace(i.Name)).Take(1000).ToArray();
    p.Objects=p.Objects.Where(i=>i!=null&&ValidId(i.Id)&&ValidId(i.Mesh)&&!string.IsNullOrWhiteSpace(i.Name)).Take(5000).ToArray();return p;
   }catch{return Empty();}
  }
  static bool ValidId(string id){return id!=null&&id.Length==32&&id.All(Uri.IsHexDigit);}
  internal static BuildingPreferences Empty(){return new BuildingPreferences{Materials=new BuildingMaterialFavorite[0],Objects=new BuildingCatalogItem[0]};}
  internal void Save(){File.WriteAllText(Files.PathOf("Baufavoriten.json"),Resources.Json.Serialize(this));}
 }
 internal sealed class BuildingCatalogRow {
  internal BuildingCatalogItem Item;public string Title{get;set;}
 }
 internal sealed class BuildingView {
  internal readonly UserControl Root;readonly MainView parent;readonly BuildingEngine engine;readonly Func<Task> connect;readonly bool preview;
  static readonly Dictionary<string,string> words=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"Workbench","Werkbank"},{"Fence","Zaun"},{"Table","Tisch"},{"Chair","Stuhl"},{"Door","Tür"},{"Window","Fenster"},{"Tree","Baum"},{"Plant","Pflanze"},{"Flower","Blume"},{"Wall","Wand"},{"Small","Klein"},{"Large","Groß"},{"Wood","Holz"},{"Stone","Stein"},{"Vase","Vase"},{"Bed","Bett"},{"Bench","Bank"},{"Torch","Fackel"},{"Chest","Truhe"},{"Light","Licht"},{"Roof","Dach"}};
  BuildingPreferences prefs;ResourceDictionary buildingLanguage;BuildingCatalogItem replacement,fixedSource;bool changing,worldReady,importing;uint lastAim,lastHeld,lastMaterial;long aimSeen,heldSeen,materialSeen;int scanGeneration;
  internal BuildingView(MainView view,GameSession session,bool isPreview,Func<Task> connectAction){
   parent=view;connect=connectAction;preview=isPreview;engine=new BuildingEngine(session);prefs=preview?BuildingPreferences.Empty():BuildingPreferences.Load();
   Root=(UserControl)XamlReader.Parse(Resources.Text("BuildingPages.xaml"));Root.Visibility=Visibility.Collapsed;
   SetLanguage(parent.CurrentLanguage);
   Find<Button>("BlocksConnect").Click+=async(s,e)=>{await connect();};Find<Button>("ObjectsConnect").Click+=async(s,e)=>{await connect();};
   Find<Button>("ApplyMaterial").Click+=(s,e)=>Try("blocks",()=>{RequireWorld();ApplyMaterial(Find<ToggleButton>("BlockToggle").IsChecked==true);Status("blocks","Ersatzmaterial gewählt: "+MaterialTitle(MaterialId()));});
   Find<ToggleButton>("BlockToggle").Click+=(s,e)=>TryToggle("blocks",()=>{RequireWorld();ApplyMaterial(Find<ToggleButton>("BlockToggle").IsChecked==true);});
   Find<ToggleButton>("PreserveToggle").Click+=(s,e)=>Try("blocks",()=>{try{RequireWorld();engine.EnableNoConsumption(Find<ToggleButton>("PreserveToggle").IsChecked==true);}catch{Find<ToggleButton>("PreserveToggle").IsChecked=false;if(engine.IsReady)engine.EnableNoConsumption(false);throw;}UpdateStates();});
   Find<ToggleButton>("FreeBuildToggle").Click+=(s,e)=>Try("blocks",()=>{try{RequireWorld();engine.EnableFreeBuild(Find<ToggleButton>("FreeBuildToggle").IsChecked==true);}catch{Find<ToggleButton>("FreeBuildToggle").IsChecked=false;if(engine.IsReady)engine.EnableFreeBuild(false);throw;}UpdateStates();});
   Find<Button>("SaveMaterialFavorite").Click+=(s,e)=>Try("blocks",()=>{
    int id=MaterialId();string name=Find<TextBox>("MaterialFavoriteName").Text.Trim();if(name.Length==0)name=MaterialTitle(id);
    prefs.Materials=prefs.Materials.Where(i=>i.Id!=id).Concat(new[]{new BuildingMaterialFavorite{Id=id,Name=name}}).ToArray();Save();RefreshMaterials();Status("blocks","Favorit gespeichert: "+name);
   });
   Find<Button>("UseMaterialFavorite").Click+=(s,e)=>Try("blocks",()=>{var item=Find<ListBox>("MaterialFavorites").SelectedItem as BuildingMaterialFavorite;if(item==null)throw new InvalidOperationException("Zuerst einen Favoriten wählen.");Find<TextBox>("MaterialId").Text=item.Id.ToString();Find<TextBox>("MaterialFavoriteName").Text=item.Name;if(worldReady&&engine.IsReady)ApplyMaterial(Find<ToggleButton>("BlockToggle").IsChecked==true);});
   Find<Button>("RemoveMaterialFavorite").Click+=(s,e)=>Try("blocks",()=>{var item=Find<ListBox>("MaterialFavorites").SelectedItem as BuildingMaterialFavorite;if(item==null)return;prefs.Materials=prefs.Materials.Where(i=>i.Id!=item.Id).ToArray();Save();RefreshMaterials();});
   Find<Button>("UseSelectedObject").Click+=(s,e)=>Try("objects",()=>UseReplacement(Selected()));
   Find<Button>("ToggleObjectFavorite").Click+=(s,e)=>Try("objects",()=>{var item=Selected();bool found=prefs.Objects.Any(i=>i.Id==item.Id);prefs.Objects=found?prefs.Objects.Where(i=>i.Id!=item.Id).ToArray():prefs.Objects.Concat(new[]{item}).ToArray();Save();FilterObjects();Status("objects",found?"Favorit entfernt.":"Objekt als Favorit gespeichert.");});
   Find<TextBox>("ObjectSearch").TextChanged+=(s,e)=>FilterObjects();Find<ToggleButton>("ObjectFavoritesOnly").Click+=(s,e)=>FilterObjects();
   Find<ToggleButton>("PropToggle").Click+=(s,e)=>TryToggle("objects",()=>{
    RequireWorld();bool on=Find<ToggleButton>("PropToggle").IsChecked==true;if(!on){engine.EnableProps(false);return;}if(replacement==null)throw new InvalidOperationException("Zuerst ein Ersatzobjekt wählen.");ApplySource();engine.SetPropTarget(replacement,true);
   });
   Find<RadioButton>("AnySource").Click+=(s,e)=>Try("objects",()=>{fixedSource=null;if(engine.IsReady)engine.UseCurrentPlaceable();Find<TextBlock>("SourceName").Text=T("buildingSourceAny");});
   Find<RadioButton>("FixedSource").Click+=(s,e)=>Try("objects",()=>{if(fixedSource==null){Find<RadioButton>("AnySource").IsChecked=true;throw new InvalidOperationException("Ein Ausgangsobjekt im Katalog wählen und als feste Quelle übernehmen.");}if(engine.IsReady)engine.SetFixedSource(fixedSource);});
   Find<Button>("UseSelectedSource").Click+=(s,e)=>Try("objects",()=>SetSource(Selected()));
   Find<Button>("CaptureSource").Click+=(s,e)=>Try("objects",()=>{RequireWorld();SetSource(engine.CaptureLastPlacedSource());});
   Find<Button>("ScanNearby").Click+=async(sender,e)=>await ScanNearby();
   Find<Button>("UseNearby").Click+=(sender,e)=>Try("objects",()=>{RequireWorld();var row=Find<ListBox>("NearbyList").SelectedItem as BuildingCatalogRow;if(row==null)throw new InvalidOperationException(T("nearbyChoose"));UseReplacement(row.Item);ApplySource();engine.SetPropTarget(row.Item,true);Find<ToggleButton>("PropToggle").IsChecked=true;UpdateStates();Status("objects","Als Ersatz aktiviert: "+NiceName(row.Item.Name));});

   if(!preview){var imported=GameCatalog.LoadCache();if(imported!=null)engine.Import(imported);}
   Find<Button>("ImportGameCatalog").Click+=async(s,e)=>await ImportGameCatalog(true);
   Find<CheckBox>("TechnicalObjects").Click+=(s,e)=>FilterObjects();
   Find<Button>("CraftConnect").Click+=async(sender,e)=>await connect();Find<ToggleButton>("CraftFavorite").IsChecked=prefs.CraftFavorite;
   Find<ToggleButton>("CraftFavorite").Click+=(sender,e)=>{prefs.CraftFavorite=Find<ToggleButton>("CraftFavorite").IsChecked==true;Save();};
   Find<ToggleButton>("CraftToggle").Click+=(sender,e)=>Try("craft",()=>{try{RequireWorld();engine.EnableNoCraftConsumption(Find<ToggleButton>("CraftToggle").IsChecked==true);}catch{Find<ToggleButton>("CraftToggle").IsChecked=false;if(engine.IsReady)engine.EnableNoCraftConsumption(false);throw;}UpdateStates();Status("craft",T("craftTestHint"));});
   Find<Button>("EquipmentConnect").Click+=async(sender,e)=>await connect();Find<ToggleButton>("DurabilityFavorite").IsChecked=prefs.DurabilityFavorite;
   Find<ToggleButton>("DurabilityFavorite").Click+=(sender,e)=>{prefs.DurabilityFavorite=Find<ToggleButton>("DurabilityFavorite").IsChecked==true;Save();};
   Find<ToggleButton>("DurabilityToggle").Click+=(sender,e)=>Try("equipment",()=>{try{RequireWorld();engine.EnableDurability(Find<ToggleButton>("DurabilityToggle").IsChecked==true);}catch{Find<ToggleButton>("DurabilityToggle").IsChecked=false;if(engine.IsReady)engine.EnableDurability(false);throw;}UpdateStates();Status("equipment",T("durabilityTestHint"));});
   RefreshMaterials();FilterObjects();CatalogSource();
  }
  internal void SetLanguage(string language){
   string code=string.Equals(language,"en",StringComparison.OrdinalIgnoreCase)?"en":"de";
   if(buildingLanguage!=null)parent.Window.Resources.MergedDictionaries.Remove(buildingLanguage);
   buildingLanguage=(ResourceDictionary)XamlReader.Parse(Resources.Text(code=="en"?"BuildingEnglish.xaml":"BuildingDeutsch.xaml"));parent.Window.Resources.MergedDictionaries.Add(buildingLanguage);
   if(Root!=null){UpdateStates();Find<TextBlock>("MaterialAim").Text=Find<TextBlock>("MaterialAim").Text.Length==0?T("buildingWaiting"):Find<TextBlock>("MaterialAim").Text;Find<TextBlock>("ObjectAim").Text=Find<TextBlock>("ObjectAim").Text.Length==0?T("buildingWaiting"):Find<TextBlock>("ObjectAim").Text;CatalogSource();}
  }
  T Find<T>(string name)where T:class{return Root.FindName(name) as T;}
  string T(string key){return (string)parent.Window.FindResource(key);}
  void Status(string page,string message){Find<TextBlock>(page=="blocks"?"BlockStatus":page=="craft"?"CraftStatus":page=="equipment"?"EquipmentStatus":"ObjectStatus").Text=message;}
  void RequireWorld(){if(!Root.IsEnabled)throw new InvalidOperationException("Spielverbindung wird gerade geändert. Kurz warten.");if(preview||!engine.IsReady)throw new InvalidOperationException(T("buildingDisconnected"));if(!worldReady)throw new InvalidOperationException("Zuerst deine Welt laden und deinen Charakter kurz bewegen.");}
  void RequirePickaxe(){if(heldSeen==0||unchecked((uint)Environment.TickCount-(uint)heldSeen)>750||!BuildingEngine.IsPickaxe(engine.Sample()))throw new InvalidOperationException("Spitzhacke nehmen, das Ziel direkt anvisieren und den Hotkey drücken.");}
  void Try(string page,Action action){if(changing)return;try{action();}catch(Exception error){Files.Log("Baufunktion: "+error.Message);Status(page,error.Message);}}
  void TryToggle(string page,Action action){Try(page,()=>{try{action();}catch{if(engine.IsReady){if(page=="blocks")engine.EnableBlocks(false);else engine.EnableProps(false);}Find<ToggleButton>(page=="blocks"?"BlockToggle":"PropToggle").IsChecked=false;throw;}});UpdateStates();}
  void Save(){if(!preview)prefs.Save();}
  int MaterialId(){int id;if(!int.TryParse(Find<TextBox>("MaterialId").Text,out id)||id<1||id>255)throw new InvalidOperationException("Eine Material-ID zwischen 1 und 255 eingeben oder ein Material im Fadenkreuz erfassen.");return id;}
  void ApplyMaterial(bool enabled){int id=MaterialId();engine.SetMaterialTargets(id<128?id:-1,id>=128?id-128:-1,enabled);}
  static string MaterialTitle(int id){return (id<128?"Bodenmaterial ":"Baublock ")+(id<128?id:id-128)+" · ID "+id;}
  void RefreshMaterials(){Find<ListBox>("MaterialFavorites").ItemsSource=prefs.Materials.OrderBy(i=>i.Name,StringComparer.OrdinalIgnoreCase).ToArray();}
  void CatalogSource(){Find<TextBlock>("CatalogSource").Text=engine.Imported==null?"Quelle: alter Cheat-Table-Katalog. Neuere Objekte können fehlen.":"Quelle: deine Spieldateien · "+engine.Catalog.Length+" Objektdefinitionen vollständig eingelesen. Nicht jede Vorlage ist als Bauobjekt nutzbar.";}
  internal void ShowCatalog(GameCatalogData data){engine.Import(data);FilterObjects();CatalogSource();}
  internal StackPanel HotkeyHost(bool objects){return Find<StackPanel>(objects?"ObjectHotkeyPanel":"MaterialHotkeyPanel");}
  async Task ScanNearby(){
   int generation=scanGeneration;var button=Find<Button>("ScanNearby");if(!button.IsEnabled)return;
   try{RequireWorld();button.IsEnabled=false;Find<TextBlock>("NearbyCount").Text=T("nearbyLoading");
    double radius;string radiusText=Find<TextBox>("NearbyRadius").Text.Trim();if(!double.TryParse(radiusText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.CurrentCulture,out radius))throw new InvalidOperationException("Bitte eine gültige Reichweite eingeben.");if(radius<1||radius>1000)throw new InvalidOperationException("Die Reichweite muss zwischen 1 und 1000 Metern liegen.");
    var loaded=await Task.Run(()=>engine.LoadedObjects(()=>System.Threading.Volatile.Read(ref scanGeneration)!=generation));var targets=BuildingEngine.FilterNearby(loaded,radius);if(targets.Length==0)throw new InvalidOperationException("Keine Objekte mit Position innerhalb dieser Reichweite gefunden.");
    if(generation!=scanGeneration||!worldReady||!engine.IsReady)return;
    Find<ListBox>("NearbyList").ItemsSource=targets.Select(t=>new BuildingCatalogRow{Item=t.Item,Title=NiceName(t.Item.Name)}).ToArray();
    Find<TextBlock>("NearbyCount").Text=targets.Length+" "+T("nearbyCount")+" ("+radius.ToString("0.#",System.Globalization.CultureInfo.CurrentCulture)+" m)";Status("objects",T("nearbyReady"));
   }catch(OperationCanceledException){}catch(Exception error){if(generation==scanGeneration){Find<TextBlock>("NearbyCount").Text=T("nearbyEmpty");Status("objects",error.Message);}}finally{button.IsEnabled=true;}
  }
  internal Task AutoCatalog(bool testOnly=false){return (preview&&!testOnly)||engine.Imported!=null?Task.FromResult(0):ImportGameCatalog(false,testOnly);}
  async Task ImportGameCatalog(bool choosePath,bool testOnly=false){
   if((preview&&!testOnly)||importing)return;importing=true;var button=Find<Button>("ImportGameCatalog");button.IsEnabled=false;
   try{
    string path=engine.Imported==null?GameCatalog.GuessPath():engine.Imported.Path;
    if(!File.Exists(path)){if(!choosePath){Status("objects","Spieldateien nicht gefunden. Mit Katalog aktualisieren deinen Spielordner auswählen.");return;}var picker=new Microsoft.Win32.OpenFileDialog{Title="Enshrouded-Ressourcenverzeichnis auswählen",Filter="Enshrouded-Ressourcen|enshrouded.kfc",FileName="enshrouded.kfc"};if(picker.ShowDialog(parent.Window)!=true)return;path=picker.FileName;}
    Find<TextBlock>("CatalogSource").Text="Der vollständige Katalog wird automatisch aus deinen Spieldateien geladen …";
    var progress=new Progress<string>(message=>parent.Window.Dispatcher.BeginInvoke(new Action(()=>{if(importing)Find<TextBlock>("CatalogSource").Text=message;})));
    string chosen=path;var imported=await Task.Run(()=>GameCatalog.Read(chosen,message=>((IProgress<string>)progress).Report(message)));
    ShowCatalog(imported);try{if(!testOnly)GameCatalog.SaveCache(imported);}catch(Exception saveError){Files.Log("Katalog-Cache: "+saveError.Message);Status("objects","Vollständiger Katalog geladen, konnte jedoch nicht für den nächsten Start gespeichert werden.");return;}
    Status("objects",imported.Items.Length+" Objektdefinitionen aus deinen Spieldateien eingelesen. Der Katalog ist gespeichert und bleibt beim nächsten Start verfügbar.");
   }catch(Exception ex){Files.Log("Katalogimport: "+ex);CatalogSource();Status("objects","Katalog konnte nicht aktualisiert werden: "+ex.Message);}finally{importing=false;button.IsEnabled=true;}
  }
  internal static bool Technical(BuildingCatalogItem item){return Regex.IsMatch(item.Name,@"^_?(?:[0-9]+_)?(?:Base_|VFX_|Visuals_|8kMapLabel_|LoreText_|Spawnpoints?_|Enemy_|Projectile_|Explosion_|Zone_|Attack_|Player(?:_|$)|NPC_|test)",RegexOptions.IgnoreCase);}
  internal static string NiceName(string name){
   string text=Regex.Replace(name??"",@"^_?(?:T[0-9]_)?(?:Prop_|Deco_|Scatter_|Loot_Node_)+","");
   return string.Join(" ",text.Split(new[]{'_',' '},StringSplitOptions.RemoveEmptyEntries).Select(token=>words.ContainsKey(token)?words[token]:token));
  }
  void FilterObjects(){
   string query=Find<TextBox>("ObjectSearch").Text.Trim();bool favorites=Find<ToggleButton>("ObjectFavoritesOnly").IsChecked==true;
   var validFavorites=prefs.Objects.Select(engine.CurrentItem).Where(i=>i!=null).ToArray();
   var set=favorites?validFavorites:engine.Catalog.Concat(validFavorites).GroupBy(i=>i.Id).Select(g=>g.First()).ToArray();
   if(Find<CheckBox>("TechnicalObjects").IsChecked!=true)set=set.Where(i=>!Technical(i)).ToArray();
   var filtered=set.Where(i=>query.Length==0||i.Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||NiceName(i.Name).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();
   string selectedId=(Find<ListBox>("ObjectList").SelectedItem as BuildingCatalogRow)==null?null:((BuildingCatalogRow)Find<ListBox>("ObjectList").SelectedItem).Item.Id;
   var rows=filtered.Take(150).Select(i=>new BuildingCatalogRow{Item=i,Title=(prefs.Objects.Any(f=>f.Id==i.Id)?"★ ":"")+NiceName(i.Name)}).ToArray();
   Find<ListBox>("ObjectList").ItemsSource=rows;Find<ListBox>("ObjectList").SelectedItem=rows.FirstOrDefault(i=>i.Item.Id==selectedId);
   Find<TextBlock>("ObjectCount").Text=filtered.Length+" Treffer · "+engine.Catalog.Length+" Definitionen im Katalog"+(filtered.Length>150?" · erste 150 angezeigt, Suche eingrenzen":"");
  }
  BuildingCatalogItem Selected(){var row=Find<ListBox>("ObjectList").SelectedItem as BuildingCatalogRow;if(row==null)throw new InvalidOperationException("Zuerst ein Objekt im Katalog auswählen.");return row.Item;}
  void UseReplacement(BuildingCatalogItem item){if(item==null)throw new InvalidOperationException("Zuerst ein Objekt erfassen oder im Katalog auswählen.");replacement=item;Find<TextBlock>("ObjectReplacement").Text=NiceName(item.Name);if(worldReady&&engine.IsReady)engine.SetPropTarget(item,Find<ToggleButton>("PropToggle").IsChecked==true);Status("objects","Ersatz gewählt: "+NiceName(item.Name)+(item.Mesh==new string('0',32)?". Die Vorschau bleibt das Ausgangsobjekt.":""));}
  void SetSource(BuildingCatalogItem item){if(item.Mesh==new string('0',32))throw new InvalidOperationException("Für eine feste Quelle ein bekanntes Objekt aus dem Katalog wählen.");if(engine.IsReady)engine.SetFixedSource(item);fixedSource=item;Find<RadioButton>("FixedSource").IsChecked=true;Find<TextBlock>("SourceName").Text="Feste Quelle: "+NiceName(item.Name);Status("objects","Ausgangsobjekt gewählt. Andere platzierbare Objekte bleiben unverändert.");}
  void ApplySource(){if(fixedSource!=null&&Find<RadioButton>("FixedSource").IsChecked==true)engine.SetFixedSource(fixedSource);else engine.UseCurrentPlaceable();}
  void UpdateStates(){Find<TextBlock>("BlockState").Text=T(Find<ToggleButton>("BlockToggle").IsChecked==true?"buildingOn":"buildingOff");Find<TextBlock>("PropState").Text=T(Find<ToggleButton>("PropToggle").IsChecked==true?"buildingOn":"buildingOff");Find<TextBlock>("PreserveState").Text=T(Find<ToggleButton>("PreserveToggle").IsChecked==true?"buildPreserveOn":"buildPreserveOff");Find<TextBlock>("FreeBuildState").Text=T(Find<ToggleButton>("FreeBuildToggle").IsChecked==true?"buildFreeOn":"buildFreeOff");Find<TextBlock>("DurabilityState").Text=T(Find<ToggleButton>("DurabilityToggle").IsChecked==true?"durabilityOn":"durabilityOff");Find<TextBlock>("CraftState").Text=T(Find<ToggleButton>("CraftToggle").IsChecked==true?"craftPreserveOn":"craftPreserveOff");}
  internal void CaptureAndEnableObject(){Try("objects",()=>{RequireWorld();RequirePickaxe();if(Environment.TickCount-(int)aimSeen>750)throw new InvalidOperationException("Ein Objekt mit der Spitzhacke im Fadenkreuz anvisieren.");var item=engine.CaptureAimedObject().Item;UseReplacement(item);ApplySource();engine.SetPropTarget(item,true);Find<ToggleButton>("PropToggle").IsChecked=true;UpdateStates();Status("objects","Als Ersatz aktiviert: "+NiceName(item.Name));});}
  internal void CaptureAndEnableMaterial(){Try("blocks",()=>{RequireWorld();RequirePickaxe();if(materialSeen==0||unchecked((uint)Environment.TickCount-(uint)materialSeen)>750)throw new InvalidOperationException("Kein frisches Materialziel. Boden oder Baublock direkt anvisieren.");int id=engine.CaptureMaterial();Find<TextBox>("MaterialId").Text=id.ToString();ApplyMaterial(true);Find<ToggleButton>("BlockToggle").IsChecked=true;UpdateStates();Status("blocks","Als Ersatz aktiviert: "+MaterialTitle(id));});}
  internal void DisableObjectReplacement(){Try("objects",()=>{if(engine.IsReady)engine.EnableProps(false);Find<ToggleButton>("PropToggle").IsChecked=false;UpdateStates();Status("objects","Objekt-Ersetzen ausgeschaltet.");});}
  internal void DisableMaterialReplacement(){Try("blocks",()=>{if(engine.IsReady)engine.EnableBlocks(false);Find<ToggleButton>("BlockToggle").IsChecked=false;UpdateStates();Status("blocks","Material-Ersetzen ausgeschaltet.");});}
  internal void ShowHotkeyLabels(string materialCapture,string materialOff,string objectCapture,string objectOff){Find<TextBlock>("MaterialHotkeys").Text="Übernehmen: "+materialCapture+" · Abwählen: "+materialOff;Find<TextBlock>("ObjectHotkeys").Text="Übernehmen: "+objectCapture+" · Abwählen: "+objectOff;}
  internal void Page(string page){Root.Visibility=page=="blocks"||page=="objects"||page=="craft"||page=="equipment"?Visibility.Visible:Visibility.Collapsed;Find<StackPanel>("BlocksPage").Visibility=page=="blocks"?Visibility.Visible:Visibility.Collapsed;Find<StackPanel>("ObjectsPage").Visibility=page=="objects"?Visibility.Visible:Visibility.Collapsed;Find<StackPanel>("EquipmentPage").Visibility=page=="equipment"?Visibility.Visible:Visibility.Collapsed;Find<StackPanel>("CraftPage").Visibility=page=="craft"?Visibility.Visible:Visibility.Collapsed;}
  internal int ActiveCount{get{return new[]{"BlockToggle","PropToggle","PreserveToggle","FreeBuildToggle","CraftToggle","DurabilityToggle"}.Count(name=>Find<ToggleButton>(name).IsChecked==true);}}
  internal void Reset(){System.Threading.Interlocked.Increment(ref scanGeneration);Find<ListBox>("NearbyList").ItemsSource=null;Find<TextBlock>("NearbyCount").Text=T("nearbyEmpty");worldReady=false;changing=true;try{foreach(string name in new[]{"BlockToggle","PropToggle","PreserveToggle","FreeBuildToggle","CraftToggle","DurabilityToggle"}){Find<ToggleButton>(name).IsChecked=false;Find<ToggleButton>(name).IsEnabled=false;}aimSeen=0;lastAim=0;heldSeen=0;lastHeld=0;materialSeen=0;lastMaterial=0;Find<TextBlock>("ObjectAim").Text=T("buildingWaiting");Find<TextBlock>("MaterialAim").Text=T("buildingWaiting");UpdateStates();}finally{changing=false;}}
  internal void Poll(bool ready){
   worldReady=ready;Find<Button>("BlocksConnect").Visibility=engine.IsReady?Visibility.Collapsed:Visibility.Visible;Find<Button>("ObjectsConnect").Visibility=engine.IsReady?Visibility.Collapsed:Visibility.Visible;
   Find<Button>("EquipmentConnect").Visibility=engine.IsReady?Visibility.Collapsed:Visibility.Visible;Find<Button>("CraftConnect").Visibility=engine.IsReady?Visibility.Collapsed:Visibility.Visible;if(!engine.IsReady){Reset();return;}
   try{
    var s=engine.Sample();if(!ready){if(s.BlocksEnabled||s.PropsEnabled||s.NoConsumption||s.FreeBuild||s.NoCraftConsumption||s.NoDurabilityLoss)engine.AllOff();Reset();return;}
    changing=true;Find<ToggleButton>("DurabilityToggle").IsEnabled=true;Find<ToggleButton>("DurabilityToggle").IsChecked=s.NoDurabilityLoss;Find<ToggleButton>("CraftToggle").IsEnabled=true;Find<ToggleButton>("CraftToggle").IsChecked=s.NoCraftConsumption;Find<ToggleButton>("PreserveToggle").IsEnabled=true;Find<ToggleButton>("PreserveToggle").IsChecked=s.NoConsumption;Find<ToggleButton>("FreeBuildToggle").IsEnabled=true;Find<ToggleButton>("FreeBuildToggle").IsChecked=s.FreeBuild;Find<ToggleButton>("BlockToggle").IsEnabled=true;Find<ToggleButton>("PropToggle").IsEnabled=true;Find<ToggleButton>("BlockToggle").IsChecked=s.BlocksEnabled;Find<ToggleButton>("PropToggle").IsChecked=s.PropsEnabled;UpdateStates();

    if(s.HeldCalls!=lastHeld){lastHeld=s.HeldCalls;heldSeen=Environment.TickCount;}
    if(s.TargetCalls!=lastMaterial){lastMaterial=s.TargetCalls;materialSeen=Environment.TickCount;}
    int currentMaterial=-1;if(heldSeen!=0&&materialSeen!=0&&unchecked((uint)Environment.TickCount-(uint)heldSeen)<=750&&unchecked((uint)Environment.TickCount-(uint)materialSeen)<=750){try{currentMaterial=engine.CaptureMaterial();}catch{}}
    Find<TextBlock>("MaterialAim").Text=currentMaterial<1?T("buildingWaiting"):MaterialTitle(currentMaterial);

    if(s.AimedCalls!=lastAim){lastAim=s.AimedCalls;aimSeen=Environment.TickCount;}
    BuildingTarget currentTarget=null;if(heldSeen!=0&&unchecked((uint)Environment.TickCount-(uint)heldSeen)<=750&&BuildingEngine.IsPickaxe(s)){try{currentTarget=engine.CaptureAimedObject();}catch{}}
    Find<TextBlock>("ObjectAim").Text=currentTarget==null?T("buildingWaiting"):NiceName(currentTarget.Item.Name);
   }catch(Exception error){Status("objects",error.Message);Reset();}finally{changing=false;}
  }
 }
}
