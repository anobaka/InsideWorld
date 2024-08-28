## 配置增强器

增强器是用于补充资源信息（如作者、发布时间、封面等）的工具

![enhancer](../img/category-enhancer-1.png)
![enhancer](../img/category-enhancer-2.png)

**优先级**

如果有多个增强器可以增强同一个资源信息，则可以通过设置优先级来避免冲突。其中**标签**无优先级概念，会将所有增强器产生的标签关联至当前资源。

### Inside World增强器

通过文件名提取相关信息，格式如下（括号内表示可选内容）：
[(20)191202][发布人1(发布人2,发布人3),发布人4(发布人5),发布人6]标题 Part 2(来源作品名)[CN]

### DLsite增强器

从资源名称中提取DLsite编号，如RJxxxxxxxxxx，然后通过DLsite补全资源信息

### ExHentai增强器(v1.6.2+)

通过资源名称在exhentai搜索资源，并将第一个搜索结果的信息补全至该资源。[如何获取cookie](#common-cookie)

![category-enhancer-exhentai](../img/category-enhancer-exhentai-1.png)

可以配置不需要的标签，支持泛匹配符号*，举例：
+ `language:chinese`会忽略获取到的`language:chinese`标签
+ `language:*`会忽略获取到的所有以`language`为**namespace**的标签，如`language:chinese`，`language:english`
+ `*:*`会忽略所有标签

### NFO增强器

通过**本程序**创建的nfo文件填充部分信息，如标签、评级等

### JavLibrary增强器(v1.6.3+)

从资源名称中提取番号(xxx-yyyy或xxxyyyy等)，然后通过JavLibrary补全资源信息，目前仅支持中文

### bangumi增强器(v1.7.1+)

![category-enhancer-bangumi-1](../img/category-enhancer-bangumi-1.png)
![category-enhancer-bangumi-2](../img/category-enhancer-bangumi-2.png)

### 自定义增强器(v1.7.1+)

可以创建自定义增强器，目前仅支持基于`正则表达式`的自定义增强器

![category-enhancer-custom-1](../img/category-enhancer-custom-1.png)

### 增强记录

该页面为临时页面，大部分情况下用于快速查看`资源增强任务`是否正常运行

![enhancement-record](/img/enhancement-record.png)