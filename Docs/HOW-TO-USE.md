# 如何使用

## 安装与初始化

### 下载与安装
从[Releases](https://github.com/Bakabase/InsideWorld/releases)下载最新版本安装包进行安装，安装时请选择以下选项

![image](https://user-images.githubusercontent.com/2888789/146113293-d6b5dab3-8fec-40da-a751-598d25119c57.png)

### 初始化内置特殊字符集

![#f03c15](https://via.placeholder.com/15/f03c15/000000?text=+) `为确保能从资源名称中提取出更多的信息，强烈建议正式使用前配置该项`

详见[分析器](#https://github.com/Bakabase/InsideWorld/blob/main/Docs/DEFINITIONS.md#%E5%88%86%E6%9E%90%E5%99%A8parser)

![image](https://user-images.githubusercontent.com/2888789/146132647-d99ec4ac-6fb9-4d11-b911-130734490d6a.png)

### ~~配置本地加速路径（从v1.5.0版本后暂时没有应用场景，可忽略本项）~~

`为确保磁盘留有足够的可用空间并且加速日常预览，强烈建议在正式使用前配置该项`

该路径主要用于存储日常产生的临时文件，如：缩略图等，如果不配置该项，则默认存储在下图中的`App Data Path/data`内

![image](https://user-images.githubusercontent.com/2888789/146113550-c2de1050-960c-4927-8c1c-2da6245235fc.png)

## 创建自定义组件

![component](https://user-images.githubusercontent.com/2888789/147113998-5ba1b988-5cf3-4a95-a579-bbb34db19ab1.png)

### 创建自定义可播放文件选择器

![component-playable-file-selector](https://user-images.githubusercontent.com/2888789/147114041-0756c05a-f52f-4dcc-8824-4d8eb3130f14.png)

### 创建自定义播放器

![component-player](https://user-images.githubusercontent.com/2888789/147114049-8faa4791-0ee1-4602-b67e-96abaf0e18fb.png)

## 分类

### 创建媒体分类

![category-custom-component](https://user-images.githubusercontent.com/2888789/146939494-64edd77f-55d5-4559-9afd-b3f7cca8a70d.png)

## 媒体库

### 创建媒体库

![image](https://user-images.githubusercontent.com/2888789/146116644-a6f3171c-f5ee-4e97-bc35-64e29ed48a6e.png)

#### 如何使用网络驱动器
[#50](https://github.com/Bakabase/InsideWorld/issues/50)

### 同步媒体库

![image](https://user-images.githubusercontent.com/2888789/146116964-1e4ec4ce-9415-4a57-96b3-76e9a92bc8ca.png)

#### 开启媒体库定时自动同步

![image](https://user-images.githubusercontent.com/2888789/150361410-68e02536-03b2-4f4c-b860-de60751c28fc.png)

### 使用媒体库

点击媒体库可以直接播放（如果是视频媒体，则需要安装PotPlayer）
![resource](https://user-images.githubusercontent.com/2888789/146939604-e605090a-a706-4f7b-a094-b666962538c5.png)

## ~~快速预览文件夹（v1.4.0+）~~

该功能于v1.5.0起已移除

~~快速预览**不会**保存任何数据~~

~~![WeChat Image_20220120154105](https://user-images.githubusercontent.com/2888789/150294673-4dbaf6a0-b142-4e00-82bb-bc12ac2ee5fe.png)~~

## 标签

### 添加标签（v1.0.3+）

![image](https://user-images.githubusercontent.com/2888789/146380493-a34990fd-7195-4bf8-adee-de9a28fb4f52.png)
![image](https://user-images.githubusercontent.com/2888789/146380545-6e1d3d64-dd52-4e00-a792-84f8eb98f149.png)

### 设置标签（v1.1.0+）

![resource-batch-tag](https://user-images.githubusercontent.com/2888789/146939623-0789bf76-7d6e-4039-bc6a-7f148b17b2e8.png)

## 下载哔哩哔哩视频（v1.2.0+）

批量下载收藏夹内所有视频的**最高清**版本，同时会保存弹幕和封面图

**该功能本质上是将人工逐项下载的操作自动化完成，短时间内频繁使用该功能会导致账号或ip被ban**

### 准备环境

+ 下载[FFmpeg](https://www.ffmpeg.org/)，用于视频音频合并，并确保ffmpeg.exe所在目录已被加入到系统环境变量中。打开命令行工具，执行ffmpeg，出现类似以下内容则说明配置成功
![image](https://user-images.githubusercontent.com/2888789/147718899-32797afa-b3c8-4f80-9b16-3da0e6721433.png)

+ 打开InsideWorld，配置哔哩哔哩Cookie与视频下载目录

**目前该Cookie仅用来获取收藏夹信息以及账号对应的最高清晰度的下载权限，没有其他用途，请放心使用**

![image](https://user-images.githubusercontent.com/2888789/147718998-50078f19-4cca-4d58-a267-f42e2538b52c.png)

### 获取收藏夹信息

![image](https://user-images.githubusercontent.com/2888789/147719209-79b8aff6-57ef-4d55-b1c3-d08860bab367.png)

### 下载视频

+ 选择收藏夹
+ 点击下载视频
+ 下载进度会出现在收藏夹下方
![image](https://user-images.githubusercontent.com/2888789/147719321-1a8d6f05-7409-4c4e-a572-5d0682dc3cc6.png)
![image](https://user-images.githubusercontent.com/2888789/147719353-e50c04eb-268c-4093-abb3-f90730c04dc2.png)

## 一键解压（v1.4.0+）

一键解压目录下所有压缩包
![image](https://user-images.githubusercontent.com/2888789/150361198-dc2ad75f-8a97-4ee3-ab5d-ce163d859656.png)

## 系统设置

### 更改语言（v1.0.1+）

![image](https://user-images.githubusercontent.com/2888789/146198873-4eb53585-574e-4745-af24-17f4bd54a0ae.png)

### 更新（v1.4.0+）

![image](https://user-images.githubusercontent.com/2888789/150296183-024d65b4-260f-4547-8f7e-ea1163976bad.png)
