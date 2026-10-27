using System;
using System.Linq;
using System.Speech.Recognition;
using Odd2U;
class VoiceChecks {
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;}
 static int Main(){try{
  foreach(bool chinese in new[]{false,true})foreach(var pair in VoiceCommands.Phrases(chinese)){string action;Check(VoiceCommands.Resolve(pair.Key,.95f,chinese,out action)&&action==pair.Value,"mapping "+pair.Key);Check(!VoiceCommands.Resolve(pair.Key,.2f,chinese,out action),"reject low confidence");}
  foreach(string word in new[]{"jump","sit","don't jump","奥德不要跳一下","跳一下","unrelated conversation"}){string a;Check(!VoiceCommands.Resolve(word,.99f,false,out a)&&!VoiceCommands.Resolve(word,.99f,true,out a),"reject unrelated or bare phrase");}
  foreach(var info in VoiceCommands.Installed())using(var engine=new SpeechRecognitionEngine(info)){
   engine.LoadGrammar(VoiceCommands.GrammarFor(info));bool chinese=info.Culture.TwoLetterISOLanguageName=="zh";
   foreach(var pair in VoiceCommands.Phrases(chinese)){var r=engine.EmulateRecognize(pair.Key);Check(r!=null,"grammar emulation "+pair.Key);}
   Console.WriteLine("Grammar emulation passed: "+info.Culture.Name+" (no microphone used)");
  }
  Console.WriteLine("PASS "+count+" checks. Mandarin audio needs an installed Mandarin recogniser and a real microphone test.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
