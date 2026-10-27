using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Shapes;

namespace Odd2U {
 public static class VoiceCommands {
  public static string Normalize(string text){return new string((text??"").ToLowerInvariant().Where(c=>!char.IsWhiteSpace(c)&&!char.IsPunctuation(c)).ToArray());}
  public static Dictionary<string,string> Phrases(bool chinese){
   var result=new Dictionary<string,string>();
   string[] names={"Idle","Walk","Run","Sit","Sleep","Play","Jump","TongueOut","Lick","Smile","ScratchWall","WiggleTail"};
   string[] english={"idle|stay","walk","run","sit","sleep","play","jump","tongue out","lick","smile","scratch wall|scratch the wall","wiggle tail|wiggle your tail"};
   string[] mandarin={"休息|待着|待著","走路|走一走","跑步|跑起来|跑起來","坐下","睡觉|睡覺","玩耍|玩一下","跳一下|跳起来|跳起來","吐舌头|吐舌頭","舔一下","微笑|笑一下","挠墙|撓牆","摇尾巴|搖尾巴"};
   for(int i=0;i<names.Length;i++)foreach(string phrase in (chinese?mandarin[i]:english[i]).Split('|'))foreach(string prefix in chinese?new[]{"奥德","奧德"}:new[]{"odd ","hey odd "})result[prefix+phrase]=names[i];
   if(chinese){result["奥德停止听我说话"]="Off";result["奧德停止聽我說話"]="Off";}else{result["odd stop listening"]="Off";result["hey odd stop listening"]="Off";}
   return result;
  }
  public static Grammar GrammarFor(RecognizerInfo info){var choices=new Choices(Phrases(info.Culture.TwoLetterISOLanguageName=="zh").Keys.ToArray());var builder=new GrammarBuilder{Culture=info.Culture};builder.Append(choices);return new Grammar(builder);}
  public static bool Resolve(string text,float confidence,bool chinese,out string action){action=null;if(confidence<.70f||float.IsNaN(confidence))return false;string key=Normalize(text);foreach(var pair in Phrases(chinese))if(Normalize(pair.Key)==key){action=pair.Value;return true;}return false;}
  public static RecognizerInfo[] Installed(){return SpeechRecognitionEngine.InstalledRecognizers().Where(r=>r.Culture.TwoLetterISOLanguageName=="en"||r.Culture.Name=="zh-CN"||r.Culture.Name=="zh-TW").GroupBy(r=>r.Culture.TwoLetterISOLanguageName).Select(g=>g.OrderByDescending(r=>r.Culture.Name=="en-US"||r.Culture.Name=="zh-CN").First()).ToArray();}
 }
 public sealed class VoiceWindow:UserControl {
  readonly PetWindow pet;readonly CheckBox listen=new CheckBox{Content="Keep listening",FontSize=17};readonly TextBlock status=new TextBlock{TextWrapping=TextWrapping.Wrap},availability=new TextBlock{TextWrapping=TextWrapping.Wrap};
  readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly List<SpeechRecognitionEngine> engines=new List<SpeechRecognitionEngine>();
  readonly ComboBox language=new ComboBox();readonly StackPanel meterPanel=new StackPanel();readonly TextBlock meterText=new TextBlock(),feedback=new TextBlock{TextWrapping=TextWrapping.Wrap};
  readonly Rectangle[] bars=new Rectangle[32];readonly DispatcherTimer meterTimer=new DispatcherTimer();readonly int[] levels=new int[32];
  int generation,restarts;bool closed;DateTime lastAction=DateTime.MinValue,lastLevel=DateTime.MinValue;string lastEngine="",lastCommand="";int audioLevel;
  public VoiceWindow(PetWindow owner){
   pet=owner;FontSize=15;Foreground=Ui.Ink;
   var panel=new Grid();panel.ColumnDefinitions.Add(new ColumnDefinition());panel.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(14)});panel.ColumnDefinitions.Add(new ColumnDefinition());Content=panel;
   var controls=new StackPanel();var microphoneCard=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(20),Padding=new Thickness(16),Child=controls};panel.Children.Add(microphoneCard);var row=new DockPanel();listen.Content="Microphone";listen.FontSize=15;DockPanel.SetDock(listen,Dock.Left);row.Children.Add(listen);controls.Children.Add(row);
   language.Items.Add("English");language.Items.Add("Mandarin / 普通话");language.Items.Add("English + Mandarin");language.SelectedIndex=0;language.MinHeight=32;language.Margin=new Thickness(0,2,0,2);System.Windows.Automation.AutomationProperties.SetName(language,"Command language");controls.Children.Add(language);
   meterPanel.Visibility=Visibility.Hidden;meterPanel.Height=35;var wave=new System.Windows.Controls.Primitives.UniformGrid{Columns=32,Height=22};for(int i=0;i<bars.Length;i++){bars[i]=new Rectangle{Height=2,Fill=Ui.Teal,Margin=new Thickness(1),VerticalAlignment=VerticalAlignment.Center,RadiusX=2,RadiusY=2};wave.Children.Add(bars[i]);}meterPanel.Children.Add(wave);meterText.FontSize=10;meterText.Foreground=Ui.Muted;meterPanel.Children.Add(meterText);controls.Children.Add(meterPanel);
   status.Text="Microphone off";status.FontSize=11;status.TextWrapping=TextWrapping.NoWrap;status.TextTrimming=TextTrimming.CharacterEllipsis;status.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("Text"){Source=status});controls.Children.Add(status);
   var guide=new StackPanel();var commandCard=new Border{Background=Brushes.White,CornerRadius=new CornerRadius(20),Padding=new Thickness(16),Child=guide};Grid.SetColumn(commandCard,2);panel.Children.Add(commandCard);guide.Children.Add(Ui.Text("Say ‘Odd’ + an action  /  ‘奥德’ + 动作",14));var chips=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(0,5,0,4)};
   foreach(Mood mood in Enum.GetValues(typeof(Mood))){string phrase=CommandFor(mood);var chip=new Border{CornerRadius=new CornerRadius(7),Background=Ui.Tint,Margin=new Thickness(0,0,4,4),Padding=new Thickness(3),ToolTip=phrase};var text=Ui.Text(phrase.Substring(5,phrase.IndexOf('\n')-5)+"\n"+phrase.Substring(phrase.IndexOf('，')+1),10);text.TextAlignment=TextAlignment.Center;chip.Child=text;chips.Children.Add(chip);}guide.Children.Add(chips);
   feedback.Text="Audio stays local · not recorded";feedback.FontSize=11;feedback.TextWrapping=TextWrapping.NoWrap;feedback.TextTrimming=TextTrimming.CharacterEllipsis;feedback.ToolTip="Say a complete command, then pause briefly. Uncertain matches are ignored.";guide.Children.Add(feedback);
   var help=Ui.Button("Speech help",delegate{MessageBox.Show(Window.GetWindow(this),availability.Text+"\n\nUse your default microphone in a quiet room. Say ‘Odd, jump’ then pause.\nStop: ‘Odd, stop listening’ / ‘奥德，停止听我说话’.\n\nFor Mandarin, install Chinese basic speech recognition in Windows language options and restart Odd.\n\nCommands control animations, not chat. Nearby speakers may trigger them. Closing the dashboard stops listening.","Voice help");});help.MinHeight=22;help.FontSize=11;help.Padding=new Thickness(6,1,6,1);help.Margin=new Thickness(0);help.HorizontalAlignment=HorizontalAlignment.Left;guide.Children.Add(help);
   language.SelectionChanged+=async delegate{restarts=0;if(listen.IsChecked==true)await Change(true);};
   meterTimer.Interval=TimeSpan.FromMilliseconds(100);meterTimer.Tick+=delegate{int level=(DateTime.UtcNow-lastLevel).TotalSeconds<.8?audioLevel:0;Array.Copy(levels,1,levels,0,levels.Length-1);levels[levels.Length-1]=level;for(int i=0;i<bars.Length;i++)bars[i].Height=2+levels[i]*.18;meterText.Text=level>2?"Input · "+level+"%":"Listening · quiet";status.SetBinding(ToolTipProperty,new System.Windows.Data.Binding("Text"){Source=status});feedback.ToolTip=feedback.Text;};   try{var installed=VoiceCommands.Installed();availability.Text="Detected: "+string.Join(", ",installed.Select(r=>r.Culture.Name))+ (installed.Any(r=>r.Culture.TwoLetterISOLanguageName=="zh")?"":"\nMandarin unavailable: install compatible Chinese speech support. English commands can be used now.");listen.IsEnabled=installed.Length>0;}catch{availability.Text="Windows speech recognition could not be loaded.";listen.IsEnabled=false;}
   listen.Checked+=async delegate{restarts=0;await Change(true);};listen.Unchecked+=async delegate{await Change(false);};

  }
  public static string CommandFor(Mood mood){string[] en={"idle","walk","run","sit","sleep","play","jump","tongue out","lick","smile","scratch wall","wiggle tail"};string[] zh={"休息","走路","跑步","坐下","睡觉","玩一下","跳一下","吐舌头","舔一下","笑一下","挠墙","摇尾巴"};int i=(int)mood;return "Odd, "+en[i]+"\n奥德，"+zh[i];}
  public async void Shutdown(){closed=true;meterTimer.Stop();await Change(false);}
  public void StopListening(){if(listen.IsChecked==true)listen.IsChecked=false;}
  async Task Change(bool start){
   int ticket=++generation;int selectedLanguage=language.SelectedIndex;meterTimer.Stop();meterPanel.Visibility=Visibility.Hidden;audioLevel=0;lastLevel=DateTime.MinValue;Array.Clear(levels,0,levels.Length);if(!closed)status.Text=start?"Starting microphone…":"Stopping microphone…";
   await gate.WaitAsync();
   try{
    bool failed=false;
    try{
    await Task.Run((Action)Release);pet.SetListeningIndicator(false);
    if(ticket!=generation||closed||!start){if(ticket==generation&&!closed)status.Text="Microphone off";return;}
    await Task.Run(delegate{
     foreach(var info in VoiceCommands.Installed().Where(r=>selectedLanguage==2||(selectedLanguage==0?r.Culture.TwoLetterISOLanguageName=="en":r.Culture.TwoLetterISOLanguageName=="zh"))){
      var engine=new SpeechRecognitionEngine(info);engines.Add(engine);engine.LoadGrammar(VoiceCommands.GrammarFor(info));
      engine.InitialSilenceTimeout=TimeSpan.Zero;engine.BabbleTimeout=TimeSpan.Zero;engine.EndSilenceTimeout=TimeSpan.FromMilliseconds(400);engine.EndSilenceTimeoutAmbiguous=TimeSpan.FromMilliseconds(650);
      bool chinese=info.Culture.TwoLetterISOLanguageName=="zh";
      engine.AudioLevelUpdated+=delegate(object s,AudioLevelUpdatedEventArgs e){int level=e.AudioLevel;Post(new Action(delegate{if(closed||ticket!=generation)return;audioLevel=level;lastLevel=DateTime.UtcNow;}));};
      engine.SpeechDetected+=delegate{Post(new Action(delegate{if(closed||ticket!=generation)return;feedback.Text="Speech detected — checking command…";}));};
      engine.SpeechRecognitionRejected+=delegate{Post(new Action(delegate{if(closed||ticket!=generation||(DateTime.UtcNow-lastAction).TotalSeconds<1)return;feedback.Text="Speech heard, but no command matched. Say ‘Odd, jump’ / ‘奥德，跳一下’ in the selected language.";}));};
      engine.SpeechRecognized+=delegate(object s,SpeechRecognizedEventArgs e){string action;bool accepted=VoiceCommands.Resolve(e.Result.Text,e.Result.Confidence,chinese,out action);string heard=e.Result.Text;float confidence=e.Result.Confidence;
       Post(new Action(delegate{if(closed||ticket!=generation||listen.IsChecked!=true)return;if(!accepted){if((DateTime.UtcNow-lastAction).TotalSeconds>=1)feedback.Text="Uncertain: “"+heard+"” ("+(int)(confidence*100)+"%). Please repeat clearly.";return;}if(action=="Off"){StopListening();return;}if(SuppressDuplicate(lastEngine,info.Id,lastCommand,action,(DateTime.UtcNow-lastAction).TotalSeconds))return;lastAction=DateTime.UtcNow;lastEngine=info.Id;lastCommand=action;restarts=0;Mood mood;if(!Enum.TryParse<Mood>(action,out mood))return;pet.Paused=false;pet.SetMood(mood,true);pet.Say("Heard: "+heard,3);feedback.Text="Heard: "+heard+" → "+action;status.Text="Listening • ready for the next command";}));};
      engine.RecognizeCompleted+=delegate(object s,RecognizeCompletedEventArgs e){Post(new Action(async delegate{if(closed||ticket!=generation||listen.IsChecked!=true)return;if(e.Error==null&&!e.Cancelled&&restarts<2){restarts++;feedback.Text="Recognition ended; reconnecting…";await Change(true);}else{StopListening();feedback.Text="Recognition stopped. Check the microphone and try listening again.";}}));};
      engine.SetInputToDefaultAudioDevice();engine.RecognizeAsync(RecognizeMode.Multiple);
     }
     if(engines.Count==0)throw new InvalidOperationException("No recogniser installed");
    });
    if(ticket==generation&&!closed){status.Text="Listening • "+string.Join(" + ",engines.Select(e=>e.RecognizerInfo.Culture.Name));pet.SetListeningIndicator(true);meterPanel.Visibility=Visibility.Visible;meterTimer.Start();}else await Task.Run((Action)Release);
    }catch(Exception){failed=true;}
    if(failed){await Task.Run((Action)Release);pet.SetListeningIndicator(false);if(ticket==generation&&!closed){listen.IsChecked=false;status.Text="Microphone unavailable. Check your default microphone and Windows microphone permission for desktop apps.";}}
   }
   finally{gate.Release();}
  }
  void Post(Action action){if(Dispatcher.HasShutdownStarted)return;try{Dispatcher.BeginInvoke(action);}catch(InvalidOperationException){}}
  public static bool SuppressDuplicate(string previousEngine,string engine,string previousCommand,string command,double seconds){return previousEngine!=engine&&previousCommand==command&&seconds>=0&&seconds<.7;}
  void Release(){foreach(var engine in engines){try{engine.RecognizeAsyncCancel();}catch{}try{engine.Dispose();}catch{}}engines.Clear();}
 }
}



