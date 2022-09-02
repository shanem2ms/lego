using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using System.Reflection;
using System.Xml;
using System.IO.Packaging;
using System.ComponentModel;
using ICSharpCode.AvalonEdit.Search;

namespace partmake
{
    public class ScriptTextEditor : TextEditor, INotifyPropertyChanged
    {
        CompletionWindow completionWindow;

        public event PropertyChangedEventHandler PropertyChanged;

        LayoutWindow parentWnd;
        public string ScriptName { get; set; }

        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set
            {
                SetValue(FilePathProperty, value);
            }
        }
        public ScriptEngine Engine
        {
            get => (ScriptEngine)GetValue(EngineProperty);
            set
            {
                SetValue(EngineProperty, value);
            }
        }

        private static void OnFilePathChangedCallBack(
        DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ScriptTextEditor c = sender as ScriptTextEditor;
            if (c != null)
            {
                c.OnFilePathChanged();
            }
        }

        private static void OnEngineChangedCallBack(
        DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ScriptTextEditor c = sender as ScriptTextEditor;
            if (c != null)
            {
                c.OnEngineChanged();
            }
        }

        public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
            "FilePath", typeof(string), typeof(ScriptTextEditor),
            new PropertyMetadata(OnFilePathChangedCallBack));
        public static readonly DependencyProperty EngineProperty = DependencyProperty.Register(
            "Engine", typeof(ScriptEngine), typeof(ScriptTextEditor),
            new PropertyMetadata(OnEngineChangedCallBack));

        public ScriptTextEditor()
        {                        
            this.FontFamily = new FontFamily("Consolas");
            this.FontSize = 14;
            this.Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));
            this.ShowLineNumbers = true;
            this.Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));

            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("partmake.CSharp-Mode.xshd"))
            {
                using (XmlTextReader reader = new XmlTextReader(s))
                {
                    this.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }

            // in the constructor:
            this.TextArea.TextEntering += TextArea_TextEntering;
            this.TextArea.TextEntered += TextArea_TextEntered;
            //Reload();
            SearchPanel.Install(this);
        }

        protected virtual void OnFilePathChanged()
        {
            Reload();
        }
        protected virtual void OnEngineChanged()
        {
            this.parentWnd = this.FindParent<LayoutWindow>();
            this.parentWnd.BeforeScriptRun += ParentWnd_BeforeScriptRun;
            if (Engine != null)
            {
                
            }
        }

        private void ParentWnd_BeforeScriptRun(object sender, bool e)
        {
            this.Save();
        }

        void Reload()
        {
            ScriptName = Path.GetFileName(FilePath);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ScriptName"));
            Load(this.FilePath);
        }
        public void SaveAs(string path)
        {
            this.FilePath = path;
            ScriptName = Path.GetFileName(path);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ScriptName"));
            Save(path);
        }
        public void Save()
        {
            Save(this.FilePath);
        }

        private void TextArea_TextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            char[] symbolTermChars = new char[] { ' ', '\t', '{', '(' };
            if (e.Text == ".")
            {
                int spaceOffset = this.Text.LastIndexOfAny(symbolTermChars, this.CaretOffset);
                int len = this.CaretOffset - spaceOffset - 2;
                if (len <= 0)
                    return;
                string word = this.Text.Substring(spaceOffset + 1, len);
                List<string> members = Engine.CodeComplete(this.Text, spaceOffset + 1, word, ScriptEngine.CodeCompleteType.Member);
                completionWindow = new CompletionWindow(this.TextArea);
                if (members != null)
                {
                    IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                    foreach (var member in members)
                    {
                        data.Add(new MyCompletionData(member, 0));
                    }
                    completionWindow.Show();
                    completionWindow.Closed += delegate
                    {
                        completionWindow = null;
                    };
                }
            }
            else if (e.Text == "(")
            {
                int spaceOffset = this.Text.LastIndexOfAny(symbolTermChars, this.CaretOffset);
                int len = this.CaretOffset - spaceOffset - 2;
                if (len <= 0)
                    return;
                string word = this.Text.Substring(spaceOffset + 1, len);
                List<string> members = Engine.CodeComplete(this.Text, spaceOffset + 1, word, ScriptEngine.CodeCompleteType.Function);
                completionWindow = new CompletionWindow(this.TextArea);
                if (members != null)
                {
                    IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                    foreach (var member in members)
                    {
                        data.Add(new MyCompletionData(member, member.IndexOf('(') + 1));
                    }
                    completionWindow.Show();
                    completionWindow.Closed += delegate
                    {
                        completionWindow = null;
                    };
                }
            }
        }

        private void TextArea_TextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }

        class ErrorColorizer : DocumentColorizingTransformer
        {
            public ErrorColorizer()
            {
            }


            protected override void ColorizeLine(ICSharpCode.AvalonEdit.Document.DocumentLine line)
            {
            }

            void ApplyChanges(VisualLineElement element)
            {

                // Create an underline text decoration. Default is underline.
                TextDecoration myUnderline = new TextDecoration();
                Brush wavyBrush = (Brush)System.Windows.Application.Current.Resources["WavyBrush"];

                // Create a linear gradient pen for the text decoration.
                Pen myPen = new Pen();
                myPen.Brush = wavyBrush;
                myPen.Thickness = 6;
                myUnderline.Pen = myPen;
                myUnderline.PenThicknessUnit = TextDecorationUnit.FontRecommended;

                // apply changes here
                element.TextRunProperties.SetTextDecorations(new TextDecorationCollection() { myUnderline });

            }
        }

        public class MyCompletionData : ICompletionData
        {
            int startReplaceIdx = 0;
            public MyCompletionData(string text, int _startReplaceIdx)
            {
                this.Text = text;
                this.startReplaceIdx = _startReplaceIdx;
            }

            public System.Windows.Media.ImageSource Image
            {
                get { return null; }
            }

            public string Text { get; private set; }

            // Use this property if you want to show a fancy UIElement in the list.
            public object Content
            {
                get { return this.Text; }
            }

            public object Description
            {
                get { return "Description for " + this.Text; }
            }

            public double Priority => 1.0;

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                textArea.Document.Replace(completionSegment, this.Text.Substring(startReplaceIdx));
            }
        }
    }
}