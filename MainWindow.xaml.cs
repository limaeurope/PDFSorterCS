using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System.Windows.Forms;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using System.Diagnostics;

namespace PDFSorterCS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public class GenericDocument
    {
        public string Path, Name;

        public GenericDocument(string p_path, string p_name)
        {
            Path = p_path;
            Name = p_name;
        }
    }


    public class PDFDocument: GenericDocument
    {
        public double Height, Width, MinSize, MaxSize;

        public PDFDocument(string p_path, string p_name): base(p_path, p_name)
        {
            var pdfReader = new PdfReader(System.IO.Path.Join(DictContainer.GetInstance().SourcePath, p_path, p_name));

            iTextSharp.text.Rectangle mediabox = pdfReader.GetPageSize(1);

            Width = mediabox.Width / 72 * 25.4;
            Height = mediabox.Height / 72 * 25.4;

            MinSize = Width < Height ? Width : Height;
            MaxSize = Width > Height ? Width : Height;
        }
    }


    public class DictContainer
    {
        private const string OVERSIZE = "Oversize";
        private const string OTHER = "Other Documents";
        private const string AREAS = "Areas.txt";
        private const int EPS = 1;
        private SizeRange srOther;
        private SizeRange srOversize;
        private static MainWindow mainWindow;


        private class SizeRange: IComparable
        {
            public double size;
            public double secondSize;
            public List<GenericDocument> docS = new List<GenericDocument>();
            public string sName;
            public double dTotalArea;
            public int iTotalPages;
            private bool isRoll;

            public SizeRange(string p_sName, double p_size, double p_secondSize = -1)
            {
                size = p_size;

                sName = p_sName;
                secondSize = p_secondSize;
                dTotalArea = 0;
                iTotalPages = 0;
                isRoll = p_secondSize > 0 ? false : true;
            }

            public bool IsSizeFit(PDFDocument p_obj)
            {
                if (sName == OVERSIZE) return true;
                if (secondSize < 0)
                    return (p_obj.MaxSize <= size + EPS);
                else
                    return (p_obj.MaxSize <= size + EPS && p_obj.MinSize <= secondSize + EPS);
            }

            public bool IsSmallerSizeFit(PDFDocument p_obj)
            {
                return (p_obj.MinSize <= size + EPS && isRoll);
            }

            public int CompareTo(object other)
            {
                if (sName == OVERSIZE) return 1;
                if ((other as SizeRange).sName == OVERSIZE) return -1;
                if (sName == OTHER) return 1;
                if ((other as SizeRange).sName == OTHER) return -1;

                return (int)size - (int)(other as SizeRange).size;
            }

            public void Add(PDFDocument p_doc)
            {
                if (isRoll)
                    dTotalArea += p_doc.Width * p_doc.Height;
                else
                    iTotalPages++;
                docS.Add(p_doc);
            }

            public override string ToString()
            {
                if (isRoll)
                    return sName + " mm: " + Math.Round(dTotalArea / 1000000, 2).ToString() + " m2";
                else
                    return sName + ": " + iTotalPages.ToString();
            }
        }


        SortedSet<SizeRange> dStandardSizesGrowing;

        public string SourcePath { set; get; }
        public string TargetPath { set; get; }

        private DictContainer() 
        {
            dStandardSizesGrowing = new SortedSet<SizeRange>();
        }

        public void SetSizes(string p_sDictSizes)
        {
            dStandardSizesGrowing.Clear();

            dStandardSizesGrowing.Add(new SizeRange("A4", 297, 210));
            dStandardSizesGrowing.Add(new SizeRange("A3", 420, 297));
            srOversize = new SizeRange(OVERSIZE, 0);
            dStandardSizesGrowing.Add(srOversize);
            srOther = new SizeRange(OTHER, 0);
            dStandardSizesGrowing.Add(srOther);

            var _dictSizes = p_sDictSizes.Split(" ");

            foreach (var s in _dictSizes)
            {
                try
                {
                    dStandardSizesGrowing.Add(new SizeRange(s, int.Parse(s)));
                }
                catch { }
            }
        }

        public void Add(PDFDocument p_obj)
        {
            foreach (var s in dStandardSizesGrowing)
                if (s.IsSizeFit(p_obj))
                {
                    if (s == srOversize)
                        foreach (var s2 in dStandardSizesGrowing)
                            if (s2.IsSmallerSizeFit(p_obj))
                            {
                                s2.Add(p_obj);
                                return;
                            }

                    s.Add(p_obj);
                    return;
                }
        }

        public void Add(GenericDocument p_obj)
        {
            srOther.docS.Add(p_obj);
            return;
        }

        public void ProcessDirs(string p_sSubDirs = "")
        {
            try
            {
                foreach (var f in Directory.GetFiles(System.IO.Path.Join(SourcePath, p_sSubDirs)))
                {
                    try
                    {
                        var p = new PDFDocument(p_sSubDirs, System.IO.Path.GetFileName(f));
                        Add(p);
                    }
                    catch
                    {
                        var p = new GenericDocument(p_sSubDirs, System.IO.Path.GetFileName(f));
                        Add(p);
                    }
                }

                foreach (var f in Directory.GetDirectories(System.IO.Path.Join(SourcePath, p_sSubDirs)))
                {
                    ProcessDirs(System.IO.Path.Join(p_sSubDirs, System.IO.Path.GetFileName(f)));
                }
            }
            catch
            {
                throw new Exception("Source folder doesn't exist.");
            }
        }

        public void CopyFiles()
        {
            foreach (var sd in dStandardSizesGrowing)
            {
                try
                {
                    if (sd.docS.Count > 0)
                        Directory.CreateDirectory(System.IO.Path.Join(TargetPath, sd.sName));

                    foreach (var f in sd.docS)
                    {
                        var src = System.IO.Path.Join(SourcePath, f.Path, f.Name);
                        var dest = System.IO.Path.Join(TargetPath, sd.sName, f.Name);

                        File.Copy(src, dest);
                    }
                }
                catch (System.IO.IOException)
                {
                    //File exists
                }
                finally
                {

                }
            }

            File.WriteAllText(System.IO.Path.Join(TargetPath, AREAS), ToString());
        }

        private static readonly Lazy<DictContainer> dictContainer = new Lazy<DictContainer>(() => new DictContainer());

        public static DictContainer GetInstance(MainWindow p_mainWindow = null)
        {
            if (p_mainWindow != null)
                mainWindow = p_mainWindow;
            return dictContainer.Value;
        }

        override public string ToString()
        {
            string sResult = "";

            foreach (var s in dStandardSizesGrowing)
                if (s.dTotalArea > EPS ||s.iTotalPages > 0)
                    sResult += s.ToString() + "\n";

            return sResult;
        }
    }


    public partial class MainWindow : Window
    {
        const string userRoot = "HKEY_CURRENT_USER";
        const string subkey = "SOFTWARE\\LIMA\\PDFSorterCS";

        public MainWindow()
        {
            InitializeComponent();

            var sRollSizes = (string)Registry.GetValue(userRoot + "\\" + subkey, "RollSizes", "594 841");
            RollSizes.Text = sRollSizes == null ? "594 841" : sRollSizes;
            SourcePath.Text = (string)Registry.GetValue(userRoot + "\\" + subkey, "SourcePath", "");
            TargetPath.Text = (string)Registry.GetValue(userRoot + "\\" + subkey, "TargetPath", "");

            SourcePath.PreviewDragEnter += new System.Windows.DragEventHandler(TextBox_PreviewDragEnter);
            SourcePath.PreviewDragOver += new System.Windows.DragEventHandler(TextBox_PreviewDragEnter);
            SourcePath.PreviewDrop += new System.Windows.DragEventHandler(TextBox_PreviewDrop);

            TargetPath.PreviewDragEnter += new System.Windows.DragEventHandler(TextBox_PreviewDragEnter);
            TargetPath.PreviewDragOver += new System.Windows.DragEventHandler(TextBox_PreviewDragEnter);
            TargetPath.PreviewDrop += new System.Windows.DragEventHandler(TextBox_PreviewDrop);
        }

        #region Event handlers
        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            var dictContainer = DictContainer.GetInstance(this);
            dictContainer.SetSizes(RollSizes.Text);

            if (!CheckTargetDirectory())
                return;

            try
            {
                dictContainer.ProcessDirs();
                dictContainer.CopyFiles();

                SetOutput("Successfully done.");
            }
            catch (Exception ex)
            {
                SetOutput(ex.Message);
            }
        }

        private void ButtonHelp_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://limadesignkft.sharepoint.com/sites/bim/BIM%20developer%20wiki/PDFSorter.aspx", UseShellExecute = true });
        }

        private void Rollsizes_TextChanged(object sender, TextChangedEventArgs e)
        {
            DictContainer.GetInstance().SetSizes(RollSizes.Text);
        }


        private void SourcePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            DictContainer.GetInstance().SourcePath = SourcePath.Text;
        }

        private void TargetPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            DictContainer.GetInstance().TargetPath = TargetPath.Text;
            CheckTargetDirectory();

        }

        private void ButtonSourcePath_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    SourcePath.Text = fbd.SelectedPath;
                }
            }
        }

        private void ButtonTargetPath_Click(object sender, RoutedEventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    TargetPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void TextBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void TextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
                if (Directory.Exists(path))
                {
                    (sender as TextBox).Text = path;
                }
            }
        }
        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Registry.SetValue(userRoot + "\\" + subkey, "RollSizes", RollSizes.Text);
            Registry.SetValue(userRoot + "\\" + subkey, "SourcePath", SourcePath.Text);
            Registry.SetValue(userRoot + "\\" + subkey, "TargetPath", TargetPath.Text);
        }

        public async Task<int> SetOutput(string p_info, int iMilliseconds = 5000)
        {
            try
            {
                OutputInfo.Text = p_info;
                await Task.Delay(iMilliseconds);
                OutputInfo.Text = "";
            }
            catch { }

            return 0;
        }

        private bool CheckTargetDirectory()
        {
            try
            {
                IEnumerable<string> items = Directory.EnumerateFileSystemEntries(TargetPath.Text);
                using (IEnumerator<string> en = items.GetEnumerator())
                {
                    if (en.MoveNext())
                    {
                        SetOutput("Target path is not empty, please empty it!");
                        return false;
                    }
                }
            }
            catch { }

            return true;
        }
    }
}
