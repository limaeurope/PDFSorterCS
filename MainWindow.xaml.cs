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
using iTextSharp.text.pdf.parser;
using System.IO;
using System.Collections.Specialized;
using Microsoft.Win32;
using System.Windows.Forms;

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
        private const string OTHER = "Other Document";
        private const int EPS = 1;
        private SizeRange srOther;
        private SizeRange srOversize;


        private class SizeRange: IComparable
        {
            public double size;
            public double secondSize;
            public List<GenericDocument> docS = new List<GenericDocument>();
            public string sName;

            public SizeRange(string p_sName, double p_size, double p_secondSize = -1)
            {
                size = p_size;

                sName = p_sName;
                secondSize = p_secondSize;
            }

            public bool isSizeFit(PDFDocument p_obj)
            {
                if (sName == OVERSIZE) return true;
                if (secondSize < 0)
                    return (p_obj.MaxSize <= size + EPS);
                else
                    return (p_obj.MaxSize <= size + EPS && p_obj.MinSize <= secondSize + EPS);
            }

            public bool isSmallerSizeFit(PDFDocument p_obj)
            {
                return (p_obj.MinSize <= size + EPS);
            }

            public int CompareTo(object other)
            {
                if (sName == OVERSIZE) return 1;
                if ((other as SizeRange).sName == OVERSIZE) return -1;
                if (sName == OTHER) return 1;
                if ((other as SizeRange).sName == OTHER) return -1;

                return (int)size - (int)(other as SizeRange).size;
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
                if (s.isSizeFit(p_obj))
                {
                    if (s == srOversize)
                        foreach (var s2 in dStandardSizesGrowing)
                            if (s2.isSmallerSizeFit(p_obj))
                            {
                                s2.docS.Add(p_obj);
                                return;
                            }

                    s.docS.Add(p_obj);
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
        }

        private static readonly Lazy<DictContainer> dictContainer = new Lazy<DictContainer>(() => new DictContainer());

        public static DictContainer GetInstance()
        {
            return dictContainer.Value;
        }
    }


    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            RollSizes.Text = "1828 1220 1067 610 420 594 841 914";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dictContainer = DictContainer.GetInstance();


            dictContainer.ProcessDirs();
            dictContainer.CopyFiles();

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

        private void ButtonSourcePath_Drop(object sender, System.Windows.DragEventArgs e)
        {
            SourcePath.Text = e.ToString();
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


    }
}
