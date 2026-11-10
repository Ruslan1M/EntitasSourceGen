using System;
using System.Text;

namespace EntitasSourceGen.Generator.Emit;

/// <summary>StringBuilder with indentation. Output is plain C# 9 (no file-scoped namespaces).</summary>
internal sealed class CodeWriter
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public CodeWriter Line(string text = "")
    {
        if (text.Length == 0)
        {
            _sb.Append('\n');
            return this;
        }
        _sb.Append(' ', _indent * 4).Append(text).Append('\n');
        return this;
    }

    /// <summary>Writes a line without indentation (preprocessor directives).</summary>
    public CodeWriter Raw(string text)
    {
        _sb.Append(text).Append('\n');
        return this;
    }

    public CodeWriter Open(string header)
    {
        Line(header);
        Line("{");
        _indent++;
        return this;
    }

    public CodeWriter Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
        return this;
    }

    public IDisposable Block(string header)
    {
        Open(header);
        return new Closer(this);
    }

    public override string ToString() => _sb.ToString();

    private sealed class Closer : IDisposable
    {
        private readonly CodeWriter _writer;
        public Closer(CodeWriter writer) => _writer = writer;
        public void Dispose() => _writer.Close();
    }
}
