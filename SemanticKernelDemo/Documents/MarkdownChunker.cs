using System.Text;

namespace SemanticKernelDemo.Documents;

/// <summary>
/// 表示从一个 Markdown 文件中切分出来的单个文本块。
/// </summary>
/// <param name="Index">
/// Chunk 在当前文档中的顺序编号，从 1 开始。后续写入向量库时，可以用它恢复原始顺序。
/// </param>
/// <param name="SourcePath">
/// Markdown 源文件的绝对路径。保留来源信息后，检索命中时可以追溯到原始文档。
/// </param>
/// <param name="HeadingPath">
/// Chunk 所在位置的完整标题路径，例如“SelfCheckTool 项目理解 &gt; 4. 串口协议理解 &gt; 4.1 接收帧边界”。
/// 标题路径单独保存，后续可以作为检索元数据，也可以与 Content 拼接后再生成 Embedding。
/// </param>
/// <param name="StartLine">Chunk 在源文件中的起始行号，从 1 开始。</param>
/// <param name="EndLine">Chunk 在源文件中的结束行号，从 1 开始。</param>
/// <param name="Content">Chunk 的 Markdown 原文。方法不会主动删除列表、表格、代码围栏等 Markdown 标记。</param>
public sealed record MarkdownChunk(
    int Index,
    string SourcePath,
    string HeadingPath,
    int StartLine,
    int EndLine,
    string Content);

/// <summary>
/// 读取 Markdown 文件，并优先按照 Markdown 的标题和段落结构切分文本。
/// </summary>
/// <remarks>
/// <para>
/// 这个类型只负责“读取 + Chunking”，不依赖 Semantic Kernel、Embedding 模型或向量数据库。
/// 保持这一层纯粹，可以让后续的向量化、入库和检索分别独立测试。
/// </para>
/// <para>
/// 当前版本使用字符数控制 Chunk 大小，而不是 Token 数。这样无需绑定某个模型的 Tokenizer；
/// 真正接入固定的 Embedding 模型后，可以再把长度策略替换为该模型对应的 Token 计数。
/// </para>
/// </remarks>
public static class MarkdownChunker
{
    /// <summary>
    /// 异步读取一个 UTF-8 Markdown 文件，并返回按文档结构排序的 Chunk 集合。
    /// </summary>
    /// <param name="filePath">
    /// Markdown 文件路径，可以是绝对路径，也可以是相对于当前进程工作目录的相对路径。
    /// 示例：<c>D:\ObsidianValut\SelfCheck\SelfCheckTool-项目理解.md</c>。
    /// </param>
    /// <param name="maxChunkCharacters">
    /// 单个 Chunk 建议容纳的最大字符数，默认 1200。
    /// 这是“软上限”：为了不破坏完整代码块或 Markdown 表格，受保护的块可能略大于该值。
    /// </param>
    /// <param name="overlapCharacters">
    /// 相邻 Chunk 之间希望保留的上下文字符数，默认 120。
    /// 重叠只复制完整段落，不会为了凑足字符数而从段落中间截取。
    /// </param>
    /// <param name="cancellationToken">用于取消文件读取操作。</param>
    /// <returns>
    /// 只读 Chunk 集合。空文件或仅包含空白字符的文件会返回空集合。
    /// </returns>
    /// <exception cref="ArgumentException">
    /// 文件路径为空、文件不是 Markdown，或者 Chunk 参数不合法时抛出。
    /// </exception>
    /// <exception cref="FileNotFoundException">指定的 Markdown 文件不存在时抛出。</exception>
    /// <remarks>
    /// 处理顺序如下：
    /// <list type="number">
    /// <item><description>检查路径和分块参数。</description></item>
    /// <item><description>使用 UTF-8 异步读取全部行。</description></item>
    /// <item><description>识别 YAML Front Matter 和 Markdown 标题层级。</description></item>
    /// <item><description>在每个标题章节内部按空行形成段落块。</description></item>
    /// <item><description>将段落组合到目标大小，并在相邻 Chunk 间保留少量完整段落重叠。</description></item>
    /// </list>
    /// </remarks>
    public static async Task<IReadOnlyList<MarkdownChunk>> ReadAndChunkMarkdownAsync(
        string filePath,
        int maxChunkCharacters = 1200,
        int overlapCharacters = 120,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Markdown 文件路径不能为空。", nameof(filePath));
        }

