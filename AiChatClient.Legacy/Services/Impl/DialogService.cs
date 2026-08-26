using System.Windows;
using AiChatClient.Views.Dialogs;

namespace AiChatClient.Services.Impl
{
    /// <summary>
    /// 鍩轰簬 WPF 绐楀彛鐨勫璇濇鏈嶅姟瀹炵幇銆?    /// 灞炰簬灞曠ず灞傛湇鍔★紝閫氳繃 DI 娉ㄥ唽涓?<see cref="IDialogService"/>銆?    /// </summary>
    public class DialogService : IDialogService
    {
        public string? ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var dialog = new InputDialog(title, prompt, defaultValue)
            {
                Owner = Application.Current.MainWindow
            };

            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }
    }
}
