using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPFPrototype
{
    public class Document
    {
        public string OriginalText { get; set; }
        public string Title { get; set; }
        public string PostProcessedText { get; set; }
        public Dictionary<string, int> BagOfWords { get; set; }
        public Dictionary<string, double> TF { get; set; }
        public double TFIDFVectorValue { get; set; }
        public double Similarity { get; set; }
        public double LsiSimilarity { get; set; }
    }
}
