# Inside World

Inside World 是一款离线媒体管理库，用于本地媒体快速搜寻、预览与播放。

目前支持动画、漫画、音声、本子、电影、图集等媒体的管理，老司机食用效果更加哦~

本人自用软件，目前提供公开测试版本供大家使用，喜欢本软件的话请右上角star，并大力推荐给自己的朋友，感谢大家的支持。

## 使用效果

![image](https://user-images.githubusercontent.com/2888789/146117804-aef8ed93-f6c8-4a2e-be27-16d7d2837fd3.png)
![image](https://user-images.githubusercontent.com/2888789/146117815-82fcc988-bc27-4117-bb27-829d97c5f33b.png)
![image](https://user-images.githubusercontent.com/2888789/146117275-1a94944d-f00b-46a6-9087-578a7bbf9469.png)

## 环境要求
+ 最低适配屏幕分辨率为1280x720
+ 系统盘至少有1GB剩余空间
+ Windows 10
+ [PotPlayer](http://potplayer.daum.net/)
### 推荐配置
+ 有至少10GB SSD

## 初次使用
### 安装

从[Releases](https://github.com/Bakabase/InsideWorld/releases)下载最新版本安装包进行安装，安装时请选择以下选项
![image](https://user-images.githubusercontent.com/2888789/146113293-d6b5dab3-8fec-40da-a751-598d25119c57.png)

### 下载视频播放器
目前仅支持[PotPlayer](http://potplayer.daum.net/)，不支持绿色免安装版

### 内置特殊字符集
详见[InsideWorldParser](#分析器)
![image](https://user-images.githubusercontent.com/2888789/146132647-d99ec4ac-6fb9-4d11-b911-130734490d6a.png)

### 本地预览加速配置
![#f03c15](https://via.placeholder.com/15/f03c15/000000?text=+) `为确保磁盘留有足够的可用空间并且加速日常预览，强烈建议在正式使用前配置该项`
![image](https://user-images.githubusercontent.com/2888789/146113550-c2de1050-960c-4927-8c1c-2da6245235fc.png)

### 核心概念

**强烈建议**了解InsideWorld的核心概念后再开始正式使用

#### 媒体分类

你可以自由创建媒体分类，名称没有限制，如：Anime、Comic、Movie、Audio等
每种媒体分类需要配置一个**解析器**`Resolver`

#### 解析器

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

#### 分析器

目前内置以下几种分析器
| 分析器 | 可解析内容 | 对应名称样例 | 解析结果 |
| ------------- | ------------- | ------------- | ------------- |
| InsideWorldParser | 发售日<br/>出版方<br/>标题<br/>期数<br/>原作<br/>语言 | [200212][Anobaka(baka1, baka2)]我是标题(我是原作名)[CN] | 发售日：2020-02-12<br/>出版方：Anobaka<br/>包含2个作者：baka1，baka2<br/>标题：我是标题<br/>原作：我是原作名<br/>语言：中文 |
| InsideWorldAvParser | 番号 | xxxxxxxxADS-925xxxxxxxxxx | 番号：ADS-925 |
| InsideWorldBilibiliParser | 暂不开放 | 暂不开放 |

#### 可播放文件查找器

目前内置以下几种可播放文件查找器
| 查找器 | 查找内容 |
| ------------- | ------------- |
| ImageStartFileSelector | 第一个后缀在[.png,.jpeg,.jpg,.bmp,.gif]内的图片文件 | 
| VideoStartFileSelector | 全部后缀在[.mp4,.avi,.mkv,.rmvb,.wmv]内的视频文件 |
| AudioStartFileSelector | 全部后缀在[.mp3,.flac]内的音频文件 | 暂不开放 |

#### 媒体库

媒体分类建立完成后，你需要将本地的资源与媒体分类关联起来，这样你的媒体资源才会和分类绑定

#### 资源

**强制约定**：媒体库目录下的每一个一级文件夹是一个资源

每个资源都会和对应的媒体库以及分类关联

## 正式使用
### 创建媒体分类

![image](https://user-images.githubusercontent.com/2888789/146116506-96291b5d-90b2-43f9-bc6b-db3627de9c94.png)

### 创建媒体库

![image](https://user-images.githubusercontent.com/2888789/146116644-a6f3171c-f5ee-4e97-bc35-64e29ed48a6e.png)

### 同步媒体库

同步媒体库将会自动扫描媒体库路径下的所有**一级文件夹**作为媒体（强制约定，详见[资源](#资源)），同时会根据**解析器**创建媒体信息、缩略图
假如你的媒体库根目录为`/aaa`，并且目录结构为
```
/aaa/资源1/封面.png
/aaa/资源1/视频.mp4
/aaa/资源2/abc/封面.png
/aaa/资源2/视频.mp4
/aaa/资源2/视频2.png
/aaa/资源3/视频.mp4
```
则会被解析为3个资源，分别具有以下信息
```
资源1：1个封面，1个视频
资源2：1个封面，2个视频
资源3：没有封面，1个视频
```
![image](https://user-images.githubusercontent.com/2888789/146116964-1e4ec4ce-9415-4a57-96b3-76e9a92bc8ca.png)

### 使用媒体库

点击媒体库可以直接播放（如果是视频媒体，则需要安装PotPlayer）
![image](https://user-images.githubusercontent.com/2888789/146117275-1a94944d-f00b-46a6-9087-578a7bbf9469.png)

### 搜索与排序

目前支持的搜索项
+ 名称
+ 出版方
+ 原作
+ 系列名
+ 语言
+ 入库时间
+ 发售时间
+ 文件创建时间
+ 文件修改时间
+ 最小评级

目前支持的排序项（可多选，会逐项排序）
+ 入库时间
+ 发售时间
+ 文件创建时间
+ 文件修改时间
+ 最小评级

## 开发中的功能
- [ ] https://github.com/Bakabase/InsideWorld/issues/1
- [ ] 自定义标签：管理自定义标签、根据自定义标签搜索媒体
- [ ] 自定义视频播放器
- [ ] 多语言
- [ ] 自动更新
- [ ] 自动资源名称优化
- [ ] Mac支持
- [ ] Bilibili视频批量拉取
- [ ] ExHentai订阅

## 如何参与

欢迎大家积极参与到本项目中，目前可以从Github提交Issue/加入微信/QQ群进行互动。

**本项目初期因为代码混乱，暂不支持研发参与，稳定后会将源码转移至本Repo。**

### QQ群
![InsideWorld群聊二维码](https://user-images.githubusercontent.com/2888789/146117768-7d92af78-37ca-426e-a820-97b896b591eb.png)
