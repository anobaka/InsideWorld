## 特殊字符配置 <!-- {docsify-ignore} -->

**InsideWorld增强器内置特殊字符集**

| 类型 | 说明 | 匹配样例/结果 |
|: - |: - |: - |
| Useless | 标记无用字符 | `DL版` |
| Language | 标记语言 | `CN` 会被识别为 `中文` |
| Wrapper | 信息块包裹符号 | `()`，`[]`，`{}` |
| StandardizeName | 标准化字符转换 | `【`会被转换成`[` |
| Volume | 期数 | `上卷` 会被转换成 `1`，`1st` 会被转换成 `1`，`2nd` 会被转换成 `2` |
| DateTime | [匹配日期文本](https://github.com/anobaka/InsideWorld/issues/367) |  |