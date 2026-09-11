namespace AiChatClient.Services
{
    /// <summary>
    /// 瀵硅瘽妗嗘湇鍔℃娊璞°€?    /// ViewModel 閫氳繃璇ユ娊璞¤姹傜敤鎴疯緭鍏ワ紝淇濇寔涓庡叿浣?View 瑙ｈ€︺€佸彲娴嬭瘯銆?    /// </summary>
    public interface IDialogService
    {
        void ShowError(string title, string message);
        /// <summary>
        /// 寮瑰嚭杈撳叆瀵硅瘽妗嗐€?        /// </summary>
        /// <param name="title">绐楀彛鏍囬銆?/param>
        /// <param name="prompt">鎻愮ず鏂囨湰銆?/param>
        /// <param name="defaultValue">杈撳叆妗嗛濉殑榛樿鍊笺€?/param>
        /// <returns>鐢ㄦ埛杈撳叆鐨勬枃鏈紱鍙栨秷鎴栫洿鎺ュ叧闂獥鍙ｆ椂杩斿洖 <c>null</c>銆?/returns>
        string? ShowInputDialog(string title, string prompt, string defaultValue = "");

        /// <summary>
        /// 选择一个 Markdown 知识文件；取消时返回 <c>null</c>。
        /// </summary>
        string? ShowMarkdownFileDialog();
    }
}
