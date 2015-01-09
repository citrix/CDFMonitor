using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CDFM.Gui
{
    /// <summary>
    /// Interaction logic for TimedSaveDialog.xaml
    /// </summary>
    public partial class TimedSaveDialog : Window
    {
        const int _timerSecs = 10;
        private Results _result;
        private ManualResetEvent _timedOut;
        private DispatcherTimer _timer;
        private EventHandler _handler;

        public enum Results
        {
            Unknown,
            Save,
            SaveAs,
            DontSave,
            Disable
        }

        public TimedSaveDialog()
        {
            InitializeComponent();
        }

        private void StartTimer()
        {

            _timer = new DispatcherTimer();
            _handler = new EventHandler(OnTimedEvent);
            _result = Results.DontSave;
            _timer.Tick += _handler;
            _timer.Interval = TimeSpan.FromSeconds(_timerSecs);
            _timer.Start();
                
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            _timer.Tick -= _handler;
            _timer.Stop();
            Disable();
            this.Close();
          
        }

        public void Disable()
        {
            this.Hide();
        }

        public void Enable()
        {

            _timedOut = new ManualResetEvent(false);
            StartTimer();
            
        }

        public Results WaitForResult()
        {
            this.ShowDialog();
            if(_timedOut != null)
            {
                _timedOut.Reset();
            }

            return _result;
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            _result = Results.Save;
            OnTimedEvent(null, null);
        }

        private void buttonSaveAs_Click(object sender, RoutedEventArgs e)
        {
            _result = Results.SaveAs;
            OnTimedEvent(null, null);
        }

        private void buttonDontSave_Click(object sender, RoutedEventArgs e)
        {
            _result = Results.DontSave;
            OnTimedEvent(null, null);
        }

        private void buttonDisable_Click(object sender, RoutedEventArgs e)
        {
            _result = Results.Disable;
            OnTimedEvent(null, null);
        }

    }
}
