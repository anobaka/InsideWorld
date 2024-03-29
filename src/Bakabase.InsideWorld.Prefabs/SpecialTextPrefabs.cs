﻿using System.Collections.Generic;
using System.Linq;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Models.Entities;

namespace Bakabase.InsideWorld.Prefabs
{
    public class SpecialTextPrefabs
    {
        public static List<SpecialText> Texts =
            new Dictionary<SpecialTextType, List<(string Value1, string Value2)>>
                {
                    {
                        SpecialTextType.Useless, new List<(string Value1, string Value2)>
                        {
                            (@"[Cc]\d{2}", null),
                            (@"COMIC1☆\d{1,2}", null),
                            ("成年コミック", null),
                            ("同人誌", null),
                            ("DL版", null),
                            ("彩頁部分", null),
                            ("無修正", null),
                            ("文盲組", null),
                            (@"\.mp4", null),
                            ("ゲームCG", null),
                            ("同人CG集", null),
                            ("18禁ゲームCG", null),
                        }
                    },
                    {
                        SpecialTextType.Language,
                        new List<(string Value1, ResourceLanguage Value2)>
                            {
                                ("汉化", ResourceLanguage.Chinese),
                                ("中文", ResourceLanguage.Chinese),
                                ("中国翻訳", ResourceLanguage.Chinese),
                                ("CE家族社", ResourceLanguage.Chinese),
                                ("漢化", ResourceLanguage.Chinese),
                                ("阿提斯整個車頭的", ResourceLanguage.Chinese),
                                ("CN", ResourceLanguage.Chinese)
                            }
                            .Select(a => (a.Value1, ((int) a.Value2).ToString())).ToList()
                    },
                    {
                        SpecialTextType.Wrapper, new List<(string Value1, string Value2)>
                        {
                            ("(", ")"),
                            ("[", "]"),
                        }
                    },
                    {
                        SpecialTextType.Standardization, new List<(string Value1, string Value2)>
                        {
                            ("（", "("),
                            ("）", ")"),
                            ("【", "["),
                            ("】", "]"),
                            ("，", "、"),
                            ("#", "＃"),
                            ("Ａ", "A"),
                            ("!", "！"),
                            ("～", "~"),
                        }
                    },
                    {
                        SpecialTextType.Volume, new List<(string Value1, int? Value2)>
                        {
                            (@"\s[A-Za-z]+[\.-]?\d+[\.-]?", null),
                            (@"[＃]\d+", null),
                            ("上[巻卷]", 1),
                            ("(1st|2nd|3rd)", null),
                            (@"\d+限目", null),
                            ("第一[話章]", 1),
                            ("下[巻卷]", 2),
                            ("第二[話章]", 2),
                            ("第三[話章]", 3),
                        }.Select(a => (a.Value1, a.Value2?.ToString())).ToList()
                    },
                    {
                        SpecialTextType.Trim, new List<(string Value1, string Value2)>()
                    },
                    {
                        SpecialTextType.DateTime, new[]
                        {
                            "yyyyMMddHHmm",
                            "yyyyMMdd",
                            "yyyy-MM-dd",
                            "yyyy-M-d",
                            "yyyy年MM月dd日",
                            "yyyy年M月d日",
                            "yyMMdd",
                            "yy年M月d日",
                            "MMdd",
                            "M月d日"
                        }.Select(a => (a, (string) null)).ToList()
                    }
                }
                .SelectMany(t => t.Value.Select(b => new SpecialText
                {
                    Type = t.Key,
                    Value1 = b.Value1,
                    Value2 = b.Value2
                })).ToList();
    }
}