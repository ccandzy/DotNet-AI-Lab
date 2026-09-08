using CommunityToolkit.Mvvm.ComponentModel;

namespace AiChatClient.Models.Rag;

/// <summary>
/// Small UI projection for one imported Markdown file.
/// </summary>
public partial class KnowledgeFileItem : ObservableObject
{
    public KnowledgeFileItem(string sourcePath)
    {
        SourcePath = sourcePath;
        FileName = Path.GetFileName(sourcePath);
    }

    public string SourcePath { get; }

    public string FileName { get; }

    [ObservableProperty]
    private string _status = "等待处理";

    [ObservableProperty]
    private int _chunkCount;

    [ObservableProperty]
    private bool _isVectorized;

    [ObservableProperty]
    private bool _isProcessing;
}
