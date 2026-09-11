using System.Windows;
using AiChatClient.Views.Dialogs;
using Microsoft.Win32;

namespace AiChatClient.Services.Impl
{
    /// <summary>
    /// 鍩轰簬 WPF 绐楀彛鐨勫璇濇鏈嶅姟瀹炵幇銆?    /// 灞炰簬灞曠ず灞傛湇鍔★紝閫氳繃 DI 娉ㄥ唽涓?<see cref="IDialogService"/>銆?    /// </summary>
    public class DialogService : IDialogService
    {
        public void ShowError(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        public string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new InputDialog(title, prompt, defaultValue)
            {
                Owner = Application.Current.MainWindow
            };

            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }

        public string? ShowMarkdownFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "添加 Markdown 知识文件",
                Filter = "Markdown 文件 (*.md)|*.md",
                CheckFileExists = true,
                Multiselect = false
            };

            return dialog.ShowDialog(Application.Current.MainWindow) == true
                ? dialog.FileName
                : null;
        }
    }
}
