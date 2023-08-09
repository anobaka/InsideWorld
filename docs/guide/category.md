## 管理分类 <!-- {docsify-ignore} -->

在正式管理资源前，你至少需要创建并配置一个分类。分类可以拖拽排序。
![category-new](../img/category-new-1.png)
![category-new](../img/category-new-2.png)

## 配置向导(v1.7.0+)

在1.7.0以后，可以使用配置向导进行分类配置，以下仅展示部分流程，具体流程和说明以软件内提示为准。

![category-setup-wizard-1](../img/category-setup-wizard-1.png)
![category-setup-wizard-2](../img/category-setup-wizard-2.png)
![category-setup-wizard-3](../img/category-setup-wizard-3.png)
![category-setup-wizard-4](../img/category-setup-wizard-4.png)
![category-setup-wizard-5](../img/category-setup-wizard-5.png)

## 匹配资源路径(高级)(v1.7.1+)

现在可以进行更为复杂的资源路径配置

### 1. 基础概念

+ **根目录**：仅且必须设置一个，仅能通过`路径`设置；
+ **资源**：仅且必须设置一个，可以通过`路径`或`正则表达式`设置；
+ **其他属性**：可不设置，可通过`路径`或`正则表达式`设置，如果该属性不能超过`1个`，则会默认使用第一个匹配到的结果；
+ **通过路径配置**：可以通过在`根目录`和`资源`之间，正序或倒序的目录层级来设置属性；
+ **通过正则表达式配置**：可以通过匹配在`根目录`和`资源`之间的路径字符串来设置属性；

### 2. 设置界面

当属性不可用时，可以将鼠标移至不能设置的属性按钮上的设置方式来查看原因，有些提示也会出现在完整路径的正上方或正下方；

**可用性提示**

![categary-path-segment-matcher-1](../img/categary-path-segment-matcher-1.png)

**通过层级设置**

![categary-path-segment-matcher-2](../img/categary-path-segment-matcher-2.png)

**通过正则设置**

![categary-path-segment-matcher-3](../img/categary-path-segment-matcher-3.png)

一些其他设置样例

+ 简单匹配全部文本
![categary-path-segment-matcher-3-1](../img/categary-path-segment-matcher-3-1.png)

+ 匹配a和b
![categary-path-segment-matcher-3-2](../img/categary-path-segment-matcher-3-2.png)

+ 匹配b和c
![categary-path-segment-matcher-3-3](../img/categary-path-segment-matcher-3-3.png)

+ 匹配a、b和c
![categary-path-segment-matcher-3-4](../img/categary-path-segment-matcher-3-4.png)

**设置结果**

![categary-path-segment-matcher-4](../img/categary-path-segment-matcher-4.png)

**使用本地资源预览**

![categary-path-segment-matcher-5](../img/categary-path-segment-matcher-5.png)

## 基础配置

![category-basic](../img/category-basic.png)

**封面查找优先级**

设置查找封面时候选图片列表的排序顺序（文件名正序、时间倒序），即下述优先级中2的选取规则，默认的封面查找优先级是：
1. 资源目录下名为cover（不包含后缀）的图片文件
2. 资源目录下的第一张图片（本项设置）
3. 资源目录下第一个视频中20%时间处的截图（默认关闭，在[系统设置](#configuration)中启用）
4. 资源目录下第一个压缩包内的第一张图片（默认关闭，在[系统设置](#configuration)中启用）

**创建nfo文件**

+ 启用后会创建本程序支持的nfo文件，用于跨设备共享部分信息；
+ 需要配合**nfo增强器**使用，并将其增强内容设置为最高优先级；

## 配置组件

![category-component](../img/category-component-1.png)

**可播放文件查找器**

用于查找哪些文件是可被播放的，可以使用系统内置的查找器，也可以在[自定义组件](#custom-component)中创建自定义查找器

![category-component](../img/category-component-2.png)

**播放器**

用于播放被`可播放文件查找器`找到的文件，可以在[自定义组件](#custom-component)中创建自定义播放器

![category-component](../img/category-component-3.png)

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

## 媒体库

每个分类可以绑定多个媒体库，每个媒体库也能绑定多个系统目录。媒体库可以拖拽排序。

![library](../img/category-library-1.png)
![library](../img/category-library-2.png)

**根目录和过滤器**

本程序会将**根目录**下的全部文件(夹)通过**过滤器**筛选后剩余的文件(夹)视作**资源**
+ 如果你只是希望简单选择**根目录**下的某一层文件(夹)作为**资源**，则直接选择**第x层**即可；
+ 如果你希望根据复杂规则选择哪些文件(夹)会被视为**资源**，则可以通过手动填写正则表达式来完成；

**标签**

可以为当前根目录下的**资源**设置**默认标签**，用于快速筛选**资源**

+ 固定标签：所有当前根目录下的**资源**均会被添加所选标签；
+ 基于路径的动态标签：可以根据路径名称，动态为**资源**添加标签。假设根目录是`/电影`，资源路径是`/电影/漫威/蜘蛛侠`，如果选择了`使用根目录后第1层`或`使用资源路径的前1层`，则会为该资源增加`漫威`这个标签

**如何使用网络驱动器**

<a href="https://github.com/Bakabase/InsideWorld/issues/50" target="_blank">#50</a>
### 路径配置(v1.7.0+)
在1.7.0版本以后，你可以为媒体库根目录与资源文件之间的每一级目录配置属性。

![library](../img/category-library-3.png)

## 删除增强记录(v1.6.1+)

**删除分类下全部增强记录**

![category-remove-enhancement-records](../img/category-remove-enhancement-records-1.png)

**删除媒体库下全部增强记录**

![category-remove-enhancement-records](../img/category-remove-enhancement-records-2.png)