using System.Windows;

namespace Rusgeocom
{
    /// <summary>
    /// Interaction logic for StartProductIdWindow.xaml
    /// </summary>
    public partial class StartProductIdWindow : Window
    {
        public int StartId => int.Parse(txtStartId.Text);

        public StartProductIdWindow()
        {
            InitializeComponent();
        }
    }
}
