# Inside World

Inside World 是一款离线媒体管理库，用于本地媒体快速搜寻、预览与播放。

目前支持动画、漫画、音声、本子、电影、图集等媒体的管理，老司机食用效果更加哦~

本人自用软件，目前提供公开测试版本供大家使用，喜欢本软件的话请右上角star，并大力推荐给自己的朋友，感谢大家的支持。

## 使用效果

![image](https://user-images.githubusercontent.com/2888789/146188623-428e6c12-f2d9-457c-a337-ad8bf24f7405.png)
![image](https://user-images.githubusercontent.com/2888789/146188658-6627e8aa-fd10-4898-9cc1-9d8bfb143573.png)
![资源列表](https://user-images.githubusercontent.com/2888789/146381926-4293f163-2347-4180-8299-c5dd03c261c7.jpg)

## 版本日志
| 版本 | 发布时间 |
| ------------- | ------------- |
| [v1.1.0](https://github.com/Bakabase/InsideWorld/milestone/4) | 爆肝中 |
| [v1.0.3](https://github.com/Bakabase/InsideWorld/releases/tag/v1.0.3) | 2021-12-16 |
| [v1.0.2](https://github.com/Bakabase/InsideWorld/releases/tag/v1.0.2) | 2021-12-16 |
| [v1.0.1](https://github.com/Bakabase/InsideWorld/releases/tag/v1.0.1) | 2021-12-15 | 
| [v1.0.0](https://github.com/Bakabase/InsideWorld/releases/tag/v1.0.0) | 2021-12-15 |

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

## 核心概念

**强烈建议**了解InsideWorld的[核心概念](https://github.com/Bakabase/InsideWorld/blob/main/Docs/DEFINITIONS.md)后再开始正式使用

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
![资源列表](https://user-images.githubusercontent.com/2888789/146381965-dba1d165-3efe-4579-b4b1-1da831a28df1.jpg)

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

### 添加标签（v1.0.3+）

![image](https://user-images.githubusercontent.com/2888789/146380493-a34990fd-7195-4bf8-adee-de9a28fb4f52.png)
![image](https://user-images.githubusercontent.com/2888789/146380545-6e1d3d64-dd52-4e00-a792-84f8eb98f149.png)

### 设置标签（v1.0.3+）

![image](https://user-images.githubusercontent.com/2888789/146381776-167760a6-27fd-4003-b73c-b8724841fd7b.png)
![image](https://user-images.githubusercontent.com/2888789/146381855-c9b51301-2025-4ba2-ba4d-7605d74285e1.png)

### 更改语言（v1.0.1+）

![image](https://user-images.githubusercontent.com/2888789/146198873-4eb53585-574e-4745-af24-17f4bd54a0ae.png)

## 常见问题

[常见问题列表](https://github.com/Bakabase/InsideWorld/issues?q=is%3Aissue+sort%3Aupdated-desc+is%3Aclosed+label%3Aquestion)

## 开发中的功能

[Milestones](https://github.com/Bakabase/InsideWorld/milestones)

## 如何参与

欢迎大家积极参与到本项目中，目前可以从Github提交Issue/加入微信/QQ群进行互动。

**本项目初期因为代码混乱，暂不支持研发参与，稳定后会将源码转移至本Repo。**

### QQ群
![InsideWorld群聊二维码](https://user-images.githubusercontent.com/2888789/146117768-7d92af78-37ca-426e-a820-97b896b591eb.png)

### 微信群（不定期更新）
![image](https://user-images.githubusercontent.com/2888789/146211611-cdb5eeff-ff83-4879-bfd7-260f272af377.png)
