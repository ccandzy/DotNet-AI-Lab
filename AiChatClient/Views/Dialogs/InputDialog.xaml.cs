using System.Windows;

namespace AiChatClient.Views.Dialogs
{
    /// <summary>
    /// 閫氱敤鍗曡杈撳叆瀵硅瘽妗嗭紙绾?View锛屼笉鍚笟鍔￠€昏緫锛夈€?    /// 閫氳繃 <see cref="InputText"/> 瀵瑰鏆撮湶杈撳叆缁撴灉銆?    /// </summary>
    public partial class InputDialog : Window
    {
        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();

            Title = title;
            PromptText.Text = prompt;
            InputTextBox.Text = defaultValue;

            Loaded += (_, _) =>
            {
                InputTextBox.Focus();
                InputTextBox.SelectAll();
            };
        }

        /// <summary>
        /// 鐢ㄦ埛杈撳叆鐨勬枃鏈€?        /// </summary>
        public string InputText => InputTextBox.Text;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
