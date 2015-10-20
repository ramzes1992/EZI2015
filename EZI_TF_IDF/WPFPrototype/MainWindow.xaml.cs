using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace WPFPrototype
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<string> _rawDocuments = new List<string>();
        List<string> _rawKeywords = new List<string>();

        List<Document> _documents = new List<Document>();
        Dictionary<string, double> IDF = new Dictionary<string, double>();

        PorterStemmer _ps = new PorterStemmer();

        public MainWindow()
        {
            InitializeComponent();
            using (StreamReader sr = new StreamReader("documents.txt"))
            {
                _rawDocuments = Regex.Split(sr.ReadToEnd(), "\r\n\r\n").ToList();
            }
            using (StreamReader sr = new StreamReader("keywords.txt"))
            {
                _rawKeywords = Regex.Split(sr.ReadToEnd(), "\r\n").ToList();
            }
            RecalculateDocuments();
        }

        private void RecalculateDocuments()
        {
            foreach (var doc in _rawDocuments)
            {
                var docRep = new Document();
                docRep.Title = Regex.Split(doc, "\r\n").FirstOrDefault();
                docRep.OriginalText = string.Concat(Regex.Split(doc, "\r\n").Skip(1));
                docRep.PostProcessedText = string.Concat(Regex.Replace(doc.ToLower(), @"[^\w\s]", "").Split(null).Select(s => _ps.stemTerm(s) + " "));
                docRep.BagOfWords = new Dictionary<string, int>();
                foreach (var keyword in _rawKeywords)
                {
                    var temp = _ps.stemTerm(keyword);
                    docRep.BagOfWords[keyword] = docRep.PostProcessedText.Split(null).Count(s => s.Equals(_ps.stemTerm(temp)));
                }

                docRep.TF = new Dictionary<string, double>();
                var tfMax = docRep.BagOfWords.Max(w => w.Value);
                foreach (var word in docRep.BagOfWords)
                {
                    if (tfMax <= 0)
                    {
                        docRep.TF[word.Key] = 0;
                    }
                    else
                    {
                        docRep.TF[word.Key] = (double)docRep.BagOfWords[word.Key] / tfMax;
                    }
                }

                _documents.Add(docRep);
            }

            //calculationg IDFs
            foreach (var keyword in _rawKeywords)
            {
                var count = (double)_documents.Count;
                var contains = _documents.Count(d => d.BagOfWords.Any(w => w.Key.Equals(keyword) && w.Value > 0));
                IDF[keyword] = contains > 0 ? Math.Log10(count / contains) : 0;
            }

            //calculationg Vector Length
            foreach (var docRep in _documents)
            {
                docRep.TFIDFVectorValue = DistanceN(docRep.TF.Select(tf => IDF[tf.Key] * tf.Value));
            }
        }

        private double DistanceN(IEnumerable<double> first)
        {
            var sum = first.Select((x) => x * x).Sum();
            return Math.Sqrt(sum);
        }

        private void v_MenuItem_Documents_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
            {
                using (StreamReader sr = new StreamReader(ofd.FileName))
                {
                    _rawDocuments = Regex.Split(sr.ReadToEnd(), "\r\n\r\n").ToList();
                }
            }
        }

        private void v_MenuItem_Keywords_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == true)
            {
                using (StreamReader sr = new StreamReader(ofd.FileName))
                {
                    _rawKeywords = Regex.Split(sr.ReadToEnd(), "\r\n").ToList();
                }
            }
        }

        private void v_MenuItem_ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            RecalculateDocuments();
        }

        private void v_MenuItem_Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void v_TextBox_SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            v_Button_Search.IsEnabled = !string.IsNullOrWhiteSpace(v_TextBox_SearchInput.Text) && _rawDocuments.Any() && _rawKeywords.Any() && _documents.Any();
        }

        private void v_Button_Search_Click(object sender, RoutedEventArgs e)
        {
            if (!_rawDocuments.Any() || !_rawKeywords.Any() || !_documents.Any())
            {
                return;
            }

            string originalQuery = v_TextBox_SearchInput.Text;

            string processedQuery = string.Concat(Regex.Replace(originalQuery.ToLower(), @"[^\w\s]", "").Split(null).Select(s => _ps.stemTerm(s) + " "));
            //Creating bag of words
            var queryBagOfWords = new Dictionary<string, int>();
            foreach (var keyword in _rawKeywords)
            {
                var temp = _ps.stemTerm(keyword);
                queryBagOfWords[keyword] = processedQuery.Split(null).Count(s => s.Equals(_ps.stemTerm(temp)));
            }

            //Calculating TF
            var queryTF = new Dictionary<string, double>();
            var queryTFMax = queryBagOfWords.Max(w => w.Value);
            foreach (var word in queryBagOfWords)
            {
                queryTF[word.Key] = queryTFMax <= 0 ? 0 : (double)queryBagOfWords[word.Key] / queryTFMax;
            }

            //Calculationg TF-IDF
            var queryTFIDFVectorValue = DistanceN(queryTF.Select(tf => IDF[tf.Key] * tf.Value));

            //CALCULATING SIMILARITY
            foreach (var docRep in _documents)
            {
                var x = docRep.TFIDFVectorValue * queryTFIDFVectorValue;
                double val = 0;
                foreach (var keyword in _rawKeywords)
                {
                    val += docRep.TF[keyword] * IDF[keyword] * queryTF[keyword] * IDF[keyword];
                }
                docRep.Similarity = x > 0 ? val / x : 0;
            }

            v_ListView_ResultList.ItemsSource = _documents.OrderByDescending(d => d.Similarity).ToList();
        }

        private void v_ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListViewItem item = sender as ListViewItem;
            object obj = item.Content;

            DocumentViewer dv = new DocumentViewer(obj as Document);
            dv.ShowDialog();
        }
    }
}
