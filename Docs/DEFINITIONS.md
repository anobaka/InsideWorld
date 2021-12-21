# 概念定义

## 分类

用于划分资源类型，每个分类会包含一系列**组件**，这些**组件**决定每个本地**资源**如何被**解析**、**存储**和**播放**。
举例：电影、漫画、动画等

## 媒体库

用于建立本地资源与分类的关系，一个分类可以设置多个媒体库，每个媒体库必须要包含一个本地资源**根目录**。
举例
| 名称 | 路径 | 所属分类 |
| ------------- | ------------- | ------------- |
| 待看 | C:\tbd | 电影 |
| 已看 | C:\done | 电影 |
| 不看后悔一辈子 | C:\godofwar | 电影 |

## 资源

媒体库根目录下的每一个**一级**文件夹或文件都是一个资源。
每个资源包含以下信息
| 信息 | 可搜索 | 可排序 |
| ------------- | ------------- | ------------- |
| 名称 | √ | × |
| 出版方 | √ | × |
| 原作 | √ | × |
| 系列名 | √ | × |
| 语言 | √ | × |
| 入库时间 | √ | √ |
| 发售时间 | √ | √ |
| 文件创建时间 | √ | √ |
| 文件修改时间 | √ | √ |
| 评级 | √ | √ |
| 标签 | √ | × |

## 组件

目前包含三类组件

| 类型 | 作用 | 是否必选 | 最大选取数量 |
| ------------- | ------------- | ------------- | ------------- |
| 分析器(Parser) | 提取并解析资源名称 | 是 | 1 |
| 可播放文件选择器(Playable File Selector) | 用于选取哪些文件是可以在软件中快捷播放的 | 是 | 1 |
| 播放器(Player) | 用于播放`可播放文件选择器`选择的文件 | 是 | 1 |

### 分析器(Parser)

| 分析器 | 系统内置 | 可解析内容 | 对应名称样例 | 解析结果 | 说明 | 
| ------------- | ------------- | ------------- | ------------- | ------------- |  ------------- | 
| InsideWorld通用资源名分析器(InsideWorldParser) | 是 | 发售日<br/>出版方<br/>标题<br/>期数<br/>原作<br/>语言 | [200212][Anobaka(baka1, baka2)]我是标题(我是原作名)[CN] | 发售日：2020-02-12<br/>出版方：Anobaka<br/>包含2个作者：baka1，baka2<br/>标题：我是标题<br/>原作：我是原作名<br/>语言：中文 | 需配合[内置特殊字符集](#内置特殊字符集)食用。[测试效果](https://user-images.githubusercontent.com/2888789/146298106-469577f9-5115-4120-9d27-f1510a3f0cbb.png) |
| InsideWorldAv资源名分析器(InsideWorldAvParser) | 是 | 番号 | xxxxxxxxADS-925xxxxxxxxxx | 番号：ADS-925 | |
| InsideWorldBiliBili资源名分析器(InsideWorldBilibiliParser) | 是 |暂不开放 | 暂不开放 | |

#### 内置特殊字符集

| 类型 | 说明 | 匹配样例 |
| - | - | - |
| Useless | 标记无用字符 | `DL版` |
| Language | 标记语言 | `CN` -> `中文` |
| Wrapper | 信息块包裹符号 | `()`，`[]`，`{}` |
| StandardizeName | 标准化字符转换 | `【`->`[` |
| Volume | 期数 | `上卷` -> `1`，`1st` -> 1，`2nd` -> 2 |

### 可播放文件选择器(Playable File Selector)

目前内置以下几种可播放文件选择器
| 查找器 | 系统内置 | 查找内容 |
| ------------- | ------------- | ------------- |
| 图片文件查找器(ImagePlayableFileSelector) | 是 | **第一个**后缀在[.png,.jpeg,.jpg,.bmp,.gif]内的图片文件 | 
| 视频文件查找器(VideoPlayableFileSelector) | 是 | **全部**后缀在[.mp4,.avi,.mkv,.rmvb,.wmv]内的视频文件 |
| 音频文件查找器(AudioPlayableFileSelector) | 是 | **全部**后缀在[.mp3,.flac]内的音频文件 | 
| 自定义可播放文件选择器 | 否 | 自定义后缀以及选择数量 |

#### 自定义可播放文件选择器

可以自定义`可播放文件选择器`，填写**目标后缀名列表**以及**最大选择数量**即可创建。

### 播放器(Player)

| 播放器 | 系统内置 | 查找内容 |
| ------------- | ------------- | ------------- |
| 图片文件查找器(ImagePlayableFileSelector) | 是 | **第一个**后缀在[.png,.jpeg,.jpg,.bmp,.gif]内的图片文件 | 
| 视频文件查找器(VideoPlayableFileSelector) | 是 | **全部**后缀在[.mp4,.avi,.mkv,.rmvb,.wmv]内的视频文件 |
| 音频文件查找器(AudioPlayableFileSelector) | 是 | **全部**后缀在[.mp3,.flac]内的音频文件 | 
| 自定义可播放文件选择器 | 否 | 自定义后缀以及选择数量 |

#### 自定义播放器

可以自定义`播放器`，填写**可执行文件地址**即可创建。 


### ~~解析器(v1.0.x)~~

**该概念已在v1.1.0版本移除**

每个解析器都是某一种媒体分类的处理器，该处理器目前包含以下几个组件：
+ **分析器**：分析每个媒体名称，提取出关键信息，如：作者/出版方、语言、标题、集数等
+ **可播放文件查找器**：寻找每个媒体的可播放文件，如mp4就是视频媒体的其中一种可播放文件，jpg对应图片媒体，其他同理

目前内置以下几种解析器供大家选择，后续会持续增加并开放插件接口：
| 解析器 | 对应分析器 | 对应可播放文件查找器 |
| ------------- | ------------- | ------------- |
| InsideWorldVideoResolver | InsideWorldParser | VideoStartFileSelector |
| InsideWorldImageResolver | InsideWorldParser | ImageStartFileSelector |
| InsideWorldAudioResolver | InsideWorldParser | AudioStartFileSelector |
| InsideWorldAvResolver | InsideWorldAvParser | VideoStartFileSelector |
| InsideWorldBiliBiliVideoResolver | InsideWorldBilibiliParser | VideoStartFileSelector |


## 标签(v1.0.3+)

可以自定义多个标签，每个资源都可以与多个标签绑定
