# 破坏性变更

## v1.6.1

+ 原在分类设置中的【从压缩包获取封面】已转移至【系统设置】，需要手动重新开启；

## v1.6.0

+ 此版本开始需要最新的Edge环境，现已集成该环境，无需单独下载安装。~~可以通过windows update始终保持edge最新版本，或下载运行环境[WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section)，Evergreen Bootstrapper和Evergreen Standalone Installer均可~~
+ 此版本暂不提供一键安装程序，仅提供自动更新机制以及绿色压缩包，启动文件保持不变，依然是**Bakabase.InsideWorld.exe**
+ 原分析器被弃用
  + **InsideWorld通用资源名分析器**已变更为**InsideWorld增强器**，请在分类-增强器中重新选择
  + **InsideWorldAv资源名分析器**和**InsideWorldBiliBili资源名分析器**因为通用性低，产出的信息价值有限，暂时弃用
+ 原**创建nfo文件**的功能已作为配置项添加至分类中，默认设为关闭，请在分类-创建NFO文件选项中激活
+ 从1.6.0开始，每次升级均会清空全部安装文件夹内的文件，请勿将重要文件保存在此

## v1.5.1

此版本开始暂不兼容1080p以下分辨率

## v1.5.0

从此版本开始不再在同步时创建封面缩略图，升级此版本后需要手动（保险起见）删除历史缩略图，可以通过【系统设置】-【未知文件】-【查找并清理】进行。

## v1.1.0

### 分类
由于数据结构有较大变化，所有分类的~~解析器~~已被移除，需要设置对应的[组件](https://github.com/Bakabase/InsideWorld/blob/main/Docs/DEFINITIONS.md#%E7%BB%84%E4%BB%B6)确保分类的状态为![image](https://user-images.githubusercontent.com/2888789/147025320-15369813-b9dd-44e1-b268-c32938423d39.png)时才可继续使用

### 同步产生的临时文件
在**未设置加速路径**的情况下，以前版本产生的临时文件未保存到正确的地址，并且会随每次安装被清空，故升级该版本后，需要手动重新同步媒体库。详见[#24](https://github.com/Bakabase/InsideWorld/issues/24)
