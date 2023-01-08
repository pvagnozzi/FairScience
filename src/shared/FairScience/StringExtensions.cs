using System.Text;

namespace FairScience;

public static class StringExtensions
{
    public static readonly string[] LineTerminators = {
        "\r\n",
        "\n",
        "\r",
    };

    public static StringBuilder AddFiller(this StringBuilder builder, char fillter, int count)
    {
        for (; count > 0; count--)
        {
            builder.Append(fillter);
        }

        return builder;
    }

    public static string AsFiller(this char ch, int count)
    {
        var sb = new StringBuilder();
        sb.AddFiller(ch, count);
        return sb.ToString();
    }

    public static string DetectLineTerminator(this string source)
    {
        foreach (var lineTerminator in LineTerminators)
        {
            if (source.Contains(lineTerminator))
            {
                return lineTerminator;
            }
        }

        return string.Empty;
    }
}

