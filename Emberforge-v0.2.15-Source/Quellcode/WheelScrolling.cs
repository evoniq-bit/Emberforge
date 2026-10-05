using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Runtime.InteropServices;

namespace Emberforge {
 // Route the wheel before child controls swallow it. Scroll an inner list first;
 // at its boundary continue with the containing page, without scrolling twice.
 internal static class WheelScrolling {
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action,uint parameter,out uint value,uint flags);
  internal static void Attach(Window window,ScrollViewer page){
   window.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler((sender,e)=>Route(e,page)),true);
  }
  static DependencyObject Parent(DependencyObject node){
   var content=node as ContentElement;
   if(content!=null){var parent=ContentOperations.GetParent(content);if(parent!=null)return parent;var framework=content as FrameworkContentElement;return framework==null?null:framework.Parent;}
   if(node is Visual||node is Visual3D)return VisualTreeHelper.GetParent(node);
   return LogicalTreeHelper.GetParent(node);
  }
  static bool CanMove(ScrollViewer viewer,int delta){
   return viewer.ScrollableHeight>0&&(delta<0?viewer.VerticalOffset<viewer.ScrollableHeight-0.0001:viewer.VerticalOffset>0.0001);
  }
  static void Move(ScrollViewer viewer,int delta){
   uint lines;if(!SystemParametersInfo(0x68,0,out lines,0))lines=3;
   double distance=lines==uint.MaxValue?viewer.ViewportHeight:lines*(viewer.CanContentScroll?1.0:16.0);
   viewer.ScrollToVerticalOffset(viewer.VerticalOffset-delta/120.0*distance);
  }
  internal static void Route(MouseWheelEventArgs e,ScrollViewer page){
   if(e.Delta==0)return;
   for(var node=e.OriginalSource as DependencyObject;node!=null;node=Parent(node)){
    var viewer=node as ScrollViewer;
    if(viewer!=null&&CanMove(viewer,e.Delta)){Move(viewer,e.Delta);e.Handled=true;return;}
   }
   if(CanMove(page,e.Delta))Move(page,e.Delta);
   e.Handled=true;
  }
 }
}
