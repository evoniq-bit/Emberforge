using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Emberforge {
 internal static class BuildingHotkeyTests {
  static IEnumerable<T> Children<T>(DependencyObject node)where T:DependencyObject{
   int count=System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);for(int i=0;i<count;i++){var child=System.Windows.Media.VisualTreeHelper.GetChild(node,i);if(child is T)yield return (T)child;foreach(var nested in Children<T>(child))yield return nested;}
  }
  internal static string Run(){
   var report=new StringBuilder();Action<bool,string> check=(ok,label)=>{report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(report.ToString());};
   var selected=new[]{new BuildingShortcut{Key=0x43},new BuildingShortcut{Key=0x23},new BuildingShortcut{Key=0x58},new BuildingShortcut{Key=0x2e}};int[] characterKeys={0,1,2,4,5,6,3};
   check(BuildingHotkeys.Validate(selected,characterKeys)==null,"four independent capture/deselect bindings accepted");
   check(BuildingHotkeys.Resolve(selected,0x58,0)==2&&BuildingHotkeys.Resolve(selected,0x2e,0)==3,"object capture and object deselect route separately");
   check(BuildingHotkeys.Resolve(selected,0x43,0)==0&&BuildingHotkeys.Resolve(selected,0x23,0)==1,"material capture and material deselect route separately");
   check(BuildingHotkeys.Resolve(selected,0x58,2)==-1&&BuildingHotkeys.Resolve(selected,0,0)==-1,"different modifiers and unassigned bindings never trigger");
   selected[0].Key=0x58;check(BuildingHotkeys.Validate(selected,characterKeys)=="buildingKeysDuplicate","duplicate binding rejected before registration");
   selected[0].Modifiers=2;check(BuildingHotkeys.Validate(selected,characterKeys)==null,"same key with different modifiers accepted");
   selected[0].Key=0x70;selected[0].Modifiers=0;check(BuildingHotkeys.Validate(selected,characterKeys)=="buildingKeysCharacterConflict","character shortcut conflict rejected");
   selected[0].Modifiers=2;check(BuildingHotkeys.Validate(selected,characterKeys)==null,"modified function key is independent from character shortcut");
   selected[0].Key=0x43;selected[0].Modifiers=0;
   check(BuildingShortcutPreferences.Normalize(new BuildingShortcutPreferences{Shortcuts=new[]{selected[0]}}).Shortcuts.All(k=>k.Key==0),"malformed saved binding file falls back to unassigned");
   check(BuildingShortcutPreferences.Normalize(Resources.Json.Deserialize<BuildingShortcutPreferences>(Resources.Json.Serialize(new BuildingShortcutPreferences{Shortcuts=selected}))).Shortcuts[2].Key==0x58,"settings serialize and restore selected object key");
   var registered=new Dictionary<int,uint>();uint lastMods=0;Func<IntPtr,int,uint,uint,bool> add=(h,id,mods,key)=>{lastMods=mods;registered[id]=key;return true;};Func<IntPtr,int,bool> remove=(h,id)=>registered.Remove(id);
   check(BuildingHotkeys.RegisterSet(new IntPtr(1),selected,true,add,remove)&&registered.Count==4&&(lastMods&0x4000)!=0,"global registration includes all four actions and suppresses held-key repeats");
   check(BuildingHotkeys.RegisterSet(new IntPtr(1),selected,false,add,remove)&&registered.Count==0,"disabling global setting releases all four native bindings");
   add=(h,id,mods,key)=>{if(id==BuildingHotkeys.FirstId+2)return false;registered[id]=key;return true;};
   check(!BuildingHotkeys.RegisterSet(new IntPtr(1),selected,true,add,remove)&&registered.Count==0,"native conflict releases partial registrations");
   check(BuildingHotkeys.InputBlocks(true,new TextBox())&&BuildingHotkeys.InputBlocks(true,new ComboBoxItem()),"typing and choosing keys in active app blocks capture actions");
   check(!BuildingHotkeys.InputBlocks(false,new ComboBox()),"stale settings focus does not block hotkeys when game is foreground");
   var view=new MainView(true);view.Page("settings");view.Render(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Emberforge-hotkey-test.png"),1040,760);
   var combos=Children<ComboBox>(view.Find<StackPanel>("BuildingHotkeySettings")).ToArray();check(combos.Length==8,"settings exposes separate key and modifier selectors for all four actions");
   for(int i=0;i<4;i++){combos[i*2].SelectedValue=selected[i].Modifiers;combos[i*2+1].SelectedValue=selected[i].Key;}
   view.Find<CheckBox>("GlobalHotkeys").IsChecked=false;view.Find<Button>("SaveSettings").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   var building=view.Find<Grid>("MainContent").Children.OfType<UserControl>().Single();
   check(((TextBlock)building.FindName("ObjectHotkeys")).Text.Contains("X")&&((TextBlock)building.FindName("ObjectHotkeys")).Text.Contains("Entf"),"saved object bindings appear on object page; footer="+view.Find<TextBlock>("Footer").Text+"; label="+((TextBlock)building.FindName("ObjectHotkeys")).Text);
   check(((TextBlock)building.FindName("MaterialHotkeys")).Text.Contains("C")&&((TextBlock)building.FindName("MaterialHotkeys")).Text.Contains("Ende"),"saved material bindings appear on block page");
   combos[1].SelectedValue=0x58;view.Find<Button>("SaveSettings").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   check(view.Find<TextBlock>("Footer").Text.Contains("unterschiedliche"),"duplicate settings show readable error in app");
   check(((TextBlock)building.FindName("MaterialHotkeys")).Text.Contains("C"),"rejected edit retains prior applied bindings");
   var objectPanel=(StackPanel)building.FindName("ObjectHotkeyPanel");var materialPanel=(StackPanel)building.FindName("MaterialHotkeyPanel");
   var objectButtons=Children<Button>(objectPanel).ToArray();var materialButtons=Children<Button>(materialPanel).ToArray();
   check(objectButtons.Length==4&&materialButtons.Length==4,"capture and remove buttons live directly in both building pages");
   view.Page("objects");objectButtons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   check(objectButtons[0].Content.ToString().Contains("Taste drücken"),"click on object binding starts key recording");
   check(view.BuildingKeyController.AcceptCapturedKey(Key.R,ModifierKeys.None,false)&&((TextBlock)building.FindName("ObjectHotkeys")).Text.Contains("R"),"recorded letter immediately replaces object binding");
   check((int)combos[5].SelectedValue==0x52,"page recording synchronizes settings selection");
   view.Page("blocks");materialButtons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));view.BuildingKeyController.AcceptCapturedKey(Key.R,ModifierKeys.None,false);
   check(((TextBlock)building.FindName("MaterialHotkeys")).Text.Contains("C")&&Children<TextBlock>(materialPanel).Any(t=>t.Text.Contains("unterschiedliche")),"duplicate recording keeps prior binding and displays error in its page");
   view.BuildingKeyController.AcceptCapturedKey(Key.Escape,ModifierKeys.None,false);check(materialButtons[0].Content.ToString()=="C","Escape cancels recording without changing binding");
   view.Page("objects");objectButtons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));view.BuildingKeyController.AcceptCapturedKey(Key.LeftCtrl,ModifierKeys.Control,false);
   check(objectButtons[0].Content.ToString().Contains("Taste drücken"),"modifier alone waits for actual key");
   view.BuildingKeyController.AcceptCapturedKey(Key.R,ModifierKeys.Control,false);check(objectButtons[0].Content.ToString()=="Strg + R","modifier combination records and displays immediately");
   objectButtons[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));view.BuildingKeyController.AcceptCapturedKey(Key.F1,ModifierKeys.None,false);
   check(objectButtons[0].Content.ToString().Contains("Taste drücken")&&Children<TextBlock>(objectPanel).Any(t=>t.Text.Contains("Charakter")),"character shortcut conflict keeps recording open with readable error");
   view.Page("blocks");check(objectButtons[0].Content.ToString()=="Strg + R","changing category cancels unfinished recording");
   objectButtons[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));check(objectButtons[0].Content.ToString()=="Nicht belegt","remove button clears the object capture binding only");
   check(((TextBlock)building.FindName("ObjectHotkeys")).Text.Contains("Entf"),"clearing capture preserves independent deselect binding");
   var materialGlobal=Children<CheckBox>(materialPanel).Single();var objectGlobal=Children<CheckBox>(objectPanel).Single();materialGlobal.IsChecked=true;materialGlobal.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));check(objectGlobal.IsChecked==true&&view.Find<CheckBox>("GlobalHotkeys").IsChecked==true,"enable in game directly from building page synchronizes both pages and settings");objectGlobal.IsChecked=false;objectGlobal.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));check(materialGlobal.IsChecked==false&&view.Find<CheckBox>("GlobalHotkeys").IsChecked==false,"disable in game directly from object page stays synchronized");
   var header=view.Find<Border>("HeaderFrame");view.Render(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Emberforge-header-test.png"));var before=header.TranslatePoint(new Point(0,0),(UIElement)view.Window.Content);view.Find<ScrollViewer>("PageScroll").ScrollToBottom();view.Render(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"Emberforge-header-test.png"));var after=header.TranslatePoint(new Point(0,0),(UIElement)view.Window.Content);check(before==after&&view.Find<ScrollViewer>("PageScroll").VerticalOffset>0&&header.Height==18&&header.BorderThickness==new Thickness(0),"soft header transition stays fixed while building content scrolls");
   check(building.FindName("ReadMaterial")==null&&building.FindName("RememberObject")==null&&building.FindName("UseAimedObject")==null,"both old two-step capture flows removed");
   check(System.Windows.Media.VisualTreeHelper.GetParent(materialPanel)!=null&&Children<StackPanel>((Border)building.FindName("UnifiedMaterialCapture")).Contains(materialPanel),"material capture and shortcut share a single action card");
   check(Children<StackPanel>((Border)building.FindName("UnifiedObjectCapture")).Contains(objectPanel),"object capture and shortcut share a single action card");
   check(building.FindName("ScanNearby") is Button&&building.FindName("NearbyList") is ListBox&&building.FindName("UseNearby") is Button,"nearby scan and direct replacement selection are restored");
   view.Find<Button>("NavEquipment").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   check(((StackPanel)building.FindName("EquipmentPage")).Visibility==Visibility.Visible&&((StackPanel)building.FindName("ObjectsPage")).Visibility==Visibility.Collapsed,"inventory navigation opens equipment without overlapping objects page");
   var durability=(ToggleButton)building.FindName("DurabilityToggle");durability.IsChecked=true;durability.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
   check(durability.IsChecked==false&&((TextBlock)building.FindName("EquipmentStatus")).Text.Contains("verbinden"),"durability cannot activate without a live world and reports connection requirement");
   var preferences=new BuildingPreferences{CraftFavorite=true,DurabilityFavorite=true,Materials=new[]{new BuildingMaterialFavorite{Id=129,Name="Ektoplasma"}},Objects=new BuildingCatalogItem[0]};
   var restored=Resources.Json.Deserialize<BuildingPreferences>(Resources.Json.Serialize(preferences));
   check(restored.CraftFavorite&&restored.DurabilityFavorite&&restored.Materials[0].Id==129,"equipment favorite persists alongside crafting favorite and material selections");
   view.Window.Close();return report.ToString();
  }
 }
}
