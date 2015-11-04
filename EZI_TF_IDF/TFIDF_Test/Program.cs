using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;

namespace TFIDF_Test
{
    class Program
    {
        private static List<string> documents = new List<string>();
        private static List<string> keywords = new List<string>();

        private static List<DocumentRep> documentsReps = new List<DocumentRep>();
        private static Dictionary<string, double> IDF = new Dictionary<string, double>();
        private static Dictionary<string, int> bagOfWords = new Dictionary<string, int>();
        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader("documents.txt"))
            {
                documents = Regex.Split(sr.ReadToEnd(), "\r\n\r\n").ToList();
            }

            using (StreamReader sr = new StreamReader("keywords.txt"))
            {
                keywords = Regex.Split(sr.ReadToEnd(), "\r\n").ToList();
            }

            foreach (var doc in documents)
            {
                var docRep = new DocumentRep();
                docRep.Title = Regex.Split(doc, "\r\n").FirstOrDefault();
                docRep.OriginalText = string.Concat(Regex.Split(doc, "\r\n").Skip(1));
                docRep.PostProcessedText = string.Concat(Regex.Replace(doc.ToLower(), @"[^\w\s]", "").Split(null).Select(s => _ps.stemTerm(s) + " "));
                docRep.BagOfWords = new Dictionary<string, int>();
                foreach (var keyword in keywords)
                {
                    var temp = _ps.stemTerm(keyword);
                    docRep.BagOfWords[keyword] = docRep.PostProcessedText.Split(null).Count(s => s.Equals(_ps.stemTerm(temp)));
                }
                documentsReps.Add(docRep);

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
            }

            foreach (var keyword in keywords)
            {
                var count = (double)documentsReps.Count;
                var contains = documentsReps.Count(d => d.BagOfWords.Any(w => w.Key.Equals(keyword) && w.Value > 0));
                IDF[keyword] = contains > 0 ? Math.Log10(count / contains) : 0;
            }

            foreach (var docRep in documentsReps)
            {
                docRep.TFIDFValue = DistanceN(docRep.TF.Select(tf => IDF[tf.Key] * tf.Value));
            }

            //LSI
            //double[,] termsDocsArray = new double[keywords.Count, documentsReps.Count];
            //for (int i = 0; i < keywords.Count; i++)
            //{
            //    for (int j = 0; j < documentsReps.Count; j++)
            //    {
            //        termsDocsArray[i, j] = documentsReps[j].BagOfWords[keywords[i]];
            //    }
            //}
            double[,] termsDocsArray = new double[6, 5]
            {
                { 1, 0, 1, 0, 0},
                { 0, 1, 0, 0, 0},
                { 1, 1, 0, 0, 0},
                { 1, 0, 0, 1, 1},
                { 0, 0, 0, 1, 0},
                { 0, 0, 0, 1, 0},
            };

            Matrix<double> termsDocsMatrix = Matrix<double>.Build.DenseOfArray(termsDocsArray);

            var svd = termsDocsMatrix.Svd();
            var K = svd.U;
            var S = Matrix<double>.Build.DenseOfDiagonalArray(svd.S.ToArray());
            var DT = svd.VT.SubMatrix(0, S.RowCount, 0, svd.VT.ColumnCount);
            var D = DT.Transpose();
            int reductionCount = 2;

            var S_s = S.SubMatrix(0, S.RowCount - reductionCount, 0, S.ColumnCount - reductionCount);
            var K_s = K.SubMatrix(0, K.RowCount, 0, K.ColumnCount - Math.Abs(K.ColumnCount - S_s.ColumnCount));
            var D_sT = DT.SubMatrix(0, DT.RowCount - reductionCount, 0, DT.ColumnCount);
            var D_s = D_sT.Transpose();

            var result = K_s * S_s * D_sT;

            //query
            string originalQuery = Console.ReadLine();

            string processedQuery = string.Concat(Regex.Replace(originalQuery.ToLower(), @"[^\w\s]", "").Split(null).Select(s => _ps.stemTerm(s) + " "));
            var queryBagOfWords = new Dictionary<string, int>();
            foreach (var keyword in keywords)
            {
                var temp = _ps.stemTerm(keyword);
                queryBagOfWords[keyword] = processedQuery.Split(null).Count(s => s.Equals(_ps.stemTerm(temp)));
            }
            var queryTF = new Dictionary<string, double>();
            var queryTFMax = queryBagOfWords.Max(w => w.Value);
            foreach (var word in queryBagOfWords)
            {
                if (queryTFMax <= 0)
                {
                    queryTF[word.Key] = 0;
                }
                else
                {
                    queryTF[word.Key] = (double)queryBagOfWords[word.Key] / queryTFMax;
                }
            }
            var queryTFIDFValue = DistanceN(queryTF.Select(tf => IDF[tf.Key] * tf.Value));
            //end query processing

            //LSI

            //Vector<double> qT = Vector<double>.Build.DenseOfArray(queryBagOfWords.Select(w => (double)w.Value).ToArray());//new double[] { 0, 1, 0, 0, 1 });
            Vector<double> qT = Vector<double>.Build.DenseOfArray(new double[] { 0, 1, 0, 0, 1 });

            var transformedQuery = qT * K_s * S_s.Inverse();

            var queryValue = DistanceN(transformedQuery);


            var docValues = new double[D_s.RowCount];
            for (int i = 0; i < docValues.Length; i++)
            {
                docValues[i] = DistanceN(D_s.Row(i));
            }

