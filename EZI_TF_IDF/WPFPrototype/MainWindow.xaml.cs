﻿using Microsoft.Win32;
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
using MathNet.Numerics.LinearAlgebra;

namespace WPFPrototype
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _rawKeywords = new List<string>();
        private List<string> _rawDocuments = new List<string>();

        private List<Document> _documents = new List<Document>();
        private Dictionary<string, double> IDF = new Dictionary<string, double>();

        private PorterStemmer _ps = new PorterStemmer();

        private List<string> allWords = new List<string>();
        private Matrix<double> wordsCorrelationMatrix;
        private double _minCorrelationValue = 0.01;

        private SuggestionsMethod _currentSuggestionsMethod = SuggestionsMethod.Correlation;
        private Dictionary<string, Dictionary<string, int>> _nextWordCounter = new Dictionary<string, Dictionary<string, int>>();

        private Matrix<double> LsiMatrix;
        private Matrix<double> K_s;
        private Matrix<double> S_s;
        private Matrix<double> D_sT;
        private Matrix<double> D_s;
        private int reductionCount = 5;

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
            //Calculating TF/IDF
            #region TF IDF
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
            #endregion

            //words Correlation
            #region Correlation
            foreach (var doc in _rawDocuments)
            {
                var wordsInDoc = GetWords(doc);
                foreach (var word in wordsInDoc)
                {
                    if (!allWords.Contains(word))
                    {
                        allWords.Add(word);
                    }
                }
            }
            double[,] docsWordsArray = new double[_rawDocuments.Count, allWords.Count];
            for (int i = 0; i < _rawDocuments.Count; i++)
            {
                var wordsInDoc = GetWords(_rawDocuments[i]);
                for (int j = 0; j < allWords.Count; j++)
                {
                    var wordsCount = wordsInDoc.Count(w => w.Equals(allWords[j]));
                    docsWordsArray[i, j] = wordsCount;
                }
            }
            var docsWordsMatrix = Matrix<double>.Build.DenseOfArray(docsWordsArray);
            wordsCorrelationMatrix = docsWordsMatrix.Transpose().Multiply(docsWordsMatrix).NormalizeRows(1.0);
            #endregion

            //NextWordCounting
            #region Next Word Counting

            for (int i = 0; i < _rawDocuments.Count; i++)
            {
                var words = GetWords(_rawDocuments[i]);
                for (int j = 0; j < words.Length; j++)
                {
                    if (!_nextWordCounter.ContainsKey(words[j]))
                    {// jeszcze nie było takiego słowa
                        _nextWordCounter[words[j]] = new Dictionary<string, int>();
                        if (j + 1 < words.Length)
                        {//istnieje następne słowo
                            _nextWordCounter[words[j]][words[j + 1]] = 1;
                        }
                    }
                    else
                    {// było już i incrementujemy wartość jego następnego słowa
                        if (j + 1 < words.Length)
                        {// istnieje następne słowo
                            if (!_nextWordCounter[words[j]].ContainsKey(words[j + 1]))
                            {//następnego słowa jeszcze nie ma w słowniku
                                _nextWordCounter[words[j]][words[j + 1]] = 1;
                            }
                            else
                            {//następne słowo już jest w słowniku - incrementujemy wartość
                                _nextWordCounter[words[j]][words[j + 1]]++;
                            }
                        }
                    }
                }
            }

            #endregion

            //LSI Calculating
            #region LSI
            double[,] termsDocsArray = new double[_rawKeywords.Count, _documents.Count];
            //bag of words
            for (int i = 0; i < _rawKeywords.Count; i++)
            {
                for (int j = 0; j < _documents.Count; j++)
                {
                    termsDocsArray[i, j] = _documents[j].BagOfWords[_rawKeywords[i]];
                }
            }
            // TF/IDF
            //for (int i = 0; i < _rawKeywords.Count; i++)
            //{
            //    for (int j = 0; j < _documents.Count; j++)
            //    {
            //        termsDocsArray[i, j] = _documents[j].TF[_rawKeywords[i]] * IDF[_rawKeywords[i]];
            //    }
            //}

            Matrix<double> termsDocsMatrix = Matrix<double>.Build.DenseOfArray(termsDocsArray);

            var svd = termsDocsMatrix.Svd();
            var K = svd.U;
            var S = Matrix<double>.Build.DenseOfDiagonalArray(svd.S.ToArray());
            var DT = svd.VT.SubMatrix(0, S.RowCount, 0, svd.VT.ColumnCount);
            var D = DT.Transpose();

            S_s = S.SubMatrix(0, S.RowCount - reductionCount, 0, S.ColumnCount - reductionCount);
            K_s = K.SubMatrix(0, K.RowCount, 0, K.ColumnCount - Math.Abs(K.ColumnCount - S_s.ColumnCount));
            D_sT = DT.SubMatrix(0, DT.RowCount - reductionCount, 0, DT.ColumnCount);
            D_s = D_sT.Transpose();

            LsiMatrix = K_s * S_s * D_sT;
            #endregion
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
            IDF = new Dictionary<string, double>();
            _documents = new List<Document>();
            allWords = new List<string>();
            wordsCorrelationMatrix = null;
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

            #region TF/IDF/SIMILARITY
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

            v_ListView_ResultList.ItemsSource = _documents.OrderByDescending(d => d.Similarity).ToList();

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
            #endregion

            //Checking Correlation
            #region Correlation
            List<CorrelatedWord> correlatedWords = new List<CorrelatedWord>();
            bool _first = true;
            foreach (var inputWord in GetWords(originalQuery))
            {
                if (_first)
                {
                    correlatedWords = GetCorrelateedWords(inputWord, allWords, wordsCorrelationMatrix).ToList();
                }
                else
                {
                    correlatedWords = GetCorrelateedWords(inputWord, allWords, wordsCorrelationMatrix).Where(c => correlatedWords.Any(w => w.Word.Equals(c.Word))).ToList();
                }

                _first = false;
            }
            var stemedWords = GetWords(originalQuery).Select(w => _ps.stemTerm(w));
            var filteredWords = correlatedWords
                .Where(w => !string.IsNullOrWhiteSpace(w.Word))
                .Where(w => !stemedWords.Contains(_ps.stemTerm(w.Word)))
                .Where(w => !originalQuery.Contains(w.Word))
                .Where(w => !_stopWords.Contains(w.Word))
                .Where(w => w.Correlation >= _minCorrelationValue);

            var orderedResult = filteredWords.GroupBy(c => c.Word)
                .Select(x => new CorrelatedWord()
                {
                    Word = x.Key,
                    Correlation = x.Select(z => z.Correlation).Aggregate((a, b) => a + b)
                })
                .OrderByDescending(w => w.Correlation)
                .Take(5);
            #endregion

            //next word
            #region Next Word

            var lastQueryWord = GetWords(originalQuery).LastOrDefault();
            List<CorrelatedWord> nextWordsOrdered = new List<CorrelatedWord>();
            if (!string.IsNullOrWhiteSpace(lastQueryWord))
            {
                if (_nextWordCounter.ContainsKey(lastQueryWord))
                {
                    var sum = _nextWordCounter[lastQueryWord].Sum(c => c.Value);
                    nextWordsOrdered = _nextWordCounter[lastQueryWord].OrderByDescending(c => c.Value)
                        .Select(w => new CorrelatedWord() { Word = w.Key, Correlation = sum > 0 ? (double)w.Value/sum : 0}).ToList();
                }
            }

            #endregion

            //LSI
            #region LSI
            //bag of words
            Vector<double> qT = Vector<double>.Build.DenseOfArray(queryBagOfWords.Select(w => (double)w.Value).ToArray());//new double[] { 0, 1, 0, 0, 1 });
            // TF/IDF
            //Vector<double> qT = Vector<double>.Build.DenseOfArray(queryTF.Select(w => w.Value * IDF[w.Key]).ToArray());//new double[] { 0, 1, 0, 0, 1 });

            var transformedQuery = qT * K_s * S_s.Inverse();

            var queryValue = DistanceN(transformedQuery);


            var docValues = new double[D_s.RowCount];
            for (int i = 0; i < docValues.Length; i++)
            {
                docValues[i] = DistanceN(D_s.Row(i));
            }

            var sumOfProducts = new double[D_s.RowCount];
            for (int i = 0; i < sumOfProducts.Length; i++)
            {
                sumOfProducts[i] = 0;
                for (int j = 0; j < D_s.Row(i).Count; j++)
                {
                    sumOfProducts[i] += D_s[i, j] * transformedQuery[j];
                }
            }

            var sim = new double[D_s.RowCount];
            for (int i = 0; i < sim.Length; i++)
            {
                sim[i] = queryValue > 0 ? sumOfProducts[i] / queryValue : 0;
                _documents[i].LsiSimilarity = sim[i];
            }

            v_ListView_LSIResultList.ItemsSource = _documents.OrderByDescending(d => d.LsiSimilarity).ToList();
            #endregion


            switch (_currentSuggestionsMethod)
            {
                case SuggestionsMethod.Correlation:
                    v_ListBox_Suggestions.ItemsSource = orderedResult;
                    break;
                case SuggestionsMethod.NextWord:
                    v_ListBox_Suggestions.ItemsSource = nextWordsOrdered;
                    break;
                default:
                    break;
            }


        }

        private void v_ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ListViewItem item = sender as ListViewItem;
            object obj = item.Content;

            DocumentViewer dv = new DocumentViewer(obj as Document);
            dv.ShowDialog();
        }

        private string[] GetWords(string document)
        {
            return Regex.Replace(document.ToLower(), @"[^\w\s]", "").Split(null).Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
        }

        private static string[] _stopWords
        {
            get
            {
                return new string[]
                {
                    #region STOP WORDS
                    "a",
                    "about",
                    "above",
                    "across",
                    "after",
                    "afterwards",
                    "again",
                    "against",
                    "all",
                    "almost",
                    "alone",
                    "along",
                    "already",
                    "also",
                    "although",
                    "always",
                    "am",
                    "among",
                    "amongst",
                    "amount",
                    "an",
                    "and",
                    "another",
                    "any",
                    "anyhow",
                    "anyone",
                    "anything",
                    "anyway",
                    "anywhere",
                    "are",
                    "around",
                    "as",
                    "at",
                    "back",
                    "be",
                    "became",
                    "because",
                    "become",
                    "becomes",
                    "becoming",
                    "been",
                    "before",
                    "beforehand",
                    "behind",
                    "being",
                    "below",
                    "beside",
                    "besides",
                    "between",
                    "beyond",
                    "bill",
                    "both",
                    "bottom",
                    "but",
                    "by",
                    "call",
                    "can",
                    "cannot",
                    "cant",
                    "co",
                    "computer",
                    "con",
                    "could",
                    "couldnt",
                    "cry",
                    "de",
                    "describe",
                    "detail",
                    "do",
                    "done",
                    "down",
                    "due",
                    "during",
                    "each",
                    "eg",
                    "eight",
                    "either",
                    "eleven",
                    "else",
                    "elsewhere",
                    "empty",
                    "enough",
                    "etc",
                    "even",
                    "ever",
                    "every",
                    "everyone",
                    "everything",
                    "everywhere",
                    "except",
                    "few",
                    "fifteen",
                    "fify",
                    "fill",
                    "find",
                    "fire",
                    "first",
                    "five",
                    "for",
                    "former",
                    "formerly",
                    "forty",
                    "found",
                    "four",
                    "from",
                    "front",
                    "full",
                    "further",
                    "get",
                    "give",
                    "go",
                    "had",
                    "has",
                    "have",
                    "he",
                    "hence",
                    "her",
                    "here",
                    "hereafter",
                    "hereby",
                    "herein",
                    "hereupon",
                    "hers",
                    "herself",
                    "him",
                    "himself",
                    "his",
                    "how",
                    "however",
                    "hundred",
                    "i",
                    "ie",
                    "if",
                    "in",
                    "inc",
                    "indeed",
                    "interest",
                    "into",
                    "is",
                    "it",
                    "its",
                    "itself",
                    "keep",
                    "last",
                    "latter",
                    "latterly",
                    "least",
                    "less",
                    "ltd",
                    "made",
                    "many",
                    "may",
                    "me",
                    "meanwhile",
                    "might",
                    "mill",
                    "mine",
                    "more",
                    "moreover",
                    "most",
                    "mostly",
                    "move",
                    "much",
                    "must",
                    "my",
                    "myself",
                    "name",
                    "namely",
                    "neither",
                    "never",
                    "nevertheless",
                    "next",
                    "nine",
                    "no",
                    "nobody",
                    "none",
                    "nor",
                    "not",
                    "nothing",
                    "now",
                    "nowhere",
                    "of",
                    "off",
                    "often",
                    "on",
                    "once",
                    "one",
                    "only",
                    "onto",
                    "or",
                    "other",
                    "others",
                    "otherwise",
                    "our",
                    "ours",
                    "ourselves",
                    "out",
                    "over",
                    "own",
                    "part",
                    "per",
                    "perhaps",
                    "please",
                    "put",
                    "rather",
                    "re",
                    "same",
                    "see",
                    "seem",
                    "seemed",
                    "seeming",
                    "seems",
                    "serious",
                    "several",
                    "she",
                    "should",
                    "show",
                    "side",
                    "since",
                    "sincere",
                    "six",
                    "sixty",
                    "so",
                    "some",
                    "somehow",
                    "someone",
                    "something",
                    "sometime",
                    "sometimes",
                    "somewhere",
                    "still",
                    "such",
                    "system",
                    "take",
                    "ten",
                    "than",
                    "that",
                    "the",
                    "their",
                    "them",
                    "themselves",
                    "then",
                    "thence",
                    "there",
                    "thereafter",
                    "thereby",
                    "therefore",
                    "therein",
                    "thereupon",
                    "these",
                    "they",
                    "thick",
                    "thin",
                    "third",
                    "this",
                    "those",
                    "though",
                    "three",
                    "through",
                    "throughout",
                    "thru",
                    "thus",
                    "to",
                    "together",
                    "too",
                    "top",
                    "toward",
                    "towards",
                    "twelve",
                    "twenty",
                    "two",
                    "un",
                    "under",
                    "until",
                    "up",
                    "upon",
                    "us",
                    "very",
                    "via",
                    "was",
                    "we",
                    "well",
                    "were",
                    "what",
                    "whatever",
                    "when",
                    "whence",
                    "whenever",
                    "where",
                    "whereafter",
                    "whereas",
                    "whereby",
                    "wherein",
                    "whereupon",
                    "wherever",
                    "whether",
                    "which",
                    "while",
                    "whither",
                    "who",
                    "whoever",
                    "whole",
                    "whom",
                    "whose",
                    "why",
                    "will",
                    "with",
                    "within",
                    "without",
                    "would",
                    "yet",
                    "you",
                    "your",
                    "yours",
                    "yourself",
                    "yourselves"
                    #endregion
                };
            }
        }

        private List<CorrelatedWord> GetCorrelateedWords(string inputWord, List<string> allWords, Matrix<double> wordsWordsMatrix)
        {
            var stemedInputWord = _ps.stemTerm(inputWord);
            var simillarWords = allWords.Where(w => _ps.stemTerm(w).Equals(stemedInputWord));
            var simillarWordsIndexes = simillarWords.Select(w => allWords.IndexOf(w));
            var correlatedWords = new List<CorrelatedWord>();
            foreach (var correlatedWordIndex in simillarWordsIndexes)
            {
                for (int i = 0; i < wordsWordsMatrix.Row(correlatedWordIndex).Count; i++)
                {
                    correlatedWords.Add(new CorrelatedWord() { Word = allWords[i], Correlation = wordsWordsMatrix.Row(correlatedWordIndex)[i] });
                }
            }

            return correlatedWords;
        }

        private void SuggestionMethod_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (v_ListBox_Suggestions != null
                && sender != null
                && (sender as RadioButton).Content != null)
            {
                v_ListBox_Suggestions.ItemsSource = new List<CorrelatedWord>();
                string content = (sender as RadioButton).Content.ToString();
                _currentSuggestionsMethod = (SuggestionsMethod)Enum.Parse(typeof(SuggestionsMethod), content);
            }
        }

        private void IndexingMethod_RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if ( sender != null
                && (sender as RadioButton).Content != null)
            {
                string content = (sender as RadioButton).Content.ToString();
                switch((IndexingMethod)Enum.Parse(typeof(IndexingMethod), content))
                {
                    case IndexingMethod.TFIDF:
                        v_ListView_ResultList.Visibility = Visibility.Visible;
                        v_ListView_LSIResultList.Visibility = Visibility.Hidden;
                        break;
                    case IndexingMethod.LSI:
                        v_ListView_ResultList.Visibility = Visibility.Hidden;
                        v_ListView_LSIResultList.Visibility = Visibility.Visible;
                        break;
                    default:
                        v_ListView_ResultList.Visibility = Visibility.Visible;
                        v_ListView_LSIResultList.Visibility = Visibility.Hidden;
                        break;
                }
            }
        }
    }

    class CorrelatedWord
    {
        public string Word { get; set; }
        public double Correlation { get; set; }
    }

    enum SuggestionsMethod
    {
        Correlation,
        NextWord,
        None
    }

    enum IndexingMethod
    {
        TFIDF,
        LSI,
    }
}
