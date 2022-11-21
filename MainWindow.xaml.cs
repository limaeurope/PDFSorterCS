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

namespace PDFSorterCS
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    public class PDFDocument
    {
        public string Path, Name;
        public double Height, Width, MinSize, MaxSize;

        public PDFDocument(string p_path, string p_name)
        {
            Path = p_path;
            Name = p_name;

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
        private const int EPS = 1;

        private class SizeRange
        {
            public double WMin, WMax, HMin, HMax;
            public List<PDFDocument> docS;
            public string sName;

            public SizeRange(double p_wMin, double p_wMax, double p_hMin, double p_hMax)
            {
                WMin = p_wMin;
                WMax = p_wMax;
                HMin = p_hMin;
                HMax = p_hMax;
            }

            public SizeRange(double p_wMax, double p_hMax)
            {
                WMin = 0;
                WMax = p_wMax;
                HMin = 0;
                HMax = p_hMax;
            }

            //public bool isSizeFit(double p_width, double p_height)
            //{
            //    return  (p_width >= WMin && p_width <= WMax)
            //    ||      (p_height >= HMin && p_height <= HMax);
            //}

            public bool isSizeFit(PDFDocument p_obj)
            {
                return (WMin <= p_obj.Width && p_obj.Width <= WMax && HMin <= p_obj.Height && p_obj.Height <= HMax
                ||      WMin <= p_obj.Height && p_obj.Height <= WMax && HMin <= p_obj.Width && p_obj.Width <= HMax);
            }
        }

        private class PaperSizeComparer : IComparer<SizeRange>
        {
            public int Compare(SizeRange left, SizeRange right)
            {
                return (int)left.WMax - (int)right.WMax;
            }
        }

        Dictionary<string, SizeRange> dStandardSizes;

        Dictionary<string, List<PDFDocument>> dictSizes = new Dictionary<string, List<PDFDocument>>();
        SortedSet<int> sizesGrowing = new SortedSet<int>();
        SortedSet<SizeRange> dStandardSizesGrowing = new SortedSet<SizeRange>();

        public string SourcePath { set; get; }
        public string TargetPath { set; get; }

        private DictContainer() 
        {
            dStandardSizes = new Dictionary<string, SizeRange>();
            dStandardSizes.Add("A4", new SizeRange(210 - EPS, 210 + EPS, 297 - EPS, 297 + EPS) );
            dStandardSizes.Add("A3", new SizeRange(297 - EPS, 297 + EPS, 420 - EPS, 420 + EPS) );
        }

        public void SetSizes(string p_sDictSizes)
        {
            var _dictSizes = p_sDictSizes.Split(" ");

            foreach (var s in _dictSizes)
            {
                dictSizes.Add(s, new List<PDFDocument>());
            }

            sizesGrowing = new SortedSet<int>();
            foreach (var k in dictSizes.Keys)
                try
                {
                    sizesGrowing.Add(int.Parse(k));
                }
                catch { }
            
            dictSizes.Add(OVERSIZE, new List<PDFDocument>());

            foreach (var s in dStandardSizes)
                dictSizes.Add(s.Key, new List<PDFDocument>());
        }

        public void Add(PDFDocument p_obj)
        {
            foreach (var s in dStandardSizes)
                if (s.Value.isSizeFit(p_obj))
                {
                    dictSizes[s.Key].Add(p_obj);
                    return;
                }

            foreach (var s in sizesGrowing.Reverse())
                if (p_obj.MaxSize <= s + EPS)
                {
                    dictSizes[s.ToString()].Add(p_obj);
                    return;
                }

            dictSizes[OVERSIZE].Add(p_obj);
        }

        public void ProcessDirs(string p_sSubDirs = "")
        {
            foreach (var f in Directory.GetFiles(System.IO.Path.Join(SourcePath, p_sSubDirs)))
            {
                var p = new PDFDocument(p_sSubDirs, System.IO.Path.GetFileName(f));
                Add(p);
            }

            foreach (var f in Directory.GetDirectories(System.IO.Path.Join(SourcePath, p_sSubDirs)))
            {
                ProcessDirs(System.IO.Path.Join(p_sSubDirs, System.IO.Path.GetFileName(f)));
            }
        }

        public void CopyFiles()
        {
            foreach (var sd in dictSizes)
            {
                try
                {
                    if (sd.Value.Count > 0)
                        Directory.CreateDirectory(System.IO.Path.Join(TargetPath, sd.Key));

                    foreach (var f in sd.Value)
                    {
                        var src = System.IO.Path.Join(SourcePath, f.Path, f.Name);
                        var dest = System.IO.Path.Join(TargetPath, sd.Key, f.Name);

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
    }
}
