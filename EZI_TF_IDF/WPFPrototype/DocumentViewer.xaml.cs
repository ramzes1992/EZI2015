using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WPFPrototype
{
    /// <summary>
    /// Interaction logic for DocumentViewer.xaml
    /// </summary>
    public partial class DocumentViewer : Window
    {
        public DocumentViewer(Document documnet)
        {
            InitializeComponent();
            v_TextBlock_Title.Text = documnet.Title;
            v_TextBlock_OriginalText.Text = documnet.OriginalText;
            v_TextBlock_ProcessedText.Text = documnet.PostProcessedText;
            v_TextBlock_Keywords.Text = string.Concat(documnet.BagOfWords.Where(s => s.Value > 0)
                                                                         .OrderByDescending(s => s.Value).Select(s => s.Key + ", "));
        }
    }
}