            var sumOfProducts = new double[D_s.RowCount];
            for (int i = 0; i < D.RowCount; i++)
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
                documentsReps[i].LsiSIM = sim[i];
            }
            //LSI end


            //Similarity
            foreach (var docRep in documentsReps)
            {
                var x = docRep.TFIDFValue * queryTFIDFValue;
                double val = 0;
                foreach (var keyword in keywords)
                {
                    val += docRep.TF[keyword] * IDF[keyword] * queryTF[keyword] * IDF[keyword];
                }
                docRep.Sim = x > 0 ? val / x : 0;
            }

            var order = documentsReps.OrderByDescending(d => d.Sim).Take(10);

            //print result
            foreach (var doc in order)
            {
                Console.WriteLine("{0}\t{1}", doc.Title, doc.Sim);
            }

            Console.WriteLine("##############");
            var orderLSI = documentsReps.OrderByDescending(d => d.LsiSIM).Take(10);

            //print result
            foreach (var doc in orderLSI)
            {
                Console.WriteLine("{0}\t{1}", doc.Title, doc.LsiSIM);
            }

            calcMatrix();
            calcLSI();


            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static PorterStemmer _ps = new PorterStemmer();
        static private bool CompareTerms(string a, string b)
        {
            return _ps.stemTerm(a.ToLower()).Equals(_ps.stemTerm(b.ToLower()));
        }

        static double DistanceN(IEnumerable<double> first)
        {
            var sum = first.Select((x) => x * x).Sum();
            return Math.Sqrt(sum);
        }

        private static void calcMatrix()
        {
            //lista wszystkich wyrazów
            List<string> allWords = new List<string>();
            foreach (var doc in documents)
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
            double[,] docsWordsArray = new double[documents.Count, allWords.Count];
            for (int i = 0; i < documents.Count; i++)
            {
                var wordsInDoc = GetWords(documents[i]);
                for (int j = 0; j < allWords.Count; j++)
                {
                    var wordsCount = wordsInDoc.Count(w => w.Equals(allWords[j]));
                    docsWordsArray[i, j] = wordsCount;
                }
            }
            var docsWordsMatrix = Matrix<double>.Build.DenseOfArray(docsWordsArray);
            var wordsWordsMatrix = docsWordsMatrix.Transpose().Multiply(docsWordsMatrix);
            wordsWordsMatrix = wordsWordsMatrix.NormalizeRows(1.0);

            var inputWords = Console.ReadLine();

            List<CorrelatedWord> correlatedWords = new List<CorrelatedWord>();
            bool _first = true;
            foreach (var inputWord in GetWords(inputWords))
            {
                if (_first)
                {
                    correlatedWords = GetCorrelateedWords(inputWord, allWords, wordsWordsMatrix).ToList();
                }
                else
                {
                    correlatedWords = GetCorrelateedWords(inputWord, allWords, wordsWordsMatrix).Where(c => correlatedWords.Any(w => w.Word.Equals(c.Word))).ToList();
                }

                _first = false;
            }
            var stemedWords = GetWords(inputWords).Select(w => _ps.stemTerm(w));
            var filteredWords = correlatedWords
                .Where(w => !string.IsNullOrWhiteSpace(w.Word))
                //.Where(w => !stemedWords.Contains(_ps.stemTerm(w.Word)))
                .Where(w => !inputWords.Contains(w.Word))
                .Where(w => w.Correlation >= 0.01)
                .Where(w => !_stopWords.Contains(w.Word));
            filteredWords.GroupBy(c => c.Word).Select(x => new CorrelatedWord() { Word = x.Key, Correlation = x.Sum(c => c.Correlation) });

            var orderedResult = filteredWords.GroupBy(c => c.Word)
                .Select(x => new CorrelatedWord() { Word = x.Key, Correlation = x.Sum(c => c.Correlation) })
                .OrderByDescending(w => w.Correlation);

            foreach (var cw in orderedResult)
            {
                Console.WriteLine("WORD: {0}\t{1}", cw.Word, cw.Correlation);
            }

            double[,] docsTermsArray = new double[documentsReps.Count, keywords.Count];

            for (int i = 0; i < documentsReps.Count; i++)
            {
                for (int j = 0; j < keywords.Count; j++)
                {
                    docsTermsArray[i, j] = documentsReps[i].BagOfWords.ElementAt(j).Value > 0 ? 1 : 0;
                }
            }

            Matrix<double> docTermsMatrix = Matrix<double>.Build.DenseOfArray(docsTermsArray);
            var result = docTermsMatrix.Transpose().Multiply(docTermsMatrix);

            for (int i = 0; i < 120; i++)
            {
                var max = result.Row(i).Max();
                for (int j = 0; j < 120; j++)
                {
                    if (max > 0)
                    {
                        result[i, j] = result[i, j] / max;
                    }
                }
            }
        }

        private static void calcLSI()
        {

        }

        public static string[] GetWords(string document)
        {
            return Regex.Replace(document.ToLower(), @"[^\w\s]", "").Split(null).Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();
        }
        private static List<CorrelatedWord> GetCorrelateedWords(string inputWord, List<string> allWords, Matrix<double> wordsWordsMatrix)
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

        class CorrelatedWord
        {
            public string Word { get; set; }
            public double Correlation { get; set; }
        }

        class DocumentRep
        {
            public string OriginalText { get; set; }
            public string Title { get; set; }
            public string PostProcessedText { get; set; }
            public Dictionary<string, int> BagOfWords { get; set; }
            public Dictionary<string, double> TF { get; set; }
            public List<string> Words { get; set; }
            public double TFIDFValue { get; set; }
            public double Sim { get; set; }
            public double LsiSIM { get; set; }
        }
    }
}
