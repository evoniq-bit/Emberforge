using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace Emberforge {
 internal sealed class ProgressionView {
  readonly MainView parent;readonly GameSession session;readonly ProgressionEngine engine;readonly FrameworkElement equipment;readonly bool preview;
  ProgressionSnapshot sample;uint calls;long seen,pendingAt;string pending;bool editing,world;int slotChoice=-1;long readFailureAt;
  internal int ActiveCount{get{return session.StackLimitCount+(sample!=null&&sample.Bonus>0?1:0);}}
  internal ProgressionView(MainView view,GameSession game,FrameworkElement building,bool isPreview){
   parent=view;session=game;engine=new ProgressionEngine(game);equipment=building;preview=isPreview;
   C<Button>("XpApply").Click+=(s,e)=>Run("Xp",()=>Queue(1,Number("Xp",1000000)));
   C<Button>("SkillApply").Click+=(s,e)=>Run("Skill",()=>{Require();engine.AddSkills(Number("Skill",50000));C<TextBlock>("SkillStatus").Text=parent.T("ProgressDone");Poll(true);});
   C<Button>("SkillClear").Click+=(s,e)=>Run("Skill",()=>{Require();engine.ClearSkills();C<TextBlock>("SkillStatus").Text=parent.T("ProgressDone");Poll(true);});
   var slots=C<ComboBox>("StackSlot");var selected=new ComboBoxItem();selected.SetResourceReference(ContentControl.ContentProperty,"StackSlotCurrent");slots.Items.Add(selected);for(int i=1;i<=8;i++)slots.Items.Add("Slot "+i);slots.SelectedIndex=0;
   slots.SelectionChanged+=(s,e)=>{slotChoice=slots.SelectedIndex-1;if(!preview&&session.IsConnected&&world)Poll(world);};
   C<Button>("StackLimitApply").Click+=(s,e)=>Run("StackLimit",()=>{Require();var current=engine.Sample(slotChoice);if(!current.Stackable)throw new InvalidOperationException(parent.T("StackEmpty"));int amount=Number("StackLimit",50000);if(amount<current.OriginalStackLimit||amount<2)throw new ArgumentException(string.Format(parent.T("StackLimitMinimum"),Math.Max(2,current.OriginalStackLimit)));engine.SetLimit(current,amount);C<TextBlock>("StackLimitStatus").Text=parent.T("ProgressDone");Poll(true);});
   C<Button>("StackLimitReset").Click+=(s,e)=>Run("StackLimit",()=>{Require();engine.ResetLimit(engine.Sample(slotChoice));C<TextBlock>("StackLimitStatus").Text=parent.T("ProgressDone");Poll(true);});
   C<TextBox>("StackLimitAmount").TextChanged+=(s,e)=>Clamp("StackLimitAmount");
   C<Button>("StackApply").Click+=(s,e)=>Run("Stack",()=>Queue(3,Number("Stack",50000)));
   C<TextBox>("StackAmount").TextChanged+=(s,e)=>Clamp("StackAmount");Reset();
  }
  void Clamp(string name){if(editing)return;var box=C<TextBox>(name);string value=ProgressionEngine.ClampStackInput(box.Text);if(value!=box.Text){editing=true;box.Text=value;box.CaretIndex=value.Length;editing=false;}}
  T C<T>(string name) where T:class{return (name.StartsWith("Stack",StringComparison.Ordinal)?equipment.FindName(name):parent.Window.FindName(name)) as T;}
  int Number(string area,int maximum){int amount;if(!int.TryParse(C<TextBox>(area+"Amount").Text,NumberStyles.Integer,CultureInfo.InvariantCulture,out amount)||amount<1||amount>maximum)throw new ArgumentException(string.Format(parent.T("ProgressNumber"),maximum));return amount;}
  void Require(){if(preview||!session.IsConnected||sample==null||seen==0||DateTime.UtcNow.Ticks-seen>TimeSpan.FromSeconds(2).Ticks)throw new InvalidOperationException(parent.T("ProgressWait"));if(pending!=null||sample.Command!=0)throw new InvalidOperationException(parent.T("ProgressBusy"));}
  void Queue(int command,int amount){Require();var current=engine.Sample(slotChoice);if(current.Calls==0)throw new InvalidOperationException(parent.T("ProgressWait"));if(command==3&&!current.Stackable)throw new InvalidOperationException(parent.T("StackEmpty"));if(command==3&&amount>current.NormalStackLimit)throw new InvalidOperationException(string.Format(parent.T("StackTooLarge"),current.NormalStackLimit));engine.Request(command,amount,current);pending=command==1?"Xp":"Stack";pendingAt=DateTime.UtcNow.Ticks;C<TextBlock>(pending+"Status").Text=parent.T("ProgressQueued");Disable();}
  void Run(string area,Action action){try{action();}catch(ArgumentException ex){C<TextBlock>(area+"Status").Text=ex.Message.StartsWith("Bitte")||ex.Message.StartsWith("Enter")||ex.Message.StartsWith("Die neue")||ex.Message.StartsWith("The new")?ex.Message:string.Format(parent.T("ProgressNumber"),area=="Xp"?1000000:50000);}catch(Exception ex){Files.Log("Progression: "+ex);C<TextBlock>(area+"Status").Text=ex.Message==parent.T("ProgressWait")||ex.Message==parent.T("ProgressBusy")||ex.Message==parent.T("StackEmpty")||ex.Message==string.Format(parent.T("StackTooLarge"),sample==null?0:sample.NormalStackLimit)?ex.Message:parent.T("ProgressError");}}
  void Disable(){foreach(string area in new[]{"Xp","Skill","Stack","StackLimit"})C<Button>(area+"Apply").IsEnabled=false;C<Button>("SkillClear").IsEnabled=false;C<Button>("StackLimitReset").IsEnabled=false;}
  internal void Reset(){Disable();world=false;sample=null;calls=0;seen=0;pending=null;C<TextBlock>("XpValues").Text=parent.T("ProgressWait");C<TextBlock>("SkillValues").Text=parent.T("ProgressWait");C<TextBlock>("StackValues").Text=parent.T("ProgressWait");C<TextBlock>("StackLimitValues").Text=parent.T("StackEmpty");}
  internal void LanguageChanged(){if(sample!=null)Labels();else Reset();}
  void Labels(){C<TextBlock>("XpValues").Text=string.Format(parent.T("XpValues"),sample.Level,sample.Experience);C<TextBlock>("SkillValues").Text=string.Format(parent.T("SkillValues"),sample.BaseSkills+sample.Bonus,sample.Bonus);C<TextBlock>("StackValues").Text=sample.Stock!=0&&sample.Amount>0?string.Format(parent.T("StackValues"),sample.QuickSlot+1,sample.Name??string.Format(parent.T("StackItem"),sample.Item),sample.Amount):parent.T("StackEmpty");C<TextBlock>("StackLimitValues").Text=sample.Definition!=0?string.Format(parent.T("StackLimitValues"),sample.Name??string.Format(parent.T("StackItem"),sample.Item),sample.OriginalStackLimit,sample.NormalStackLimit):parent.T("StackEmpty");}
  internal void Poll(bool worldReady){
   world=worldReady;
   if(preview||!session.IsConnected||!worldReady){if(session.IsConnected){if(pending!=null)GameSession.Write(session.Handle,session.Receipt.Data+ProgressionEngine.RelativeData,new byte[4]);if(sample!=null&&sample.Bonus!=0)engine.ClearSkills();}Reset();return;}
   try{
    sample=engine.Sample(slotChoice);long now=DateTime.UtcNow.Ticks;if(sample.Calls!=calls){calls=sample.Calls;seen=now;}
    bool fresh=seen!=0&&now-seen<TimeSpan.FromSeconds(2).Ticks&&sample.Player>0x10000;
    if(pending!=null){if(sample.Command==0&&sample.Result!=0){C<TextBlock>(pending+"Status").Text=parent.T(sample.Result==1?"ProgressDone":"ProgressRejected");pending=null;}
     else if(now-pendingAt>TimeSpan.FromSeconds(3).Ticks){GameSession.Write(session.Handle,session.Receipt.Data+ProgressionEngine.RelativeData,new byte[4]);C<TextBlock>(pending+"Status").Text=parent.T("ProgressTimeout");pending=null;}}
    bool available=fresh&&pending==null&&sample.Command==0;C<Button>("XpApply").IsEnabled=available&&sample.Level>0;C<Button>("SkillApply").IsEnabled=available&&sample.SkillCalls!=0;C<Button>("SkillClear").IsEnabled=available&&sample.Bonus>0;C<Button>("StackApply").IsEnabled=available&&sample.Stackable;C<Button>("StackLimitApply").IsEnabled=available&&sample.Stackable;C<Button>("StackLimitReset").IsEnabled=available&&sample.Definition!=0&&sample.NormalStackLimit!=sample.OriginalStackLimit;Labels();
   }catch(Exception ex){if(seen==0||DateTime.UtcNow.Ticks-seen>TimeSpan.FromSeconds(2).Ticks)Disable();if(DateTime.UtcNow.Ticks-readFailureAt>TimeSpan.FromSeconds(30).Ticks){Files.Log("Progression poll: "+ex);readFailureAt=DateTime.UtcNow.Ticks;}}
  }
 }
}
