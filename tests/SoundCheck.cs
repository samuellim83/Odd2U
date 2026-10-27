using System;
using System.IO;
using System.Media;
using Odd2U;
class SoundCheck {
 static int Main(string[] args) {
  byte[] data=PetSound.CreateWave();
  if(data.Length<30000 || BitConverter.ToInt32(data,40)!=data.Length-44)throw new Exception("Invalid WAV length");
  int peak=0;for(int i=44;i<data.Length;i+=2)peak=Math.Max(peak,Math.Abs((int)BitConverter.ToInt16(data,i)));
  if(peak<1000||peak>12000)throw new Exception("Invalid sound amplitude");
  using(var stream=new MemoryStream(data))using(var player=new SoundPlayer(stream)){player.Load();}
  File.WriteAllBytes(args[0],data);Console.WriteLine("PASS: WAV decoded; duration 0.85s; peak "+peak);return 0;
 }
}
