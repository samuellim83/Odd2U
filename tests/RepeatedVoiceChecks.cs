using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading;
using Odd2U;
class RepeatedVoiceChecks {
 static int Main(){try{
  if(VoiceWindow.SuppressDuplicate("en","en","Jump","Jump",.2))throw new Exception("Repeated same-language command suppressed");
  if(VoiceWindow.SuppressDuplicate("en","en","Jump","Sit",.2))throw new Exception("Different next command suppressed");
  if(!VoiceWindow.SuppressDuplicate("en","zh","Jump","Jump",.2))throw new Exception("Cross-engine duplicate accepted");
  using(var audio=new MemoryStream())using(var synth=new SpeechSynthesizer()){
   synth.SelectVoiceByHints(VoiceGender.NotSet,VoiceAge.NotSet,0,new CultureInfo("en-US"));synth.SetOutputToWaveStream(audio);
   var prompt=new PromptBuilder(new CultureInfo("en-US"));foreach(string command in new[]{"odd jump","odd sit","odd smile"}){prompt.AppendText(command);prompt.AppendBreak(TimeSpan.FromSeconds(1));}synth.Speak(prompt);synth.SetOutputToNull();audio.Position=0;
   var info=VoiceCommands.Installed().First(r=>r.Culture.TwoLetterISOLanguageName=="en");
   using(var engine=new SpeechRecognitionEngine(info))using(var done=new ManualResetEvent(false)){
    engine.LoadGrammar(VoiceCommands.GrammarFor(info));engine.InitialSilenceTimeout=TimeSpan.Zero;engine.BabbleTimeout=TimeSpan.Zero;engine.EndSilenceTimeout=TimeSpan.FromMilliseconds(400);engine.EndSilenceTimeoutAmbiguous=TimeSpan.FromMilliseconds(650);
    var actions=new List<string>();engine.SpeechRecognized+=delegate(object s,SpeechRecognizedEventArgs e){string action;Console.WriteLine(e.Result.Text+" "+e.Result.Confidence);if(VoiceCommands.Resolve(e.Result.Text,e.Result.Confidence,false,out action))actions.Add(action);};engine.RecognizeCompleted+=delegate{done.Set();};engine.SetInputToWaveStream(audio);engine.RecognizeAsync(RecognizeMode.Multiple);
    if(!done.WaitOne(20000))throw new Exception("Repeated audio recognition timed out");if(string.Join(",",actions)!="Jump,Sit,Smile")throw new Exception("Expected Jump,Sit,Smile; got "+string.Join(",",actions));
   }
  }Console.WriteLine("PASS: three successive spoken commands in one continuous recognition session; duplicate guards passed. Synthesised input, no microphone.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
