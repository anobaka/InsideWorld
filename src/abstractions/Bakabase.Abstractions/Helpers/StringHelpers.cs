﻿using System.Text.RegularExpressions;

namespace Bakabase.Abstractions.Helpers;

public class StringHelpers
{
    public static Regex BuildRegexWithWrapper(string left, string right, string word)
    {
        //获取自动非left和right的padding
        //右中括号特殊处理
        var rBracket = right == "]" ? @"\]" : Regex.Escape(right);
        var lBracket = Regex.Escape(left);
        //如果左右为空，则不生成padding
        var leftPadding = true;
        if (word[0] == '^')
        {
            leftPadding = false;
            word = word.TrimStart('^');
        }

        var rightPadding = true;
        if (word.EndsWith("$") && !word.EndsWith(@"\$"))
        {
            rightPadding = false;
            word = word.TrimEnd('$');
        }

        var padding = string.IsNullOrEmpty(rBracket) && string.IsNullOrEmpty(lBracket)
            ? null
            : rBracket == lBracket
                ? $"[^{rBracket}]*?"
                : $"[^{lBracket}{rBracket}]*?";
        var lPadding = leftPadding ? padding : string.Empty;
        var rPadding = rightPadding ? padding : string.Empty;
        left = Regex.Escape(left);
        right = Regex.Escape(right);
        return new Regex($"{left}{lPadding}{word}{rPadding}{right}");
    }
}