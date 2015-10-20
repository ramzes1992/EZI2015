using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                    if(tfMax <= 0)
                    {
                        docRep.TF[word.Key] = 0;
                    }
                    else
                    {
                        docRep.TF[word.Key] = (double)docRep.BagOfWords[word.Key] / tfMax;
                    }
                }
            }

            foreach(var keyword in keywords)
            {
                var count = (double)documentsReps.Count;
                var contains = documentsReps.Count(d => d.BagOfWords.Any(w => w.Key.Equals(keyword) && w.Value > 0));
                IDF[keyword] = contains > 0 ? Math.Log10( count / contains) : 0;
            }

            foreach(var docRep in documentsReps)
            {
                docRep.TFIDFValue = DistanceN(docRep.TF.Select(tf => IDF[tf.Key] * tf.Value));
            }

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

            //Similarity
            foreach (var docRep in documentsReps)
            {
                var x = docRep.TFIDFValue * queryTFIDFValue;
                double val = 0;
                foreach(var keyword in keywords)
                {
                    val += docRep.TF[keyword] * IDF[keyword] * queryTF[keyword] * IDF[keyword];
                }
                docRep.Sim = x > 0 ? val/x : 0;
            }

            var order = documentsReps.OrderByDescending(d => d.Sim).Take(10);

            //print result
            foreach(var doc in order)
            {
                Console.WriteLine("{0}\t{1}", doc.Title, doc.Sim);
            }


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
            var sum = first.Select((x) => x*x).Sum();
            return Math.Sqrt(sum);
        }
    }

    class DocumentRep
    {
        public string OriginalText { get; set; }
        public string Title { get; set; }
        public string PostProcessedText { get; set; }
        public Dictionary<string, int> BagOfWords { get; set; }
        public Dictionary<string, double> TF { get; set; }
        public double TFIDFValue { get; set; }
        public double Sim { get; set; }
    }
}