        // 过小的上限会产生大量几乎没有语义的信息碎片，因此设置一个保守的最低值。
        if (maxChunkCharacters < 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxChunkCharacters),
                maxChunkCharacters,
                "单个 Chunk 的最大字符数不能小于 200。");
        }

        // overlap 必须小于 Chunk 上限，否则每个新 Chunk 可能只剩重复内容而无法向前推进。
        if (overlapCharacters < 0 || overlapCharacters >= maxChunkCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapCharacters),
                overlapCharacters,
                "重叠字符数必须大于等于 0，并且小于单个 Chunk 的最大字符数。");
        }

        var fullPath = Path.GetFullPath(filePath);
        var extension = Path.GetExtension(fullPath);

        if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"只支持 .md 或 .markdown 文件，当前文件扩展名为“{extension}”。",
                nameof(filePath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到要切分的 Markdown 文件。", fullPath);
        }

        // File.ReadAllLinesAsync 会正确处理 UTF-8 BOM；显式指定 UTF-8，避免受系统默认编码影响。
        var lines = await File.ReadAllLinesAsync(
            fullPath,
            Encoding.UTF8,
            cancellationToken);

        if (lines.Length == 0 || lines.All(string.IsNullOrWhiteSpace))
        {
            return Array.Empty<MarkdownChunk>();
        }

        var documentName = Path.GetFileNameWithoutExtension(fullPath);

        // 第一阶段只分析文档结构：每个 Section 对应 YAML Front Matter、标题章节或标题前的正文。
        var sections = ParseSections(lines, documentName);
        var drafts = new List<ChunkDraft>();

        foreach (var section in sections)
        {
            // 第二阶段在章节内部按段落分块。标题边界永远不会被跨越，便于保留准确的标题路径。
            var blocks = CreateBlocks(section);
            var normalizedBlocks = blocks
                .SelectMany(block => SplitOversizedBlock(block, maxChunkCharacters))
                .ToArray();

            CombineBlocksIntoChunks(
                normalizedBlocks,
                section.HeadingPath,
                maxChunkCharacters,
                overlapCharacters,
                drafts);
        }

        // 直到全部分块结束后再统一编号，保证编号连续，并与返回集合中的实际顺序完全一致。
        return drafts
            .Select((draft, index) => new MarkdownChunk(
                Index: index + 1,
                SourcePath: fullPath,
                HeadingPath: draft.HeadingPath,
                StartLine: draft.StartLine,
                EndLine: draft.EndLine,
                Content: draft.Content))
            .ToArray();
    }

    /// <summary>
    /// 按 YAML Front Matter 和 Markdown 标题，把整篇文档解析成互不跨标题的章节。
    /// </summary>
    private static IReadOnlyList<MarkdownSection> ParseSections(
        IReadOnlyList<string> lines,
        string documentName)
    {
        var sections = new List<MarkdownSection>();
        var nextLineIndex = 0;

        // Obsidian 文档经常使用 --- 包围 YAML 元数据。
        // Front Matter 作为独立章节保存，既不丢失元数据，也不会污染第一个正文标题的层级。
        if (string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
        {
            var closingIndex = -1;

            for (var index = 1; index < lines.Count; index++)
            {
                if (string.Equals(lines[index].Trim(), "---", StringComparison.Ordinal))
                {
                    closingIndex = index;
                    break;
                }
            }

            // 只有找到成对的结束标记才把它当作 Front Matter；否则按普通正文处理，避免误删内容。
            if (closingIndex >= 0)
            {
                sections.Add(new MarkdownSection(
                    HeadingPath: $"{documentName} > YAML Front Matter",
                    Lines: CreateSourceLines(lines, 0, closingIndex)));

                nextLineIndex = closingIndex + 1;
            }
        }

        // 数组下标分别代表 # 到 ######。遇到较浅标题时，会清除它下面已经失效的深层标题。
        var headingLevels = new string?[6];
        var currentHeadingPath = documentName;
        var currentLines = new List<SourceLine>();
        FenceState? activeFence = null;

        for (var index = nextLineIndex; index < lines.Count; index++)
        {
            var line = lines[index];

            // 代码围栏内部即使出现“# 示例”，也应当视为代码内容而不是 Markdown 标题。
            if (activeFence is null && TryReadHeading(line, out var level, out var title))
            {
                FlushSection(sections, currentHeadingPath, currentLines);

                headingLevels[level - 1] = title;
                for (var deeperLevel = level; deeperLevel < headingLevels.Length; deeperLevel++)
                {
                    headingLevels[deeperLevel] = null;
                }

                currentHeadingPath = string.Join(
                    " > ",
                    headingLevels.Where(value => !string.IsNullOrWhiteSpace(value))!);
            }

            currentLines.Add(new SourceLine(index + 1, line));
            UpdateFenceState(line, ref activeFence);
        }

        FlushSection(sections, currentHeadingPath, currentLines);
        return sections;
    }

    /// <summary>
    /// 把一个章节按空行拆成语义块，同时保证代码围栏内部的空行不会触发拆分。
    /// </summary>
    private static IReadOnlyList<MarkdownBlock> CreateBlocks(MarkdownSection section)
    {
        var blocks = new List<MarkdownBlock>();
        var currentLines = new List<SourceLine>();
        FenceState? activeFence = null;

        foreach (var sourceLine in section.Lines)
        {
            // 普通正文中的空行是自然段落边界；代码块中的空行属于代码本身，必须原样保留。
            if (activeFence is null && string.IsNullOrWhiteSpace(sourceLine.Text))
            {
                FlushBlock(blocks, currentLines);
                continue;
            }

            currentLines.Add(sourceLine);
            UpdateFenceState(sourceLine.Text, ref activeFence);
        }

        FlushBlock(blocks, currentLines);
        return blocks;
    }

    /// <summary>
    /// 对超过目标大小的普通段落继续拆分；代码块和 Markdown 表格保持完整。
    /// </summary>
    private static IEnumerable<MarkdownBlock> SplitOversizedBlock(
        MarkdownBlock block,
        int maxChunkCharacters)
    {
        if (block.Content.Length <= maxChunkCharacters || block.IsProtected)
        {
            yield return block;
            yield break;
        }

        var currentParts = new List<MarkdownBlock>();
        var currentLength = 0;

        foreach (var sourceLine in block.Lines)
        {
            // 极长单行常见于没有换行的中文段落。先按标点或空白位置切成不超过上限的小片段。
            foreach (var part in SplitLongLine(sourceLine, maxChunkCharacters))
            {
                var separatorLength = currentParts.Count == 0 ? 0 : Environment.NewLine.Length;

                if (currentParts.Count > 0
                    && currentLength + separatorLength + part.Content.Length > maxChunkCharacters)
                {
                    yield return MergeBlocks(currentParts, Environment.NewLine);
                    currentParts.Clear();
                    currentLength = 0;
                    separatorLength = 0;
                }

                currentParts.Add(part);
                currentLength += separatorLength + part.Content.Length;
            }
        }

        if (currentParts.Count > 0)
        {
            yield return MergeBlocks(currentParts, Environment.NewLine);
        }
    }

    /// <summary>
    /// 把段落块组合成最终 Chunk，并在 Chunk 边界保留少量完整段落作为上下文重叠。
    /// </summary>
    private static void CombineBlocksIntoChunks(
        IReadOnlyList<MarkdownBlock> blocks,
        string headingPath,
        int maxChunkCharacters,
        int overlapCharacters,
        ICollection<ChunkDraft> output)
    {
        var currentBlocks = new List<MarkdownBlock>();
        var currentLength = 0;

        foreach (var block in blocks)
        {
            var separatorLength = currentBlocks.Count == 0
                ? 0
                : Environment.NewLine.Length * 2;
            var projectedLength = currentLength + separatorLength + block.Content.Length;

            if (currentBlocks.Count > 0 && projectedLength > maxChunkCharacters)
            {
                AddChunk(output, headingPath, currentBlocks);

                // 重叠内容只从上一个 Chunk 尾部取完整段落。
                // 如果最后一个段落本身已经超过 overlap 上限，就不复制它，避免新 Chunk 被重复内容占满。
                var overlapBlocks = SelectOverlapBlocks(currentBlocks, overlapCharacters);
                currentBlocks.Clear();
                currentBlocks.AddRange(overlapBlocks);
                currentLength = CalculateCombinedLength(currentBlocks, Environment.NewLine.Length * 2);

                // 重叠段落加上新段落仍然超限时，舍弃重叠而保留真正的新内容。
                separatorLength = currentBlocks.Count == 0 ? 0 : Environment.NewLine.Length * 2;
                if (currentBlocks.Count > 0
                    && currentLength + separatorLength + block.Content.Length > maxChunkCharacters)
                {
                    currentBlocks.Clear();
                    currentLength = 0;
                    separatorLength = 0;
                }
            }

            currentBlocks.Add(block);
            currentLength += separatorLength + block.Content.Length;
        }

        if (currentBlocks.Count > 0)
        {
            AddChunk(output, headingPath, currentBlocks);
        }
    }

    /// <summary>
    /// 从一个超长单行中寻找尽量自然的切点，优先在空白或中英文标点之后断开。
    /// </summary>
    private static IEnumerable<MarkdownBlock> SplitLongLine(
        SourceLine sourceLine,
        int maxChunkCharacters)
    {
        if (sourceLine.Text.Length <= maxChunkCharacters)
        {
            yield return CreateBlock([sourceLine]);
            yield break;
        }

        // 这里使用 string 而不是 ReadOnlySpan<char>：包含 yield return 的迭代器会被编译成状态机，
        // ref struct 类型的 Span 不能跨越 yield 边界保存到状态机字段中。
        var remaining = sourceLine.Text;

        while (remaining.Length > maxChunkCharacters)
        {
            // 只在当前上限内向后寻找切点，并避免为了一个很早的标点产生过小片段。
            var candidate = remaining[..maxChunkCharacters];
            var minimumPreferredIndex = maxChunkCharacters * 3 / 5;
            var splitLength = maxChunkCharacters;

            for (var index = candidate.Length - 1; index >= minimumPreferredIndex; index--)
            {
                if (IsPreferredSplitCharacter(candidate[index]))
                {
                    // 标点本身归入前一个片段，使返回内容保持自然的句子结尾。
                    splitLength = index + 1;
                    break;
                }
            }

            var text = remaining[..splitLength].Trim();
            if (text.Length > 0)
            {
                yield return CreateBlock([new SourceLine(sourceLine.LineNumber, text)]);
            }

            remaining = remaining[splitLength..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return CreateBlock([
                new SourceLine(sourceLine.LineNumber, remaining)
            ]);
        }
    }

    private static bool TryReadHeading(string line, out int level, out string title)
    {
        var trimmed = line.AsSpan().TrimStart();
        level = 0;
        title = string.Empty;

        while (level < trimmed.Length && level < 6 && trimmed[level] == '#')
        {
            level++;
        }

        // CommonMark 的 ATX 标题要求 # 后跟空白；这也避免把“#define”识别成标题。
        if (level == 0 || level >= trimmed.Length || !char.IsWhiteSpace(trimmed[level]))
        {
            level = 0;
            return false;
        }

        title = trimmed[level..].ToString().Trim().TrimEnd('#').Trim();
        return title.Length > 0;
    }

    private static void UpdateFenceState(string line, ref FenceState? activeFence)
    {
        var trimmed = line.AsSpan().TrimStart();
        if (!TryReadFence(trimmed, out var marker, out var length))
        {
            return;
        }

        if (activeFence is null)
        {
            activeFence = new FenceState(marker, length);
            return;
        }

        // 结束围栏必须使用与开始围栏相同的字符，并且长度不能短于开始围栏。
        if (activeFence.Marker == marker && length >= activeFence.Length)
        {
            activeFence = null;
        }
    }

    private static bool TryReadFence(ReadOnlySpan<char> line, out char marker, out int length)
    {
        marker = default;
        length = 0;

        if (line.Length < 3 || (line[0] != '`' && line[0] != '~'))
        {
            return false;
        }

        marker = line[0];
        while (length < line.Length && line[length] == marker)
        {
            length++;
        }

        return length >= 3;
    }

    private static bool IsPreferredSplitCharacter(char value)
    {
        return char.IsWhiteSpace(value)
            || value is '。' or '！' or '？' or '；' or '，'
            or '.' or '!' or '?' or ';' or ',' or ':' or '：'
            or ')' or '）' or ']' or '】';
    }

    private static bool LooksLikeMarkdownTable(IReadOnlyList<SourceLine> lines)
    {
        if (lines.Count < 2 || !lines[0].Text.Contains('|'))
        {
            return false;
        }

        // Markdown 表格的第二行通常形如“| --- | :---: |”。
        // 删除合法的分隔字符后应当不剩其他字符。
        var separator = lines[1].Text
            .Replace("|", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();

        return separator.Length == 0 && lines[1].Text.Contains('-');
    }

    private static IReadOnlyList<MarkdownBlock> SelectOverlapBlocks(
        IReadOnlyList<MarkdownBlock> blocks,
        int overlapCharacters)
    {
        if (overlapCharacters == 0)
        {
            return Array.Empty<MarkdownBlock>();
        }

        var selected = new List<MarkdownBlock>();
        var selectedLength = 0;

        for (var index = blocks.Count - 1; index >= 0; index--)
        {
            var block = blocks[index];
            var separatorLength = selected.Count == 0 ? 0 : Environment.NewLine.Length * 2;

            if (selectedLength + separatorLength + block.Content.Length > overlapCharacters)
            {
                break;
            }

            selected.Insert(0, block);
            selectedLength += separatorLength + block.Content.Length;
        }

        return selected;
    }

    private static void AddChunk(
        ICollection<ChunkDraft> output,
        string headingPath,
        IReadOnlyList<MarkdownBlock> blocks)
    {
        var content = string.Join(
            Environment.NewLine + Environment.NewLine,
            blocks.Select(block => block.Content));

        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        output.Add(new ChunkDraft(
            HeadingPath: headingPath,
            StartLine: blocks.Min(block => block.StartLine),
            EndLine: blocks.Max(block => block.EndLine),
            Content: content));
    }

    private static int CalculateCombinedLength(
        IReadOnlyCollection<MarkdownBlock> blocks,
        int separatorLength)
    {
        return blocks.Sum(block => block.Content.Length)
            + Math.Max(0, blocks.Count - 1) * separatorLength;
    }

    private static MarkdownBlock MergeBlocks(
        IReadOnlyList<MarkdownBlock> blocks,
        string separator)
    {
        var mergedLines = blocks.SelectMany(block => block.Lines).ToArray();
        return new MarkdownBlock(
            StartLine: blocks.Min(block => block.StartLine),
            EndLine: blocks.Max(block => block.EndLine),
            Content: string.Join(separator, blocks.Select(block => block.Content)),
            Lines: mergedLines,
            IsProtected: blocks.Any(block => block.IsProtected));
    }

    private static MarkdownBlock CreateBlock(IReadOnlyList<SourceLine> lines)
    {
        var content = string.Join(Environment.NewLine, lines.Select(line => line.Text));
        var containsFence = lines.Any(line =>
            TryReadFence(line.Text.AsSpan().TrimStart(), out _, out _));

        return new MarkdownBlock(
            StartLine: lines[0].LineNumber,
            EndLine: lines[^1].LineNumber,
            Content: content,
            Lines: lines.ToArray(),
            IsProtected: containsFence || LooksLikeMarkdownTable(lines));
    }

    private static void FlushSection(
        ICollection<MarkdownSection> sections,
        string headingPath,
        ICollection<SourceLine> currentLines)
    {
        if (currentLines.Any(line => !string.IsNullOrWhiteSpace(line.Text)))
        {
            sections.Add(new MarkdownSection(headingPath, currentLines.ToArray()));
        }

        currentLines.Clear();
    }

    private static void FlushBlock(
        ICollection<MarkdownBlock> blocks,
        ICollection<SourceLine> currentLines)
    {
        if (currentLines.Count > 0)
        {
            blocks.Add(CreateBlock(currentLines.ToArray()));
            currentLines.Clear();
        }
    }

    private static IReadOnlyList<SourceLine> CreateSourceLines(
        IReadOnlyList<string> lines,
        int startIndex,
        int endIndex)
    {
        var result = new List<SourceLine>(endIndex - startIndex + 1);
        for (var index = startIndex; index <= endIndex; index++)
        {
            result.Add(new SourceLine(index + 1, lines[index]));
        }

        return result;
    }

    // 以下私有 record 只在切块过程中承载中间状态，不会暴露给调用方。
    private sealed record SourceLine(int LineNumber, string Text);

    private sealed record MarkdownSection(
        string HeadingPath,
        IReadOnlyList<SourceLine> Lines);

    private sealed record MarkdownBlock(
        int StartLine,
        int EndLine,
        string Content,
        IReadOnlyList<SourceLine> Lines,
        bool IsProtected);

    private sealed record ChunkDraft(
        string HeadingPath,
        int StartLine,
        int EndLine,
        string Content);

    private sealed record FenceState(char Marker, int Length);
}
