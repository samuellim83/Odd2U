using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;

namespace Odd2U {
 public static class Desktop {
  [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
  [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO {public uint Size,Time;}
  public delegate bool EnumProc(IntPtr handle,IntPtr param);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr param);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  public static void ToolWindow(Window window){var h=new WindowInteropHelper(window).Handle;SetWindowLong(h,-20,GetWindowLong(h,-20)|0x80);}
  public static Point Cursor(Window window){POINT p;GetCursorPos(out p);return FromPixels(window,new Point(p.X,p.Y));}
  public static Point FromPixels(Window w,Point p){var source=PresentationSource.FromVisual(w);return source==null?p:source.CompositionTarget.TransformFromDevice.Transform(p);}
  public static Point ToPixels(Window w,Point p){var source=PresentationSource.FromVisual(w);return source==null?p:source.CompositionTarget.TransformToDevice.Transform(p);}
  public static Rect WorkArea(Window w) {
   var pixel=ToPixels(w,new Point(w.Left+w.Width/2,w.Top+w.Height/2));var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)pixel.X,(int)pixel.Y)).WorkingArea;
   var a=FromPixels(w,new Point(area.Left,area.Top));var b=FromPixels(w,new Point(area.Right,area.Bottom));return new Rect(a,b);
  }
  public static Rect Clamp(Rect pet,Rect work){return new Rect(Math.Max(work.Left,Math.Min(work.Right-pet.Width,pet.Left)),Math.Max(work.Top,Math.Min(work.Bottom-pet.Height,pet.Top)),pet.Width,pet.Height);}
  public static double IdleSeconds(){var info=new LASTINPUTINFO();info.Size=(uint)Marshal.SizeOf(info);if(!GetLastInputInfo(ref info))return 0;return unchecked((uint)Environment.TickCount-info.Time)/1000.0;}
  public static bool Surface(Window w,IntPtr handle,out Rect rect){rect=Rect.Empty;RECT r;if(handle==IntPtr.Zero||!IsWindowVisible(handle)||IsIconic(handle)||!GetWindowRect(handle,out r))return false;rect=new Rect(FromPixels(w,new Point(r.Left,r.Top)),FromPixels(w,new Point(r.Right,r.Bottom)));return rect.Width>200&&rect.Height>100;}
  public static IntPtr FindPerch(Window w) {
   IntPtr found=IntPtr.Zero,own=new WindowInteropHelper(w).Handle;double best=90;
   EnumWindows(delegate(IntPtr h,IntPtr unused){
    if(h==own||(GetWindowLong(h,-20)&0x80)!=0)return true;
    Rect r;if(!Surface(w,h,out r))return true;
    double bottom=w.Top+w.Height*.89,d=Math.Abs(bottom-r.Top);
    if(d<best && w.Left+w.Width*.5>r.Left+40 && w.Left+w.Width*.5<r.Right-40){best=d;found=h;}return true;
   },IntPtr.Zero);return found;
  }
  public static bool FullscreenOther(Window w) {
   var h=GetForegroundWindow();if(h==IntPtr.Zero||h==new WindowInteropHelper(w).Handle)return false;RECT r;if(!GetWindowRect(h,out r))return false;
   var screen=Forms.Screen.FromHandle(h).Bounds;
   return r.Left<=screen.Left&&r.Top<=screen.Top&&r.Right>=screen.Right&&r.Bottom>=screen.Bottom;
  }
 }
}
