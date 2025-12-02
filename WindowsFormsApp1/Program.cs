using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace TextFileViewer
{
    using System;
    using System.Reflection;

    // Observer Pattern для відстеження подій редагування
    public interface ITextObserver
    {
        void Update(TextEvent textEvent);
    }

    public enum TextEventType
    {
        IntegerDetected,
        ParagraphAdded,
        AutoSaved
    }
    //клас для івенту 2 частини ідз
    public class TextEvent
    {
        public TextEventType Type { get; set; }
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }

        public TextEvent(TextEventType type, string data)
        {
            Type = type;
            Data = data;
            Timestamp = DateTime.Now;
        }
    }

    // Паблішер
    public class TextEditorSubject
    {
        private List<ITextObserver> observers = new List<ITextObserver>();

        public void Attach(ITextObserver observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
        }

        public void Detach(ITextObserver observer)
        {
            observers.Remove(observer);
        }

        public void Notify(TextEvent textEvent)
        {
            foreach (var observer in observers)
            {
                observer.Update(textEvent);
            }
        }
    }
    public class IntegerDetectorObserver : ITextObserver
    {
        private Form parentForm;

        public IntegerDetectorObserver(Form parent)
        {
            parentForm = parent;
        }

        public void Update(TextEvent textEvent)
        {
            if (textEvent.Type == TextEventType.IntegerDetected)
            {
                if (parentForm.InvokeRequired)
                {
                    parentForm.Invoke(new Action(() => ShowMessage(textEvent.Data)));
                }
                else
                {
                    ShowMessage(textEvent.Data);
                }
            }
        }

        private void ShowMessage(string number)
        {
            MessageBox.Show($"Виявлено нове ціле число: {number}",
                "Integer Detector",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    public class AutoSaveObserver : ITextObserver
    {
        private Form1 editorForm;
        private Form parentForm;

        public AutoSaveObserver(Form1 editor, Form parent)
        {
            editorForm = editor;
            parentForm = parent;
        }

        public async void Update(TextEvent textEvent)
        {
            if (textEvent.Type == TextEventType.ParagraphAdded)
            {
                await Task.Run(() => PerformAutoSave());
            }
            else if (textEvent.Type == TextEventType.AutoSaved)
            {
                if (parentForm.InvokeRequired)
                {
                    parentForm.Invoke(new Action(() => ShowSaveMessage()));
                }
                else
                {
                    ShowSaveMessage();
                }
            }
        }

        private void PerformAutoSave()
        {
            if (parentForm.InvokeRequired)
            {
                parentForm.Invoke(new Action(() => editorForm.PerformAutoSave()));
            }
            else
            {
                editorForm.PerformAutoSave();
            }
        }

        private void ShowSaveMessage()
        {
            MessageBox.Show("Дані у файлі оновлено (автозбереження)",
                "Auto Save",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }




    // Базові інтерфейси для завантажувачів та зберігачів
    public interface IFileLoader
    {
        string Load(string filePath);
    }

    public interface IFileSaver
    {
        void Save(string filePath, string content);
    }

    // Абстрактна фабрика
    public abstract class FileFactory
    {
        public abstract IFileLoader CreateLoader();
        public abstract IFileSaver CreateSaver();
    }

    public class TXTFactory : FileFactory
    {
        public override IFileLoader CreateLoader()
        {
            return new TXTLoader();
        }

        public override IFileSaver CreateSaver()
        {
            return new TXTSaver();
        }
    }
    public class TXTLoader : IFileLoader
    {
        public string Load(string filePath)
        {
            StringBuilder content = new StringBuilder();

            using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    content.Append(line + Environment.NewLine);
                }
            }

            return content.ToString();
        }
    }

    public class TXTSaver : IFileSaver
    {
        public void Save(string filePath, string content)
        {
            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
    }


    public class HTMLFactory : FileFactory
    {
        public override IFileLoader CreateLoader()
        {
            return new HTMLLoader();
        }

        public override IFileSaver CreateSaver()
        {
            return new HTMLSaver();
        }
    }

    public class HTMLLoader : IFileLoader
    {
        public string Load(string filePath)
        {
            string htmlContent = File.ReadAllText(filePath, Encoding.UTF8);
            string text = htmlContent;

            // \r\n для <br>
            text = Regex.Replace(text, @"<br\s*/?>", "\r\n", RegexOptions.IgnoreCase);

            // Для абзаців використовуємо подвійний \r\n
            text = Regex.Replace(text, @"</p>", "\r\n\r\n", RegexOptions.IgnoreCase);

            // Видаляємо відкриваючі теги <p>
            text = Regex.Replace(text, @"<p[^>]*>", "", RegexOptions.IgnoreCase);

            // Видаляємо всі інші HTML теги
            text = Regex.Replace(text, @"<[^>]+>", "");

            // Декодуємо HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            //Нормалізація порожніх рядків через \r\n
            text = Regex.Replace(text, @"(\r?\n){3,}", "\r\n\r\n");

            text = text.Trim();

            return text;
        }
    }

    public class HTMLSaver : IFileSaver
    {
        public void Save(string filePath, string content)
        {
            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <title>Document</title>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine();

            string[] paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);

            foreach (string paragraph in paragraphs)
            {
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    // Розбиваємо абзац на рядки
                    string[] lines = paragraph.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    // HTML-кодуємо лише текст, а перенос рядка замінюємо на <br>
                    for (int i = 0; i < lines.Length; i++)
                    {
                        lines[i] = System.Net.WebUtility.HtmlEncode(lines[i]);
                    }

                    string safeParagraph = string.Join("<br>", lines);

                    html.AppendLine($"    <p>{safeParagraph}</p>");
                }
            }

            html.AppendLine();
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(filePath, html.ToString(), Encoding.UTF8);
        }
    }


    public class BINFactory : FileFactory
    {
        public override IFileLoader CreateLoader()
        {
            return new BINLoader();
        }

        public override IFileSaver CreateSaver()
        {
            return new BINSaver();
        }
    }

    public class BINLoader : IFileLoader
    {
        public string Load(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);

            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Якщо не вдалося, повертаємо як hex-представлення
                return BitConverter.ToString(bytes).Replace("-", " ");
            }
        }
    }

    public class BINSaver : IFileSaver
    {
        public void Save(string filePath, string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            File.WriteAllBytes(filePath, bytes);
        }
    }
    public static class FileFactoryManager
    {
        public static FileFactory GetFactory(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            switch (extension)
            {
                case ".html":
                case ".htm":
                    return new HTMLFactory();

                case ".txt":
                case ".cs":
                    return new TXTFactory();

                case ".bin":
                case ".dat":
                    return new BINFactory();

                default:
                    return new TXTFactory();
            }
        }
    }

    public class Singleton<T> where T : class
    {
        private static T instance_;

        protected Singleton() { }

        public static T Instance
        {
            get
            {
                if (instance_ == null)
                {
                    instance_ = CreateInstance();
                }
                return instance_;
            }
        }

        private static T CreateInstance()
        {
            var ctor = typeof(T).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (ctor == null)
                throw new InvalidOperationException($"{typeof(T).Name} має містити приватний або protected конструктор без параметрів.");

            return (T)ctor.Invoke(null);
        }
    }

    public class EventLogger : Singleton<EventLogger>
    {
        //private static EventLogger instance_ = null;

        private EventLogger()
        {
            events_ = new List<LogEvent>();
            Console.WriteLine("[EventLogger] Створено екземпляр логера");
        }


        // Список всіх подій
        private List<LogEvent> events_;

        /// Додає подію до журналу
        public void LogEvent(EventType type, int lineNumber, int charPosition, string text)
        {
            LogEvent logEvent = new LogEvent
            {
                Timestamp = DateTime.Now,
                Type = type,
                LineNumber = lineNumber,
                CharPosition = charPosition,
                Text = text
            };

            events_.Add(logEvent);

        }

        public List<LogEvent> GetAllEvents()
        {
            return new List<LogEvent>(events_);
        }

        /// Отримує останні N подій
        public List<LogEvent> GetRecentEvents(int count)
        {
            int startIndex = Math.Max(0, events_.Count - count);
            return events_.GetRange(startIndex, Math.Min(count, events_.Count));
        }

        /// Отримує події за типом
        public List<LogEvent> GetEventsByType(EventType type)
        {
            List<LogEvent> result = new List<LogEvent>();
            foreach (var evt in events_)
            {
                if (evt.Type == type)
                    result.Add(evt);
            }
            return result;
        }

        public void ClearLog()
        {
            events_.Clear();
            Console.WriteLine("[EventLogger] Журнал очищено");
        }

        public int EventCount => events_.Count;

        public async Task SaveToFile(string filePath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine("ЖУРНАЛ ПОДІЙ ТЕКСТОВОГО РЕДАКТОРА");
            sb.AppendLine("========================================");
            sb.AppendLine();
            sb.AppendLine($"Всього подій: {events_.Count}");
            sb.AppendLine($"Дата створення звіту: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("========================================");
            sb.AppendLine();

            foreach (var evt in events_)
            {
                sb.AppendLine(evt.ToDetailedString());
                sb.AppendLine(new string('-', 60));
            }
            string logContent = sb.ToString();
            await Task.Run(() =>
            {
                File.WriteAllText(filePath, logContent, Encoding.UTF8);
            });
        }

        /// Отримує статистику подій
        public string GetStatistics()
        {
            int addCount = 0;
            int deleteCount = 0;
         

            foreach (var evt in events_)
            {
                switch (evt.Type)
                {
                    case EventType.CharAdded: addCount++; break;
                    case EventType.CharDeleted: deleteCount++; break;
                  
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("========== СТАТИСТИКА ПОДІЙ ==========");
            sb.AppendLine();
            sb.AppendLine($"Всього подій: {events_.Count}");
            sb.AppendLine();
            sb.AppendLine($"Додано символів: {addCount}");
            sb.AppendLine($"Видалено символів: {deleteCount}");
            sb.AppendLine();

            if (events_.Count > 0)
            {
                sb.AppendLine($"Перша подія: {events_[0].Timestamp:HH:mm:ss}");
                sb.AppendLine($"Остання подія: {events_[events_.Count - 1].Timestamp:HH:mm:ss}");
            }

            return sb.ToString();
        }
    }

 
    // Типи подій для протоколювання
    public enum EventType
    {
        CharAdded,      
        CharDeleted,   
    }

    // Клас для зберігання інформації про подію
    public class LogEvent
    {
        public DateTime Timestamp { get; set; }
        public EventType Type { get; set; }
        public int LineNumber { get; set; }
        public int CharPosition { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            string typeStr = GetEventTypeString(Type);
            return $"{Timestamp:HH:mm:ss.fff} | {typeStr} | Рядок:{LineNumber} Поз:{CharPosition} | '{Text}'";
        }

        public string ToDetailedString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Час: {Timestamp:dd.MM.yyyy HH:mm:ss.fff}");
            sb.AppendLine($"Тип події: {GetEventTypeString(Type)}");
            sb.AppendLine($"Позиція: Рядок {LineNumber}, Символ {CharPosition}");
            sb.AppendLine($"Текст: '{Text}'");
            return sb.ToString();
        }

        private string GetEventTypeString(EventType type)
        {
            switch (type)
            {
                case EventType.CharAdded: return "Додано символ";
                case EventType.CharDeleted: return "Видалено символ";
           
                default: return "Невідома подія";
            }
        }
    }

public partial class Form1 : Form
    {
        private TextBox source;
        private OpenFileDialog openFileDialog;
        private SaveFileDialog saveFileDialog;
        private MenuStrip menuStrip;
        private string currentFilePath = "";
        private static readonly HttpClient httpClient = new HttpClient();

        private string previousText = "";

        private TextEditorSubject textSubject;
        private IntegerDetectorObserver integerObserver;
        private AutoSaveObserver autoSaveObserver;
        private string lastProcessedText = "";
        private HashSet<string> detectedIntegers = new HashSet<string>();

        public Form1()
        {
            InitializeComponent();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        private void InitializeComponent()
        {
            this.Text = "Текстовий редактор";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterScreen;

            menuStrip = new MenuStrip();
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Меню File
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");

            ToolStripMenuItem newFile = new ToolStripMenuItem("New");
            newFile.ShortcutKeys = Keys.Control | Keys.N;
            newFile.Click += NewFile_Click;

            ToolStripMenuItem openFile = new ToolStripMenuItem("Open...");
            openFile.ShortcutKeys = Keys.Control | Keys.O;
            openFile.Click += OpenFile_Click;

            ToolStripMenuItem saveFile = new ToolStripMenuItem("Save");
            saveFile.ShortcutKeys = Keys.Control | Keys.S;
            saveFile.Click += SaveFile_Click;

            ToolStripMenuItem saveAsFile = new ToolStripMenuItem("Save As...");
            saveAsFile.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            saveAsFile.Click += SaveAsFile_Click;

            ToolStripSeparator separator1 = new ToolStripSeparator();

            ToolStripMenuItem exitFile = new ToolStripMenuItem("Exit");
            exitFile.ShortcutKeys = Keys.Alt | Keys.F4;
            exitFile.Click += ExitFile_Click;

            fileMenu.DropDownItems.Add(newFile);
            fileMenu.DropDownItems.Add(openFile);
            fileMenu.DropDownItems.Add(saveFile);
            fileMenu.DropDownItems.Add(saveAsFile);
            fileMenu.DropDownItems.Add(separator1);
            fileMenu.DropDownItems.Add(exitFile);

            // Меню Edit
            ToolStripMenuItem editMenu = new ToolStripMenuItem("Edit");

            ToolStripMenuItem undoEdit = new ToolStripMenuItem("Undo");
            undoEdit.ShortcutKeys = Keys.Control | Keys.Z;
            undoEdit.Click += UndoEdit_Click;

            ToolStripSeparator separator2 = new ToolStripSeparator();

            ToolStripMenuItem cutEdit = new ToolStripMenuItem("Cut");
            cutEdit.ShortcutKeys = Keys.Control | Keys.X;
            cutEdit.Click += CutEdit_Click;

            ToolStripMenuItem copyEdit = new ToolStripMenuItem("Copy");
            copyEdit.ShortcutKeys = Keys.Control | Keys.C;
            copyEdit.Click += CopyEdit_Click;

            ToolStripMenuItem pasteEdit = new ToolStripMenuItem("Paste");
            pasteEdit.ShortcutKeys = Keys.Control | Keys.V;
            pasteEdit.Click += PasteEdit_Click;

            ToolStripSeparator separator3 = new ToolStripSeparator();

            ToolStripMenuItem selectAllEdit = new ToolStripMenuItem("Select All");
            selectAllEdit.ShortcutKeys = Keys.Control | Keys.A;
            selectAllEdit.Click += SelectAllEdit_Click;

            ToolStripSeparator separator4 = new ToolStripSeparator();

            ToolStripMenuItem removeSpacesEdit = new ToolStripMenuItem("Remove Extra Spaces (Regex)");
            removeSpacesEdit.Click += RemoveSpacesRegex_Click;

            ToolStripMenuItem replaceTextEdit = new ToolStripMenuItem("Replace Text...");
            replaceTextEdit.ShortcutKeys = Keys.Control | Keys.H;
            replaceTextEdit.Click += ReplaceText_Click;

            editMenu.DropDownItems.Add(undoEdit);
            editMenu.DropDownItems.Add(separator2);
            editMenu.DropDownItems.Add(cutEdit);
            editMenu.DropDownItems.Add(copyEdit);
            editMenu.DropDownItems.Add(pasteEdit);
            editMenu.DropDownItems.Add(separator3);
            editMenu.DropDownItems.Add(selectAllEdit);
            editMenu.DropDownItems.Add(separator4);
            editMenu.DropDownItems.Add(removeSpacesEdit);
            editMenu.DropDownItems.Add(replaceTextEdit);

            // Меню Search (Regex)
            ToolStripMenuItem searchMenu = new ToolStripMenuItem("Search");

            ToolStripMenuItem findPhones = new ToolStripMenuItem("Find Ukrainian Phone Numbers");
            findPhones.Click += FindPhones_Click;

            ToolStripMenuItem findFullName = new ToolStripMenuItem("Find First Full Name");
            findFullName.Click += FindFullName_Click;

            ToolStripMenuItem checkCSharpKeywords = new ToolStripMenuItem("Check C# Keywords");
            checkCSharpKeywords.Click += CheckCSharpKeywords_Click;

            searchMenu.DropDownItems.Add(findPhones);
            searchMenu.DropDownItems.Add(findFullName);
            searchMenu.DropDownItems.Add(checkCSharpKeywords);

            // Меню Statistics
            ToolStripMenuItem statisticsMenu = new ToolStripMenuItem("Statistics");

            ToolStripMenuItem showStats = new ToolStripMenuItem("Show Statistics");
            showStats.ShortcutKeys = Keys.F5;
            showStats.Click += ShowStats_Click;

            ToolStripMenuItem wordFrequency = new ToolStripMenuItem("Word Frequency");
            wordFrequency.Click += WordFrequency_Click;

            statisticsMenu.DropDownItems.Add(showStats);
            statisticsMenu.DropDownItems.Add(wordFrequency);

            // МЕНЮ: Web
            ToolStripMenuItem webMenu = new ToolStripMenuItem("Web");

            ToolStripMenuItem loadNews = new ToolStripMenuItem("Load News from ZNU...");
            loadNews.ShortcutKeys = Keys.Control | Keys.W;
            loadNews.Click += LoadNews_Click;

            webMenu.DropDownItems.Add(loadNews);

            // МЕНЮ: LOG

            ToolStripMenuItem logMenu = new ToolStripMenuItem("Log");

            ToolStripMenuItem showLog = new ToolStripMenuItem("Показати журнал подій");
            showLog.ShortcutKeys = Keys.F6;
            showLog.Click += ShowLog_Click;

            ToolStripMenuItem showLogStats = new ToolStripMenuItem("Статистика подій");
            showLogStats.Click += ShowLogStats_Click;

            ToolStripMenuItem saveLog = new ToolStripMenuItem("Зберегти журнал у файл...");
            saveLog.Click += SaveLog_Click;

            ToolStripMenuItem clearLog = new ToolStripMenuItem("Очистити журнал");
            clearLog.Click += ClearLog_Click;
          
            ToolStripMenuItem quickTest = new ToolStripMenuItem("Швидкий тест Singleton");
            quickTest.ShortcutKeys = Keys.F7;
            quickTest.Click += (s, e) => QuickSingletonTest.ShowQuickTest();
            logMenu.DropDownItems.Add(quickTest);

            ToolStripMenuItem testObserver = new ToolStripMenuItem("Тест Observer Pattern");
            testObserver.ShortcutKeys = Keys.F8;
            testObserver.Click += TestObserver_Click;
            logMenu.DropDownItems.Add(testObserver);

            logMenu.DropDownItems.Add(showLog);
            logMenu.DropDownItems.Add(showLogStats);
            logMenu.DropDownItems.Add(new ToolStripSeparator());
            logMenu.DropDownItems.Add(saveLog);
            logMenu.DropDownItems.Add(clearLog);

            menuStrip.Items.Add(logMenu);

            // Меню About
            ToolStripMenuItem aboutMenu = new ToolStripMenuItem("About");
            aboutMenu.Click += AboutMenu_Click;

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(editMenu);
            menuStrip.Items.Add(searchMenu);
            menuStrip.Items.Add(statisticsMenu);
            menuStrip.Items.Add(webMenu);
            menuStrip.Items.Add(aboutMenu);


            source = new TextBox();
            source.Location = new Point(0, menuStrip.Height);
            source.Size = new Size(this.ClientSize.Width, this.ClientSize.Height - menuStrip.Height);
            source.Multiline = true;
            source.ScrollBars = ScrollBars.Both;
            source.Font = new Font("Consolas", 10);
            source.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            source.AcceptsTab = true;
            source.WordWrap = false;
            this.Controls.Add(source);

            source.TextChanged += Source_TextChanged;
            source.KeyDown += Source_NewEnter;
            var logger = EventLogger.Instance;
            Console.WriteLine("EventLogger ініціалізовано");

            // Ініціалізація Observerа
            textSubject = new TextEditorSubject();
            integerObserver = new IntegerDetectorObserver(this);
            autoSaveObserver = new AutoSaveObserver(this, this);

            textSubject.Attach(integerObserver);
            textSubject.Attach(autoSaveObserver);

            Console.WriteLine("Observer Pattern ініціалізовано");

            openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All supported|*.txt;*.html;*.htm;*.bin;*.dat;*.cs|Text files|*.txt|HTML files|*.html;*.htm|Binary files|*.bin;*.dat|C# files|*.cs|All files|*.*";

            saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files|*.txt|HTML files|*.html|Binary files|*.bin|C# files|*.cs|All files|*.*";
        }


        private async void Source_TextChanged(object sender, EventArgs e)
        {
            string currentText = source.Text;
            string oldText = previousText;

            previousText = currentText;

            if (currentText == oldText) return;

            await Task.Run(() =>
            {
                ProcessTextChange(oldText, currentText);
                CheckForIntegers(currentText);
            });
        }
        private void Source_NewEnter(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var textEvent = new TextEvent(TextEventType.ParagraphAdded,
                        $"Додано абзац");
                textSubject.Notify(textEvent);
            }
        }
        private void CheckForIntegers(string text)
        {
            try
            {
                // Регулярний вираз для виявлення цілих чисел
                string pattern = @"(?:^|[\s\p{P}])(-?\d+)(?=[\s\p{P}]|$)";
                MatchCollection matches = Regex.Matches(text, pattern);

                foreach (Match match in matches)
                {
                    string number = match.Groups[1].Value;

                    if (!detectedIntegers.Contains(number))
                    {
                        detectedIntegers.Add(number);

                        var textEvent = new TextEvent(TextEventType.IntegerDetected, number);
                        textSubject.Notify(textEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка виявлення чисел: {ex.Message}");
            }
        }

        //private void CheckForNewParagraph(string oldText, string newText)
        //{
        //    try
        //    {
        //        // Виявляємо додавання нового абзацу (подвійний Enter)
        //        int oldParagraphs = Regex.Matches(oldText, @"\r?\n\r?\n").Count;
        //        int newParagraphs = Regex.Matches(newText, @"\r?\n\r?\n").Count;

        //        if (newParagraphs > oldParagraphs)
        //        {
        //            var textEvent = new TextEvent(TextEventType.ParagraphAdded,
        //                $"Додано {newParagraphs - oldParagraphs} абзац(ів)");
        //            textSubject.Notify(textEvent);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Помилка виявлення абзаців: {ex.Message}");
        //    }
        //}

        public void PerformAutoSave()
        {
            try
            {
                if (!string.IsNullOrEmpty(currentFilePath))
                {
                    FileFactory factory = FileFactoryManager.GetFactory(currentFilePath);
                    IFileSaver saver = factory.CreateSaver();

                    string textToSave = "";
                    if (source.InvokeRequired)
                    {
                        source.Invoke(new Action(() => textToSave = source.Text));
                    }
                    else
                    {
                        textToSave = source.Text;
                    }

                    saver.Save(currentFilePath, textToSave);

                    var textEvent = new TextEvent(TextEventType.AutoSaved, currentFilePath);
                    textSubject.Notify(textEvent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка автозбереження: {ex.Message}");
            }
        }

        private void ProcessTextChange(string oldText, string currentText)
        {
            try
            {
                if (currentText.Length > oldText.Length)
                {
                    // nекст додано 
                    int diffLength = currentText.Length - oldText.Length;
                    int changePos = FindChangePosition(oldText, currentText);

                    if (changePos >= 0 && changePos + diffLength <= currentText.Length)
                    {
                        string addedText = currentText.Substring(changePos, diffLength);

                        var (currentLine, currentChar) = GetLineAndCharPosition(currentText, changePos);

                        foreach (char ch in addedText)
                        {
                            EventLogger.Instance.LogEvent(
                                EventType.CharAdded,
                                currentLine,
                                currentChar,
                                ch.ToString()
                            );

                            if (ch == '\n')
                            {
                                currentLine++;
                                currentChar = 1;
                            }
                            else
                            {
                                currentChar++;
                            }
                        }
                    }
                }
                else if (currentText.Length < oldText.Length)
                {
                    // Текст видалено 
                    int diffLength = oldText.Length - currentText.Length;
                    int changePos = FindChangePosition(currentText, oldText);

                    if (changePos >= 0 && changePos + diffLength <= oldText.Length)
                    {
                        string deletedText = oldText.Substring(changePos, diffLength);

                        var (currentLine, currentChar) = GetLineAndCharPosition(oldText, changePos);

                        foreach (char ch in deletedText)
                        {
                            EventLogger.Instance.LogEvent(
                                EventType.CharDeleted,
                                currentLine,
                                currentChar,
                                ch.ToString()
                            );

                            if (ch == '\n')
                            {
                                currentLine++;
                                currentChar = 1;
                            }
                            else
                            {
                                currentChar++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка фонового логування: {ex.Message}");
            }
        }

        private int FindChangePosition(string oldText, string newText)
        {
            int minLength = Math.Min(oldText.Length, newText.Length);
           
            for (int i = 0; i < minLength; i++)
            {
                if (oldText[i] != newText[i])
                    return i;
            }
            return minLength;
        }

        private (int Line, int Char) GetLineAndCharPosition(string text, int absolutePosition)
        {
            if (string.IsNullOrEmpty(text) || absolutePosition < 0)
                return (1, 1);

            int line = 1;
            int charPos = 1;

            int limit = Math.Min(absolutePosition, text.Length);

            for (int i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    charPos = 1;
                }
                else
                {
                    charPos++;
                }
            }

            return (line, charPos);
        }


        // File Menu

        private void OpenFile_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    currentFilePath = openFileDialog.FileName;

                  

                    // Отримуємо відповідну фабрику на основі розширення файлу
                    FileFactory factory = FileFactoryManager.GetFactory(currentFilePath);

                    IFileLoader loader = factory.CreateLoader();

                  
                    string content = loader.Load(currentFilePath);

                    source.Text = content;

                    previousText = content;

                  

                    string extension = Path.GetExtension(currentFilePath).ToUpper();
                    this.Text = $"Текстовий редактор - {Path.GetFileName(currentFilePath)} [{extension}]";

                    MessageBox.Show($"Файл успішно завантажено через {factory.GetType().Name}!",
                        "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {

                    MessageBox.Show("Помилка читання файлу: " + ex.Message,
                        "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SaveFile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                SaveAsFile_Click(sender, e);
            }
            else
            {
                try
                {
                    // Отримуємо відповідну фабрику
                    FileFactory factory = FileFactoryManager.GetFactory(currentFilePath);

                
                    IFileSaver saver = factory.CreateSaver();

                    saver.Save(currentFilePath, source.Text);

                    MessageBox.Show($"Файл збережено через {factory.GetType().Name}!",
                        "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Помилка збереження: " + ex.Message,
                        "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void NewFile_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(source.Text))
            {
                DialogResult result = MessageBox.Show(
                    "Зберегти поточний файл?",
                    "Новий файл",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    SaveFile_Click(sender, e);
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
            }


            source.Clear();
            previousText = "";


            currentFilePath = "";
            this.Text = "Текстовий редактор - Новий документ";
        }
        private void ShowLog_Click(object sender, EventArgs e)
        {
            var events = EventLogger.Instance.GetAllEvents();

            if (events.Count == 0)
            {
                MessageBox.Show("Журнал подій порожній!", "Журнал подій",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Form logForm = new Form();
            logForm.Text = $"Журнал подій ({events.Count} записів)";
            logForm.Size = new Size(800, 600);
            logForm.StartPosition = FormStartPosition.CenterParent;

            TextBox logBox = new TextBox();
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Both;
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.Font = new Font("Consolas", 9);
            logBox.WordWrap = false;

            StringBuilder logText = new StringBuilder();
            logText.AppendLine("========== ЖУРНАЛ ПОДІЙ РЕДАКТОРА ==========");
            logText.AppendLine($"Всього подій: {events.Count}");
            logText.AppendLine();

            // останні 500 подій
            var recentEvents = events.Count > 500
                ? events.GetRange(events.Count - 500, 500)
                : events;

            foreach (var evt in recentEvents)
            {
                logText.AppendLine(evt.ToString());
            }

            if (events.Count > 500)
            {
                logText.AppendLine();
                logText.AppendLine($"(Показано останні 500 з {events.Count} подій)");
            }

            logBox.Text = logText.ToString();
            logForm.Controls.Add(logBox);
            logForm.ShowDialog();
        }

        private void ShowLogStats_Click(object sender, EventArgs e)
        {
            string stats = EventLogger.Instance.GetStatistics();

            MessageBox.Show(stats, "Статистика подій",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveLog_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveLogDialog = new SaveFileDialog();
            saveLogDialog.Filter = "Text files|*.txt|All files|*.*";
            saveLogDialog.FileName = $"EventLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

            if (saveLogDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    EventLogger.Instance.SaveToFile(saveLogDialog.FileName);
                    MessageBox.Show("Журнал подій збережено!", "Успіх",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Помилка збереження журналу: {ex.Message}",
                        "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ClearLog_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Ви впевнені, що хочете очистити весь журнал подій?",
                "Підтвердження",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                EventLogger.Instance.ClearLog();
                MessageBox.Show("Журнал подій очищено!", "Успіх",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SaveAsFile_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    currentFilePath = saveFileDialog.FileName;

                    // Отримуємо відповідну фабрику
                    FileFactory factory = FileFactoryManager.GetFactory(currentFilePath);

                    IFileSaver saver = factory.CreateSaver();

                    saver.Save(currentFilePath, source.Text);

                    string extension = Path.GetExtension(currentFilePath).ToUpper();
                    this.Text = $"Текстовий редактор - {Path.GetFileName(currentFilePath)} [{extension}]";

                    MessageBox.Show($"Файл збережено через {factory.GetType().Name}!",
                        "Успіх", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Помилка збереження: " + ex.Message,
                        "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExitFile_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Edit Menu
        private void UndoEdit_Click(object sender, EventArgs e)
        {
            if (source.CanUndo)
            {
                source.Undo();
            }
        }

        private void CutEdit_Click(object sender, EventArgs e)
        {
            if (source.SelectionLength > 0)
            {
                source.Cut();
            }
        }

        private void CopyEdit_Click(object sender, EventArgs e)
        {
            if (source.SelectionLength > 0)
            {
                source.Copy();
            }
        }

        private void PasteEdit_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                source.Paste();
            }
        }

        private void SelectAllEdit_Click(object sender, EventArgs e)
        {
            source.SelectAll();
        }

        // REGEX ФУНКЦІЇ

        private void RemoveSpacesRegex_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Текст порожній!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string text = source.Text;

            text = Regex.Replace(text, @"^\s*(\r?\n)?$", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = Regex.Replace(text, @"^[ \t]+", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"[ \t]+$", "", RegexOptions.Multiline);

            source.Text = text;
            MessageBox.Show("Зайві пробуски та табуляції видалено!", "Успіх",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ReplaceText_Click(object sender, EventArgs e)
        {
            Form replaceForm = new Form();
            replaceForm.Text = "Заміна тексту";
            replaceForm.Size = new Size(400, 180);
            replaceForm.StartPosition = FormStartPosition.CenterParent;
            replaceForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            replaceForm.MaximizeBox = false;
            replaceForm.MinimizeBox = false;

            Label lblFind = new Label();
            lblFind.Text = "Знайти:";
            lblFind.Location = new Point(20, 20);
            lblFind.Size = new Size(80, 20);
            replaceForm.Controls.Add(lblFind);

            TextBox txtFind = new TextBox();
            txtFind.Location = new Point(100, 18);
            txtFind.Size = new Size(260, 20);
            replaceForm.Controls.Add(txtFind);

            Label lblReplace = new Label();
            lblReplace.Text = "Замінити на:";
            lblReplace.Location = new Point(20, 55);
            lblReplace.Size = new Size(80, 20);
            replaceForm.Controls.Add(lblReplace);

            TextBox txtReplace = new TextBox();
            txtReplace.Location = new Point(100, 53);
            txtReplace.Size = new Size(260, 20);
            replaceForm.Controls.Add(txtReplace);

            Button btnReplace = new Button();
            btnReplace.Text = "Замінити все";
            btnReplace.Location = new Point(100, 90);
            btnReplace.Size = new Size(120, 30);
            btnReplace.Click += (s, ev) =>
            {
                if (string.IsNullOrEmpty(txtFind.Text))
                {
                    MessageBox.Show("Введіть текст для пошуку!", "Увага",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string findText = txtFind.Text;
                string replaceText = txtReplace.Text;

                int count = Regex.Matches(source.Text, Regex.Escape(findText)).Count;

                if (count > 0)
                {
                    source.Text = Regex.Replace(source.Text, Regex.Escape(findText), replaceText);
                    MessageBox.Show($"Замінено входжень: {count}", "Успіх",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    replaceForm.Close();
                }
                else
                {
                    MessageBox.Show("Текст не знайдено!", "Результат",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            replaceForm.Controls.Add(btnReplace);

            Button btnCancel = new Button();
            btnCancel.Text = "Скасувати";
            btnCancel.Location = new Point(230, 90);
            btnCancel.Size = new Size(100, 30);
            btnCancel.Click += (s, ev) => replaceForm.Close();
            replaceForm.Controls.Add(btnCancel);

            replaceForm.ShowDialog();
        }

        // Search Menu (Regex)

        private void FindPhones_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Текст порожній!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pattern = @"\b(?:\+380\d{9}|380\d{9}|0\d{9})\b";

            MatchCollection matches = Regex.Matches(source.Text, pattern);

            if (matches.Count > 0)
            {
                StringBuilder result = new StringBuilder();
                result.AppendLine($"Знайдено номерів телефонів: {matches.Count}\n");

                HashSet<string> uniquePhones = new HashSet<string>();
                foreach (Match match in matches)
                {
                    uniquePhones.Add(match.Value);
                }

                foreach (string phone in uniquePhones)
                {
                    result.AppendLine($"{phone}");
                }

                MessageBox.Show(result.ToString(), "Знайдені номери телефонів",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Номери телефонів не знайдено!", "Результат",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void FindFullName_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Текст порожній!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pattern = @"\b[A-ZА-ЯІЇЄҐ][a-zа-яіїєґ']+ [A-ZА-ЯІЇЄҐ][a-zа-яіїєґ']+\b";

            Match match = Regex.Match(source.Text, pattern);

            if (match.Success)
            {
                MessageBox.Show($"Знайдено ім'я та прізвище:\n\n{match.Value}",
                    "Результат пошуку",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                source.Select(match.Index, match.Length);
                source.Focus();
            }
            else
            {
                MessageBox.Show("Ім'я та прізвище не знайдено!\n\n" +
                    "Шукається формат: Ім'я Прізвище\n" +
                    "(дві великі букви, розділені одним пробілом)",
                    "Результат",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CheckCSharpKeywords_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Текст порожній!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] keywords = {
                "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
                "char", "checked", "class", "const", "continue", "decimal", "default",
                "delegate", "do", "double", "else", "enum", "event", "explicit",
                "extern", "false", "finally", "fixed", "float", "for", "foreach",
                "goto", "if", "implicit", "in", "int", "interface", "internal",
                "is", "lock", "long", "namespace", "new", "null", "object",
                "operator", "out", "override", "params", "private", "protected",
                "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
                "sizeof", "stackalloc", "static", "string", "struct", "switch",
                "this", "throw", "true", "try", "typeof", "uint", "ulong",
                "unchecked", "unsafe", "ushort", "using", "virtual", "void",
                "volatile", "while"
            };

            List<string> foundKeywords = new List<string>();

            foreach (string keyword in keywords)
            {
                string pattern = @"\b" + keyword + @"\b";
                if (Regex.IsMatch(source.Text, pattern))
                {
                    foundKeywords.Add(keyword);
                }
            }

            if (foundKeywords.Count > 0)
            {
                StringBuilder result = new StringBuilder();
                result.AppendLine($"Знайдено ключових слів C#: {foundKeywords.Count}\n");
                result.AppendLine(string.Join(", ", foundKeywords));

                MessageBox.Show(result.ToString(), "Ключові слова C#",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Ключові слова C# не знайдено!", "Результат",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Statistics Menu

        private void ShowStats_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Спочатку відкрийте файл або введіть текст!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string text = source.Text;

            int totalChars = text.Length;
            int totalCharsNoSpaces = text.Count(c => !char.IsWhiteSpace(c));

            int wordCount = 0;
            bool inWord = false;
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    inWord = false;
                }
                else
                {
                    if (!inWord)
                    {
                        wordCount++;
                        inWord = true;
                    }
                }
                i++;
            }

            int paragraphs = 0;
            int emptyLines = 0;
            using (StringReader reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        emptyLines++;
                    else
                        paragraphs++;
                }
            }

            double sizeKB = Encoding.UTF8.GetByteCount(text) / 1024.0;
            double authorPages = totalChars / 1800.0;

            StringBuilder stats = new StringBuilder();
            stats.AppendLine("========== СТАТИСТИКА ТЕКСТУ ==========");
            stats.AppendLine();
            stats.AppendLine($"Розмір: {sizeKB:F2} КБ");
            stats.AppendLine($"Всього символів: {totalChars}");
            stats.AppendLine($"Символів без пробілів: {totalCharsNoSpaces}");
            stats.AppendLine($"Кількість слів: {wordCount}");
            stats.AppendLine($"Кількість абзаців: {paragraphs}");
            stats.AppendLine($"Порожніх рядків: {emptyLines}");
            stats.AppendLine($"Авторських сторінок: {authorPages:F2}");

            MessageBox.Show(stats.ToString(), "Статистика файлу",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void WordFrequency_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(source.Text))
            {
                MessageBox.Show("Текст порожній!", "Увага",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string pattern = @"\b[a-zA-Zа-яА-ЯіїєґІЇЄҐ']+\b";
            MatchCollection matches = Regex.Matches(source.Text, pattern);

            Dictionary<string, int> wordCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                string word = match.Value.ToLower();
                if (wordCount.ContainsKey(word))
                {
                    wordCount[word]++;
                }
                else
                {
                    wordCount[word] = 1;
                }
            }

            var sortedWords = wordCount.OrderByDescending(x => x.Value).Take(20);

            StringBuilder result = new StringBuilder();
            result.AppendLine("===== ЧАСТОТА СЛІВ (ТОП-20) =====\n");
            result.AppendLine($"Всього унікальних слів: {wordCount.Count}\n");

            int rank = 1;
            foreach (var pair in sortedWords)
            {
                result.AppendLine($"{rank}. {pair.Key} — {pair.Value} раз(ів)");
                rank++;
            }

            Form resultForm = new Form();
            resultForm.Text = "Частота слів";
            resultForm.Size = new Size(500, 600);
            resultForm.StartPosition = FormStartPosition.CenterParent;

            TextBox resultBox = new TextBox();
            resultBox.Multiline = true;
            resultBox.ScrollBars = ScrollBars.Vertical;
            resultBox.Dock = DockStyle.Fill;
            resultBox.ReadOnly = true;
            resultBox.Font = new Font("Consolas", 10);
            resultBox.Text = result.ToString();

            resultForm.Controls.Add(resultBox);
            resultForm.ShowDialog();
        }



        // WEB MENU - ЗАВАНТАЖЕННЯ НОВИН

        private async void LoadNews_Click(object sender, EventArgs e)
        {
            Form inputForm = new Form();
            inputForm.Text = "Завантаження новин";
            inputForm.Size = new Size(400, 200);
            inputForm.StartPosition = FormStartPosition.CenterParent;
            inputForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputForm.MaximizeBox = false;
            inputForm.MinimizeBox = false;

            Label lblInfo = new Label();
            lblInfo.Text = "Завантаження новин з сайту ZNU\n\nВведіть кількість новин для завантаження:";
            lblInfo.Location = new Point(20, 20);
            lblInfo.Size = new Size(350, 40);
            inputForm.Controls.Add(lblInfo);

            NumericUpDown numCount = new NumericUpDown();
            numCount.Location = new Point(20, 70);
            numCount.Size = new Size(100, 20);
            numCount.Minimum = 1;
            numCount.Maximum = 50;
            numCount.Value = 10;
            inputForm.Controls.Add(numCount);

            Label lblWarning = new Label();
            lblWarning.Text = "(максимум 50 новин)";
            lblWarning.Location = new Point(130, 72);
            lblWarning.Size = new Size(150, 20);
            lblWarning.ForeColor = Color.Gray;
            inputForm.Controls.Add(lblWarning);

            Button btnLoad = new Button();
            btnLoad.Text = "Завантажити";
            btnLoad.Location = new Point(20, 110);
            btnLoad.Size = new Size(120, 30);
            btnLoad.Click += async (s, ev) =>
            {
                inputForm.Hide();
                int count = (int)numCount.Value;
                await LoadNewsFromZNU(count);
                inputForm.Close();
            };
            inputForm.Controls.Add(btnLoad);

            Button btnCancel = new Button();
            btnCancel.Text = "Скасувати";
            btnCancel.Location = new Point(150, 110);
            btnCancel.Size = new Size(100, 30);
            btnCancel.Click += (s, ev) => inputForm.Close();
            inputForm.Controls.Add(btnCancel);

            inputForm.ShowDialog();
        }

        private async Task LoadNewsFromZNU(int desiredCount)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                this.Text = "Текстовий редактор - Завантаження новин...";

                string urlTemplate = "https://www.znu.edu.ua/cms/index.php?action=news/view&site_id=27&lang=ukr&start={0}&ordering=weight%20ASC%2Cdate%20DESC&filtermode=&";
                int start = 0;
                int pageIndex = 0;
                const int maxPages = 50;
                var collected = new List<NewsItem>();
                var seenIds = new HashSet<string>();

                while (collected.Count < desiredCount && pageIndex < maxPages)
                {
                    string url = string.Format(urlTemplate, start);
                    string html;

                    try
                    {
                        html = await httpClient.GetStringAsync(url);
                    }
                    catch (HttpRequestException ex)
                    {
                        MessageBox.Show($"Помилка підключення до сайту:\n{ex.Message}", "Помилка мережі", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    }

                    var pageItems = ParseNewsFromPage(html);

                    
                    if (pageItems == null || pageItems.Count == 0)
                        break;

                    // Додаємо унікальні (за Id; якщо Id пусте — також за Title)
                    foreach (var item in pageItems)
                    {
                        string uniqueKey = !string.IsNullOrWhiteSpace(item.Id) ? ("id:" + item.Id) : ("title:" + item.Title);
                        if (!seenIds.Contains(uniqueKey))
                        {
                            seenIds.Add(uniqueKey);
                            collected.Add(item);
                            if (collected.Count >= desiredCount) break;
                        }
                    }

                    
                    start += pageItems.Count > 0 ? pageItems.Count : 10;

                    pageIndex++;

                   
                }

                if (collected.Count == 0)
                {
                    MessageBox.Show("Не знайдено жодної новини на сайті.", "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                
                var newsText = new StringBuilder();
                newsText.AppendLine("========================================");
                newsText.AppendLine($"НОВИНИ ЗНУ ({Math.Min(collected.Count, desiredCount)} записів)");
                newsText.AppendLine("========================================");
                newsText.AppendLine();

                int index = 1;
                foreach (var news in collected.Take(desiredCount))
                {
                    newsText.AppendLine($"[{index}] {news.Title}");
                    newsText.AppendLine(new string('-', 60));
                    
                    if (!string.IsNullOrWhiteSpace(news.Description))
                    {
                        newsText.AppendLine();
                        newsText.AppendLine(news.Description);
                    }
                    newsText.AppendLine();
                    newsText.AppendLine();
                    index++;
                }

                source.Text = newsText.ToString();
                this.Text = "Текстовий редактор - Новини ZNU";
                MessageBox.Show($"Успішно завантажено {Math.Min(collected.Count, desiredCount)} новин!", "Завантаження завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження новин:\n{ex.Message}", "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }


        private List<NewsItem> ParseNewsFromPage(string html)
        {
            var newsList = new List<NewsItem>();
            if (string.IsNullOrWhiteSpace(html)) return newsList;

            
            string itemPattern = @"<div[^>]*class\s*=\s*""[^""]*znu-2016-new[^""]*""[^>]*>([\s\S]*?)</div>\s*</div>\s*</div>";
            var itemMatches = Regex.Matches(html, itemPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in itemMatches)
            {
                string itemHtml = m.Groups[1].Value;

               
                string href = "";
                string title = "";

                var mTitle = Regex.Match(itemHtml, @"<h4[^>]*>.*?<a[^>]*href\s*=\s*""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (mTitle.Success)
                {
                    href = mTitle.Groups[1].Value;
                    title = CleanHtml(mTitle.Groups[2].Value);
                }
                else
                {
                    // fallback: атрибут title у посиланні (зображення)
                    var mTitle2 = Regex.Match(itemHtml, @"<a[^>]*title\s*=\s*""([^""]+)""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (mTitle2.Success)
                    {
                        title = CleanHtml(mTitle2.Groups[1].Value);
                    }
                    // fallback alt у img
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        var mImgAlt = Regex.Match(itemHtml, @"<img[^>]*alt\s*=\s*""([^""]+)""", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        if (mImgAlt.Success) title = CleanHtml(mImgAlt.Groups[1].Value);
                    }
                }

                if (string.IsNullOrWhiteSpace(title)) continue; // без заголовку пропускаємо

                // Опис
                string desc = "";
                var mDesc = Regex.Match(itemHtml, @"<div[^>]*class\s*=\s*""[^""]*text[^""]*""[^>]*>.*?<p[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (mDesc.Success) desc = CleanHtml(mDesc.Groups[1].Value);
                else
                {
                    var mDesc2 = Regex.Match(itemHtml, @"<div[^>]*class\s*=\s*""[^""]*text[^""]*""[^>]*>(.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (mDesc2.Success) desc = CleanHtml(mDesc2.Groups[1].Value);
                }

                

                // news_id з href
                string id = "";
                if (!string.IsNullOrWhiteSpace(href))
                {
                    var mId = Regex.Match(href, @"news_id=(\d+)");
                    if (mId.Success) id = mId.Groups[1].Value;
                }

                newsList.Add(new NewsItem
                {
                    Id = id,
                    Title = title,
                    Description = desc,
                   
                });
            }

            return newsList;
        }



        private string CleanHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return "";

            // Видаляємо HTML теги
            string text = Regex.Replace(html, @"<[^>]+>", "");

            // Декодуємо HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);

            // Видаляємо зайві пробіли
            text = Regex.Replace(text, @"\s+", " ");

            // Обрізаємо пробіли на початку та в кінці
            text = text.Trim();

            return text;
        }



        // About Menu
        private void AboutMenu_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Текстовий редактор\n\n" +
                "Розроблено для лабораторної роботи 4-5\n\n" +
                "Функції:\n" +
                "• Створення, відкриття, збереження файлів\n" +
                "• Редагування тексту (Cut, Copy, Paste)\n" +
                "• Статистика тексту та частота слів\n" +
                "• Пошук телефонів (Regex)\n" +
                "• Пошук імені та прізвища (Regex)\n" +
                "• Перевірка ключових слів C# (Regex)\n" +
                "• Видалення зайвих пробілів (Regex)\n" +
                "• Заміна тексту (Regex)\n" +
                "• Завантаження новин з веб-сайту ZNU (HttpClient)\n\n" +
                "© 2025 Потужний Проект by AnKu",
                "Про програму",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        public class QuickSingletonTest
        {
            public static void ShowQuickTest()
            {
                StringBuilder result = new StringBuilder();


                result.AppendLine("ТЕСТ 1: Отримання трьох екземплярів");

                var logger1 = EventLogger.Instance;
                var logger2 = EventLogger.Instance;
                var logger3 = EventLogger.Instance;

                result.AppendLine("var logger1 = EventLogger.Instance;");
                result.AppendLine("var logger2 = EventLogger.Instance;");
                result.AppendLine("var logger3 = EventLogger.Instance;");
                result.AppendLine();

                result.AppendLine("ТЕСТ 2: Порівняння посилань");
         
                bool test1 = (logger1 == logger2);
                bool test2 = (logger2 == logger3);
                bool test3 = ReferenceEquals(logger1, logger3);

                result.AppendLine($"logger1 == logger2:           {test1}  {GetStatus(test1)}");
                result.AppendLine($"logger2 == logger3:           {test2}  {GetStatus(test2)}");
                result.AppendLine($"ReferenceEquals(logger1, logger3):  {test3}  {GetStatus(test3)}");
                result.AppendLine();

                result.AppendLine("ТЕСТ 3: Перевірка HashCode (адреса в пам'яті)");
             
                int hash1 = logger1.GetHashCode();
                int hash2 = logger2.GetHashCode();
                int hash3 = logger3.GetHashCode();

                result.AppendLine($"logger1.GetHashCode(): {hash1}");
                result.AppendLine($"logger2.GetHashCode(): {hash2}");
                result.AppendLine($"logger3.GetHashCode(): {hash3}");
                result.AppendLine();

                bool sameHash = (hash1 == hash2 && hash2 == hash3);
                result.AppendLine($"Всі HashCode однакові? {sameHash}  {GetStatus(sameHash)}");
                result.AppendLine();

                result.AppendLine("ТЕСТ 4: Перевірка спільних даних");
               
                // Очищаємо журнал
                EventLogger.Instance.ClearLog();
                result.AppendLine("EventLogger.Instance.ClearLog();");
                result.AppendLine();

                // logger1 додає подію
                result.AppendLine("logger1.LogEvent(...);  // Додаємо 1 подію");
                logger1.LogEvent(EventType.CharAdded, 1, 1, "Тест");

                int count1 = logger1.EventCount;
                int count2 = logger2.EventCount;
                int count3 = logger3.EventCount;

                result.AppendLine($"logger1.EventCount: {count1}");
                result.AppendLine($"logger2.EventCount: {count2}");
                result.AppendLine($"logger3.EventCount: {count3}");
                result.AppendLine();

                bool sameCount = (count1 == count2 && count2 == count3 && count1 == 1);
                result.AppendLine($"Всі бачать 1 подію? {sameCount}  {GetStatus(sameCount)}");
                result.AppendLine();


                bool allPassed = test1 && test2 && test3 && sameHash && sameCount;

                if (allPassed)
                {
                    result.AppendLine(" ВСІ ТЕСТИ ПРОЙДЕНО!");
                    result.AppendLine();
                    result.AppendLine(" ДОВЕДЕНО:");
                    result.AppendLine("  Існує лише ОДИН екземпляр EventLogger");
                    result.AppendLine("  Всі змінні вказують на ОДИН об'єкт");
                    result.AppendLine("  Дані спільні для всіх посилань");
                    result.AppendLine("  EventLogger - справжній Singleton!");
                }
                else
                {
                    result.AppendLine("ДЕЯКІ ТЕСТИ НЕ ПРОЙДЕНО!");
                    result.AppendLine("EventLogger НЕ відповідає патерну Singleton");
                }

                // Показуємо результат
                Form resultForm = new Form();
                resultForm.Text = allPassed ? "Тест пройдено" : "Тест не пройдено";
                resultForm.Size = new Size(650, 700);
                resultForm.StartPosition = FormStartPosition.CenterScreen;

                TextBox txtResult = new TextBox();
                txtResult.Multiline = true;
                txtResult.ScrollBars = ScrollBars.Vertical;
                txtResult.Dock = DockStyle.Fill;
                txtResult.Font = new Font("Consolas", 10);
                txtResult.ReadOnly = true;
                txtResult.Text = result.ToString();
               
                Button btnClose = new Button();
                btnClose.Text = "Закрити";
                btnClose.Dock = DockStyle.Bottom;
                btnClose.Height = 40;
                btnClose.Font = new Font("Arial", 10, FontStyle.Bold);
                btnClose.Click += (s, e) => resultForm.Close();

                resultForm.Controls.Add(txtResult);
                resultForm.Controls.Add(btnClose);

                resultForm.ShowDialog();
            }

            private static string GetStatus(bool passed)
            {
                return passed ? "PASS" : "FAIL";
            }

        }
        private void TestObserver_Click(object sender, EventArgs e)
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine("========== ТЕСТ OBSERVER PATTERN ==========");
            info.AppendLine();
            info.AppendLine("Активні спостерігачі:");
            info.AppendLine("1. IntegerDetectorObserver - виявлення цілих чисел");
            info.AppendLine("2. AutoSaveObserver - автозбереження після абзацу");
            info.AppendLine();
            info.AppendLine("Спробуйте:");
            info.AppendLine("• Ввести число, виокремлене пробілами");
            info.AppendLine("• Додати новий абзац (двічі натиснути Enter)");
            info.AppendLine();
            info.AppendLine($"Виявлено унікальних чисел: {detectedIntegers.Count}");

            MessageBox.Show(info.ToString(), "Observer Pattern Test",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }


    // Клас для зберігання інформації про новину
    public class NewsItem
    {
        public string Id { get; set; }         
        public string Title { get; set; }
       
        public string Description { get; set; }

        public NewsItem()
        {
            Id = "";
            Title = "";
            
            Description = "";
        }
    }

}