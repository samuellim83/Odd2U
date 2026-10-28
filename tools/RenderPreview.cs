using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Odd2U;

// Compile against a built Odd2U.exe plus WPF, then run beside Odd2U.exe.
class RenderPreview {
 [STAThread] static void Main(string[] args){
  new Application();string output=args[0];Directory.CreateDirectory(output);
  var scene=new Scene();scene.Camera(12,8);
  var frame=new Border{Width=480,Height=480,Background=new SolidColorBrush(Color.FromRgb(20,40,68)),Child=scene.View};
  frame.Measure(new Size(480,480));frame.Arrange(new Rect(0,0,480,480));frame.UpdateLayout();
  for(int n=0;n<120;n++)scene.Cat.Animate(Mood.Idle,0,1.0/30,0,0);
  Mood previous=Mood.Idle;
  for(int i=0;i<120;i++){
   double t=i/15.0;Mood mood=t<1?Mood.Idle:t<3?Mood.WiggleTail:t<4.2?Mood.Jump:t<6?Mood.Smile:Mood.Idle;
   if(mood!=previous){scene.Cat.BeginAction();previous=mood;}
   scene.Cat.Animate(mood,t,1.0/15,0,Math.Sin(t*Math.PI/4)*10);
   frame.UpdateLayout();var bitmap=new RenderTargetBitmap(480,480,96,96,PixelFormats.Pbgra32);bitmap.Render(frame);
   var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(output,i.ToString("D3")+".png")))png.Save(f);
  }
 }
}
