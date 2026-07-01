using System;
using System.Text;

namespace ComposableSettings.Generator.IndentedStringBuilder;

/// <summary>
///  Lekki wrapper nad <see cref="StringBuilder"/> zapewniający kontrolę wcięć.
/// </summary>
public  class IndentedStringBuilder(string indentUnit = "    ")
{
    private readonly StringBuilder _sb = new();
    private int _level;


    /*---------- public API ----------*/

    public IDisposable Indent()
    {
        _level++;
        return new PopIndent(this);
    }

    public IndentedStringBuilder AppendLine(string? text = null)
    {
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < _level; i++)
                _sb.Append(indentUnit);

            _sb.Append(text);
        }

        _sb.AppendLine();
        return this;
    }

    public IndentedStringBuilder Append(string text)
    {
        _sb.Append(text);
        return this;
    }

    public IDisposable Block(string header, bool endWithSemicolon = false)
    {
        AppendLine(header);
        AppendLine("{");
        var indent = Indent();
        return new DisposableAction(() =>
        {
            indent.Dispose();
            AppendLine(endWithSemicolon ? "};" : "}");
        });
    }

    /// <summary>
    /// Writes an XML documentation summary comment
    /// </summary>
    public void WriteSummary(string summary)
    {
        AppendLine("/// <summary>");
        AppendLine($"/// {summary}");
        AppendLine("/// </summary>");
    }

    public void AddProperty(string typeAndName, string? value = null)
    {
        AppendLine($"{typeAndName}{(value is not null ? $"={value}" : string.Empty)};");
    }


    public override string ToString() => _sb.ToString();

    /*---------- implementation ----------*/

    private void Pop() => _level--;

    private  class PopIndent(IndentedStringBuilder parent) : IDisposable
    {
        public void Dispose() => parent.Pop();
    }
}
