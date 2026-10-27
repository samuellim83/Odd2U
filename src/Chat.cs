using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;

namespace Odd2U {
 public sealed class ChatMessage { public string Role,Text; public bool Complete=true; }
 public sealed class Conversation {
  public string Id=Guid.NewGuid().ToString("N"),Title="New conversation";
  public List<ChatMessage> Messages=new List<ChatMessage>();
  public override string ToString(){return Title;}
 }
 public sealed class ChatStore {
  readonly string root;
  public ChatStore(string directory){root=directory;Directory.CreateDirectory(root);}
  public List<Conversation> Load(out int skipped){
   var list=new List<Conversation>();skipped=0;
   foreach(var path in Directory.GetFiles(root,"*.xml").OrderByDescending(File.GetLastWriteTimeUtc))try{
    var x=XElement.Load(path);var c=new Conversation{Id=Path.GetFileNameWithoutExtension(path),Title=(string)x.Attribute("title")??"Conversation"};
    foreach(var m in x.Elements("message")){string role=(string)m.Attribute("role");if(role!="user"&&role!="assistant")continue;c.Messages.Add(new ChatMessage{Role=role,Text=m.Value,Complete=(bool?)m.Attribute("complete")??true});}list.Add(c);
   }catch{skipped++;}return list;
  }
  string PathFor(Conversation c){Guid id;if(!Guid.TryParseExact(c.Id,"N",out id))throw new InvalidDataException("Invalid conversation identifier");return Path.Combine(root,c.Id+".xml");}
  public void Save(Conversation c){
   string path=PathFor(c),temp=path+".tmp";
   try{new XElement("conversation",new XAttribute("title",c.Title),new XAttribute("updatedUtc",DateTime.UtcNow.ToString("o")),c.Messages.Select(m=>new XElement("message",new XAttribute("role",m.Role),new XAttribute("complete",m.Complete),m.Text))).Save(temp);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public void Delete(Conversation c){File.Delete(PathFor(c));}
  public void DeleteAll(){foreach(var path in Directory.GetFiles(root,"*.xml"))File.Delete(path);}
 }
 public static class LocalChat {
  public const string Model="qwen3:1.7b";
  public static object[] Context(Conversation c,string profile){
   var selected=new List<ChatMessage>();int remaining=3200;
   for(int i=c.Messages.Count-1;i>=0;i--){var m=c.Messages[i];if(!m.Complete)continue;if(m.Text.Length>remaining)break;selected.Insert(0,m);remaining-=m.Text.Length;}
   while(selected.Count>0&&selected[0].Role=="assistant")selected.RemoveAt(0);
   var result=selected.Select(m=>(object)new {role=m.Role,content=m.Text}).ToList();result.Insert(0,new {role="system",content=profile});return result.ToArray();
  }
  public static async Task Stream(object[] messages,Action<string> chunk,CancellationToken token){
   string endpoint=await BundledAI.Ensure(token);var request=(HttpWebRequest)WebRequest.Create(endpoint+"/api/chat");request.Proxy=null;request.Method="POST";request.ContentType="application/json";
   var json=new JavaScriptSerializer();byte[] data=Encoding.UTF8.GetBytes(json.Serialize(new{model=Model,stream=true,think=false,keep_alive="30s",options=new{num_ctx=2048,num_predict=192,temperature=.3},messages=messages}));
   using(token.Register(request.Abort)){
    using(var stream=await request.GetRequestStreamAsync()){await stream.WriteAsync(data,0,data.Length,token);}
    using(var response=await request.GetResponseAsync())using(var reader=new StreamReader(response.GetResponseStream())){
     bool done=false;string line;
     while((line=await reader.ReadLineAsync())!=null){token.ThrowIfCancellationRequested();if(line.Length==0)continue;
      var item=json.Deserialize<Dictionary<string,object>>(line);if(item.ContainsKey("error"))throw new InvalidDataException("Ollama returned an error.");
      object value;if(item.TryGetValue("message",out value)){var m=value as Dictionary<string,object>;if(m!=null&&m.TryGetValue("content",out value)&&value is string)chunk((string)value);}
      if(item.TryGetValue("done",out value)&&value is bool&&(bool)value){done=true;break;}
     }if(!done)throw new IOException("The reply ended unexpectedly.");
    }
   }
  }
 }
 public sealed class ChatWindow:UserControl {
  readonly PetWindow pet;readonly ChatStore store;readonly ListBox history=new ListBox();readonly StackPanel transcript=new StackPanel();readonly ScrollViewer transcriptScroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};TextBlock liveText;readonly TextBox input=new TextBox();readonly TextBlock status=new TextBlock();readonly CheckBox enabled=new CheckBox{Content="Enable local AI",IsChecked=false};
  readonly Button send=new Button{Content="Send"},stop=new Button{Content="Stop"};
  Conversation current;CancellationTokenSource active;int generation;bool busy,closed;string partial="";ChatMessage pending;
  const string DefaultProfile="You are Odd, a calm, practical assistant with occasional subtle cat-like touches. Answer every part of the user's request. Be concise, honest about uncertainty, and use the user's language. You cannot see the desktop, access files or perform computer actions. Do not claim otherwise. Avoid adding meows or emoji to every reply. Keep replies under 80 words unless asked for more.";
  string Profile(){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"odd-personality.txt");return File.Exists(path)?File.ReadAllText(path):DefaultProfile;}
  public ChatWindow(PetWindow owner){
   pet=owner;FontSize=15;Foreground=Ui.Ink;
   store=new ChatStore(Path.Combine(AppData.Root,"Chats"));
   var host=new Grid();Content=host;var root=new DockPanel();host.Children.Add(root);
   var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);var title=new DockPanel();enabled.Content="Local AI";DockPanel.SetDock(enabled,Dock.Right);title.Children.Add(enabled);title.Children.Add(Ui.Heading("Chat with Odd",21));top.Children.Add(title);
   var historyPanel=new Border{Background=Ui.Brush("#EDF3F8"),CornerRadius=new CornerRadius(14),Padding=new Thickness(16),Visibility=Visibility.Collapsed};
   var tools=new StackPanel{Orientation=Orientation.Horizontal};tools.Children.Add(Ui.Button("＋ New",NewChat));tools.Children.Add(Ui.Button("History",delegate{historyPanel.Visibility=Visibility.Visible;}));top.Children.Add(tools);status.FontSize=12;status.Foreground=Ui.Muted;status.Text="Turn on Local AI to chat.";status.Margin=new Thickness(0,0,0,6);status.TextTrimming=TextTrimming.CharacterEllipsis;status.ToolTip="Qwen3 1.7B · Ollama · history saved on this PC";top.Children.Add(status);
   var entry=new StackPanel();DockPanel.SetDock(entry,Dock.Bottom);root.Children.Add(entry);input.Height=50;input.Margin=new Thickness(0,8,0,6);input.AcceptsReturn=true;input.TextWrapping=TextWrapping.Wrap;input.MaxLength=2000;input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;System.Windows.Automation.AutomationProperties.SetName(input,"Message to Odd");entry.Children.Add(input);
   var buttons=new DockPanel{LastChildFill=false};entry.Children.Add(buttons);send.Background=Ui.Brush("#142844");send.Foreground=Brushes.White;send.Content="Send ↗";stop.Content="Stop";DockPanel.SetDock(send,Dock.Right);DockPanel.SetDock(stop,Dock.Right);buttons.Children.Add(send);buttons.Children.Add(stop);var hint=Ui.Text("Ctrl + Enter",11,Ui.Muted);hint.VerticalAlignment=VerticalAlignment.Center;buttons.Children.Add(hint);send.IsEnabled=false;stop.IsEnabled=false;
   input.PreviewKeyDown+=async delegate(object s,System.Windows.Input.KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Enter&&(System.Windows.Input.Keyboard.Modifiers&System.Windows.Input.ModifierKeys.Control)!=0){e.Handled=true;await Send();}};
   transcriptScroll.Content=transcript;root.Children.Add(transcriptScroll);
   var historyContent=new DockPanel();historyPanel.Child=historyContent;var historyTop=new DockPanel();DockPanel.SetDock(historyTop,Dock.Top);historyContent.Children.Add(historyTop);var close=Ui.Button("Done",delegate{historyPanel.Visibility=Visibility.Collapsed;});DockPanel.SetDock(close,Dock.Right);historyTop.Children.Add(close);historyTop.Children.Add(Ui.Heading("Your chats",20));var deletion=new System.Windows.Controls.Primitives.UniformGrid{Columns=2};DockPanel.SetDock(deletion,Dock.Bottom);historyContent.Children.Add(deletion);deletion.Children.Add(ActionButton("Delete selected",DeleteCurrent));deletion.Children.Add(ActionButton("Clear all",DeleteAll));historyContent.Children.Add(history);host.Children.Add(historyPanel);
   history.ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}' TextTrimming='CharacterEllipsis' ToolTip='{Binding}'/></DataTemplate>");   enabled.Checked+=delegate{status.Text="Ready. First reply may take a few seconds to load.";send.IsEnabled=!busy;};enabled.Unchecked+=delegate{Cancel();BundledAI.Stop();status.Text="AI off. Pet animations continue normally.";send.IsEnabled=false;};
   send.Click+=async delegate{await Send();};stop.Click+=delegate{Cancel();status.Text="Stopped. Partial reply marked incomplete.";};
   history.SelectionChanged+=delegate{var c=history.SelectedItem as Conversation;if(c!=null&&c!=current){Cancel();current=c;Render();historyPanel.Visibility=Visibility.Collapsed;}};
   int skipped;foreach(var c in store.Load(out skipped))history.Items.Add(c);NewChat();if(skipped>0)status.Text="Some history files could not be read. Delete all history can remove them.";

  }
  public string Summary {get{return busy?"Odd is replying…":enabled.IsChecked==true?"Chat ready":"Chat off";}}
  public void Suspend(){Cancel();if(enabled.IsChecked==true)status.Text="Ready when you are.";}
  public void Shutdown(){Cancel();closed=true;BundledAI.Stop();}
  static Button ActionButton(string title,Action action){return Ui.Button(title,action);}
  void Save(){try{store.Save(current);}catch(Exception){status.Text="Could not save history. Check disk space and folder access.";}}
  TextBlock AddBubble(string speaker,string text,bool user){var content=new StackPanel();var label=Ui.Text(speaker,11,Ui.Muted);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(0,0,0,6);content.Children.Add(label);var body=Ui.Text(text,15);content.Children.Add(body);var card=Ui.Card(content,user?Ui.Tint:Ui.Brush("#F3F4EF"));card.Padding=new Thickness(14);card.Margin=new Thickness(user?30:0,0,user?0:30,12);transcript.Children.Add(card);return body;}
  void Render(){transcript.Children.Clear();liveText=null;if(current.Messages.Count==0){var empty=Ui.Section("What's on your mind?","Ask a question, plan your day, or just say hello.");empty.Margin=new Thickness(10,12,10,10);transcript.Children.Add(empty);}foreach(var m in current.Messages)AddBubble((m.Role=="user"?"YOU":"ODD")+(m.Complete?"":" · incomplete"),m.Text,m.Role=="user");if(busy)liveText=AddBubble("ODD",partial.Length==0?"Thinking…":partial,false);transcriptScroll.ScrollToEnd();}
  void NewChat(){Cancel();current=new Conversation();history.SelectedItem=null;input.Clear();Render();}
  void Cancel(){generation++;if(active!=null)active.Cancel();if(busy&&pending!=null){pending.Text=partial;pending.Complete=false;if(partial.Length>0)current.Messages.Add(pending);Save();}pending=null;partial="";busy=false;stop.IsEnabled=false;send.IsEnabled=enabled.IsChecked==true;pet.ClearBubble();if(current!=null)Render();}
  void DeleteCurrent(){if(current==null||MessageBox.Show(Window.GetWindow(this),"Delete this conversation from this PC?","Delete conversation",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;Cancel();try{store.Delete(current);history.Items.Remove(current);NewChat();status.Text="Conversation deleted.";}catch{status.Text="Could not delete this conversation.";}}
  void DeleteAll(){if(MessageBox.Show(Window.GetWindow(this),"Permanently delete all Odd chat history on this PC?","Delete all history",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;Cancel();try{store.DeleteAll();history.Items.Clear();NewChat();status.Text="All chat history deleted.";}catch{status.Text="Some history could not be deleted. Please try again.";}}
  async Task Send(){
   string text=input.Text.Trim();if(busy||enabled.IsChecked!=true||text.Length==0)return;
   current.Messages.Add(new ChatMessage{Role="user",Text=text});if(current.Messages.Count==1){current.Title=text.Substring(0,Math.Min(36,text.Length));history.Items.Insert(0,current);}Save();input.Clear();
   busy=true;send.IsEnabled=false;stop.IsEnabled=true;partial="";pending=new ChatMessage{Role="assistant",Complete=false};int ticket=++generation;var cancel=new CancellationTokenSource();active=cancel;cancel.CancelAfter(TimeSpan.FromSeconds(90));status.Text="Loading / thinking…";Render();
   try{
    var context=LocalChat.Context(current,Profile());
    await LocalChat.Stream(context,delegate(string delta){if(closed||ticket!=generation)return;partial+=delta;status.Text="Replying…";if(liveText!=null)liveText.Text=partial;transcriptScroll.ScrollToEnd();pet.Say(partial.Length>200?partial.Substring(0,200)+"… Open chat for more.":partial,15);},cancel.Token);
    if(closed||ticket!=generation)return;if(partial.Length==0)throw new IOException("Empty reply");pending.Text=partial;pending.Complete=true;current.Messages.Add(pending);pending=null;busy=false;status.Text="Ready • saved locally";Save();Render();
   }catch(Exception){if(!closed&&ticket==generation){Cancel();status.Text=cancel.IsCancellationRequested?"Reply timed out. Try a shorter message.":"Ollama unavailable or reply failed. Open Ollama and check qwen3:1.7b is installed.";}}
   finally{if(active==cancel)active=null;cancel.Dispose();if(!closed&&ticket==generation){busy=false;send.IsEnabled=enabled.IsChecked==true;stop.IsEnabled=false;}}
  }
 }
}




