using System;
using System.IO;
using System.Media;

namespace Odd2U {
 // Original synthesised cartoon meow, kept in memory and played asynchronously.
 public sealed class PetSound:IDisposable {
  readonly MemoryStream wave;readonly SoundPlayer player;DateTime last=DateTime.MinValue;
  public PetSound(){wave=new MemoryStream(CreateWave());player=new SoundPlayer(wave);}
  public void Meow(){if((DateTime.UtcNow-last).TotalSeconds<1.2)return;last=DateTime.UtcNow;try{player.Play();}catch{}}
  public void Stop(){try{player.Stop();}catch{}}
  public void Dispose(){Stop();player.Dispose();wave.Dispose();}
  public static byte[] CreateWave(){
   const int rate=22050;const double duration=.85;int count=(int)(rate*duration);var stream=new MemoryStream();
   using(var writer=new BinaryWriter(stream)){
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
    double phase=0;
    for(int i=0;i<count;i++){
     double t=i/(double)rate,u=t/duration;
     // Rising 'mee', then a rounded falling 'ow', with a little vocal flutter.
     double pitch=(u<.28?470+240*Math.Sin(u/.28*Math.PI/2):710-400*Math.Pow((u-.28)/.72,.65))*(1+.012*Math.Sin(2*Math.PI*27*t));phase+=2*Math.PI*pitch/rate;
     double vowel=Math.Max(0,Math.Min(1,(u-.20)/.65));double f1=1100-500*vowel,f2=2600-1500*vowel,sample=0;
     for(int h=1;h<=12;h++){double frequency=h*pitch;double weight=(.12+Math.Exp(-Math.Pow((frequency-f1)/350,2))+.55*Math.Exp(-Math.Pow((frequency-f2)/500,2)))/h;sample+=weight*Math.Sin(h*phase);}
     double envelope=Math.Min(1,t/.045)*Math.Pow(Math.Sin(Math.PI*u),.65)*Math.Min(1,(duration-t)/.09);
     writer.Write((short)(Math.Tanh(sample*1.5)*envelope*11000));
    }
    writer.Flush();return stream.ToArray();
   }
  }
 }
}
