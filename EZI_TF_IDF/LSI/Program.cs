//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
////using Bluebit.MatrixLibrary;
//using System.Collections;
//using System.Text.RegularExpressions;
//using MathNet.Numerics.Statistics;
//using MathNet.Numerics.LinearAlgebra;
//using MathNet.Numerics.LinearAlgebra.Complex;

//namespace LSI
//{
//    class Program
//    {
//        static double[,] A; //term-document Array
//        static double[] q; //term-query Array
//        static List<string> wordlist = new List<string>(); //List of terms found in documents
//        static SortedDictionary<string,string> sortedList = new SortedDictionary<string, string>(); //Documents ranked by VSM with angle value
//        static string[] docs ={"gold silver truck", //Query
//                                "shipment of gold damaged in a fire",//Doc 1
//                                "delivery of silver arrived in a silver truck", //Doc 2
//                                "shipment of gold arrived in a truck"}; //Doc 3

//        static void Main(string[] args)
//        {
//            createWordList();
//            createVector();
//            LatentSemanticIndexing();

//            foreach (KeyValuePair<string, string> kvp in sortedList)
//            {

//                Console.WriteLine(kvp.Value + " " +kvp.Key);
//            }
//            Console.ReadLine();
//        }

//        public static void createWordList()
//        {
//            foreach (string doc in docs)
//            {
//                wordlist = getWordList(wordlist, doc);
//            }
//            wordlist.Sort(); //sort the wordlist alphabetically
//        }

//        public static List<string> getWordList(List<string> wordlist, string query)
//        {
//            Regex exp = new Regex("\\w +", RegexOptions.IgnoreCase);
//            MatchCollection MCollection = exp.Matches(query);

//            foreach (Match match in MCollection)
//            {
//                if (!wordlist.Contains(match.Value))
//                {
//                    wordlist.Add(match.Value);
//                }
//            }

//            return wordlist;
//        }

//        public static void createVector()
//        {
//            double[] queryvector;
//            q = new double[wordlist.Count];
//            A = new double[wordlist.Count, docs.Length - 1];
//            for (int j = 0; j < docs.Length; j++)
//            {
//                queryvector = new double[wordlist.Count];

//                for (int i = 0; i < wordlist.Count; i++)
//                {
//                    //calculate Term Frequency
//                    double tf = getTF(docs[j], wordlist[i]);

//                    if (j == 0) //if Query term then add it to query array
//                    {
//                        q[i] = tf;
//                    }
//                    else //if document term then add it to document array
//                    {
//                        A[i, j - 1] = tf;
//                    }
//                }

//            }

//        }

//        private static void LatentSemanticIndexing()
//        {
//            //Singular Value Decomposition
//            Matrix docMatrix = Matrix.Build.Dense(A);
//            var svd = docMatrix.Svd();

//            //A = U S VT
//            var U = svd.U;
//            var S = svd.S;
//            //svd.W
//            var V = svd.VT.Transpose();
//            var VT = svd.VT;

//            //Dimensionality Reduction: Computing Uk, Sk, Vk and VkT
//            var Uk = U.SubMatrix(0, U.RowCount, 0, U.ColumnCount - 1);
//            var Sk = S.SubVector(0, S.Count-1);//(S.ToArray(), S.Count - 1, S.Count - 1);
//            var Vk = V.SubMatrix(0, V.RowCount, 0, V.ColumnCount - 1);
//            var VkT = Vk.Transpose();

//            //q = qT Uk Sk-1
//            Matrix queryMatrix = new Matrix(q, q.Length, 1);
//            queryMatrix = queryMatrix.Transpose() * Uk * Sk.Reverse();

//            //sim(q, d) = sim(qT Uk Sk-1, dT Uk Sk-1) using cosine similarities
//            for (int i = 0; i < V.Rows; i++)
//            {
//                Vector docVector = Vk.RowVector(i);
//                Vector queryVector = queryMatrix.RowVector(0);
//                double sim = Vector.DotProduct(docVector, queryVector) / (docVector.Length * queryVector.Length);

//                Console.WriteLine("Doc " +(i + 1).ToString() + " :" +sim);
//            }

//        }
//        private static double getTF(string document, string term)
//        {
//            string[] queryTerms = Regex.Split(document, "\\s");
//            double count = 0;

//            foreach (string t in queryTerms)
//            {
//                if (t == term)
//                {
//                    count++;
//                }
//            }
//            return count;

//        }

//    }
//}